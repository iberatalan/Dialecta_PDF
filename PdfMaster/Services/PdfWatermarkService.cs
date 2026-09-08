using System;
using System.Diagnostics;
using System.IO;
using PdfMaster.Models;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace PdfMaster.Services
{
    public class PdfWatermarkService
    {
        public static OperationResult ApplyWatermark(string sourceFilePath, WatermarkOptions options, string? customOutputName = null, IProgress<double>? progress = null)
        {
            var sw = Stopwatch.StartNew();
            if (!File.Exists(sourceFilePath))
                return OperationResult.Failure("Kaynak dosya bulunamadı.");

            StorageManager.Instance.ImportOriginal(sourceFilePath);
            var sourceHash = SecurityAndHashService.ComputeSha256(sourceFilePath);
            var baseFileName = !string.IsNullOrWhiteSpace(customOutputName)
                ? customOutputName
                : Path.GetFileNameWithoutExtension(sourceFilePath);

            try
            {
                using var document = PdfReader.Open(sourceFilePath, PdfDocumentOpenMode.Modify);
                int totalPages = document.PageCount;

                // Parse color
                var color = ParseColor(options.ColorHex, options.Opacity);
                var brush = new XSolidBrush(color);
                var font = new XFont(options.FontFamily, options.FontSize, XFontStyle.Bold);

                XImage? imageWatermark = null;
                if (options.Type == WatermarkType.Image && !string.IsNullOrEmpty(options.ImagePath) && File.Exists(options.ImagePath))
                {
                    imageWatermark = XImage.FromFile(options.ImagePath);
                }

                for (int i = 0; i < totalPages; i++)
                {
                    int pageNum = i + 1;
                    if (ShouldApplyToPage(pageNum, totalPages, options))
                    {
                        var page = document.Pages[i];
                        using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);

                        var width = page.Width.Point;
                        var height = page.Height.Point;

                        if (options.Type == WatermarkType.Text)
                        {
                            DrawTextWatermark(gfx, options.Text, font, brush, width, height, options);
                        }
                        else if (imageWatermark != null)
                        {
                            DrawImageWatermark(gfx, imageWatermark, width, height, options);
                        }
                    }
                    progress?.Report((i + 1) / (double)totalPages * 100);
                }

                var outputPath = StorageManager.Instance.GenerateVersionedOutputPath(baseFileName, "filigran");
                document.Save(outputPath);

                sw.Stop();
                var outputHash = SecurityAndHashService.ComputeSha256(outputPath);
                var fi = new FileInfo(outputPath);

                // Audit log
                var audit = new AuditEvent
                {
                    Action = ActionType.Watermark,
                    ActionDescription = $"PDF belgesine filigran uygulandı ({options.Type}: '{(options.Type == WatermarkType.Text ? options.Text : Path.GetFileName(options.ImagePath))}').",
                    SourceFileName = Path.GetFileName(sourceFilePath),
                    SourceFilePath = sourceFilePath,
                    SourceFileHash = sourceHash,
                    OutputFileName = Path.GetFileName(outputPath),
                    OutputFilePath = outputPath,
                    OutputFileHash = outputHash,
                    PageCount = totalPages,
                    FileSizeBytes = fi.Length,
                    DurationMs = sw.ElapsedMilliseconds,
                    IsSuccess = true
                };
                AuditLogger.LogEvent(audit);

                var result = OperationResult.Success(outputPath, "Filigran başarıyla uygulandı ve kaydedildi.", outputHash);
                result.Duration = sw.Elapsed;
                result.ResultSizeBytes = fi.Length;
                return result;
            }
            catch (Exception ex)
            {
                sw.Stop();
                string friendlyMessage = ex.Message;
                if (friendlyMessage.Contains("XRef") || friendlyMessage.Contains("cross-reference"))
                {
                    try
                    {
                        var repairedFile = PdfRepairService.RepairPdf(sourceFilePath);
                        var result = ApplyWatermark(repairedFile, options, customOutputName);
                        if (File.Exists(repairedFile)) File.Delete(repairedFile);
                        return result;
                    }
                    catch (Exception)
                    {
                        friendlyMessage = "Bu PDF dosyasının yapısal bütünlüğü bozulmuş veya güncel olmayan bir format kullanıyor. Otomatik onarım başarısız oldu. Lütfen dosyayı farklı bir PDF aracı ile kaydedip (örneğin Chrome'da açıp 'PDF olarak yazdır' diyerek) tekrar deneyin.";
                    }
                }

                var audit = new AuditEvent
                {
                    Action = ActionType.Watermark,
                    ActionDescription = "Filigran işlemi başarısız oldu.",
                    SourceFileName = Path.GetFileName(sourceFilePath),
                    IsSuccess = false,
                    ErrorMessage = friendlyMessage,
                    DurationMs = sw.ElapsedMilliseconds
                };
                AuditLogger.LogEvent(audit);

                return OperationResult.Failure($"Filigran hatası: {friendlyMessage}");
            }
        }

        private static void DrawTextWatermark(XGraphics gfx, string text, XFont font, XBrush brush, double width, double height, WatermarkOptions options)
        {
            var size = gfx.MeasureString(text, font);

            if (options.Placement == WatermarkPlacement.Tile)
            {
                double stepX = size.Width * 1.6;
                double stepY = size.Height * 3.0;

                for (double x = -width; x < width * 2; x += stepX)
                {
                    for (double y = -height; y < height * 2; y += stepY)
                    {
                        var state = gfx.Save();
                        gfx.TranslateTransform(x, y);
                        gfx.RotateTransform(options.RotationAngle);
                        gfx.DrawString(text, font, brush, 0, 0);
                        gfx.Restore(state);
                    }
                }
            }
            else
            {
                var state = gfx.Save();
                double posX = width / 2;
                double posY = height / 2;
                double angle = options.RotationAngle;

                switch (options.Placement)
                {
                    case WatermarkPlacement.TopLeft:
                        posX = size.Width / 2 + 30;
                        posY = size.Height + 30;
                        break;
                    case WatermarkPlacement.TopRight:
                        posX = width - (size.Width / 2 + 30);
                        posY = size.Height + 30;
                        break;
                    case WatermarkPlacement.BottomLeft:
                        posX = size.Width / 2 + 30;
                        posY = height - 30;
                        break;
                    case WatermarkPlacement.BottomRight:
                        posX = width - (size.Width / 2 + 30);
                        posY = height - 30;
                        break;
                    case WatermarkPlacement.Center:
                        angle = 0;
                        break;
                    case WatermarkPlacement.Diagonal:
                    default:
                        angle = options.RotationAngle != 0 ? options.RotationAngle : 45;
                        break;
                }

                gfx.TranslateTransform(posX, posY);
                gfx.RotateTransform(angle);

                var format = new XStringFormat
                {
                    Alignment = XStringAlignment.Center,
                    LineAlignment = XLineAlignment.Center
                };

                gfx.DrawString(text, font, brush, 0, 0, format);
                gfx.Restore(state);
            }
        }

        private static void DrawImageWatermark(XGraphics gfx, XImage img, double width, double height, WatermarkOptions options)
        {
            var state = gfx.Save();
            double imgW = img.PointWidth * options.ImageScale;
            double imgH = img.PointHeight * options.ImageScale;

            double posX = (width - imgW) / 2;
            double posY = (height - imgH) / 2;

            gfx.TranslateTransform(width / 2, height / 2);
            gfx.RotateTransform(options.RotationAngle);
            gfx.DrawImage(img, -imgW / 2, -imgH / 2, imgW, imgH);
            gfx.Restore(state);
        }

        private static bool ShouldApplyToPage(int pageNum, int totalPages, WatermarkOptions options)
        {
            return options.TargetPages switch
            {
                TargetPagesMode.OddPages => pageNum % 2 != 0,
                TargetPagesMode.EvenPages => pageNum % 2 == 0,
                TargetPagesMode.CustomRange => IsPageInRange(pageNum, options.CustomPageRange, totalPages),
                _ => true
            };
        }

        private static bool IsPageInRange(int pageNum, string rangeText, int totalPages)
        {
            var groups = PdfSplitService.ParsePageRanges(rangeText, totalPages);
            return groups.Any(g => g.Contains(pageNum));
        }

        private static XColor ParseColor(string hex, double opacity)
        {
            try
            {
                hex = hex.TrimStart('#');
                byte a = (byte)(Math.Clamp(opacity, 0.0, 1.0) * 255);
                byte r = 0, g = 0, b = 0;
                if (hex.Length == 6)
                {
                    r = Convert.ToByte(hex.Substring(0, 2), 16);
                    g = Convert.ToByte(hex.Substring(2, 2), 16);
                    b = Convert.ToByte(hex.Substring(4, 2), 16);
                }
                return XColor.FromArgb(a, r, g, b);
            }
            catch
            {
                return XColor.FromArgb((byte)(opacity * 255), 200, 0, 0);
            }
        }
    }
}
