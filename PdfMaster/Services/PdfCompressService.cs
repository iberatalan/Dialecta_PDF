using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Ghostscript.NET;
using Ghostscript.NET.Processor;
using PdfMaster.Models;
using iText.Kernel.Pdf;

namespace PdfMaster.Services
{
    public class PdfCompressService
    {
        public static OperationResult CompressPdf(string sourceFilePath, CompressionOptions options, string? customOutputName = null)
        {
            var sw = Stopwatch.StartNew();
            if (!File.Exists(sourceFilePath))
                return OperationResult.Failure("Kaynak dosya bulunamadı.");

            var originalSize = new FileInfo(sourceFilePath).Length;
            StorageManager.Instance.ImportOriginal(sourceFilePath);
            var sourceHash = SecurityAndHashService.ComputeSha256(sourceFilePath);
            var baseFileName = !string.IsNullOrWhiteSpace(customOutputName)
                ? customOutputName
                : Path.GetFileNameWithoutExtension(sourceFilePath);

            try
            {
                var outputPath = StorageManager.Instance.GenerateVersionedOutputPath(baseFileName, "compressed");
                
                string pdfSettings = "/ebook"; // Varsayılan
                switch (options.Preset)
                {
                    case CompressionPreset.Low:
                        pdfSettings = "/printer"; // ~300 DPI, yüksek kalite
                        break;
                    case CompressionPreset.Medium:
                        pdfSettings = "/ebook"; // ~150 DPI, dengeli
                        break;
                    case CompressionPreset.High:
                        pdfSettings = "/screen"; // ~72 DPI, maksimum sıkıştırma
                        break;
                }

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string dllPath = Path.Combine(baseDir, "Assets", "Ghostscript", "gsdll64.dll");
                
                if (!File.Exists(dllPath))
                {
                    dllPath = Path.Combine(baseDir, "gsdll64.dll");
                }

                if (!File.Exists(dllPath))
                {
                    return OperationResult.Failure($"Ghostscript motoru bulunamadı. Lütfen '{dllPath}' dosyasının var olduğundan emin olun.");
                }

                int pageCount = 0;
                try
                {
                    using (var reader = new PdfReader(sourceFilePath))
                    using (var pdfDoc = new PdfDocument(reader))
                    {
                        pageCount = pdfDoc.GetNumberOfPages();
                    }
                }
                catch { /* Ignore, sadece sayfa sayısı içindi */ }

                GhostscriptVersionInfo gvi = new GhostscriptVersionInfo(new Version(0, 0, 0), dllPath, string.Empty, GhostscriptLicense.GPL);

                using (GhostscriptProcessor processor = new GhostscriptProcessor(gvi))
                {
                    var switches = new List<string>
                    {
                        "-empty",
                        "-dQUIET",
                        "-dSAFER",
                        "-dBATCH",
                        "-dNOPAUSE",
                        "-dNOPROMPT",
                        "-sDEVICE=pdfwrite",
                        "-dCompatibilityLevel=1.4",
                        $"-dPDFSETTINGS={pdfSettings}",
                        $"-dColorImageDownsampleType={GetDownsampleType(options.Preset)}",
                        "-dColorImageResolution=" + GetDpi(options.Preset),
                        $"-dGrayImageDownsampleType={GetDownsampleType(options.Preset)}",
                        "-dGrayImageResolution=" + GetDpi(options.Preset),
                        $"-dMonoImageDownsampleType={GetDownsampleType(options.Preset)}",
                        "-dMonoImageResolution=" + GetDpi(options.Preset),
                        $"-dNumRenderingThreads={Math.Max(1, Environment.ProcessorCount - 1)}",
                        $"-dCompressPages={(options.CompressStreams ? "true" : "false")}",
                        $"-sOutputFile={outputPath}",
                        sourceFilePath
                    };

                    processor.StartProcessing(switches.ToArray(), null);
                }

                sw.Stop();
                var fi = new FileInfo(outputPath);
                var resultSize = fi.Length;
                var outputHash = SecurityAndHashService.ComputeSha256(outputPath);

                var savedPercent = originalSize > 0 
                    ? Math.Max(0, (double)(originalSize - resultSize) / originalSize * 100.0) 
                    : 0;

                var audit = new AuditEvent
                {
                    Action = ActionType.Compress,
                    ActionDescription = $"Ghostscript ile PDF Sıkıştırıldı. Orijinal: {originalSize / 1024.0:F1} KB -> Çıktı: {resultSize / 1024.0:F1} KB (%{savedPercent:F1} tasarruf).",
                    SourceFileName = Path.GetFileName(sourceFilePath),
                    SourceFilePath = sourceFilePath,
                    SourceFileHash = sourceHash,
                    OutputFileName = Path.GetFileName(outputPath),
                    OutputFilePath = outputPath,
                    OutputFileHash = outputHash,
                    PageCount = pageCount,
                    FileSizeBytes = resultSize,
                    DurationMs = sw.ElapsedMilliseconds,
                    IsSuccess = true
                };
                AuditLogger.LogEvent(audit);

                var message = savedPercent > 0
                    ? $"Sıkıştırma tamamlandı! Boyut {originalSize / 1024.0:F1} KB'tan {resultSize / 1024.0:F1} KB'a indirildi (%{savedPercent:F1} kazanç)."
                    : $"Belge optimize edildi ({resultSize / 1024.0:F1} KB). Boyut daha fazla küçültülemedi çünkü vektörel alanlar ağırlıkta olabilir veya zaten maksimum sıkıştırılmış.";

                var result = OperationResult.Success(outputPath, message, outputHash);
                result.Duration = sw.Elapsed;
                result.OriginalSizeBytes = originalSize;
                result.ResultSizeBytes = resultSize;
                return result;
            }
            catch (Exception ex)
            {
                sw.Stop();
                var audit = new AuditEvent
                {
                    Action = ActionType.Compress,
                    ActionDescription = "Sıkıştırma işlemi başarısız oldu.",
                    SourceFileName = Path.GetFileName(sourceFilePath),
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    DurationMs = sw.ElapsedMilliseconds
                };
                AuditLogger.LogEvent(audit);

                return OperationResult.Failure($"Sıkıştırma hatası (Ghostscript): {ex.Message}");
            }
        }

        private static string GetDpi(CompressionPreset preset)
        {
            return preset switch
            {
                CompressionPreset.Low => "300",
                CompressionPreset.Medium => "150",
                CompressionPreset.High => "72",
                _ => "150"
            };
        }

        private static string GetDownsampleType(CompressionPreset preset)
        {
            return preset switch
            {
                CompressionPreset.Low => "/Bicubic", 
                CompressionPreset.Medium => "/Average",
                CompressionPreset.High => "/Subsample",
                _ => "/Average"
            };
        }
    }
}
