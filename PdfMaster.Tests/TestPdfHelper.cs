using System.IO;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;

namespace PdfMaster.Tests
{
    public static class TestPdfHelper
    {
        public static string CreateSamplePdf(string folderPath, string fileName, int pageCount, string contentPrefix = "Sayfa")
        {
            Directory.CreateDirectory(folderPath);
            var filePath = Path.Combine(folderPath, fileName);

            using var doc = new PdfDocument();
            var font = new XFont("Arial", 16, XFontStyle.Regular);
            var brush = new XSolidBrush(XColors.DarkBlue);

            for (int i = 1; i <= pageCount; i++)
            {
                var page = doc.AddPage();
                using var gfx = XGraphics.FromPdfPage(page);
                gfx.DrawString($"{contentPrefix} {i} - Test Belgesi", font, brush, 50, 100);
                gfx.DrawString("TC Kimlik No: 12345678901", font, brush, 50, 140);
                gfx.DrawString("Gizli ve Hassas Not: Proje Raporu", font, brush, 50, 180);
            }

            doc.Save(filePath);
            return filePath;
        }
    }
}
