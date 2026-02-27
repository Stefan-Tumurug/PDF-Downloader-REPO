namespace PdfDownloader.Core.Application;

/// <summary>
/// Configuration object controlling download execution behavior.
/// 
/// Responsibility:
/// - Defines runtime constraints (max downloads)
/// - Controls overwrite behavior
/// - Defines status output location
/// - Specifies per-request timeout
/// 
/// Immutable after construction to ensure predictable execution.
/// </summary>
public sealed class DownloadOptions
{
    /// <summary>
    /// Creates a new set of download options.
    /// </summary>
    /// <param name="maxSuccessfulDownloads">
    /// Maximum number of successfully downloaded PDFs before stopping execution.
    /// Must be greater than zero.
    /// </param>
    /// <param name="statusFileRelativePath">
    /// Relative path (within the configured file store root)
    /// where the status file will be written.
    /// </param>
    /// <param name="overwriteExisting">
    /// If true, existing files will be replaced.
    /// If false, existing files are skipped.
    /// </param>
    /// <param name="requestTimeout">
    /// Optional per-request timeout.
    /// Defaults to 15 seconds to avoid hanging on dead hosts.
    /// </param>
    public DownloadOptions(
        int maxSuccessfulDownloads,
        string statusFileRelativePath,
        bool overwriteExisting = false,
        TimeSpan? requestTimeout = null)
    {
        if (maxSuccessfulDownloads <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSuccessfulDownloads));
        }

        if (string.IsNullOrWhiteSpace(statusFileRelativePath))
        {
            throw new ArgumentException("Status file path is required.", nameof(statusFileRelativePath));
        }

        MaxSuccessfulDownloads = maxSuccessfulDownloads;
        StatusFileRelativePath = statusFileRelativePath;
        OverwriteExisting = overwriteExisting;

        // Safe default to prevent indefinite blocking on unresponsive hosts.
        RequestTimeout = requestTimeout ?? TimeSpan.FromSeconds(15);

        // Ensures non-null value for consumers expecting a path.
        StatusFilePath = statusFileRelativePath;
    }

    /// <summary>
    /// Full status file path (relative to the file store root).
    /// </summary>
    public string StatusFilePath { get; }

    /// <summary>
    /// Indicates whether existing files should be overwritten.
    /// </summary>
    public bool OverwriteExisting { get; }

    /// <summary>
    /// Maximum number of successful downloads before execution stops.
    /// </summary>
    public int MaxSuccessfulDownloads { get; }

    /// <summary>
    /// Relative path to the status file within the configured file store.
    /// </summary>
    public string StatusFileRelativePath { get; }

    /// <summary>
    /// Per-request timeout used by the download runner.
    /// Keeps the workflow responsive even when hosts are unresponsive.
    /// </summary>
    public TimeSpan RequestTimeout { get; }
}