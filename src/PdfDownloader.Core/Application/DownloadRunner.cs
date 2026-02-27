using PdfDownloader.Core.Abstractions;
using PdfDownloader.Core.Domain;

namespace PdfDownloader.Core.Application;

/// <summary>
/// DownloadRunner
///
/// Purpose
/// - Coordinates the end-to-end workflow for downloading PDF reports.
/// - Produces per-record <see cref="DownloadStatusRow"/> results and writes a final status file.
/// - Reports progress via <see cref="IProgress{T}"/> without depending on any UI framework.
///
/// What lives here (Orchestration)
/// - Choose PrimaryUrl first, then FallbackUrl if needed
/// - Apply retry policy for transient failures
/// - Apply per-request timeout (using a linked CancellationToken)
/// - Validate that downloaded content is a PDF by checking the "%PDF-" header
/// - Decide whether to skip existing files or overwrite them
///
/// What does NOT live here (Infrastructure)
/// - No direct HTTP implementation (delegated to <see cref="IHttpDownloader"/>)
/// - No direct file system access (delegated to <see cref="IFileStore"/>)
/// - No CSV formatting details (delegated to <see cref="IStatusWriter"/>)
///
/// Execution Flow (high level)
/// 1) Iterate records until out of input OR MaxSuccessfulDownloads reached
/// 2) For each record:
///    a) Validate BR number and determine file name
///    b) Skip if file exists and overwrite is disabled
///    c) Try PrimaryUrl (with retry/timeout + PDF validation)
///    d) If Primary fails and fallback exists, try FallbackUrl
///    e) Persist file if success and count toward MaxSuccessfulDownloads
///    f) Always collect a status row (Downloaded/Failed/SkippedExists)
/// 3) Write status file at the end (status.csv)
///
/// Retry Strategy
/// - Up to (1 + MaxRetries) attempts per URL.
/// - Retries only happen for likely-transient failures:
///   * timeouts
///   * HTTP 408 / 429 / 5xx
///   * transport errors without a status code
/// - Deterministic failures are not retried:
///   * "Not a PDF" (server returned HTML or other content)
///   * unsupported URL scheme
///
/// Cancellation & Timeouts
/// - A user CancellationToken cancels the whole run immediately.
/// - Per-request timeout is implemented by linking a timeout token to the user token.
///   If the timeout triggers, it is treated as transient and may be retried.
/// </summary>
public sealed class DownloadRunner
{
    private const int MaxRetries = 2; // Total attempts per URL = 1 + MaxRetries

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
        => url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps;

    /// <summary>
    /// Runs the download process for the provided records.
    ///
    /// Stops early when:
    /// - cancellation is requested, or
    /// - <see cref="DownloadOptions.MaxSuccessfulDownloads"/> is reached.
    ///
    /// A status file is written after the loop finishes (even if some records failed),
    /// so the caller always gets a complete report of what was attempted.
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
                cancellationToken.ThrowIfCancellationRequested();

                if (successfulDownloads >= options.MaxSuccessfulDownloads)
                {
                    break;
                }

                ReportRecord record = records[i];
                int recordIndex = i + 1;

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

                // Emit a completion snapshot for UI layers that want to update the row display.
                Report(
                    progress,
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

            // Status file is written once at the end for a consistent "single source of truth".
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
    /// Processes one record by trying:
    /// 1) PrimaryUrl (if present)
    /// 2) FallbackUrl (if present and primary did not succeed)
    ///
    /// This method does not change counters; it only returns the outcome row.
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
                await TryDownloadAndSaveAsync(
                    record,
                    record.PrimaryUrl,
                    pdfRelativePath,
                    options,
                    cancellationToken,
                    progress,
                    recordIndex,
                    totalRecords,
                    successfulDownloads,
                    DownloadStage.TryingPrimary);

            if (primaryRow.Status == DownloadStatus.Downloaded)
            {
                return primaryRow;
            }

            // If primary fails and there is no fallback, we return the primary failure row.
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

    /// <summary>
    /// Performs the "download + validate + save" pipeline for a single URL.
    ///
    /// Important:
    /// - Validation happens before writing to disk so we never persist HTML error pages as ".pdf".
    /// - Retry happens inside <see cref="TryDownloadPdfWithRetryAsync"/> and is based on failure type.
    /// </summary>
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

        Report(progress, recordIndex, totalRecords, successfulDownloads, options.MaxSuccessfulDownloads, br, entryStage, url.ToString(), url);
        Report(progress, recordIndex, totalRecords, successfulDownloads, options.MaxSuccessfulDownloads, br, DownloadStage.Downloading, "Downloading...", url);

        DownloadAttempt attempt = await TryDownloadPdfWithRetryAsync(url, options.RequestTimeout, cancellationToken);

        if (!attempt.IsSuccess)
        {
            return CreateRow(record.BrNum, url.ToString(), DownloadStatus.Failed, attempt.ErrorMessage);
        }

        Report(progress, recordIndex, totalRecords, successfulDownloads, options.MaxSuccessfulDownloads, br, DownloadStage.ValidatingPdf, "Validating PDF...", url);
        Report(progress, recordIndex, totalRecords, successfulDownloads, options.MaxSuccessfulDownloads, br, DownloadStage.SavingFile, "Saving file...", url);

        await _fileStore.SaveAsync(pdfRelativePath, attempt.Bytes!, cancellationToken);

        return CreateRow(record.BrNum, url.ToString(), DownloadStatus.Downloaded, string.Empty);
    }

    /// <summary>
    /// Downloads a PDF with a small retry policy.
    /// Retries only happen for transient failures (timeouts, 408/429/5xx, transport errors).
    /// </summary>
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

            // Deterministic failures are not worth retrying.
            if (!lastAttempt.IsTransientFailure)
            {
                return lastAttempt;
            }

            // Very small backoff to avoid hammering a server that is temporarily failing.
            if (attemptNo < MaxRetries)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250 * (attemptNo + 1)), cancellationToken);
            }
        }

        return lastAttempt;
    }

    /// <summary>
    /// Performs a single download attempt and validates the PDF signature.
    /// Returns a <see cref="DownloadAttempt"/> that also marks whether the failure is transient.
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
                // Useful debugging: many endpoints return HTML (login pages, 403 pages, error pages).
                string preview = ToAsciiPreview(bytes, 32);
                return DownloadAttempt.Failed($"Not a PDF. First bytes: {preview}", isTransientFailure: false);
            }

            return DownloadAttempt.Success(bytes);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // User cancellation: stop immediately, do not convert to a "failure row".
            throw;
        }
        catch (OperationCanceledException)
        {
            // Timeout (linked token cancelled), not user cancellation.
            return DownloadAttempt.Failed($"Timeout after {requestTimeout.TotalSeconds:0} seconds.", isTransientFailure: true);
        }
        catch (HttpRequestException ex)
        {
            // Some HTTP failures are likely transient and worth retrying.
            bool isTransient = IsTransientHttpFailure(ex.StatusCode);
            return DownloadAttempt.Failed(ex.Message, isTransientFailure: isTransient);
        }
        catch (Exception ex)
        {
            // Transport-level errors without a status code are commonly transient.
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
    /// PDF files start with the ASCII signature "%PDF-".
    /// This is a cheap sanity check that prevents saving HTML or error pages as PDF files.
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
    /// Converts the first N bytes to printable ASCII (non-printables become '.').
    /// Used for debugging when content is not a PDF.
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

    /// <summary>
    /// Internal result type that classifies failures as transient or deterministic
    /// to drive the retry policy.
    /// </summary>
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
