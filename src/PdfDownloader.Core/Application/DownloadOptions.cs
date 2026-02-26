namespace PdfDownloader.Core.Application;

/// <summary>
/// Controls how many PDFs to download and where outputs are written.
/// </summary>
public sealed class DownloadOptions
{
    public DownloadOptions(int maxSuccessfulDownloads, string statusFileRelativePath, bool overwriteExisting = false)
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
    }

    public bool OverwriteExisting { get; }
    public int MaxSuccessfulDownloads { get; }
    public string StatusFileRelativePath { get; }
}