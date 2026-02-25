namespace PdfDownloader.Core.Application;

/// <summary>
/// Represents one line in the status output.
/// </summary>
public sealed class DownloadStatusRow
{
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

    public string BrNum { get; }
    public string AttemptedUrl { get; }
    public DownloadStatus Status { get; }
    public string Error { get; }
}