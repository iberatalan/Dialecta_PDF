using System;
using System.Collections.Generic;
using System.IO;
using PdfMaster.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Tokens;

namespace PdfMaster.Services
{
    public class PdfInspectionService
    {
        public static PdfWarningInfo InspectPdf(string filePath)
        {
            var info = new PdfWarningInfo();

            if (!File.Exists(filePath))
            {
                info.WarningMessages.Add("Dosya bulunamadı.");
                return info;
            }

            try
            {
                using var document = PdfDocument.Open(filePath);
                info.PageCount = document.NumberOfPages;
                info.Version = document.Information?.Producer ?? "PDF 1.7";
                info.HasEncryption = document.IsEncrypted;

                if (info.HasEncryption)
                {
                    info.WarningMessages.Add(" Belge şifrelenmiş veya kısıtlanmış erişim izinlerine sahip.");
                }

                try
                {
                    if (document.Structure?.Catalog?.CatalogDictionary?.ContainsKey(NameToken.AcroForm) == true)
                    {
                        info.HasAcroForms = true;
                        info.WarningMessages.Add(" Belgede interaktif form alanları (AcroForm) bulundu. Sayfa birleştirme/ayrıştırma işlemleri form alanlarını düzleştirebilir.");
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                info.WarningMessages.Add($" Belge taranırken uyarı: {ex.Message}");
            }

            return info;
        }
    }
}
