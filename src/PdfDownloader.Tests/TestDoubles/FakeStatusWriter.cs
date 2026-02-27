using PdfDownloader.Core.Abstractions;
using PdfDownloader.Core.Application;

namespace PdfDownloader.Tests.TestDoubles;

/// <summary>
/// In-memory test double for <see cref="IStatusWriter"/>.
/// 
/// Responsibility:
/// - Capture the last write operation
/// - Expose written path and rows for assertions
///
/// This fake performs no file IO.
/// It is used to verify that:
/// - The correct relative path is used
/// - The correct set of <see cref="DownloadStatusRow"/> entries is written
/// </summary>
public sealed class FakeStatusWriter : IStatusWriter
{
    /// <summary>
    /// Gets the last relative path passed to WriteAsync.
    /// </summary>
    public string? LastRelativePath { get; private set; }

    /// <summary>
    /// Gets the last collection of status rows passed to WriteAsync.
    /// </summary>
    public IReadOnlyList<DownloadStatusRow>? LastRows { get; private set; }

    /// <summary>
    /// Captures the arguments and completes immediately.
    /// </summary>
    public Task WriteAsync(
        string relativePath,
        IReadOnlyList<DownloadStatusRow> rows,
        CancellationToken cancellationToken)
    {
        LastRelativePath = relativePath;
        LastRows = rows;
        return Task.CompletedTask;
    }
}