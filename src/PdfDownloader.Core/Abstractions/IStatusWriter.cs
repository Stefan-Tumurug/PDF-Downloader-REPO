using PdfDownloader.Core.Application;

namespace PdfDownloader.Core.Abstractions;

/// <summary>
/// Writes a status report for processed records.
/// </summary>
public interface IStatusWriter
{
    Task WriteAsync(
        string relativePath,
        IReadOnlyList<DownloadStatusRow> rows,
        CancellationToken cancellationToken);
}