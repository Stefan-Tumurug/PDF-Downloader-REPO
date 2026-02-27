using PdfDownloader.Core.Domain;

namespace PdfDownloader.Core.Application;

/// <summary>
/// Represents a progress snapshot emitted during download execution.
/// 
/// Responsibility:
/// - Provide structured progress information to presentation layers
///   (CLI or GUI).
/// - Remain immutable so each instance represents a single,
///   consistent state in time.
/// 
/// This class contains no business logic.
/// It is a transport model used with IProgress&lt;DownloadProgress&gt;.
/// </summary>
public sealed class DownloadProgress
{
    /// <summary>
    /// Creates a new progress snapshot.
    /// </summary>
    /// <param name="recordIndex">Current record index being processed.</param>
    /// <param name="totalRecords">Total number of input records.</param>
    /// <param name="successfulDownloads">Number of successful downloads so far.</param>
    /// <param name="maxSuccessfulDownloads">Maximum allowed successful downloads.</param>
    /// <param name="brNum">BR number of the current record.</param>
    /// <param name="stage">Current stage of processing.</param>
    /// <param name="message">Optional informational message.</param>
    /// <param name="attemptedUrl">URL currently being attempted, if applicable.</param>
    /// <param name="completedRow">
    /// Finalized status row when a record has completed processing.
    /// </param>
    public DownloadProgress(
        int recordIndex,
        int totalRecords,
        int successfulDownloads,
        int maxSuccessfulDownloads,
        string brNum,
        DownloadStage stage,
        string message = "",
        Uri? attemptedUrl = null,
        DownloadStatusRow? completedRow = null)
    {
        RecordIndex = recordIndex;
        TotalRecords = totalRecords;
        SuccessfulDownloads = successfulDownloads;
        MaxSuccessfulDownloads = maxSuccessfulDownloads;
        BrNum = brNum;
        Stage = stage;
        Message = message;
        AttemptedUrl = attemptedUrl;
        CompletedRow = completedRow;
    }

    /// <summary>Index of the record currently being processed.</summary>
    public int RecordIndex { get; }

    /// <summary>Total number of records to process.</summary>
    public int TotalRecords { get; }

    /// <summary>Number of successful downloads so far.</summary>
    public int SuccessfulDownloads { get; }

    /// <summary>Configured maximum successful downloads.</summary>
    public int MaxSuccessfulDownloads { get; }

    /// <summary>Business identifier (BR number) of the current record.</summary>
    public string BrNum { get; }

    /// <summary>Current processing stage.</summary>
    public DownloadStage Stage { get; }

    /// <summary>Optional descriptive message for UI display.</summary>
    public string Message { get; }

    /// <summary>URL currently being attempted, if applicable.</summary>
    public Uri? AttemptedUrl { get; }

    /// <summary>
    /// Finalized status row when a record has completed.
    /// Null while processing is still ongoing.
    /// </summary>
    public DownloadStatusRow? CompletedRow { get; }

    /// <summary>
    /// Percentage of completion relative to the maximum successful downloads.
    /// Returns 0 if max limit is invalid.
    /// </summary>
    public double Percent
        => MaxSuccessfulDownloads <= 0
            ? 0
            : (double)SuccessfulDownloads / MaxSuccessfulDownloads * 100.0;
}