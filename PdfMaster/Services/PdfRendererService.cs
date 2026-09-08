using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Docnet.Core;
using Docnet.Core.Models;

namespace PdfMaster.Services
{
    public class PdfRendererService
    {
        public static BitmapSource? RenderPageToBitmap(string filePath, int pageIndex, int targetWidth = 800, int targetHeight = 0)
        {
            if (!File.Exists(filePath)) return null;

            try
            {
                // If targetWidth is 0, let DocLib use native dimension
                int h = targetHeight > 0 ? targetHeight : (targetWidth > 0 ? targetWidth * 2 : 0);
                PageDimensions dimensions = targetWidth > 0 
                    ? new PageDimensions(targetWidth, h)
                    : new PageDimensions(1.0);

                using var docReader = DocLib.Instance.GetDocReader(filePath, dimensions);
                if (pageIndex < 0 || pageIndex >= docReader.GetPageCount())
                    return null;

                using var pageReader = docReader.GetPageReader(pageIndex);
                var width = pageReader.GetPageWidth();
                var height = pageReader.GetPageHeight();
                var rawBytes = pageReader.GetImage();

                if (rawBytes == null || rawBytes.Length == 0 || width <= 0 || height <= 0)
                    return null;

                var bitmap = BitmapSource.Create(
                    width,
                    height,
                    96,
                    96,
                    PixelFormats.Bgra32,
                    null,
                    rawBytes,
                    width * 4);

                bitmap.Freeze();
                return bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error rendering PDF page: {ex.Message}");
                return null;
            }
        }

        public static BitmapSource?[] RenderPagesToBitmaps(string filePath, int startIndex, int count, int targetWidth = 800, int targetHeight = 0, IProgress<double>? progress = null)
        {
            var results = new BitmapSource?[count];
            if (!File.Exists(filePath)) return results;

            try
            {
                int h = targetHeight > 0 ? targetHeight : (targetWidth > 0 ? targetWidth * 2 : 0);
                PageDimensions dimensions = targetWidth > 0 
                    ? new PageDimensions(targetWidth, h)
                    : new PageDimensions(1.0);

                int totalPages = 0;
                using (var infoReader = DocLib.Instance.GetDocReader(filePath, new PageDimensions(1.0)))
                {
                    totalPages = infoReader.GetPageCount();
                }

                int processedCount = 0;
                int maxThreads = (int)Math.Max(1, Environment.ProcessorCount * 0.75);

                Parallel.For(0, count, new ParallelOptions { MaxDegreeOfParallelism = maxThreads }, i =>
                {
                    int pageIndex = startIndex + i;
                    if (pageIndex >= 0 && pageIndex < totalPages)
                    {
                        try
                        {
                            using var docReader = DocLib.Instance.GetDocReader(filePath, dimensions);
                            using var pageReader = docReader.GetPageReader(pageIndex);
                            var width = pageReader.GetPageWidth();
                            var height = pageReader.GetPageHeight();
                            var rawBytes = pageReader.GetImage();

                            if (rawBytes != null && rawBytes.Length > 0 && width > 0 && height > 0)
                            {
                                var bitmap = BitmapSource.Create(
                                    width,
                                    height,
                                    96,
                                    96,
                                    PixelFormats.Bgra32,
                                    null,
                                    rawBytes,
                                    width * 4);

                                bitmap.Freeze();
                                results[i] = bitmap;
                            }
                        }
                        catch
                        {
                            // Ignore individual page errors in parallel loop
                        }
                    }

                    int current = System.Threading.Interlocked.Increment(ref processedCount);
                    progress?.Report((double)current / count * 100);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error rendering PDF pages: {ex.Message}");
            }
            
            return results;
        }

        public static int GetPageCount(string filePath)
        {
            if (!File.Exists(filePath)) return 0;
            try
            {
                using var docReader = DocLib.Instance.GetDocReader(filePath, new PageDimensions(1.0));
                return docReader.GetPageCount();
            }
            catch
            {
                // Fallback to UglyToad
                try
                {
                    using var doc = UglyToad.PdfPig.PdfDocument.Open(filePath);
                    return doc.NumberOfPages;
                }
                catch
                {
                    return 0;
                }
            }
        }

        public static (double Width, double Height) GetPageDimensions(string filePath, int pageIndex = 0)
        {
            if (!File.Exists(filePath)) return (595, 842);
            try
            {
                using var doc = UglyToad.PdfPig.PdfDocument.Open(filePath);
                if (pageIndex >= 0 && pageIndex < doc.NumberOfPages)
                {
                    var page = doc.GetPage(pageIndex + 1); // 1-indexed
                    return (page.Width, page.Height);
                }
            }
            catch { }
            return (595, 842);
        }
    }
}
