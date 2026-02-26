using PdfDownloader.Cli;
using PdfDownloader.Core.Application;
using PdfDownloader.Core.Domain;
using PdfDownloader.Core.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

bool cancelRequested = false;

using CancellationTokenSource cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancelRequested = true;
    cts.Cancel();
};

try
{
    CliArgumentsParser parser = new CliArgumentsParser(AppContext.BaseDirectory);

    if (!parser.TryParse(args, out CliArguments? cliArgs, out string? error, out int exitCode))
    {
        Console.Error.WriteLine(error);
        return exitCode;
    }

    string xlsxPath = cliArgs!.XlsxPath;
    string outputFolder = cliArgs.OutputFolder;

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

    IReadOnlyList<DownloadStatusRow> rows =
        await runner.RunAsync(records, options, cts.Token);

    if (cancelRequested)
    {
        Console.Error.WriteLine("Cancelled by user.");
        return 130;
    }

    int downloaded = rows.Count(r => r.Status == DownloadStatus.Downloaded);
    int failed = rows.Count(r => r.Status == DownloadStatus.Failed);
    int skipped = rows.Count(r => r.Status == DownloadStatus.SkippedExists);

    Console.WriteLine($"Loaded records : {records.Count}");
    Console.WriteLine($"Downloaded     : {downloaded}");
    Console.WriteLine($"Failed         : {failed}");
    Console.WriteLine($"Skipped        : {skipped}");
    Console.WriteLine($"Processed rows : {rows.Count}");
    Console.WriteLine($"Output folder  : {outputFolder}");
    Console.WriteLine($"Status file    : {Path.Combine(outputFolder, "status.csv")}");

    return 0;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Cancelled by user.");
    return 130;
}
catch (Exception ex)
{
    Console.Error.WriteLine("Unexpected error:");
    Console.Error.WriteLine(ex);
    return 1;
}

static HttpClient CreateHttpClient()
{
    HttpClient httpClient = new HttpClient
    {
        // Timeout is handled per request in the Core layer.
        // Keep HttpClient infinite to avoid double-timeout behavior.
        Timeout = Timeout.InfiniteTimeSpan
    };

    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PdfDownloader/1.0");
    return httpClient;
}