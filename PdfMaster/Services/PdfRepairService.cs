using System;
using System.IO;
using iText.Kernel.Pdf;
using PdfMaster.Models;

namespace PdfMaster.Services
{
    public static class PdfRepairService
    {
        public static string RepairPdf(string sourceFilePath)
        {
            try
            {
                var directory = Path.GetDirectoryName(sourceFilePath);
                var filename = Path.GetFileNameWithoutExtension(sourceFilePath);
                var ext = Path.GetExtension(sourceFilePath);
                
                string tempOutput = Path.Combine(directory ?? "", $"{filename}_repaired{ext}");

                using (var reader = new PdfReader(sourceFilePath))
                using (var writer = new PdfWriter(tempOutput))
                using (var pdfDoc = new PdfDocument(reader, writer))
                {
                }

                return tempOutput;
            }
            catch (Exception ex)
            {
                throw new Exception($"Dosya onarılamadı: {ex.Message}", ex);
            }
        }
    }
}
