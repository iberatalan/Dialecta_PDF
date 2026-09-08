using System;
using System.IO;
using System.Security.Cryptography;

namespace PdfMaster.Services
{
    public class SecurityAndHashService
    {
        public static string ComputeSha256(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Dosya bulunamadı", filePath);

            using var stream = File.OpenRead(filePath);
            return ComputeSha256(stream);
        }

        public static string ComputeSha256(byte[] data)
        {
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(data);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        public static string ComputeSha256(Stream stream)
        {
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(stream);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        public static bool VerifyFileIntegrity(string filePath, string expectedHash)
        {
            if (!File.Exists(filePath))
                return false;

            var actualHash = ComputeSha256(filePath);
            return string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
        }
    }
}
