using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using PdfMaster.Models;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace PdfMaster.Services
{
    public class PdfOrganizeService
    {
        public static OperationResult OrganizeAndRotate(string sourceFilePath, List<PdfPageItem> pageItems, string? customOutputName = null)
        {
            var sw = Stopwatch.StartNew();
            if (!File.Exists(sourceFilePath))
                return OperationResult.Failure("Kaynak dosya bulunamadı.");

            var activePages = pageItems.Where(p => !p.IsDeleted).ToList();
            if (activePages.Count == 0)
                return OperationResult.Failure("En az bir sayfa seçili olmalıdır (Tüm sayfalar silinemez).");

            var warnings = new List<string>();
            var warningInfo = PdfInspectionService.InspectPdf(sourceFilePath);
            if (warningInfo.HasAnyWarning)
            {
                warnings.AddRange(warningInfo.WarningMessages);
            }

            StorageManager.Instance.ImportOriginal(sourceFilePath);
            var sourceHash = SecurityAndHashService.ComputeSha256(sourceFilePath);
            var baseFileName = !string.IsNullOrWhiteSpace(customOutputName)
                ? customOutputName
                : Path.GetFileNameWithoutExtension(sourceFilePath);

            try
            {
                using var inputDocument = PdfReader.Open(sourceFilePath, PdfDocumentOpenMode.Import);
                using var outputDocument = new PdfDocument();

                foreach (var item in activePages)
                {
                    if (item.OriginalPageIndex >= 0 && item.OriginalPageIndex < inputDocument.PageCount)
                    {
                        var page = inputDocument.Pages[item.OriginalPageIndex];
                        var newPage = outputDocument.AddPage(page);

                            if (item.RotationAngle != 0)
                        {
                            newPage.Rotate = (newPage.Rotate + item.RotationAngle) % 360;
                        }
                    }
                }

                var outputPath = StorageManager.Instance.GenerateVersionedOutputPath(baseFileName, "duzenle");
                outputDocument.Save(outputPath);

                sw.Stop();
                var outputHash = SecurityAndHashService.ComputeSha256(outputPath);
                var fi = new FileInfo(outputPath);

                var audit = new AuditEvent
                {
                    Action = ActionType.Organize,
                    ActionDescription = $"PDF sayfaları yeniden düzenlendi/döndürüldü ({activePages.Count} sayfa kaldı).",
                    SourceFileName = Path.GetFileName(sourceFilePath),
                    SourceFilePath = sourceFilePath,
                    SourceFileHash = sourceHash,
                    OutputFileName = Path.GetFileName(outputPath),
                    OutputFilePath = outputPath,
                    OutputFileHash = outputHash,
                    PageCount = activePages.Count,
                    FileSizeBytes = fi.Length,
                    DurationMs = sw.ElapsedMilliseconds,
                    IsSuccess = true
                };
                AuditLogger.LogEvent(audit);

                var result = OperationResult.Success(
                    outputPath,
                    $"Sayfalar başarıyla yeniden düzenlendi ve kaydedildi ({activePages.Count} sayfa).",
                    outputHash,
                    warnings
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
                    Action = ActionType.Organize,
                    ActionDescription = "Sayfa düzenleme işlemi başarısız oldu.",
                    SourceFileName = Path.GetFileName(sourceFilePath),
                    IsSuccess = false,
                    ErrorMessage = friendlyMessage,
                    DurationMs = sw.ElapsedMilliseconds
                };
                AuditLogger.LogEvent(audit);

                return OperationResult.Failure($"Sayfa düzenleme hatası: {friendlyMessage}");
            }
        }
    }
}
