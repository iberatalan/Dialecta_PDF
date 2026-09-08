using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace PdfMaster.Services
{
    public class ImageToPdfService
    {
        public static readonly string[] SupportedExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff" };

        public static async Task ConvertMultipleImagesToSinglePdf(IEnumerable<string> imagePaths, string outputPath, Action<int> progressCallback = null)
        {
            var paths = imagePaths.ToList();
            int total = paths.Count;
            if (total == 0) return;

            await Task.Run(() =>
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "DialectaPDF_ImageToPdf_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempDir);

                try
                {
                    int completed = 0;
                    var tempFiles = new string[total];

                    int maxThreads = (int)Math.Max(1, Environment.ProcessorCount * 0.75);

                    Parallel.ForEach(paths, new ParallelOptions { MaxDegreeOfParallelism = maxThreads }, (imgPath, state, index) =>
                    {
                        try
                        {
                            using var document = new PdfDocument();
                            using var correctedStream = GetCorrectedImageStream(imgPath);
                            using var image = XImage.FromStream(() => correctedStream);
                            var page = document.AddPage();
                            
                            page.Width = image.PointWidth;
                            page.Height = image.PointHeight;

                            using var gfx = XGraphics.FromPdfPage(page);
                            gfx.DrawImage(image, 0, 0, page.Width, page.Height);

                            string tempFile = Path.Combine(tempDir, $"{index:D5}.pdf");
                            document.Save(tempFile);
                            tempFiles[index] = tempFile;
                        }
                        catch (Exception ex)
                        {
                            throw new Exception($"'{Path.GetFileName(imgPath)}' işlenirken hata oluştu: {ex.Message}", ex);
                        }

                        int c = Interlocked.Increment(ref completed);
                        progressCallback?.Invoke((int)(c / (double)total * 80));
                    });

                    progressCallback?.Invoke(85);
                    using var outputDocument = new PdfDocument();
                    
                    int mergeCount = 0;
                    var validTempFiles = tempFiles.Where(f => !string.IsNullOrEmpty(f) && File.Exists(f)).ToList();
                    int totalValid = validTempFiles.Count;

                    foreach (var tf in validTempFiles)
                    {
                        using var inputDocument = PdfReader.Open(tf, PdfDocumentOpenMode.Import);
                        outputDocument.AddPage(inputDocument.Pages[0]);
                        
                        mergeCount++;
                        progressCallback?.Invoke(80 + (int)(mergeCount / (double)totalValid * 20));
                    }

                    outputDocument.Save(outputPath);
                }
                finally
                {
                    try
                    {
                        if (Directory.Exists(tempDir))
                        {
                            Directory.Delete(tempDir, true);
                        }
                    }
                    catch { }
                }
            });
        }

        public static async Task ConvertMultipleImagesToSeparatePdfs(IEnumerable<string> imagePaths, string outputDirectory, Action<int> progressCallback = null)
        {
            var paths = imagePaths.ToList();
            int total = paths.Count;
            int completed = 0;

            await Task.Run(() =>
            {
                int maxThreads = (int)Math.Max(1, Environment.ProcessorCount * 0.75);

                Parallel.ForEach(paths, new ParallelOptions { MaxDegreeOfParallelism = maxThreads }, imgPath =>
                {
                    try
                    {
                        using var document = new PdfDocument();
                        using var correctedStream = GetCorrectedImageStream(imgPath);
                        using var image = XImage.FromStream(() => correctedStream);
                        var page = document.AddPage();
                        
                        page.Width = image.PointWidth;
                        page.Height = image.PointHeight;

                        using var gfx = XGraphics.FromPdfPage(page);
                        gfx.DrawImage(image, 0, 0, page.Width, page.Height);

                        string fileName = Path.GetFileNameWithoutExtension(imgPath) + ".pdf";
                        string outputPath = Path.Combine(outputDirectory, fileName);
                        document.Save(outputPath);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"'{Path.GetFileName(imgPath)}' işlenirken hata oluştu: {ex.Message}", ex);
                    }

                    int c = Interlocked.Increment(ref completed);
                    progressCallback?.Invoke((int)(c / (double)total * 100));
                });
            });
        }
        private static MemoryStream GetCorrectedImageStream(string imagePath)
        {
            var ms = new MemoryStream();
            using (var img = System.Drawing.Image.FromFile(imagePath))
            {
                if (img.PropertyIdList.Contains(0x0112))
                {
                    var prop = img.GetPropertyItem(0x0112);
                    if (prop != null && prop.Value != null)
                    {
                        int orientation = BitConverter.ToUInt16(prop.Value, 0);
                        switch (orientation)
                        {
                            case 1: break; // Normal
                            case 2: img.RotateFlip(System.Drawing.RotateFlipType.RotateNoneFlipX); break;
                            case 3: img.RotateFlip(System.Drawing.RotateFlipType.Rotate180FlipNone); break;
                            case 4: img.RotateFlip(System.Drawing.RotateFlipType.Rotate180FlipX); break;
                            case 5: img.RotateFlip(System.Drawing.RotateFlipType.Rotate90FlipX); break;
                            case 6: img.RotateFlip(System.Drawing.RotateFlipType.Rotate90FlipNone); break;
                            case 7: img.RotateFlip(System.Drawing.RotateFlipType.Rotate270FlipX); break;
                            case 8: img.RotateFlip(System.Drawing.RotateFlipType.Rotate270FlipNone); break;
                        }
                        img.RemovePropertyItem(0x0112);
                    }
                }
                
                var format = imagePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) 
                    ? System.Drawing.Imaging.ImageFormat.Png 
                    : System.Drawing.Imaging.ImageFormat.Jpeg;
                img.Save(ms, format);
            }
            
            ms.Position = 0;
            return ms;
        }
    }
}
