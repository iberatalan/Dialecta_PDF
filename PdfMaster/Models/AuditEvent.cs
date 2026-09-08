using System;

namespace PdfMaster.Models
{
    public enum ActionType
    {
        Merge,
        Split,
        Organize,
        Rotate,
        Compress,
        Watermark,
        Redact,
        Ocr,
        SignAndSeal,
        ImageToPdf,
        Delete,
        Backup,
        IntegrityCheck
    }

    public class AuditEvent
    {
        public string EventId { get; set; } = Guid.NewGuid().ToString("N");
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
        public DateTime TimestampLocal { get; set; } = DateTime.Now;
        public ActionType Action { get; set; }
        public string ActionDescription { get; set; } = string.Empty;
        public string SourceFileName { get; set; } = string.Empty;
        public string? SourceFilePath { get; set; }
        public string? SourceFileHash { get; set; }
        public string? OutputFileName { get; set; }
        public string? OutputFilePath { get; set; }
        public string? OutputFileHash { get; set; }
        public int PageCount { get; set; }
        public long FileSizeBytes { get; set; }
        public double DurationMs { get; set; }
        public bool IsSuccess { get; set; } = true;
        public string? ErrorMessage { get; set; }
        public string? AdditionalDetails { get; set; }
    }
}
