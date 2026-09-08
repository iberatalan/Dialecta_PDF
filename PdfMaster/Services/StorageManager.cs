using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace PdfMaster.Services
{
    public class StorageManager
    {
        private static StorageManager? _instance;
        public static StorageManager Instance => _instance ??= new StorageManager();

        private string _baseDirectory;

        public StorageManager(string? customBaseDirectory = null)
        {
            if (!string.IsNullOrWhiteSpace(customBaseDirectory))
            {
                _baseDirectory = customBaseDirectory;
            }
            else
            {
                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                _baseDirectory = Path.Combine(appDir, "DialectaPDF_Veri");
            }

            EnsureDirectoriesExist();
        }

        public string BaseDirectory => _baseDirectory;
        public string OriginalsDirectory => Path.Combine(_baseDirectory, "Orijinaller");
        public string OutputsDirectory => Path.Combine(_baseDirectory, "Ciktilar");
        public string BackupsDirectory => Path.Combine(_baseDirectory, "Yedekler");
        public string AuditLogPath => Path.Combine(_baseDirectory, "denetim_kayitlari.jsonl");

        public void SetBaseDirectory(string newPath)
        {
            if (string.IsNullOrWhiteSpace(newPath)) return;
            _baseDirectory = newPath;
            EnsureDirectoriesExist();
        }

        public void EnsureDirectoriesExist()
        {
            Directory.CreateDirectory(_baseDirectory);
            Directory.CreateDirectory(OriginalsDirectory);
            Directory.CreateDirectory(OutputsDirectory);
            Directory.CreateDirectory(BackupsDirectory);
        }

        public string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(fileName
                .Where(c => !invalidChars.Contains(c))
                .ToArray())
                .Trim();

            if (string.IsNullOrWhiteSpace(sanitized))
            {
                sanitized = "Belge_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            }

            return sanitized;
        }

        public string ImportOriginal(string sourceFilePath)
        {
            EnsureDirectoriesExist();

            if (!File.Exists(sourceFilePath))
                throw new FileNotFoundException("Orijinal dosya bulunamadı", sourceFilePath);

            var fileName = Path.GetFileNameWithoutExtension(sourceFilePath);
            var extension = Path.GetExtension(sourceFilePath);
            var safeName = SanitizeFileName(fileName);
            var hash = SecurityAndHashService.ComputeSha256(sourceFilePath);
            var shortHash = hash.Substring(0, 8);

            var targetFileName = $"{safeName}_{shortHash}{extension}";
            var targetPath = Path.Combine(OriginalsDirectory, targetFileName);

            if (!File.Exists(targetPath))
            {
                File.Copy(sourceFilePath, targetPath, overwrite: false);
                try
                {
                    File.SetAttributes(targetPath, FileAttributes.ReadOnly);
                }
                catch { }
            }

            return targetPath;
        }

        public string GenerateVersionedOutputPath(string originalFileNameOrPath, string actionName, string extension = ".pdf")
        {
            EnsureDirectoriesExist();

            var rawName = Path.GetFileNameWithoutExtension(originalFileNameOrPath);
            var cleanName = SanitizeFileName(rawName);
            var cleanAction = SanitizeFileName(actionName);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            var outputFileName = $"{cleanName}_{cleanAction}_v{timestamp}{extension}";
            var outputPath = Path.Combine(OutputsDirectory, outputFileName);

            int counter = 1;
            while (File.Exists(outputPath))
            {
                outputFileName = $"{cleanName}_{cleanAction}_v{timestamp}_{counter}{extension}";
                outputPath = Path.Combine(OutputsDirectory, outputFileName);
                counter++;
            }

            return outputPath;
        }

        public List<FileInfo> GetOutputFiles()
        {
            EnsureDirectoriesExist();
            var dir = new DirectoryInfo(OutputsDirectory);
            if (!dir.Exists) return new List<FileInfo>();
            return dir.GetFiles().OrderByDescending(f => f.LastWriteTime).ToList();
        }

        public List<FileInfo> GetOriginalFiles()
        {
            EnsureDirectoriesExist();
            var dir = new DirectoryInfo(OriginalsDirectory);
            if (!dir.Exists) return new List<FileInfo>();
            return dir.GetFiles().OrderByDescending(f => f.LastWriteTime).ToList();
        }

        public int CleanupOldOutputs(int daysOld)
        {
            EnsureDirectoriesExist();
            int deletedCount = 0;
            var cutoff = DateTime.Now.AddDays(-daysOld);
            var dir = new DirectoryInfo(OutputsDirectory);
            if (!dir.Exists) return 0;

            foreach (var file in dir.GetFiles())
            {
                if (file.LastWriteTime < cutoff)
                {
                    try
                    {
                        if (file.IsReadOnly) file.IsReadOnly = false;
                        file.Delete();
                        deletedCount++;
                    }
                    catch { }
                }
            }

            return deletedCount;
        }

        public bool ManualDeleteFile(string filePath)
        {
            if (!File.Exists(filePath)) return false;

            try
            {
                var attr = File.GetAttributes(filePath);
                if ((attr & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    File.SetAttributes(filePath, attr & ~FileAttributes.ReadOnly);
                }
                File.Delete(filePath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public string CreateBackupZip(string? customDestinationPath = null)
        {
            EnsureDirectoriesExist();
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupFileName = $"DialectaPDF_Yedek_{timestamp}.zip";
            var backupPath = customDestinationPath ?? Path.Combine(BackupsDirectory, backupFileName);

            var tempDir = Path.Combine(Path.GetTempPath(), "DialectaPDF_Backup_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var destOutputs = Path.Combine(tempDir, "Ciktilar");
                Directory.CreateDirectory(destOutputs);
                foreach (var f in Directory.GetFiles(OutputsDirectory))
                {
                    File.Copy(f, Path.Combine(destOutputs, Path.GetFileName(f)), true);
                }

                var destOriginals = Path.Combine(tempDir, "Orijinaller");
                Directory.CreateDirectory(destOriginals);
                foreach (var f in Directory.GetFiles(OriginalsDirectory))
                {
                    File.Copy(f, Path.Combine(destOriginals, Path.GetFileName(f)), true);
                }

                if (File.Exists(AuditLogPath))
                {
                    File.Copy(AuditLogPath, Path.Combine(tempDir, Path.GetFileName(AuditLogPath)), true);
                }

                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }

                ZipFile.CreateFromDirectory(tempDir, backupPath, CompressionLevel.Optimal, false);
                return backupPath;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, true);
                }
                catch { }
            }
        }
    }
}
