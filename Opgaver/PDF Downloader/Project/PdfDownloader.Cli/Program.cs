using PdfDownloader.Core.Domain;
using PdfDownloader.Core.Infrastructure;
using System.Net.Http;

if (args.Length < 2)
{
    Console.WriteLine("Usage: PdfDownloader.Cli <path-to-xlsx> <output-folder>");
    return;
}

string xlsxPath = args[0];
string outputFolder = args[1];

Directory.CreateDirectory(outputFolder);

ExcelReportSource source = new ExcelReportSource(xlsxPath);
IReadOnlyList<ReportRecord> records = source.ReadAll();

Console.WriteLine($"Loaded records: {records.Count}");

const int maxAttempts = 10;
bool saved = await TryDownloadFirstValidPdfAsync(records, outputFolder, maxAttempts);

if (!saved)
{
    Console.WriteLine($"No valid PDF found within {maxAttempts} attempts.");
}

/// <summary>
/// Tries to download the first record that returns a valid PDF response.
/// This is a Day 1 prototype helper to avoid getting stuck on the first bad URL.
/// </summary>
/// <param name="records">Report records read from the dataset.</param>
/// <param name="outputFolder">Folder to store downloaded PDFs.</param>
/// <param name="maxAttempts">Maximum number of candidate records to try.</param>
/// <returns>True if a valid PDF was downloaded and saved; otherwise false.</returns>
static async Task<bool> TryDownloadFirstValidPdfAsync(
    IReadOnlyList<ReportRecord> records,
    string outputFolder,
    int maxAttempts)
{
    using HttpClient http = CreateHttpClient();

    int attempts = 0;

    foreach (ReportRecord record in records)
    {
        if (attempts >= maxAttempts)
        {
            return false;
        }

        Uri? urlToTry = GetBestUrl(record);
        if (urlToTry is null)
        {
            continue;
        }

        attempts++;

        string filePath = Path.Combine(outputFolder, $"{record.BrNum}.pdf");

        if (File.Exists(filePath))
        {
            Console.WriteLine($"Skipped (already exists): {filePath}");
            return true;
        }

        try
        {
            byte[] bytes = await http.GetByteArrayAsync(urlToTry);

            if (!LooksLikePdf(bytes))
            {
                Console.WriteLine($"Attempt {attempts}/{maxAttempts}: Not a PDF for {record.BrNum}");
                Console.WriteLine($"URL: {urlToTry}");
                Console.WriteLine($"First bytes: {ToAsciiPreview(bytes, maxBytes: 32)}");
                continue;
            }

            File.WriteAllBytes(filePath, bytes);

            Console.WriteLine($"Saved: {filePath}");
            Console.WriteLine($"Bytes: {bytes.Length}");
            Console.WriteLine($"Source: {urlToTry}");
            return true;
        }
        catch (Exception ex)
        {
            // Keep going. A single failing URL should not stop the prototype.
            Console.WriteLine($"Attempt {attempts}/{maxAttempts}: Failed for {record.BrNum}");
            Console.WriteLine($"URL: {urlToTry}");
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    return false;
}

/// <summary>
/// Picks the primary URL if present; otherwise uses the fallback URL.
/// </summary>
/// <param name="record">The report record.</param>
/// <returns>A URL to try, or null if no URLs are available.</returns>
static Uri? GetBestUrl(ReportRecord record)
{
    return record.PrimaryUrl ?? record.FallbackUrl;
}

/// <summary>
/// Determines whether the downloaded bytes represent a PDF.
/// PDF files start with the ASCII header "%PDF-".
/// </summary>
/// <param name="bytes">Downloaded response bytes.</param>
/// <returns>True if bytes look like a PDF; otherwise false.</returns>
static bool LooksLikePdf(byte[] bytes)
{
    if (bytes.Length < 5)
    {
        return false;
    }

    return bytes[0] == (byte)'%' &&
           bytes[1] == (byte)'P' &&
           bytes[2] == (byte)'D' &&
           bytes[3] == (byte)'F' &&
           bytes[4] == (byte)'-';
}

/// <summary>
/// Converts the first bytes of a response to a readable ASCII preview.
/// Useful when servers return HTML (e.g., error pages) instead of a PDF.
/// </summary>
/// <param name="bytes">The response bytes.</param>
/// <param name="maxBytes">Maximum bytes to include in the preview.</param>
/// <returns>A printable ASCII string.</returns>
static string ToAsciiPreview(byte[] bytes, int maxBytes)
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
/// Creates a single HttpClient instance with a timeout and a simple User-Agent.
/// </summary>
static HttpClient CreateHttpClient()
{
    HttpClient httpClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PdfDownloader/1.0");
    return httpClient;
}