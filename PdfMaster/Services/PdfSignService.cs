using System;
using System.Diagnostics;
using System.IO;
using PdfMaster.Models;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace PdfMaster.Services
{
    public class PdfSignService
    {
        public static OperationResult SignAndSealPdf(string sourceFilePath, SignSealOptions options, string? customOutputName = null)
        {
            var sw = Stopwatch.StartNew();
            if (!File.Exists(sourceFilePath))
                return OperationResult.Failure("Kaynak dosya bulunamadı.");

            StorageManager.Instance.ImportOriginal(sourceFilePath);
            var preSealHash = SecurityAndHashService.ComputeSha256(sourceFilePath);
            var baseFileName = !string.IsNullOrWhiteSpace(customOutputName)
                ? customOutputName
                : Path.GetFileNameWithoutExtension(sourceFilePath);

            try
            {
                using var document = PdfReader.Open(sourceFilePath, PdfDocumentOpenMode.Modify);

                int targetPage = Math.Clamp(options.PageIndex, 0, document.PageCount - 1);
                var page = document.Pages[targetPage];
                using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);

                double x = options.X;
                double y = options.Y;
                double w = options.Width;
                double h = options.Height;

                if (options.Type == SignType.CryptoSealStamp || options.Type == SignType.Both)
                {
                    DrawCryptoSealBox(gfx, x, y, w, h, options, preSealHash);
                }

                if ((options.Type == SignType.DrawnOrImage || options.Type == SignType.Both) && options.SignatureImageBytes != null && options.SignatureImageBytes.Length > 0)
                {
                    using var ms = new MemoryStream(options.SignatureImageBytes);
                    var img = XImage.FromStream(() => ms);

                    double sigW = Math.Min(w * 0.45, 120);
                    double sigH = Math.Min(h * 0.7, 50);
                    double sigX = x + w - sigW - 8;
                    double sigY = y + (h - sigH) / 2;

                    gfx.DrawImage(img, sigX, sigY, sigW, sigH);
                }

                var outputPath = StorageManager.Instance.GenerateVersionedOutputPath(baseFileName, "imzali_muhurlu");
                document.Save(outputPath);

                sw.Stop();
                var finalSealHash = SecurityAndHashService.ComputeSha256(outputPath);
                var fi = new FileInfo(outputPath);

                var audit = new AuditEvent
                {
                    Action = ActionType.SignAndSeal,
                    ActionDescription = $"Belge yerel kriptografik mühür ve imza ile mühürlendi (İmzalayan: {options.SignerName}).",
                    SourceFileName = Path.GetFileName(sourceFilePath),
                    SourceFilePath = sourceFilePath,
                    SourceFileHash = preSealHash,
                    OutputFileName = Path.GetFileName(outputPath),
                    OutputFilePath = outputPath,
                    OutputFileHash = finalSealHash,
                    PageCount = document.PageCount,
                    FileSizeBytes = fi.Length,
                    DurationMs = sw.ElapsedMilliseconds,
                    IsSuccess = true,
                    AdditionalDetails = $"İmzalayan: {options.SignerName} | Sebep: {options.Reason} | Konum: {options.Location} | Özet: {preSealHash.Substring(0, 16)}..."
                };
                AuditLogger.LogEvent(audit);

                var result = OperationResult.Success(
                    outputPath,
                    $"Belge başarıyla mühürlendi ve imzalandı.\nBelge Özeti (SHA-256): {finalSealHash}",
                    finalSealHash
                );
                result.Duration = sw.Elapsed;
                result.ResultSizeBytes = fi.Length;
                return result;
            }
            catch (Exception ex)
            {
                sw.Stop();
                string friendlyMessage = ex.Message;
                if (friendlyMessage.Contains("XRef table") || friendlyMessage.Contains("cross-reference"))
                {
                    try
                    {
                        var repairedFile = PdfRepairService.RepairPdf(sourceFilePath);
                        var result = SignAndSealPdf(repairedFile, options, customOutputName);
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
                    Action = ActionType.SignAndSeal,
                    ActionDescription = "İmzalama ve mühürleme işlemi başarısız oldu.",
                    SourceFileName = Path.GetFileName(sourceFilePath),
                    IsSuccess = false,
                    ErrorMessage = friendlyMessage,
                    DurationMs = sw.ElapsedMilliseconds
                };
                AuditLogger.LogEvent(audit);

                return OperationResult.Failure($"İmza/Mühürleme hatası: {friendlyMessage}");
            }
        }

        private static void DrawCryptoSealBox(XGraphics gfx, double x, double y, double w, double h, SignSealOptions options, string sourceHash)
        {
            var bgBrush = new XSolidBrush(XColor.FromArgb(245, 248, 255));
            var borderPen = new XPen(XColor.FromArgb(41, 128, 185), 1.5);
            var headerBrush = new XSolidBrush(XColor.FromArgb(41, 128, 185));
            var textBrush = new XSolidBrush(XColor.FromArgb(30, 41, 59));
            var metaBrush = new XSolidBrush(XColor.FromArgb(100, 116, 139));

            gfx.DrawRoundedRectangle(borderPen, bgBrush, x, y, w, h, 6, 6);

            var barBrush = new XSolidBrush(XColor.FromArgb(41, 128, 185));
            gfx.DrawRoundedRectangle(barBrush, x, y, 5, h, 2, 2);

            var titleFont = new XFont("Arial", 8, XFontStyle.Bold);
            var bodyFont = new XFont("Arial", 7.5, XFontStyle.Regular);
            var hashFont = new XFont("Courier New", 6.5, XFontStyle.Regular);

            double curY = y + 12;
            double curX = x + 12;

            gfx.DrawString("YEREL GÜVENLİ MÜHÜR & DİJİTAL İMZA", titleFont, headerBrush, curX, curY);
            curY += 12;

            var signerText = options.SignerName.Length > 25 ? options.SignerName.Substring(0, 22) + "..." : options.SignerName;
            gfx.DrawString($"İmzalayan: {signerText}", bodyFont, textBrush, curX, curY);
            curY += 10;

            if (!string.IsNullOrEmpty(options.Reason))
            {
                var reasonText = options.Reason.Length > 25 ? options.Reason.Substring(0, 22) + "..." : options.Reason;
                gfx.DrawString($"Amaç: {reasonText}", bodyFont, metaBrush, curX, curY);
                curY += 10;
            }

            if (options.IncludeTimestampInStamp)
            {
                gfx.DrawString($"Tarih: {DateTime.Now:dd.MM.yyyy HH:mm:ss} (Yerel)", bodyFont, metaBrush, curX, curY);
            }

            if (options.IncludeSha256HashInStamp)
            {
                var shortHash = sourceHash.Length >= 24 ? sourceHash.Substring(0, 24) + "..." : sourceHash;
                gfx.DrawString($"SHA-256: {shortHash}", hashFont, metaBrush, x + 12, y + h - 8);
            }
        }
    }
}
