using System;
using System.Collections.Generic;

namespace PdfMaster.Models
{
    public class OperationResult
    {
        public bool IsSuccess { get; set; }
        public string? OutputFilePath { get; set; }
        public List<string> OutputFiles { get; set; } = new();
        public string? OutputFileHash { get; set; }
        public string Message { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public List<string> Warnings { get; set; } = new();
        public long OriginalSizeBytes { get; set; }
        public long ResultSizeBytes { get; set; }

        public static OperationResult Success(string outputPath, string message, string? hash = null, List<string>? warnings = null)
        {
            var res = new OperationResult
            {
                IsSuccess = true,
                OutputFilePath = outputPath,
                OutputFileHash = hash,
                Message = message,
                Warnings = warnings ?? new List<string>()
            };
            if (!string.IsNullOrEmpty(outputPath))
            {
                res.OutputFiles.Add(outputPath);
            }
            return res;
        }

        public static OperationResult SuccessMultiple(List<string> outputPaths, string message)
        {
            return new OperationResult
            {
                IsSuccess = true,
                OutputFiles = outputPaths,
                OutputFilePath = outputPaths.Count > 0 ? outputPaths[0] : null,
                Message = message
            };
        }

        public static OperationResult Failure(string errorMessage, List<string>? warnings = null)
        {
            return new OperationResult
            {
                IsSuccess = false,
                Message = errorMessage,
                Warnings = warnings ?? new List<string>()
            };
        }
    }

    public class PdfWarningInfo
    {
        public bool HasEncryption { get; set; }
        public bool HasAcroForms { get; set; }
        public bool HasDigitalSignatures { get; set; }
        public bool HasNonStandardFonts { get; set; }
        public int PageCount { get; set; }
        public string Version { get; set; } = "1.7";
        public List<string> WarningMessages { get; set; } = new();

        public bool HasAnyWarning => WarningMessages.Count > 0 || HasEncryption || HasAcroForms || HasDigitalSignatures;
    }
}
