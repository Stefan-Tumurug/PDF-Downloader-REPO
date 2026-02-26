using PdfDownloader.Core.Abstractions;
using PdfDownloader.Core.Domain;

namespace PdfDownloader.Core.Application;

/// <summary>
/// Coordinates the full download workflow:
/// - selects primary or fallback URL
/// - downloads bytes
/// - validates PDF header
/// - persists files
/// - collects status rows
///
/// This class contains orchestration logic only.
/// No direct IO or HTTP implementation details live here.
/// </summary>
public sealed class DownloadRunner
{
    private const int MaxRetries = 2; // total attempts = 1 + MaxRetries

    private readonly IHttpDownloader _httpDownloader;
    private readonly IFileStore _fileStore;
    private readonly IStatusWriter _statusWriter;

    public DownloadRunner(
        IHttpDownloader httpDownloader,
        IFileStore fileStore,
        IStatusWriter statusWriter)
    {
        _httpDownloader = httpDownloader;
        _fileStore = fileStore;
        _statusWriter = statusWriter;
    }

    private static bool IsSupportedScheme(Uri url)
    {
        return url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps;
    }

    /// <summary>
    /// Executes the download run.
    /// Stops after reaching the configured number of successful downloads.
    /// Writes a CSV status file after completion.
    /// </summary>
    public async Task<IReadOnlyList<DownloadStatusRow>> RunAsync(
        IReadOnlyList<ReportRecord> records,
        DownloadOptions options,
        CancellationToken cancellationToken,
        IProgress<DownloadProgress>? progress = null)
    {
        if (records is null) throw new ArgumentNullException(nameof(records));
        if (options is null) throw new ArgumentNullException(nameof(options));

        List<DownloadStatusRow> rows = new();
        int successfulDownloads = 0;

        Report(progress, 0, records.Count, successfulDownloads, options.MaxSuccessfulDownloads, "", DownloadStage.Starting, "Starting run");

        try
        {
            for (int i = 0; i < records.Count; i++)
            {
                ReportRecord record = records[i];
                int recordIndex = i + 1;

                cancellationToken.ThrowIfCancellationRequested();

                if (successfulDownloads >= options.MaxSuccessfulDownloads)
                {
                    break;
                }

                string br = record.BrNum?.Trim() ?? string.Empty;
                Report(progress, recordIndex, records.Count, successfulDownloads, options.MaxSuccessfulDownloads, br, DownloadStage.ProcessingRecord);

                if (!TryGetPdfFileName(record, out string pdfFileName, out DownloadStatusRow? invalidRow))
                {
                    rows.Add(invalidRow!);
                    Report(progress, recordIndex, records.Count, successfulDownloads, options.MaxSuccessfulDownloads, br, DownloadStage.RecordFailed, invalidRow!.Error);
                    continue;
                }

                if (_fileStore.Exists(pdfFileName) && !options.OverwriteExisting)
                {
                    DownloadStatusRow skipped = CreateRow(record.BrNum, string.Empty, DownloadStatus.SkippedExists, "File already exists.");
                    rows.Add(skipped);

                    Report(progress, recordIndex, records.Count, successfulDownloads, options.MaxSuccessfulDownloads, br, DownloadStage.SkippedExists, "Skipped (exists)");
                    continue;
                }

                DownloadStatusRow result = await ProcessRecordAsync(
                    record,
                    pdfFileName,
                    options,
                    cancellationToken,
                    progress,
                    recordIndex,
                    records.Count,
                    successfulDownloads);

                rows.Add(result);
                Report(progress,
                recordIndex,
                records.Count,
                successfulDownloads,
                options.MaxSuccessfulDownloads,
                br,
                DownloadStage.ProcessingRecord,
                "Completed",
                TryCreateUri(result.AttemptedUrl),
                result);
                if (result.Status == DownloadStatus.Downloaded)
                {
                    successfulDownloads++;
                    Report(progress, recordIndex, records.Count, successfulDownloads, options.MaxSuccessfulDownloads, br, DownloadStage.RecordSucceeded, "Downloaded", TryCreateUri(result.AttemptedUrl));
                }
                else
                {
                    Report(progress, recordIndex, records.Count, successfulDownloads, options.MaxSuccessfulDownloads, br, DownloadStage.RecordFailed, result.Error, TryCreateUri(result.AttemptedUrl));
                }
            }

            // IMPORTANT: status.csv writing stages
            Report(progress, records.Count, records.Count, successfulDownloads, options.MaxSuccessfulDownloads, "", DownloadStage.WritingStatusFile, "Writing status.csv");

            await _statusWriter.WriteAsync(options.StatusFileRelativePath, rows, cancellationToken);

            Report(progress, records.Count, records.Count, successfulDownloads, options.MaxSuccessfulDownloads, "", DownloadStage.Finished, "Finished run");
            return rows;
        }
        catch (OperationCanceledException)
        {
            Report(progress, 0, records.Count, successfulDownloads, options.MaxSuccessfulDownloads, "", DownloadStage.Cancelled, "Cancelled");
            throw;
        }
    }

    /// <summary>
    /// Attempts to download a single record using primary URL first,
    /// then fallback URL if necessary.
    /// </summary>
    private async Task<DownloadStatusRow> ProcessRecordAsync(
        ReportRecord record,
        string pdfRelativePath,
        DownloadOptions options,
        CancellationToken cancellationToken,
        IProgress<DownloadProgress>? progress,
        int recordIndex,
        int totalRecords,
        int successfulDownloads)
    {
        if (record.PrimaryUrl is not null)
        {
            DownloadStatusRow primaryRow =
            await TryDownloadAndSaveAsync(record, record.PrimaryUrl, pdfRelativePath, options, cancellationToken, progress, recordIndex, totalRecords, successfulDownloads, DownloadStage.TryingPrimary);

            if (primaryRow.Status == DownloadStatus.Downloaded)
            {
                return primaryRow;
            }

            // If there is no fallback, return primary failure.
            if (record.FallbackUrl is null)
            {
                return primaryRow;
            }
        }

        if (record.FallbackUrl is not null)
        {
            return await TryDownloadAndSaveAsync(
                record,
                record.FallbackUrl,
                pdfRelativePath,
                options,
                cancellationToken,
                progress,
                recordIndex,
                totalRecords,
                successfulDownloads,
                DownloadStage.TryingFallback);
        }

        return CreateRow(record.BrNum, string.Empty, DownloadStatus.Failed, "No URL available.");
    }

    private async Task<DownloadStatusRow> TryDownloadAndSaveAsync(
        ReportRecord record,
        Uri url,
        string pdfRelativePath,
        DownloadOptions options,
        CancellationToken cancellationToken,
        IProgress<DownloadProgress>? progress,
        int recordIndex,
        int totalRecords,
        int successfulDownloads,
        DownloadStage entryStage)
    {
        if (!IsSupportedScheme(url))
        {
            return CreateRow(record.BrNum, url.ToString(), DownloadStatus.Failed, $"Unsupported URL scheme: {url.Scheme}");
        }

        string br = record.BrNum ?? string.Empty;

        // Stage: trying primary/fallback
        Report(progress, recordIndex, totalRecords, successfulDownloads, options.MaxSuccessfulDownloads, br, entryStage, url.ToString(), url);

        // Stage: downloading
        Report(progress, recordIndex, totalRecords, successfulDownloads, options.MaxSuccessfulDownloads, br, DownloadStage.Downloading, "Downloading...", url);

        DownloadAttempt attempt = await TryDownloadPdfWithRetryAsync(url, options.RequestTimeout, cancellationToken);

        if (!attempt.IsSuccess)
        {
            return CreateRow(record.BrNum, url.ToString(), DownloadStatus.Failed, attempt.ErrorMessage);
        }

        // OPTIONAL stage: validating pdf (hvis du vil være ekstra tydelig i UI)
        Report(progress, recordIndex, totalRecords, successfulDownloads, options.MaxSuccessfulDownloads, br, DownloadStage.ValidatingPdf, "Validating PDF...", url);

        // Stage: saving file
        Report(progress, recordIndex, totalRecords, successfulDownloads, options.MaxSuccessfulDownloads, br, DownloadStage.SavingFile, "Saving file...", url);

        await _fileStore.SaveAsync(pdfRelativePath, attempt.Bytes!, cancellationToken);

        return CreateRow(record.BrNum, url.ToString(), DownloadStatus.Downloaded, string.Empty);
    }

    private async Task<DownloadAttempt> TryDownloadPdfWithRetryAsync(
        Uri url,
        TimeSpan requestTimeout,
        CancellationToken cancellationToken)
    {
        DownloadAttempt lastAttempt = DownloadAttempt.Failed("Unknown error.", isTransientFailure: true);

        for (int attemptNo = 0; attemptNo <= MaxRetries; attemptNo++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lastAttempt = await TryDownloadPdfOnceAsync(url, requestTimeout, cancellationToken);

            if (lastAttempt.IsSuccess)
            {
                return lastAttempt;
            }

            // Don't retry deterministic failures (e.g. not a PDF)
            if (!lastAttempt.IsTransientFailure)
            {
                return lastAttempt;
            }

            if (attemptNo < MaxRetries)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250 * (attemptNo + 1)), cancellationToken);
            }
        }

        return lastAttempt;
    }

    /// <summary>
    /// Downloads bytes and validates that the response is a PDF.
    /// </summary>
    private async Task<DownloadAttempt> TryDownloadPdfOnceAsync(
        Uri url,
        TimeSpan requestTimeout,
        CancellationToken cancellationToken)
    {
        try
        {
            byte[] bytes = await DownloadBytesWithTimeoutAsync(url, requestTimeout, cancellationToken);

            if (!LooksLikePdf(bytes))
            {
                string preview = ToAsciiPreview(bytes, 32);
                return DownloadAttempt.Failed($"Not a PDF. First bytes: {preview}", isTransientFailure: false);
            }

            return DownloadAttempt.Success(bytes);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Respect user cancellation immediately.
            throw;
        }
        catch (OperationCanceledException)
        {
            // Timeout (linked token cancelled) without the user cancelling the whole run.
            return DownloadAttempt.Failed($"Timeout after {requestTimeout.TotalSeconds:0} seconds.", isTransientFailure: true);
        }
        catch (HttpRequestException ex)
        {
            // Retry only likely-transient HTTP errors.
            bool isTransient = IsTransientHttpFailure(ex.StatusCode);
            return DownloadAttempt.Failed(ex.Message, isTransientFailure: isTransient);
        }
        catch (Exception ex)
        {
            // Network stack errors without a status code are often transient.
            return DownloadAttempt.Failed(ex.Message, isTransientFailure: true);
        }
    }

    private async Task<byte[]> DownloadBytesWithTimeoutAsync(
        Uri url,
        TimeSpan requestTimeout,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(requestTimeout);
        return await _httpDownloader.GetBytesAsync(url, timeoutCts.Token);
    }

    private static bool IsTransientHttpFailure(System.Net.HttpStatusCode? statusCode)
    {
        if (statusCode is null)
        {
            return true;
        }

        int code = (int)statusCode;

        // Typical transient codes: request timeout, too many requests, and server errors.
        return code == 408 || code == 429 || (code >= 500 && code <= 599);
    }

    private static bool TryGetPdfFileName(ReportRecord record, out string pdfFileName, out DownloadStatusRow? invalidRow)
    {
        pdfFileName = string.Empty;
        invalidRow = null;

        if (record.BrNum is null)
        {
            invalidRow = CreateRow(string.Empty, string.Empty, DownloadStatus.Failed, "Missing BR number.");
            return false;
        }

        string br = record.BrNum.Trim();
        if (br.Length == 0)
        {
            invalidRow = CreateRow(record.BrNum, string.Empty, DownloadStatus.Failed, "Missing BR number.");
            return false;
        }

        pdfFileName = $"{br}.pdf";
        return true;
    }

    private static DownloadStatusRow CreateRow(string? brNum, string url, DownloadStatus status, string errorMessage)
        => new(brNum?.Trim() ?? string.Empty, url, status, errorMessage);

    /// <summary>
    /// PDF files always start with "%PDF-".
    /// </summary>
    private static bool LooksLikePdf(byte[] bytes)
    {
        return bytes.Length >= 5 &&
               bytes[0] == (byte)'%' &&
               bytes[1] == (byte)'P' &&
               bytes[2] == (byte)'D' &&
               bytes[3] == (byte)'F' &&
               bytes[4] == (byte)'-';
    }

    /// <summary>
    /// Converts the first bytes to printable ASCII for debugging.
    /// Used when servers return HTML instead of PDF.
    /// </summary>
    private static string ToAsciiPreview(byte[] bytes, int maxBytes)
    {
        int len = Math.Min(bytes.Length, maxBytes);
        char[] chars = new char[len];

        for (int i = 0; i < len; i++)
        {
            byte b = bytes[i];
            chars[i] = b >= 32 && b <= 126 ? (char)b : '.';
        }

        return new string(chars);
    }

    private sealed class DownloadAttempt
    {
        private DownloadAttempt(bool isSuccess, byte[]? bytes, string errorMessage, bool isTransientFailure)
        {
            IsSuccess = isSuccess;
            Bytes = bytes;
            ErrorMessage = errorMessage;
            IsTransientFailure = isTransientFailure;
        }

        public bool IsSuccess { get; }
        public byte[]? Bytes { get; }
        public string ErrorMessage { get; }
        public bool IsTransientFailure { get; }

        public static DownloadAttempt Success(byte[] bytes) => new(true, bytes, string.Empty, false);
        public static DownloadAttempt Failed(string error, bool isTransientFailure)
            => new(false, null, error, isTransientFailure);
    }
    private static void Report(
        IProgress<DownloadProgress>? progress,
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
        if (progress is null) return;

        progress.Report(new DownloadProgress(
            recordIndex,
            totalRecords,
            successfulDownloads,
            maxSuccessfulDownloads,
            brNum,
            stage,
            message,
            attemptedUrl,
            completedRow));
    }

    private static Uri? TryCreateUri(string attemptedUrl)
    {
        if (string.IsNullOrWhiteSpace(attemptedUrl)) return null;
        return Uri.TryCreate(attemptedUrl, UriKind.Absolute, out Uri? uri) ? uri : null;
    }
}