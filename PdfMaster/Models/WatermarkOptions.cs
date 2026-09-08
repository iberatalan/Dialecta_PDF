namespace PdfMaster.Models
{
    public enum WatermarkType
    {
        Text,
        Image
    }

    public enum WatermarkPlacement
    {
        Center,
        Diagonal,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        Tile
    }

    public enum TargetPagesMode
    {
        AllPages,
        OddPages,
        EvenPages,
        CustomRange
    }

    public class WatermarkOptions
    {
        public WatermarkType Type { get; set; } = WatermarkType.Text;
        public string Text { get; set; } = "GİZLİ / KİŞİSEL";
        public string FontFamily { get; set; } = "Arial";
        public double FontSize { get; set; } = 42;
        public string ColorHex { get; set; } = "#FF0000"; // Red default
        public double Opacity { get; set; } = 0.35; // 0.0 to 1.0
        public double RotationAngle { get; set; } = 45; // Degrees
        public WatermarkPlacement Placement { get; set; } = WatermarkPlacement.Diagonal;
        public TargetPagesMode TargetPages { get; set; } = TargetPagesMode.AllPages;
        public string CustomPageRange { get; set; } = string.Empty;
        public string? ImagePath { get; set; }
        public double ImageScale { get; set; } = 0.5;
    }
}
