using System;
using System.Collections.Generic;
using System.IO;
using PdfMaster.Models;
using PdfMaster.Services;
using Xunit;

namespace PdfMaster.Tests
{
    public class EndToEndWorkflowTests : IDisposable
    {
        private readonly string _testDir;

        public EndToEndWorkflowTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "PdfMaster_E2E_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
            StorageManager.Instance.SetBaseDirectory(Path.Combine(_testDir, "Storage"));
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testDir))
                {
                    Directory.Delete(_testDir, true);
                }
            }
            catch { }
        }

        [Fact]
        public void FullHappyPath_Workflow_ShouldSucceedAndPreserveIntegrity()
        {
            // 1. Create initial original PDF
            var originalPdf = TestPdfHelper.CreateSamplePdf(_testDir, "orijinal_sozlesme.pdf", 4, "Madde");
            var originalHash = SecurityAndHashService.ComputeSha256(originalPdf);

            // 2. Import original into storage & check immutability
            var imported = StorageManager.Instance.ImportOriginal(originalPdf);
            Assert.True(File.Exists(imported));
            var importedHash = SecurityAndHashService.ComputeSha256(imported);
            Assert.Equal(originalHash, importedHash);

            // 3. Step 1: Organize & Rotate page 2 by 90 degrees
            var pages = new List<PdfPageItem>
            {
                new PdfPageItem { OriginalPageIndex = 0 },
                new PdfPageItem { OriginalPageIndex = 1, RotationAngle = 90 },
                new PdfPageItem { OriginalPageIndex = 2 },
                new PdfPageItem { OriginalPageIndex = 3 }
            };
            var organizeRes = PdfOrganizeService.OrganizeAndRotate(imported, pages);
            Assert.True(organizeRes.IsSuccess);
            Assert.NotNull(organizeRes.OutputFilePath);

            // 4. Step 2: Apply Watermark to rotated document
            var watermarkRes = PdfWatermarkService.ApplyWatermark(organizeRes.OutputFilePath, new WatermarkOptions
            {
                Text = "TASLAK SURET",
                Opacity = 0.3
            });
            Assert.True(watermarkRes.IsSuccess);
            Assert.NotNull(watermarkRes.OutputFilePath);

            // 5. Step 3: Redact sensitive information
            var redactRes = PdfRedactionService.RedactPdf(watermarkRes.OutputFilePath, new RedactionOptions
            {
                KeywordsToRedact = new List<string> { "Kimlik" },
                OverlayText = true,
                OverlayTextContent = "[GİZLİ]"
            });
            Assert.True(redactRes.IsSuccess);
            Assert.NotNull(redactRes.OutputFilePath);

            // 6. Step 4: Sign & Seal document
            var signRes = PdfSignService.SignAndSealPdf(redactRes.OutputFilePath, new SignSealOptions
            {
                Type = SignType.CryptoSealStamp,
                SignerName = "Mehmet Demir",
                Reason = "E2E Doğrulama Testi",
                Location = "Ankara"
            });
            Assert.True(signRes.IsSuccess);
            Assert.NotNull(signRes.OutputFilePath);

            // 7. Verify Audit Log contains all 4 sequential events
            var allEvents = AuditLogger.GetAllEvents();
            Assert.True(allEvents.Count >= 4);

            // 8. Verify the final signed PDF against the Audit Log
            var (isFound, auditEvent, isOutput) = AuditLogger.VerifyFileInAuditLog(signRes.OutputFilePath);
            Assert.True(isFound);
            Assert.NotNull(auditEvent);
            Assert.True(isOutput);
            Assert.Equal(ActionType.SignAndSeal, auditEvent.Action);

            // 9. Original PDF must still be intact and identical
            var currentOriginalHash = SecurityAndHashService.ComputeSha256(originalPdf);
            Assert.Equal(originalHash, currentOriginalHash);

            // 10. Test 1-click backup creation
            var backupZip = StorageManager.Instance.CreateBackupZip();
            Assert.True(File.Exists(backupZip));
            Assert.True(new FileInfo(backupZip).Length > 0);
        }
    }
}
