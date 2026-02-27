namespace PdfDownloader.Core.Application;

/// <summary>
/// Represents a single result entry written to the status output (e.g. status.csv).
/// 
/// Each instance corresponds to one processed report record and
/// contains the final outcome of that processing.
/// 
/// This is a pure data model with no behavior.
/// </summary>
public sealed class DownloadStatusRow
{
    /// <summary>
    /// Creates a new status row.
    /// </summary>
    /// <param name="brNum">
    /// Business identifier of the report (used as file name base).
    /// </param>
    /// <param name="attemptedUrl">
    /// The URL that was attempted for the final outcome
    /// (empty for skipped cases).
    /// </param>
    /// <param name="status">
    /// Final result of processing the record.
    /// </param>
    /// <param name="error">
    /// Error message if the download failed; otherwise empty.
    /// </param>
    public DownloadStatusRow(
        string brNum,
        string attemptedUrl,
        DownloadStatus status,
        string error)
    {
        BrNum = brNum;
        AttemptedUrl = attemptedUrl;
        Status = status;
        Error = error;
    }

    /// <summary>Business identifier of the report.</summary>
    public string BrNum { get; }

    /// <summary>URL associated with the final attempt.</summary>
    public string AttemptedUrl { get; }

    /// <summary>Final processing result.</summary>
    public DownloadStatus Status { get; }

    /// <summary>Error message if <see cref="Status"/> is Failed; otherwise empty.</summary>
    public string Error { get; }
}