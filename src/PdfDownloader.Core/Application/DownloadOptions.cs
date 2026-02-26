namespace PdfDownloader.Core.Application;

/// <summary>
/// Controls how many PDFs to download and where outputs are written.
/// </summary>
public sealed class DownloadOptions
{
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

        // Keep a safe default so the app never "hangs" on dead links.
        RequestTimeout = requestTimeout ?? TimeSpan.FromSeconds(15);

        // Initialize StatusFilePath to a non-null value.
        StatusFilePath = statusFileRelativePath;
    }

    public string StatusFilePath { get; }
    public bool OverwriteExisting { get; }
    public int MaxSuccessfulDownloads { get; }
    public string StatusFileRelativePath { get; }

    /// <summary>
    /// Per-request timeout used by the download runner.
    /// Keeps the workflow responsive even when hosts are unresponsive.
    /// </summary>
    public TimeSpan RequestTimeout { get; }
}