namespace PdfDownloader.Core.Application;

/// <summary>
/// Represents the final outcome of processing a single record.
/// 
/// This is a result state (persisted to status.csv),
/// not a runtime execution stage.
/// 
/// Used by <see cref="DownloadStatusRow"/> to describe
/// whether a report was successfully downloaded,
/// skipped, or failed.
/// </summary>
public enum DownloadStatus
{
    /// <summary>
    /// The PDF was successfully downloaded and saved.
    /// </summary>
    Downloaded,

    /// <summary>
    /// The file already existed and overwrite was disabled.
    /// </summary>
    SkippedExists,

    /// <summary>
    /// The download failed (invalid URL, timeout, not a PDF, etc.).
    /// </summary>
    Failed
}