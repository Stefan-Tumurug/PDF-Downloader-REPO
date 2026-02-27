using Microsoft.VisualStudio.TestTools.UnitTesting;
using PdfDownloader.Core.Application;
using PdfDownloader.Core.Domain;
using PdfDownloader.Tests.TestDoubles;
using System.Net;
using System.Text;

namespace PdfDownloader.Tests.Application;

/// <summary>
/// Additional behavioral tests for <see cref="DownloadRunner"/>.
/// 
/// Focus:
/// - Edge cases and policy decisions (overwrite, URL scheme validation, retry rules, fallback behavior)
/// 
/// These tests use deterministic test doubles (no network/disk)
/// to validate orchestration logic in isolation.
/// </summary>
[TestClass]
public sealed class DownloadRunnerAdditionalTests
{
    /// <summary>
    /// Ensures that when overwrite is enabled:
    /// - existing output files do not cause a skip
    /// - the new bytes replace the old bytes on disk
    /// - exactly one HTTP call is made for the record
    /// </summary>
    [TestMethod]
    public async Task RunAsync_FileExists_WithOverwriteTrue_DownloadsAndReplacesFile()
    {
        // Arrange: deterministic fakes to isolate runner behavior
        FakeHttpDownloader http = new();
        InMemoryFileStore fileStore = new();
        FakeStatusWriter statusWriter = new();

        DownloadRunner runner = new(http, fileStore, statusWriter);

        Uri url = new("https://example.com/new.pdf");

        byte[] existingBytes = CreatePdfBytes("OLD");
        byte[] newBytes = CreatePdfBytes("NEW");

        fileStore.Seed("BR90000.pdf", existingBytes);
        http.SetupBytes(url, newBytes);

        ReportRecord record = new(brNum: "BR90000", primaryUrl: url, fallbackUrl: null);

        DownloadOptions options = new(
            maxSuccessfulDownloads: 10,
            statusFileRelativePath: "status.csv",
            overwriteExisting: true);

        // Act
        IReadOnlyList<DownloadStatusRow> rows = await runner.RunAsync(
            records: [record],
            options: options,
            cancellationToken: CancellationToken.None);

        // Assert: correct status + file content replaced
        Assert.HasCount(1, rows);
        Assert.AreEqual(DownloadStatus.Downloaded, rows[0].Status);

        byte[]? saved = fileStore.TryGet("BR90000.pdf");
        Assert.IsNotNull(saved);
        CollectionAssert.AreEqual(newBytes, saved, "Expected existing file to be replaced when overwrite is enabled.");

        Assert.HasCount(1, http.RequestedUrls);
        Assert.AreEqual(url.ToString(), http.RequestedUrls[0]);
    }

    /// <summary>
    /// Ensures that non-HTTP schemes are rejected early:
    /// - no HTTP request is performed
    /// - record ends as Failed with a clear error message
    /// </summary>
    [TestMethod]
    public async Task RunAsync_UnsupportedUrlScheme_FailsWithoutHttpCall()
    {
        // Arrange
        FakeHttpDownloader http = new();
        InMemoryFileStore fileStore = new();
        FakeStatusWriter statusWriter = new();

        DownloadRunner runner = new(http, fileStore, statusWriter);

        Uri ftpUrl = new("ftp://example.com/report.pdf");

        ReportRecord record = new(brNum: "BR91000", primaryUrl: ftpUrl, fallbackUrl: null);
        DownloadOptions options = new(10, "status.csv");

        // Act
        IReadOnlyList<DownloadStatusRow> rows = await runner.RunAsync([record], options, CancellationToken.None);

        // Assert
        Assert.HasCount(1, rows);
        Assert.AreEqual(DownloadStatus.Failed, rows[0].Status);
        StringAssert.Contains(rows[0].Error, "Unsupported URL scheme");

        Assert.HasCount(0, http.RequestedUrls, "No HTTP call should be made for unsupported schemes.");
    }

    /// <summary>
    /// Ensures that deterministic "Not a PDF" responses are NOT retried.
    /// This protects hosts from repeated requests when the URL points to HTML content.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_PrimaryReturnsNotPdf_DoesNotRetry()
    {
        // Arrange
        FakeHttpDownloader http = new();
        InMemoryFileStore fileStore = new();
        FakeStatusWriter statusWriter = new();

        DownloadRunner runner = new(http, fileStore, statusWriter);

        Uri url = new("https://example.com/not-pdf");
        http.SetupBytes(url, CreateHtmlBytes());

        ReportRecord record = new(brNum: "BR92000", primaryUrl: url, fallbackUrl: null);
        DownloadOptions options = new(10, "status.csv");

        // Act
        IReadOnlyList<DownloadStatusRow> rows = await runner.RunAsync([record], options, CancellationToken.None);

        // Assert
        Assert.HasCount(1, rows);
        Assert.AreEqual(DownloadStatus.Failed, rows[0].Status);
        StringAssert.Contains(rows[0].Error, "Not a PDF");

        // Deterministic failure should not be retried.
        Assert.HasCount(1, http.RequestedUrls);
    }

    /// <summary>
    /// Ensures fallback behavior works when primary fails with a deterministic HTTP status (404):
    /// - primary is attempted once (no retry)
    /// - fallback is attempted next
    /// - final status reflects the fallback URL as the attempted URL
    /// </summary>
    [TestMethod]
    public async Task RunAsync_PrimaryReturns404_FallbackPdf_IsDownloaded()
    {
        // Arrange
        FakeHttpDownloader http = new();
        InMemoryFileStore fileStore = new();
        FakeStatusWriter statusWriter = new();

        DownloadRunner runner = new(http, fileStore, statusWriter);

        Uri primaryUrl = new("https://example.com/missing.pdf");
        Uri fallbackUrl = new("https://example.com/fallback.pdf");

        http.SetupThrowTimesWithStatus(primaryUrl, times: 1, statusCode: HttpStatusCode.NotFound);
        http.SetupBytes(fallbackUrl, CreatePdfBytes("OK"));

        ReportRecord record = new(brNum: "BR93000", primaryUrl: primaryUrl, fallbackUrl: fallbackUrl);
        DownloadOptions options = new(10, "status.csv");

        // Act
        IReadOnlyList<DownloadStatusRow> rows = await runner.RunAsync([record], options, CancellationToken.None);

        // Assert
        Assert.HasCount(1, rows);
        Assert.AreEqual(DownloadStatus.Downloaded, rows[0].Status);
        Assert.AreEqual(fallbackUrl.ToString(), rows[0].AttemptedUrl);

        // 404 is not transient => no retry. Should attempt primary once, then fallback.
        Assert.HasCount(2, http.RequestedUrls);
        Assert.AreEqual(primaryUrl.ToString(), http.RequestedUrls[0]);
        Assert.AreEqual(fallbackUrl.ToString(), http.RequestedUrls[1]);
    }

    private static byte[] CreatePdfBytes(string marker)
    {
        // Minimal PDF signature plus marker bytes (enough for the "%PDF-" header validation).
        return Encoding.ASCII.GetBytes($"%PDF-1.7\n{marker}");
    }

    private static byte[] CreateHtmlBytes()
    {
        return Encoding.UTF8.GetBytes("<!DOCTYPE html><html><body>Not a PDF</body></html>");
    }
}