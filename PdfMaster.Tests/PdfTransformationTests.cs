using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PdfMaster.Models;
using PdfMaster.Services;
using PdfSharpCore.Pdf.IO;
using Xunit;

namespace PdfMaster.Tests
{
    public class PdfTransformationTests : IDisposable
    {
        private readonly string _testDir;

        public PdfTransformationTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "PdfMaster_Tests_" + Guid.NewGuid().ToString("N"));
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
        public void MergePdfs_ShouldCombineMultiplePdfs_Correctly()
        {
            var pdf1 = TestPdfHelper.CreateSamplePdf(_testDir, "doc1.pdf", 2, "Dokuman 1");
            var pdf2 = TestPdfHelper.CreateSamplePdf(_testDir, "doc2.pdf", 3, "Dokuman 2");

            var result = PdfMergeService.MergePdfs(new List<string> { pdf1, pdf2 }, "test_merge");

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.OutputFilePath);
            Assert.True(File.Exists(result.OutputFilePath));

            using var mergedDoc = PdfReader.Open(result.OutputFilePath, PdfDocumentOpenMode.Import);
            Assert.Equal(5, mergedDoc.PageCount);
            Assert.False(string.IsNullOrEmpty(result.OutputFileHash));
        }

        [Fact]
        public void SplitPdf_ExtractAllPages_ShouldCreateCorrectNumberOfFiles()
        {
            var pdf = TestPdfHelper.CreateSamplePdf(_testDir, "split_source.pdf", 3, "Sayfa");

            var result = PdfSplitService.SplitPdf(pdf, SplitMode.ExtractAllPages);

            Assert.True(result.IsSuccess);
            Assert.Equal(3, result.OutputFiles.Count);
            foreach (var f in result.OutputFiles)
            {
                Assert.True(File.Exists(f));
                using var doc = PdfReader.Open(f, PdfDocumentOpenMode.Import);
                Assert.Equal(1, doc.PageCount);
            }
        }

        [Fact]
        public void SplitPdf_PageRanges_ShouldParseAndSplitCorrectly()
        {
            var pdf = TestPdfHelper.CreateSamplePdf(_testDir, "range_source.pdf", 5, "Sayfa");

            var result = PdfSplitService.SplitPdf(pdf, SplitMode.PageRanges, "1-2, 4-5");

            Assert.True(result.IsSuccess);
            Assert.Equal(2, result.OutputFiles.Count);

            using var doc1 = PdfReader.Open(result.OutputFiles[0], PdfDocumentOpenMode.Import);
            Assert.Equal(2, doc1.PageCount);

            using var doc2 = PdfReader.Open(result.OutputFiles[1], PdfDocumentOpenMode.Import);
            Assert.Equal(2, doc2.PageCount);
        }

        [Fact]
        public void OrganizeAndRotate_ShouldApplyRotationAndReordering()
        {
            var pdf = TestPdfHelper.CreateSamplePdf(_testDir, "organize_source.pdf", 3, "Sayfa");

            var pages = new List<PdfPageItem>
            {
                new PdfPageItem { OriginalPageIndex = 2, RotationAngle = 90 }, // Page 3 moved first and rotated 90 deg
                new PdfPageItem { OriginalPageIndex = 0, RotationAngle = 180 }, // Page 1 moved second and rotated 180 deg
                new PdfPageItem { OriginalPageIndex = 1, IsDeleted = true } // Page 2 deleted
            };

            var result = PdfOrganizeService.OrganizeAndRotate(pdf, pages, "test_organize");

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.OutputFilePath);

            using var doc = PdfReader.Open(result.OutputFilePath, PdfDocumentOpenMode.Import);
            Assert.Equal(2, doc.PageCount);
            Assert.Equal(90, doc.Pages[0].Rotate);
            Assert.Equal(180, doc.Pages[1].Rotate);
        }

        [Fact]
        public void ApplyWatermark_TextWatermark_ShouldSucceed()
        {
            var pdf = TestPdfHelper.CreateSamplePdf(_testDir, "watermark_source.pdf", 2, "Sayfa");

            var options = new WatermarkOptions
            {
                Type = WatermarkType.Text,
                Text = "GİZLİ BELGE",
                FontSize = 36,
                Opacity = 0.4,
                Placement = WatermarkPlacement.Diagonal
            };

            var result = PdfWatermarkService.ApplyWatermark(pdf, options, "test_watermark");

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.OutputFilePath);
            Assert.True(File.Exists(result.OutputFilePath));

            using var doc = PdfReader.Open(result.OutputFilePath, PdfDocumentOpenMode.Import);
            Assert.Equal(2, doc.PageCount);
        }

        [Fact]
        public void RedactPdf_KeywordAndCoordinate_ShouldSucceed()
        {
            var pdf = TestPdfHelper.CreateSamplePdf(_testDir, "redact_source.pdf", 2, "Sayfa");

            var options = new RedactionOptions
            {
                KeywordsToRedact = new List<string> { "Kimlik", "Hassas" },
                ManualRectangles = new List<RedactionRect>
                {
                    new RedactionRect { PageIndex = 0, X = 40, Y = 90, Width = 200, Height = 30 }
                },
                OverlayText = true,
                OverlayTextContent = "[GİZLENDİ]"
            };

            var result = PdfRedactionService.RedactPdf(pdf, options, "test_redact");

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.OutputFilePath);
            Assert.True(File.Exists(result.OutputFilePath));
        }

        [Fact]
        public void CompressPdf_ShouldProduceValidOptimizedDocument()
        {
            var pdf = TestPdfHelper.CreateSamplePdf(_testDir, "compress_source.pdf", 3, "Sayfa");

            var options = new CompressionOptions
            {
                Preset = CompressionPreset.Medium,
                CompressStreams = true
            };

            var result = PdfCompressService.CompressPdf(pdf, options, "test_compress");

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.OutputFilePath);
            Assert.True(File.Exists(result.OutputFilePath));

            using var doc = PdfReader.Open(result.OutputFilePath, PdfDocumentOpenMode.Import);
            Assert.Equal(3, doc.PageCount);
        }

        [Fact]
        public void SignAndSealPdf_ShouldStampSealAndLogAuditHash()
        {
            var pdf = TestPdfHelper.CreateSamplePdf(_testDir, "sign_source.pdf", 1, "Sayfa");

            var options = new SignSealOptions
            {
                Type = SignType.CryptoSealStamp,
                SignerName = "Ahmet Yılmaz",
                Reason = "Sözleşme Onayı",
                Location = "İstanbul",
                X = 50,
                Y = 200,
                Width = 250,
                Height = 80
            };

            var result = PdfSignService.SignAndSealPdf(pdf, options, "test_sign");

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.OutputFilePath);
            Assert.False(string.IsNullOrEmpty(result.OutputFileHash));

            // Verify in audit log
            var (found, matchingEvent, isOutput) = AuditLogger.VerifyFileInAuditLog(result.OutputFilePath);
            Assert.True(found);
            Assert.NotNull(matchingEvent);
            Assert.True(isOutput);
            Assert.Equal(ActionType.SignAndSeal, matchingEvent.Action);
        }
    }
}
