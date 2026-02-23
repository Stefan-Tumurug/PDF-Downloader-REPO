using PdfDownloader.Core.Domain;

namespace PdfDownloader.Core.Abstractions;

/// <summary>
/// Provides report metadata from a data source (e.g. Excel, CSV, API).
/// Responsible only for reading data, not downloading files.
/// </summary>
public interface IReportSource
{
    /// <summary>
    /// Reads all available report records from the underlying data source.
    /// Invalid or incomplete rows may be skipped by the implementation.
    /// </summary>
    IReadOnlyList<ReportRecord> ReadAll();
}