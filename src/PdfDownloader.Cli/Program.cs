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

/*
 Program.cs (CLI composition root)

 Responsibility:
 - Acts as the composition root for the CLI application.
 - Parses CLI arguments, wires up Core dependencies, and runs the download workflow.
 - Handles process-level concerns (Ctrl+C cancellation and exit codes).
 
 Notes:
 - Business rules and orchestration live in PdfDownloader.Core (DownloadRunner).
 - This file should stay thin: read input, compose services, run, report results.
 */

bool cancelRequested = false;

using CancellationTokenSource cts = new CancellationTokenSource();

// Handle Ctrl+C gracefully:
// - Prevent abrupt process termination (e.Cancel = true)
// - Signal cancellation to the Core workflow via CancellationTokenSource
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancelRequested = true;
    cts.Cancel();
};

try
{
    // Parse CLI args into validated values (paths + defaults).
    CliArgumentsParser parser = new CliArgumentsParser(AppContext.BaseDirectory);

    if (!parser.TryParse(args, out CliArguments? cliArgs, out string? error, out int exitCode))
    {
        Console.Error.WriteLine(error);
        return exitCode;
    }

    // Safe due to TryParse success.
    string xlsxPath = cliArgs!.XlsxPath;
    string outputFolder = cliArgs.OutputFolder;

    // Read input records from the Excel dataset.
    // This is infrastructure (I/O) but kept in CLI because the CLI is responsible for choosing the source.
    ExcelReportSource source = new ExcelReportSource(xlsxPath);
    IReadOnlyList<ReportRecord> records = source.ReadAll();

    // Use a single HttpClient instance for the entire run.
    // Timeout is managed in Core per request, so HttpClient is configured as infinite.
    using HttpClient httpClient = CreateHttpClient();

    // Compose Core runner with concrete infrastructure implementations.
    DownloadRunner runner = new DownloadRunner(
        httpDownloader: new HttpClientDownloader(httpClient),
        fileStore: new LocalFileStore(outputFolder),
        statusWriter: new CsvStatusWriter(outputFolder));

    // Runtime options:
    // - Limit successful downloads to avoid excessive network usage in prototype mode.
    // - Control where status is written relative to output folder.
    DownloadOptions options = new DownloadOptions(
        maxSuccessfulDownloads: 10,
        statusFileRelativePath: "status.csv");

    // Execute the download workflow.
    // The Core returns a per-record status list, which the CLI summarizes for the user.
    IReadOnlyList<DownloadStatusRow> rows =
        await runner.RunAsync(records, options, cts.Token);

    // If user cancelled, prefer a conventional cancellation exit code.
    if (cancelRequested)
    {
        Console.Error.WriteLine("Cancelled by user.");
        return 130;
    }

    // Summarize results for CLI output.
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
    // Cancellation can bubble out as an exception (common pattern for async + tokens).
    Console.Error.WriteLine("Cancelled by user.");
    return 130;
}
catch (Exception ex)
{
    // Last-resort catch so the CLI returns a consistent non-zero exit code
    // and prints useful diagnostics.
    Console.Error.WriteLine("Unexpected error:");
    Console.Error.WriteLine(ex);
    return 1;
}

static HttpClient CreateHttpClient()
{
    // Factory method to keep Program flow clean and make configuration explicit in one place.
    HttpClient httpClient = new HttpClient
    {
        // Timeout is handled per request in the Core layer.
        // Keep HttpClient infinite to avoid double-timeout behavior.
        Timeout = Timeout.InfiniteTimeSpan
    };

    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PdfDownloader/1.0");
    return httpClient;
}