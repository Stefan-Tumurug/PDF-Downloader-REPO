using Microsoft.VisualStudio.TestTools.UnitTesting;
using PdfDownloader.Core.Application;
using PdfDownloader.Core.Domain;
using PdfDownloader.Tests.TestDoubles;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PdfDownloader.Tests.Application;

/// <summary>
/// Edge case tests for <see cref="DownloadRunner"/>.
/// 
/// Focus:
/// - Retry behavior on transient failures
/// - Invalid input records (missing URLs, missing/invalid BR number)
/// 
/// These tests validate policy decisions that are easy to regress
/// when refactoring the orchestration logic.
/// </summary>
[TestClass]
public sealed class DownloadRunnerEdgeCaseTests
{
    // Valid minimal PDF signature used across tests.
    private static readonly byte[] ValidPdfBytes =
        Encoding.ASCII.GetBytes("%PDF-1.4 fake content");

    /// <summary>
    /// Verifies retry policy on transient failures:
    /// - first attempt fails (simulated transient error)
    /// - second attempt succeeds
    /// - final status is Downloaded
    /// </summary>
    [TestMethod]
    public async Task RunAsync_RetriesAndSucceeds_OnTransientHttpFailure()
    {
        // Arrange
        Uri url = new Uri("https://example.com/test.pdf");

        FakeHttpDownloader http = new();
        http.SetupThrowTimes(url, 1);       // first call fails (transient)
        http.SetupBytes(url, ValidPdfBytes); // next call succeeds

        InMemoryFileStore fileStore = new();
        FakeStatusWriter statusWriter = new();

        DownloadRunner runner = new(http, fileStore, statusWriter);

        ReportRecord record = new("123", url, null);
        DownloadOptions options = new(10, "status.csv");

        // Act
        IReadOnlyList<DownloadStatusRow> rows =
            await runner.RunAsync(new[] { record }, options, CancellationToken.None);

        // Assert
        Assert.HasCount(2, http.RequestedUrls, "Expected retry after failure.");
        Assert.AreEqual(DownloadStatus.Downloaded, rows[0].Status);
    }

    /// <summary>
    /// Verifies handling of invalid records with no usable URLs:
    /// - record should fail fast
    /// - error message should indicate URL is missing
    /// - no HTTP calls should occur
    /// </summary>
    [TestMethod]
    public async Task RunAsync_Fails_WhenNoUrlsProvided()
    {
        // Arrange
        FakeHttpDownloader http = new();
        InMemoryFileStore fileStore = new();
        FakeStatusWriter statusWriter = new();

        DownloadRunner runner = new(http, fileStore, statusWriter);

        ReportRecord record = new("123", null, null);
        DownloadOptions options = new(10, "status.csv");

        // Act
        IReadOnlyList<DownloadStatusRow> rows =
            await runner.RunAsync(new[] { record }, options, CancellationToken.None);

        // Assert
        Assert.AreEqual(DownloadStatus.Failed, rows[0].Status);
        StringAssert.Contains(rows[0].Error, "No URL");
        Assert.HasCount(0, http.RequestedUrls);
    }

    /// <summary>
    /// Verifies input validation for BR number:
    /// - missing/whitespace BR number should produce Failed status
    /// - prevents producing invalid file names like ".pdf"
    /// </summary>
    [TestMethod]
    public async Task RunAsync_Fails_WhenBrNumIsMissing()
    {
        // Arrange
        Uri url = new Uri("https://example.com/test.pdf");

        FakeHttpDownloader http = new();
        http.SetupBytes(url, ValidPdfBytes);

        InMemoryFileStore fileStore = new();
        FakeStatusWriter statusWriter = new();

        DownloadRunner runner = new(http, fileStore, statusWriter);

        ReportRecord record = new("   ", url, null);
        DownloadOptions options = new(10, "status.csv");

        // Act
        IReadOnlyList<DownloadStatusRow> rows =
            await runner.RunAsync(new[] { record }, options, CancellationToken.None);

        // Assert
        Assert.AreEqual(DownloadStatus.Failed, rows[0].Status);
        StringAssert.Contains(rows[0].Error, "BR");
    }
}