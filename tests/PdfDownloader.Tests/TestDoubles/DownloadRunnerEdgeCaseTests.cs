using Microsoft.VisualStudio.TestTools.UnitTesting;
using PdfDownloader.Core.Application;
using PdfDownloader.Core.Domain;
using PdfDownloader.Tests.TestDoubles;
using PdfDownloader.Core.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PdfDownloader.Tests.Application;

[TestClass]
public sealed class DownloadRunnerEdgeCaseTests
{
    private static readonly byte[] ValidPdfBytes =
        Encoding.ASCII.GetBytes("%PDF-1.4 fake content");

    [TestMethod]
    public async Task RunAsync_RetriesAndSucceeds_OnTransientHttpFailure()
    {
        // Arrange
        Uri url = new Uri("https://example.com/test.pdf");

        FakeHttpDownloader http = new();
        http.SetupThrowTimes(url, 1);
        http.SetupBytes(url, ValidPdfBytes);

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
    }

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