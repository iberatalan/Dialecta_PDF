using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using PdfMaster.Models;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf.IO;
using PigPdfDocument = UglyToad.PdfPig.PdfDocument;
using SharpPdfDocument = PdfSharpCore.Pdf.PdfDocument;

namespace PdfMaster.Services
{
    public class PdfRedactionService
    {
        public static OperationResult RedactPdf(string sourceFilePath, RedactionOptions options, string? customOutputName = null)
        {
            var sw = Stopwatch.StartNew();
            if (!File.Exists(sourceFilePath))
                return OperationResult.Failure("Kaynak dosya bulunamadı.");

            StorageManager.Instance.ImportOriginal(sourceFilePath);
            var sourceHash = SecurityAndHashService.ComputeSha256(sourceFilePath);
            var baseFileName = !string.IsNullOrWhiteSpace(customOutputName)
                ? customOutputName
                : Path.GetFileNameWithoutExtension(sourceFilePath);

            var redactionsByPage = new Dictionary<int, List<RedactionRect>>();

            foreach (var rect in options.ManualRectangles)
            {
                if (!redactionsByPage.ContainsKey(rect.PageIndex))
                    redactionsByPage[rect.PageIndex] = new List<RedactionRect>();
                redactionsByPage[rect.PageIndex].Add(rect);
            }

            int keywordMatchesCount = 0;

            
            if (options.KeywordsToRedact.Count > 0)
            {
                try
                {
                    using var pigDoc = PigPdfDocument.Open(sourceFilePath);
                    for (int pageIndex = 0; pageIndex < pigDoc.NumberOfPages; pageIndex++)
                    {
                        var pigPage = pigDoc.GetPage(pageIndex + 1);
                        var words = pigPage.GetWords().ToList();
                        var pageHeight = pigPage.Height;

                        foreach (var kw in options.KeywordsToRedact)
                        {
                            if (string.IsNullOrWhiteSpace(kw)) continue;

                            var comp = options.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

                            foreach (var word in words)
                            {
                                bool matches = options.MatchWholeWord 
                                    ? string.Equals(word.Text, kw, comp)
                                    : word.Text.Contains(kw, comp);

                                if (matches)
                                {
                                    keywordMatchesCount++;
                                    if (!redactionsByPage.ContainsKey(pageIndex))
                                        redactionsByPage[pageIndex] = new List<RedactionRect>();

                                    double x = word.BoundingBox.Left;
                                    double y = pageHeight - word.BoundingBox.Top;
                                    double w = word.BoundingBox.Width;
                                    double h = word.BoundingBox.Height;

                                    redactionsByPage[pageIndex].Add(new RedactionRect
                                    {
                                        PageIndex = pageIndex,
                                        X = Math.Max(0, x - 2),
                                        Y = Math.Max(0, y - 2),
                                        Width = w + 4,
                                        Height = h + 4,
                                        Note = $"Anahtar Kelime: {kw}"
                                    });
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Keyword search error: {ex.Message}");
                }
            }

            int totalRedactions = redactionsByPage.Values.Sum(l => l.Count);
            if (totalRedactions == 0)
            {
                return OperationResult.Failure("Karartılacak herhangi bir alan veya eşleşen anahtar kelime bulunamadı.");
            }

            try
            {
                using var document = PdfReader.Open(sourceFilePath, PdfDocumentOpenMode.Modify);

                var fillColor = ParseFillColor(options.FillColorHex);
                var brush = new XSolidBrush(fillColor);
                var textBrush = new XSolidBrush(XColors.White);
                var font = new XFont("Arial", 8, XFontStyle.Bold);

                foreach (var kvp in redactionsByPage)
                {
                    int pageIndex = kvp.Key;
                    if (pageIndex < 0 || pageIndex >= document.PageCount) continue;

                    var page = document.Pages[pageIndex];
                    using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);

                    foreach (var rect in kvp.Value)
                    {
                        gfx.DrawRectangle(brush, rect.X, rect.Y, rect.Width, rect.Height);

                        if (options.OverlayText && !string.IsNullOrEmpty(options.OverlayTextContent) && rect.Width > 30 && rect.Height > 10)
                        {
                            var format = new XStringFormat
                            {
                                Alignment = XStringAlignment.Center,
                                LineAlignment = XLineAlignment.Center
                            };
                            gfx.DrawString(options.OverlayTextContent, font, textBrush,
                                new XRect(rect.X, rect.Y, rect.Width, rect.Height), format);
                        }
                    }
                }

                var outputPath = StorageManager.Instance.GenerateVersionedOutputPath(baseFileName, "karartilmis");
                document.Save(outputPath);

                sw.Stop();
                var outputHash = SecurityAndHashService.ComputeSha256(outputPath);
                var fi = new FileInfo(outputPath);

                var audit = new AuditEvent
                {
                    Action = ActionType.Redact,
                    ActionDescription = $"PDF belgesinde toplam {totalRedactions} alan kalıcı olarak karartıldı/maskelendi.",
                    SourceFileName = Path.GetFileName(sourceFilePath),
                    SourceFilePath = sourceFilePath,
                    SourceFileHash = sourceHash,
                    OutputFileName = Path.GetFileName(outputPath),
                    OutputFilePath = outputPath,
                    OutputFileHash = outputHash,
                    PageCount = document.PageCount,
                    FileSizeBytes = fi.Length,
                    DurationMs = sw.ElapsedMilliseconds,
                    IsSuccess = true
                };
                AuditLogger.LogEvent(audit);

                var result = OperationResult.Success(
                    outputPath,
                    $"Toplam {totalRedactions} hassas alan başarıyla karartıldı ve maskelendi.",
                    outputHash
                );
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
                    friendlyMessage = "Bu PDF dosyasının yapısal bütünlüğü bozulmuş veya güncel olmayan bir format kullanıyor. Lütfen dosyayı farklı bir PDF aracı ile kaydedip (örneğin Chrome'da açıp 'PDF olarak yazdır' diyerek) tekrar deneyin.";
                }

                var audit = new AuditEvent
                {
                    Action = ActionType.Redact,
                    ActionDescription = "Karartma işlemi başarısız oldu.",
                    SourceFileName = Path.GetFileName(sourceFilePath),
                    IsSuccess = false,
                    ErrorMessage = friendlyMessage,
                    DurationMs = sw.ElapsedMilliseconds
                };
                AuditLogger.LogEvent(audit);

                return OperationResult.Failure($"Karartma hatası: {friendlyMessage}");
            }
        }

        private static XColor ParseFillColor(string hex)
        {
            try
            {
                hex = hex.TrimStart('#');
                if (hex.Length == 6)
                {
                    byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                    byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                    byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                    return XColor.FromArgb(255, r, g, b);
                }
            }
            catch { }
            return XColors.Black;
        }
    }
}
