using PdfDownloader.Core.Application;
using PdfDownloader.Core.Domain;
using PdfDownloader.Core.Infrastructure;
using System.Linq;

(string xlsxPath, string outputFolder) = ResolvePaths(args);

ExcelReportSource source = new ExcelReportSource(xlsxPath);
IReadOnlyList<ReportRecord> records = source.ReadAll();

using HttpClient httpClient = CreateHttpClient();

DownloadRunner runner = new DownloadRunner(
    httpDownloader: new HttpClientDownloader(httpClient),
    fileStore: new LocalFileStore(outputFolder),
    statusWriter: new CsvStatusWriter(outputFolder));

DownloadOptions options = new DownloadOptions(
    maxSuccessfulDownloads: 10,
    statusFileRelativePath: "status.csv");

IReadOnlyList<DownloadStatusRow> rows = await runner.RunAsync(records, options, CancellationToken.None);

int downloaded = rows.Count(r => r.Status == DownloadStatus.Downloaded);
int failed = rows.Count(r => r.Status == DownloadStatus.Failed);
int skipped = rows.Count(r => r.Status == DownloadStatus.SkippedExists);

Console.WriteLine($"Loaded records : {records.Count}");
Console.WriteLine($"Downloaded     : {downloaded}");
Console.WriteLine($"Failed         : {failed}");
Console.WriteLine($"Skipped        : {skipped}");
Console.WriteLine($"Processed rows: {rows.Count}");
Console.WriteLine($"Output folder : {outputFolder}");
Console.WriteLine($"Status file   : {Path.Combine(outputFolder, "status.csv")}");

static (string xlsxPath, string outputFolder) ResolvePaths(string[] args)
{
    // Preferred: explicit arguments (production usage)
    if (args.Length >= 2)
    {
        return (args[0], args[1]);
    }

    // Fallback: developer-friendly defaults (Visual Studio run)
    // Use relative paths so the project can move location without code changes.
    string repoRoot = FindRepoRoot(AppContext.BaseDirectory);
    string xlsxPath = Path.Combine(repoRoot, "data", "GRI_2017_2020.xlsx");
    string outputFolder = Path.Combine(repoRoot, "out");

    Console.WriteLine("No args provided. Using default paths:");
    Console.WriteLine($"XLSX: {xlsxPath}");
    Console.WriteLine($"OUT : {outputFolder}");

    return (xlsxPath, outputFolder);
}

static string FindRepoRoot(string startDirectory)
{
    DirectoryInfo? dir = new DirectoryInfo(startDirectory);

    while (dir is not null)
    {
        // We consider repo root the folder that contains "data" and "out" (or at least "data").
        string dataFolder = Path.Combine(dir.FullName, "data");
        if (Directory.Exists(dataFolder))
        {
            return dir.FullName;
        }

        dir = dir.Parent;
    }

    // Last resort: use current working directory
    return Directory.GetCurrentDirectory();
}

static HttpClient CreateHttpClient()
{
    HttpClient httpClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PdfDownloader/1.0");
    return httpClient;
}