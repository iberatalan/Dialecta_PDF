using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using PdfMaster.Models;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace PdfMaster.Services
{
    public enum SplitMode
    {
        ExtractAllPages,
        PageRanges,
        EveryNPages
    }

    public class PdfSplitService
    {
        public static OperationResult SplitPdf(
            string sourceFilePath, 
            SplitMode mode, 
            string? rangeString = null, 
            int everyNPages = 1,
            IProgress<double>? progress = null)
        {
            var sw = Stopwatch.StartNew();
            if (!File.Exists(sourceFilePath))
                return OperationResult.Failure("Kaynak dosya bulunamadı.");

            var warnings = new List<string>();
            var warningInfo = PdfInspectionService.InspectPdf(sourceFilePath);
            if (warningInfo.HasAnyWarning)
            {
                warnings.AddRange(warningInfo.WarningMessages);
            }

            StorageManager.Instance.ImportOriginal(sourceFilePath);
            var sourceHash = SecurityAndHashService.ComputeSha256(sourceFilePath);
            var baseFileName = Path.GetFileNameWithoutExtension(sourceFilePath);
            var generatedFiles = new List<string>();

            try
            {
                int totalPages = 0;
                using (var fs = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    using var inputDocument = PdfReader.Open(fs, PdfDocumentOpenMode.InformationOnly);
                    totalPages = inputDocument.PageCount;
                }

                if (totalPages == 0)
                    return OperationResult.Failure("PDF dosyasında ayrılacak sayfa bulunamadı.");

                int processedCount = 0;

                if (mode == SplitMode.ExtractAllPages)
                {
                    var tempFiles = new string[totalPages];
                    int maxThreads = (int)Math.Max(1, Environment.ProcessorCount * 0.75);

                    Parallel.For(0, totalPages, new ParallelOptions { MaxDegreeOfParallelism = maxThreads }, i =>
                    {
                        using var fs = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                        using var threadDoc = PdfReader.Open(fs, PdfDocumentOpenMode.Import);
                        using var singleDoc = new PdfDocument();
                        singleDoc.AddPage(threadDoc.Pages[i]);
                        
                        var outPath = StorageManager.Instance.GenerateVersionedOutputPath($"{baseFileName}_sayfa_{i + 1}", "ayir");
                        singleDoc.Save(outPath);
                        tempFiles[i] = outPath;

                        int count = System.Threading.Interlocked.Increment(ref processedCount);
                        progress?.Report((double)count / totalPages * 100);
                    });
                    
                    generatedFiles.AddRange(tempFiles.Where(f => !string.IsNullOrEmpty(f)));
                }
                else if (mode == SplitMode.EveryNPages)
                {
                    int n = Math.Max(1, everyNPages);
                    int partCount = (int)Math.Ceiling((double)totalPages / n);
                    var tempFiles = new string[partCount];
                    int maxThreads = (int)Math.Max(1, Environment.ProcessorCount * 0.75);

                    Parallel.For(0, partCount, new ParallelOptions { MaxDegreeOfParallelism = maxThreads }, partIndex =>
                    {
                        int startPage = partIndex * n;
                        int endPage = Math.Min(startPage + n, totalPages);
                        
                        using var fs = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                        using var threadDoc = PdfReader.Open(fs, PdfDocumentOpenMode.Import);
                        using var partDoc = new PdfDocument();
                        
                        for (int p = startPage; p < endPage; p++)
                        {
                            partDoc.AddPage(threadDoc.Pages[p]);
                        }
                        
                        var outPath = StorageManager.Instance.GenerateVersionedOutputPath($"{baseFileName}_bolum_{partIndex + 1}", "ayir");
                        partDoc.Save(outPath);
                        tempFiles[partIndex] = outPath;

                        int count = System.Threading.Interlocked.Increment(ref processedCount);
                        progress?.Report((double)count / partCount * 100);
                    });
                    
                    generatedFiles.AddRange(tempFiles.Where(f => !string.IsNullOrEmpty(f)));
                }
                else if (mode == SplitMode.PageRanges)
                {
                    var pageGroups = ParsePageRanges(rangeString ?? "", totalPages);
                    if (pageGroups.Count == 0)
                    {
                        return OperationResult.Failure("Geçerli bir sayfa aralığı belirtilmedi (Örn: 1-3, 5, 7-10).");
                    }

                    var tempFiles = new string[pageGroups.Count];
                    int maxThreads = (int)Math.Max(1, Environment.ProcessorCount * 0.75);

                    Parallel.For(0, pageGroups.Count, new ParallelOptions { MaxDegreeOfParallelism = maxThreads }, i =>
                    {
                        var group = pageGroups[i];
                        using var fs = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                        using var threadDoc = PdfReader.Open(fs, PdfDocumentOpenMode.Import);
                        using var rangeDoc = new PdfDocument();
                        
                        foreach (var pageNum in group)
                        {
                            if (pageNum >= 1 && pageNum <= totalPages)
                            {
                                rangeDoc.AddPage(threadDoc.Pages[pageNum - 1]);
                            }
                        }

                        if (rangeDoc.PageCount > 0)
                        {
                            var outPath = StorageManager.Instance.GenerateVersionedOutputPath($"{baseFileName}_aralik_{i + 1}", "ayir");
                            rangeDoc.Save(outPath);
                            tempFiles[i] = outPath;
                        }

                        int count = System.Threading.Interlocked.Increment(ref processedCount);
                        progress?.Report((double)count / pageGroups.Count * 100);
                    });
                    
                    generatedFiles.AddRange(tempFiles.Where(f => !string.IsNullOrEmpty(f)));
                }

                sw.Stop();
                var firstHash = generatedFiles.Count > 0 ? SecurityAndHashService.ComputeSha256(generatedFiles[0]) : null;

                // Audit log
                var audit = new AuditEvent
                {
                    Action = ActionType.Split,
                    ActionDescription = $"PDF belgesi {generatedFiles.Count} parçaya ayrıldı ({mode}).",
                    SourceFileName = Path.GetFileName(sourceFilePath),
                    SourceFilePath = sourceFilePath,
                    SourceFileHash = sourceHash,
                    OutputFileName = string.Join(", ", generatedFiles.Select(Path.GetFileName)),
                    OutputFilePath = string.Join(";", generatedFiles),
                    OutputFileHash = firstHash,
                    PageCount = totalPages,
                    DurationMs = sw.ElapsedMilliseconds,
                    IsSuccess = true
                };
                AuditLogger.LogEvent(audit);

                var result = OperationResult.SuccessMultiple(
                    generatedFiles,
                    $"PDF başarıyla {generatedFiles.Count} ayrı belgeye bölündü."
                );
                result.Duration = sw.Elapsed;
                result.Warnings = warnings;
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
                    Action = ActionType.Split,
                    ActionDescription = "Ayırma işlemi başarısız oldu.",
                    SourceFileName = Path.GetFileName(sourceFilePath),
                    IsSuccess = false,
                    ErrorMessage = friendlyMessage,
                    DurationMs = sw.ElapsedMilliseconds
                };
                AuditLogger.LogEvent(audit);

                return OperationResult.Failure($"Bölme işlemi sırasında hata: {friendlyMessage}");
            }
        }

        public static List<List<int>> ParsePageRanges(string rangeText, int maxPages)
        {
            var result = new List<List<int>>();
            if (string.IsNullOrWhiteSpace(rangeText)) return result;

            var parts = rangeText.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                var matchRange = Regex.Match(trimmed, @"^(\d+)\s*-\s*(\d+)$");
                if (matchRange.Success)
                {
                    int start = int.Parse(matchRange.Groups[1].Value);
                    int end = int.Parse(matchRange.Groups[2].Value);
                    if (start > end) { int tmp = start; start = end; end = tmp; }

                    var rangeList = new List<int>();
                    for (int p = Math.Max(1, start); p <= Math.Min(maxPages, end); p++)
                    {
                        rangeList.Add(p);
                    }
                    if (rangeList.Count > 0) result.Add(rangeList);
                }
                else if (int.TryParse(trimmed, out int singlePage))
                {
                    if (singlePage >= 1 && singlePage <= maxPages)
                    {
                        result.Add(new List<int> { singlePage });
                    }
                }
            }

            return result;
        }
    }
}
