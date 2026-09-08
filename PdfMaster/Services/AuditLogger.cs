using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using PdfMaster.Models;

namespace PdfMaster.Services
{
    public class AuditLogger
    {
        private static readonly object _lock = new();
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            WriteIndented = false
        };

        public static void LogEvent(AuditEvent ev)
        {
            var logPath = StorageManager.Instance.AuditLogPath;
            var json = JsonSerializer.Serialize(ev, _jsonOptions);

            lock (_lock)
            {
                StorageManager.Instance.EnsureDirectoriesExist();
                using var writer = new StreamWriter(logPath, append: true, System.Text.Encoding.UTF8);
                writer.WriteLine(json);
            }
        }

        public static List<AuditEvent> GetAllEvents()
        {
            var logPath = StorageManager.Instance.AuditLogPath;
            var list = new List<AuditEvent>();

            if (!File.Exists(logPath))
                return list;

            lock (_lock)
            {
                try
                {
                    using var reader = new StreamReader(logPath, System.Text.Encoding.UTF8);
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        try
                        {
                            var ev = JsonSerializer.Deserialize<AuditEvent>(line, _jsonOptions);
                            if (ev != null)
                            {
                                list.Add(ev);
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }

            list.Reverse(); // Newest first
            return list;
        }

        public static List<AuditEvent> SearchEvents(string query)
        {
            var all = GetAllEvents();
            if (string.IsNullOrWhiteSpace(query))
                return all;

            var q = query.Trim().ToLowerInvariant();
            return all.Where(e =>
                (e.SourceFileName?.ToLowerInvariant().Contains(q) ?? false) ||
                (e.OutputFileName?.ToLowerInvariant().Contains(q) ?? false) ||
                (e.SourceFileHash?.ToLowerInvariant().Contains(q) ?? false) ||
                (e.OutputFileHash?.ToLowerInvariant().Contains(q) ?? false) ||
                (e.ActionDescription?.ToLowerInvariant().Contains(q) ?? false) ||
                e.Action.ToString().ToLowerInvariant().Contains(q)
            ).ToList();
        }

        public static (bool Found, AuditEvent? MatchingEvent, bool IsOutput) VerifyFileInAuditLog(string filePath)
        {
            if (!File.Exists(filePath))
                return (false, null, false);

            var hash = SecurityAndHashService.ComputeSha256(filePath);
            var all = GetAllEvents();

            var matchOutput = all.FirstOrDefault(e => string.Equals(e.OutputFileHash, hash, StringComparison.OrdinalIgnoreCase));
            if (matchOutput != null)
                return (true, matchOutput, true);

            var matchSource = all.FirstOrDefault(e => string.Equals(e.SourceFileHash, hash, StringComparison.OrdinalIgnoreCase));
            if (matchSource != null)
                return (true, matchSource, false);

            return (false, null, false);
        }
    }
}
