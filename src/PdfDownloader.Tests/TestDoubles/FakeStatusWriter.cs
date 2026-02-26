using PdfDownloader.Core.Abstractions;
using PdfDownloader.Core.Application;

namespace PdfDownloader.Tests.TestDoubles;

/// <summary>
/// Captures status output in memory.
/// </summary>
public sealed class FakeStatusWriter : IStatusWriter
{
    public string? LastRelativePath { get; private set; }
    public IReadOnlyList<DownloadStatusRow>? LastRows { get; private set; }

    public Task WriteAsync(string relativePath, IReadOnlyList<DownloadStatusRow> rows, CancellationToken cancellationToken)
    {
        LastRelativePath = relativePath;
        LastRows = rows;
        return Task.CompletedTask;
    }
}