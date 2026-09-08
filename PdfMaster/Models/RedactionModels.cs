using System.Collections.Generic;

namespace PdfMaster.Models
{
    public class RedactionRect
    {
        public int PageIndex { get; set; } // 0-based
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string? Note { get; set; }
    }

    public class RedactionOptions
    {
        public List<RedactionRect> ManualRectangles { get; set; } = new();
        public List<string> KeywordsToRedact { get; set; } = new();
        public bool MatchWholeWord { get; set; } = false;
        public bool CaseSensitive { get; set; } = false;
        public string FillColorHex { get; set; } = "#000000"; // Black
        public bool OverlayText { get; set; } = false;
        public string OverlayTextContent { get; set; } = "[GİZLENDİ]";
    }
}
