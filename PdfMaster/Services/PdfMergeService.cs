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
    public class PdfMergeService
    {
        public static OperationResult MergePdfs(List<string> sourceFilePaths, string? customOutputName = null)
        {
            var sw = Stopwatch.StartNew();
            if (sourceFilePaths == null || sourceFilePaths.Count < 2)
            {
                return OperationResult.Failure("Birleştirme için en az 2 geçerli PDF dosyası seçilmelidir.");
            }

            var warnings = new List<string>();

            try
            {
                int maxThreads = (int)Math.Max(1, Environment.ProcessorCount * 0.75);
                var importedOriginals = new string[sourceFilePaths.Count];
                var sourceHashes = new string[sourceFilePaths.Count];
                var warningsDict = new System.Collections.Concurrent.ConcurrentBag<string>();

                Parallel.For(0, sourceFilePaths.Count, new ParallelOptions { MaxDegreeOfParallelism = maxThreads }, i =>
                {
                    var path = sourceFilePaths[i];
                    if (!File.Exists(path)) return;

                    var warningInfo = PdfInspectionService.InspectPdf(path);
                    if (warningInfo.HasAnyWarning)
                    {
                        foreach (var w in warningInfo.WarningMessages) warningsDict.Add(w);
                    }

                    importedOriginals[i] = StorageManager.Instance.ImportOriginal(path);
                    sourceHashes[i] = SecurityAndHashService.ComputeSha256(path);
                });

                warnings.AddRange(warningsDict.Distinct());

                using var outputDocument = new PdfDocument();
                int totalPages = 0;

                foreach (var path in sourceFilePaths)
                {
                    if (!File.Exists(path))
                    {
                        return OperationResult.Failure($"Kaynak dosya bulunamadı: {path}");
                    }

                    using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using var inputDocument = PdfReader.Open(fs, PdfDocumentOpenMode.Import);
                    for (int i = 0; i < inputDocument.PageCount; i++)
                    {
                        outputDocument.AddPage(inputDocument.Pages[i]);
                        totalPages++;
                    }
                }

                var baseName = !string.IsNullOrWhiteSpace(customOutputName) 
                    ? customOutputName 
                    : Path.GetFileNameWithoutExtension(sourceFilePaths[0]) + "_birlestirilmis";

                var outputPath = StorageManager.Instance.GenerateVersionedOutputPath(baseName, "birlestir");
                outputDocument.Save(outputPath);

                sw.Stop();
                var outputHash = SecurityAndHashService.ComputeSha256(outputPath);
                var fi = new FileInfo(outputPath);

                // Audit log
                var audit = new AuditEvent
                {
                    Action = ActionType.Merge,
                    ActionDescription = $"{sourceFilePaths.Count} adet PDF belgesi birleştirildi ({totalPages} sayfa).",
                    SourceFileName = string.Join(", ", sourceFilePaths.Select(Path.GetFileName)),
                    SourceFilePath = string.Join(";", sourceFilePaths),
                    SourceFileHash = string.Join(";", sourceHashes),
                    OutputFileName = Path.GetFileName(outputPath),
                    OutputFilePath = outputPath,
                    OutputFileHash = outputHash,
                    PageCount = totalPages,
                    FileSizeBytes = fi.Length,
                    DurationMs = sw.ElapsedMilliseconds,
                    IsSuccess = true
                };
                AuditLogger.LogEvent(audit);

                var result = OperationResult.Success(
                    outputPath,
                    $"Toplam {sourceFilePaths.Count} dosya başarıyla birleştirildi ({totalPages} sayfa).",
                    outputHash,
                    warnings.Distinct().ToList()
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
                    friendlyMessage = "PDF dosyalarından birinin yapısal bütünlüğü bozulmuş veya güncel olmayan bir format kullanıyor. Lütfen dosyaları farklı bir PDF aracı ile kaydedip (örneğin Chrome'da açıp 'PDF olarak yazdır' diyerek) tekrar deneyin.";
                }

                var audit = new AuditEvent
                {
                    Action = ActionType.Merge,
                    ActionDescription = "Birleştirme işlemi başarısız oldu.",
                    IsSuccess = false,
                    ErrorMessage = friendlyMessage,
                    DurationMs = sw.ElapsedMilliseconds
                };
                AuditLogger.LogEvent(audit);

                return OperationResult.Failure($"Birleştirme işlemi sırasında hata oluştu: {friendlyMessage}");
            }
        }
    }
}
