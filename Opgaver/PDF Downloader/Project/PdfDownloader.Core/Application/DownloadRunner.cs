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

    /// <summary>
    /// Executes the download run.
    /// Stops after reaching the configured number of successful downloads.
    /// Writes a CSV status file after completion.
    /// </summary>
    public async Task<IReadOnlyList<DownloadStatusRow>> RunAsync(
        IReadOnlyList<ReportRecord> records,
        DownloadOptions options,
        CancellationToken cancellationToken)
    {
        List<DownloadStatusRow> rows = new();
        int successfulDownloads = 0;

        foreach (ReportRecord record in records)
        {
            if (successfulDownloads >= options.MaxSuccessfulDownloads)
            {
                break;
            }

            string pdfFileName = $"{record.BrNum}.pdf";

            // Skip if file already exists.
            if (_fileStore.Exists(pdfFileName))
            {
                rows.Add(new DownloadStatusRow(
                    record.BrNum,
                    string.Empty,
                    DownloadStatus.SkippedExists,
                    "File already exists."));

                continue;
            }

            DownloadStatusRow result = await ProcessRecordAsync(record, pdfFileName, cancellationToken);
            rows.Add(result);

            if (result.Status == DownloadStatus.Downloaded)
            {
                successfulDownloads++;
            }
        }

        await _statusWriter.WriteAsync(options.StatusFileRelativePath, rows, cancellationToken);
        return rows;
    }

    /// <summary>
    /// Attempts to download a single record using primary URL first,
    /// then fallback URL if necessary.
    /// </summary>
    private async Task<DownloadStatusRow> ProcessRecordAsync(
        ReportRecord record,
        string pdfRelativePath,
        CancellationToken cancellationToken)
    {
        if (record.PrimaryUrl is not null)
        {
            DownloadAttempt attempt = await TryDownloadPdfAsync(record.PrimaryUrl, cancellationToken);
            if (attempt.IsSuccess)
            {
                await _fileStore.SaveAsync(pdfRelativePath, attempt.Bytes!, cancellationToken);
                return new DownloadStatusRow(record.BrNum, record.PrimaryUrl.ToString(), DownloadStatus.Downloaded, string.Empty);
            }

            if (record.FallbackUrl is null)
            {
                return new DownloadStatusRow(record.BrNum, record.PrimaryUrl.ToString(), DownloadStatus.Failed, attempt.ErrorMessage);
            }
        }

        if (record.FallbackUrl is not null)
        {
            DownloadAttempt attempt = await TryDownloadPdfAsync(record.FallbackUrl, cancellationToken);
            if (attempt.IsSuccess)
            {
                await _fileStore.SaveAsync(pdfRelativePath, attempt.Bytes!, cancellationToken);
                return new DownloadStatusRow(record.BrNum, record.FallbackUrl.ToString(), DownloadStatus.Downloaded, string.Empty);
            }

            return new DownloadStatusRow(record.BrNum, record.FallbackUrl.ToString(), DownloadStatus.Failed, attempt.ErrorMessage);
        }

        return new DownloadStatusRow(record.BrNum, string.Empty, DownloadStatus.Failed, "No URL available.");
    }

    /// <summary>
    /// Downloads bytes and validates that the response is a PDF.
    /// </summary>
    private async Task<DownloadAttempt> TryDownloadPdfAsync(Uri url, CancellationToken cancellationToken)
    {
        try
        {
            byte[] bytes = await _httpDownloader.GetBytesAsync(url, cancellationToken);

            if (!LooksLikePdf(bytes))
            {
                string preview = ToAsciiPreview(bytes, 32);
                return DownloadAttempt.Failed($"Not a PDF. First bytes: {preview}");
            }

            return DownloadAttempt.Success(bytes);
        }
        catch (Exception ex)
        {
            return DownloadAttempt.Failed(ex.Message);
        }
    }

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
        private DownloadAttempt(bool isSuccess, byte[]? bytes, string errorMessage)
        {
            IsSuccess = isSuccess;
            Bytes = bytes;
            ErrorMessage = errorMessage;
        }

        public bool IsSuccess { get; }
        public byte[]? Bytes { get; }
        public string ErrorMessage { get; }

        public static DownloadAttempt Success(byte[] bytes) => new(true, bytes, string.Empty);
        public static DownloadAttempt Failed(string error) => new(false, null, error);
    }
}