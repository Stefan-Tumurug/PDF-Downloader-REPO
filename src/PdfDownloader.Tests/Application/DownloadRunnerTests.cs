using Microsoft.VisualStudio.TestTools.UnitTesting;
using PdfDownloader.Core.Application;
using PdfDownloader.Core.Domain;
using PdfDownloader.Tests.TestDoubles;

namespace PdfDownloader.Tests.Application;

/// <summary>
/// Core behavior tests for <see cref="DownloadRunner"/>.
/// 
/// Focus:
/// - Happy path download + save
/// - Fallback selection rules
/// - Skip policy when file exists
/// - Stop condition when max successful downloads is reached
///
/// These tests use deterministic fakes to avoid network and disk IO.
/// </summary>
[TestClass]
public sealed class DownloadRunnerTests
{
    /// <summary>
    /// Verifies the happy path:
    /// - primary URL returns valid PDF bytes
    /// - file is saved using BR number naming
    /// - status row is returned and status file write is invoked
    /// </summary>
    [TestMethod]
    public async Task RunAsync_PrimaryPdf_IsDownloaded_AndSaved()
    {
        // Arrange
        FakeHttpDownloader http = new();
        InMemoryFileStore fileStore = new();
        FakeStatusWriter statusWriter = new();

        DownloadRunner runner = new(http, fileStore, statusWriter);

        Uri primaryUrl = new("https://example.com/report.pdf");
        byte[] pdfBytes = CreatePdfBytes();

        http.SetupBytes(primaryUrl, pdfBytes);

        ReportRecord record = new(
            brNum: "BR12345",
            primaryUrl: primaryUrl,
            fallbackUrl: null);

        DownloadOptions options = new(
            maxSuccessfulDownloads: 10,
            statusFileRelativePath: "status.csv");

        // Act
        IReadOnlyList<DownloadStatusRow> rows = await runner.RunAsync(
            records: [record],
            options: options,
            cancellationToken: CancellationToken.None);

        // Assert: outcome row is correct
        Assert.HasCount(1, rows);
        Assert.AreEqual(DownloadStatus.Downloaded, rows[0].Status);
        Assert.AreEqual(primaryUrl.ToString(), rows[0].AttemptedUrl);

        // Assert: file persisted under expected name
        byte[]? saved = fileStore.TryGet("BR12345.pdf");
        Assert.IsNotNull(saved);
        CollectionAssert.AreEqual(pdfBytes, saved);

        // Assert: status writer invoked with correct path + rows
        Assert.AreEqual("status.csv", statusWriter.LastRelativePath);
        Assert.IsNotNull(statusWriter.LastRows);
        Assert.HasCount(1, statusWriter.LastRows);
    }

    /// <summary>
    /// Verifies fallback behavior when primary returns deterministic non-PDF content:
    /// - primary attempted first
    /// - fallback attempted next
    /// - fallback result is persisted and reflected in AttemptedUrl
    /// </summary>
    [TestMethod]
    public async Task RunAsync_PrimaryNotPdf_FallbackPdf_IsDownloaded()
    {
        // Arrange
        FakeHttpDownloader http = new();
        InMemoryFileStore fileStore = new();
        FakeStatusWriter statusWriter = new();

        DownloadRunner runner = new(http, fileStore, statusWriter);

        Uri primaryUrl = new("https://example.com/not-a-pdf");
        Uri fallbackUrl = new("https://example.com/real.pdf");

        http.SetupBytes(primaryUrl, CreateHtmlBytes());
        http.SetupBytes(fallbackUrl, CreatePdfBytes());

        ReportRecord record = new(
            brNum: "BR20000",
            primaryUrl: primaryUrl,
            fallbackUrl: fallbackUrl);

        DownloadOptions options = new(10, "status.csv");

        // Act
        IReadOnlyList<DownloadStatusRow> rows = await runner.RunAsync(
            [record],
            options,
            CancellationToken.None);

        // Assert
        Assert.HasCount(1, rows);
        Assert.AreEqual(DownloadStatus.Downloaded, rows[0].Status);
        Assert.AreEqual(fallbackUrl.ToString(), rows[0].AttemptedUrl);

        Assert.IsNotNull(fileStore.TryGet("BR20000.pdf"));

        // Ensures primary was attempted first, then fallback.
        Assert.HasCount(2, http.RequestedUrls);
        Assert.AreEqual(primaryUrl.ToString(), http.RequestedUrls[0]);
        Assert.AreEqual(fallbackUrl.ToString(), http.RequestedUrls[1]);
    }

    /// <summary>
    /// Verifies skip behavior:
    /// - if file already exists and overwrite is disabled (default),
    ///   the record is marked as SkippedExists
    /// - no HTTP request is performed
    /// </summary>
    [TestMethod]
    public async Task RunAsync_FileAlreadyExists_IsSkipped()
    {
        // Arrange
        FakeHttpDownloader http = new();
        InMemoryFileStore fileStore = new();
        FakeStatusWriter statusWriter = new();

        DownloadRunner runner = new(http, fileStore, statusWriter);

        fileStore.Seed("BR30000.pdf", CreatePdfBytes());

        ReportRecord record = new(
            brNum: "BR30000",
            primaryUrl: new Uri("https://example.com/ignored.pdf"),
            fallbackUrl: null);

        DownloadOptions options = new(10, "status.csv");

        // Act
        IReadOnlyList<DownloadStatusRow> rows = await runner.RunAsync(
            [record],
            options,
            CancellationToken.None);

        // Assert
        Assert.HasCount(1, rows);
        Assert.AreEqual(DownloadStatus.SkippedExists, rows[0].Status);
        Assert.HasCount(0, http.RequestedUrls);
    }

    /// <summary>
    /// Verifies the stop condition:
    /// - the runner stops once it reaches MaxSuccessfulDownloads
    /// - later records are not attempted
    /// </summary>
    [TestMethod]
    public async Task RunAsync_StopsAfterTenSuccessfulDownloads()
    {
        // Arrange
        FakeHttpDownloader http = new();
        InMemoryFileStore fileStore = new();
        FakeStatusWriter statusWriter = new();

        DownloadRunner runner = new(http, fileStore, statusWriter);

        byte[] pdfBytes = CreatePdfBytes();
        List<ReportRecord> records = [];

        for (int i = 1; i <= 20; i++)
        {
            string brNum = $"BR{i:00000}";
            Uri url = new($"https://example.com/{brNum}.pdf");

            http.SetupBytes(url, pdfBytes);

            records.Add(new ReportRecord(
                brNum: brNum,
                primaryUrl: url,
                fallbackUrl: null));
        }

        DownloadOptions options = new(maxSuccessfulDownloads: 10, statusFileRelativePath: "status.csv");

        // Act
        IReadOnlyList<DownloadStatusRow> rows = await runner.RunAsync(records, options, CancellationToken.None);

        // Assert: only 10 successes are recorded
        int downloaded = rows.Count(r => r.Status == DownloadStatus.Downloaded);
        Assert.AreEqual(10, downloaded);

        // Assert: only 10 files saved
        Assert.AreEqual(10, fileStore.Count);

        // Assert: only first 10 URLs attempted
        Assert.HasCount(10, http.RequestedUrls);
    }

    private static byte[] CreatePdfBytes()
    {
        // Minimal PDF signature (enough for the "%PDF-" header validation).
        return "%PDF-1.7\n"u8.ToArray();
    }

    private static byte[] CreateHtmlBytes()
    {
        string html = "<!DOCTYPE html><html><body>Not a PDF</body></html>";
        return System.Text.Encoding.UTF8.GetBytes(html);
    }
}