namespace PdfDownloader.Core.Application;

public enum DownloadStage
{
    Starting,
    ProcessingRecord,
    TryingPrimary,
    TryingFallback,
    Downloading,
    ValidatingPdf,
    SavingFile,
    SkippedExists,
    RecordSucceeded,
    RecordFailed,
    WritingStatusFile,
    Finished,
    Cancelled
}