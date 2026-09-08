using System;

namespace PdfMaster.Models
{
    public enum SignType
    {
        DrawnOrImage,
        CryptoSealStamp,
        Both
    }

    public class SignSealOptions
    {
        public SignType Type { get; set; } = SignType.Both;
        public string SignerName { get; set; } = "Yerel Kullanıcı";
        public string Reason { get; set; } = "Kişisel Doğrulama ve Belge Onayı";
        public string Location { get; set; } = "Yerel Cihaz (Çevrimdışı)";
        public int PageIndex { get; set; } = 0; // 0-based
        public double X { get; set; } = 50;
        public double Y { get; set; } = 50;
        public double Width { get; set; } = 220;
        public double Height { get; set; } = 80;
        public byte[]? SignatureImageBytes { get; set; }
        public bool IncludeSha256HashInStamp { get; set; } = true;
        public bool IncludeTimestampInStamp { get; set; } = true;
    }
}
