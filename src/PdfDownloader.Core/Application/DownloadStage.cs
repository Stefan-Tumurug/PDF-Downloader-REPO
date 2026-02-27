namespace PdfDownloader.Core.Application;

/// <summary>
/// Represents the current execution stage of the download workflow.
/// 
/// Used by <see cref="DownloadProgress"/> to communicate state transitions
/// to CLI or GUI layers via IProgress.
/// 
/// The enum values describe *what the runner is doing right now*,
/// not the final outcome of a record.
/// </summary>
public enum DownloadStage
{
    /// <summary>Initial state before processing begins.</summary>
    Starting,

    /// <summary>A record is being prepared for processing.</summary>
    ProcessingRecord,

    /// <summary>Attempting download using the record's PrimaryUrl.</summary>
    TryingPrimary,

    /// <summary>Attempting download using the record's FallbackUrl.</summary>
    TryingFallback,

    /// <summary>Actively downloading bytes from the remote server.</summary>
    Downloading,

    /// <summary>Validating that downloaded content has a valid PDF signature.</summary>
    ValidatingPdf,

    /// <summary>Persisting the validated PDF to storage.</summary>
    SavingFile,

    /// <summary>Record skipped because the file already exists and overwrite is disabled.</summary>
    SkippedExists,

    /// <summary>The record completed successfully and the file was downloaded.</summary>
    RecordSucceeded,

    /// <summary>The record processing failed.</summary>
    RecordFailed,

    /// <summary>Writing the final status file (status.csv).</summary>
    WritingStatusFile,

    /// <summary>The entire run completed successfully.</summary>
    Finished,

    /// <summary>The run was cancelled by the user.</summary>
    Cancelled
}