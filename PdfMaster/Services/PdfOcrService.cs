using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Media.Imaging;
using Docnet.Core;
using Docnet.Core.Models;
using PdfMaster.Models;
using Tesseract;

namespace PdfMaster.Services
{
    public class OcrResult
    {
        public bool IsSuccess { get; set; }
        public string ExtractedText { get; set; } = string.Empty;
        public string? TextFilePath { get; set; }
        public string? SearchablePdfPath { get; set; }
        public float MeanConfidence { get; set; }
        public int PageCount { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public class PdfOcrService
    {
        public static string GetTessdataPath()
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var path = Path.Combine(appDir, "tessdata");
            if (!Directory.Exists(path))
            {
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    var exeDir = Path.GetDirectoryName(exePath);
                    if (exeDir != null)
                    {
                        var exeFallback = Path.Combine(exeDir, "tessdata");
                        if (Directory.Exists(exeFallback)) return exeFallback;
                    }
                }

                var fallback = Path.Combine(Directory.GetCurrentDirectory(), "tessdata");
                if (Directory.Exists(fallback)) return fallback;
            }
            return path;
        }

        public static bool IsTessdataAvailable(string language = "tur")
        {
            var path = GetTessdataPath();
            if (!Directory.Exists(path)) return false;
            var file = Path.Combine(path, $"{language}.traineddata");
            return File.Exists(file);
        }

        public static OcrResult PerformOcr(
            string sourceFilePath, 
            string language = "tur+eng", 
            IProgress<int>? progress = null, 
            CancellationToken cancellationToken = default)
        {
            var sw = Stopwatch.StartNew();
            if (!File.Exists(sourceFilePath))
            {
                return new OcrResult { IsSuccess = false, ErrorMessage = "Kaynak dosya bulunamadı." };
            }

            var tessdataPath = GetTessdataPath();
            if (!Directory.Exists(tessdataPath))
            {
                return new OcrResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"Tesseract dil modelleri dizini bulunamadı: {tessdataPath}. Lütfen tessdata klasörünün mevcut olduğundan emin olun."
                };
            }

            var actualLang = language;
            if (language.Contains('+'))
            {
                var primary = language.Split('+')[0];
                if (!IsTessdataAvailable(primary))
                {
                    actualLang = IsTessdataAvailable("eng") ? "eng" : (IsTessdataAvailable("tur") ? "tur" : primary);
                }
            }

            StorageManager.Instance.ImportOriginal(sourceFilePath);
            var sourceHash = SecurityAndHashService.ComputeSha256(sourceFilePath);
            var baseFileName = Path.GetFileNameWithoutExtension(sourceFilePath);

            var sb = new StringBuilder();
            float totalConfidence = 0;
            int processedPages = 0;

            try
            {
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    var exeDir = Path.GetDirectoryName(exePath);
                    if (exeDir != null)
                    {
                        TesseractEnviornment.CustomSearchPath = exeDir;
                    }
                }

                using var engine = new TesseractEngine(tessdataPath, actualLang, EngineMode.Default);
                using var docReader = DocLib.Instance.GetDocReader(sourceFilePath, new PageDimensions(4.0));
                int pageCount = docReader.GetPageCount();

                for (int i = 0; i < pageCount; i++)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return new OcrResult { IsSuccess = false, ErrorMessage = "İşlem kullanıcı tarafından iptal edildi." };
                    }

                    using var pageReader = docReader.GetPageReader(i);
                    var width = pageReader.GetPageWidth();
                    var height = pageReader.GetPageHeight();
                    var rawBytes = pageReader.GetImage(); // BGRA

                    if (rawBytes != null && width > 0 && height > 0)
                    {
                        using var pix = PixFromBgraBytes(rawBytes, width, height);
                        using var page = engine.Process(pix);
                        
                        var text = page.GetText();
                        var conf = page.GetMeanConfidence();

                        totalConfidence += conf;
                        processedPages++;

                        sb.AppendLine($"--- [Sayfa {i + 1} / {pageCount}] (Güven Oranı: %{conf * 100:F0}) ---");
                        sb.AppendLine(text.Trim());
                        sb.AppendLine();
                    }

                    progress?.Report((int)((i + 1.0) / pageCount * 100));

                    if (i > 0 && i % 5 == 0)
                    {
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                    }
                }

                sw.Stop();
                var fullText = sb.ToString();

                var textOutputPath = StorageManager.Instance.GenerateVersionedOutputPath(baseFileName, "ocr_metin", ".txt");
                File.WriteAllText(textOutputPath, fullText, Encoding.UTF8);

                float meanConfidence = processedPages > 0 ? (totalConfidence / processedPages) : 0;

                var audit = new AuditEvent
                {
                    Action = ActionType.Ocr,
                    ActionDescription = $"PDF belgesinde OCR gerçekleştirildi ({processedPages} sayfa, Ortalama Güven: %{meanConfidence * 100:F0}).",
                    SourceFileName = Path.GetFileName(sourceFilePath),
                    SourceFilePath = sourceFilePath,
                    SourceFileHash = sourceHash,
                    OutputFileName = Path.GetFileName(textOutputPath),
                    OutputFilePath = textOutputPath,
                    OutputFileHash = SecurityAndHashService.ComputeSha256(textOutputPath),
                    PageCount = processedPages,
                    DurationMs = sw.ElapsedMilliseconds,
                    IsSuccess = true
                };
                AuditLogger.LogEvent(audit);

                return new OcrResult
                {
                    IsSuccess = true,
                    ExtractedText = fullText,
                    TextFilePath = textOutputPath,
                    MeanConfidence = meanConfidence,
                    PageCount = processedPages
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                var audit = new AuditEvent
                {
                    Action = ActionType.Ocr,
                    ActionDescription = "Yerel OCR işlemi başarısız oldu.",
                    SourceFileName = Path.GetFileName(sourceFilePath),
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    DurationMs = sw.ElapsedMilliseconds
                };
                AuditLogger.LogEvent(audit);

                return new OcrResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"OCR Motoru hatası: {ex.Message}"
                };
            }
        }

        private static Pix PixFromBgraBytes(byte[] bgraBytes, int width, int height)
        {
            var bitmap = BitmapSource.Create(
                width,
                height,
                300,
                300,
                System.Windows.Media.PixelFormats.Bgra32,
                null,
                bgraBytes,
                width * 4);
            bitmap.Freeze();

            var grayBitmap = new System.Windows.Media.Imaging.FormatConvertedBitmap();
            grayBitmap.BeginInit();
            grayBitmap.Source = bitmap;
            grayBitmap.DestinationFormat = System.Windows.Media.PixelFormats.Gray8;
            grayBitmap.EndInit();
            grayBitmap.Freeze();

            using var memory = new MemoryStream();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(grayBitmap));
            encoder.Save(memory);
            var imageBytes = memory.ToArray();

            return Pix.LoadFromMemory(imageBytes);
        }
    }
}
