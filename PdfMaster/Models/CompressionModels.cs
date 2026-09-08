namespace PdfMaster.Models
{
    public enum CompressionPreset
    {
        Low,      // Yüksek Kalite, Az Sıkıştırma (Düşük)
        Medium,   // Dengeli (Orta)
        High      // Maksimum Sıkıştırma, Düşük Boyut (Yüksek)
    }

    public class CompressionOptions
    {
        public CompressionPreset Preset { get; set; } = CompressionPreset.Medium;
        public int ImageQualityPercent { get; set; } = 70; // 1-100
        public int MaxImageDpi { get; set; } = 150;
        public bool RemoveUnusedObjects { get; set; } = true;
        public bool CompressStreams { get; set; } = true;
    }
}
