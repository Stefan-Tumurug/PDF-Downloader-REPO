using PdfDownloader.Core.Domain;

namespace PdfDownloader.Core.Application;

public sealed class DownloadProgress
{
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

    public int RecordIndex { get; }
    public int TotalRecords { get; }
    public int SuccessfulDownloads { get; }
    public int MaxSuccessfulDownloads { get; }
    public string BrNum { get; }
    public DownloadStage Stage { get; }
    public string Message { get; }
    public Uri? AttemptedUrl { get; }
    public DownloadStatusRow? CompletedRow { get; }
    public double Percent
        => MaxSuccessfulDownloads <= 0
            ? 0
            : (double)SuccessfulDownloads / MaxSuccessfulDownloads * 100.0;
}