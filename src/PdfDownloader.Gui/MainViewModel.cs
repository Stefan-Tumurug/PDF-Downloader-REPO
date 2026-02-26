using Microsoft.Win32;
using PdfDownloader.Core.Application;
using PdfDownloader.Core.Domain;
using PdfDownloader.Core.Infrastructure;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;


namespace PdfDownloader.Gui;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private string _xlsxPath = string.Empty;
    private string _outputFolder = string.Empty;
    private int _maxSuccessfulDownloads = 10;

    private bool _isRunning;
    private bool _overwriteExisting;
    private string _statusText = "Ready.";
    private CancellationTokenSource? _cts;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string XlsxPath
    {
        get => _xlsxPath;
        set { _xlsxPath = value; OnPropertyChanged(); }
    }

    public string OutputFolder
    {
        get => _outputFolder;
        set { _outputFolder = value; OnPropertyChanged(); }
    }

    public int MaxSuccessfulDownloads
    {
        get => _maxSuccessfulDownloads;
        set { _maxSuccessfulDownloads = value; OnPropertyChanged(); }
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set { _isRunning = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanStart)); OnPropertyChanged(nameof(CanCancel)); }
    }
    public bool OverwriteExisting
    {
        get => _overwriteExisting;
        set
        {
            _overwriteExisting = value;
            OnPropertyChanged();
        }
    }
    public bool CanStart => !IsRunning;
    public bool CanCancel => IsRunning;

    public string StatusText
    {
        get => _statusText;
        private set { _statusText = value; OnPropertyChanged(); }
    }

    private double _progressPercent;
    public double ProgressPercent
    {
        get => _progressPercent;
        private set { _progressPercent = value; OnPropertyChanged(); }
    }

    private string _progressText = string.Empty;
    public string ProgressText
    {
        get => _progressText;
        private set { _progressText = value; OnPropertyChanged(); }
    }
    public ObservableCollection<DownloadStatusRow> Rows { get; } = new();

    public int DownloadedCount => Rows.Count(r => r.Status == DownloadStatus.Downloaded);
    public int FailedCount => Rows.Count(r => r.Status == DownloadStatus.Failed);
    public int SkippedCount => Rows.Count(r => r.Status == DownloadStatus.SkippedExists);

    public async Task StartAsync()
    {
        if (IsRunning) return;

        if (!File.Exists(XlsxPath))
        {
            MessageBox.Show("Excel file not found.", "Input error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(OutputFolder))
        {
            MessageBox.Show("Output folder is required.", "Input error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Directory.CreateDirectory(OutputFolder);

        if (!OverwriteExisting &&
        Directory.Exists(OutputFolder) &&
        Directory.EnumerateFiles(OutputFolder, "*.pdf").Any())
        {
            StatusText = "Existing files will be skipped.";
        }

        IsRunning = true;
        StatusText = "Running...";
        ProgressPercent = 0;
        ProgressText = "Starting...";
        Rows.Clear();
        OnCountsChanged();

        _cts = new CancellationTokenSource();

        try
        {
            ExcelReportSource source = new ExcelReportSource(XlsxPath);
            IReadOnlyList<ReportRecord> records = source.ReadAll();

            using HttpClient httpClient = CreateHttpClient();

            DownloadRunner runner = new DownloadRunner(
                httpDownloader: new HttpClientDownloader(httpClient),
                fileStore: new LocalFileStore(OutputFolder),
                statusWriter: new CsvStatusWriter(OutputFolder));

            DownloadOptions options = new DownloadOptions(
                maxSuccessfulDownloads: MaxSuccessfulDownloads,
                statusFileRelativePath: "status.csv",
                overwriteExisting: OverwriteExisting);

            IProgress<DownloadProgress> progress = new Progress<DownloadProgress>(p =>
            {
                ProgressPercent = p.Percent;

                string success = $"Success {p.SuccessfulDownloads}/{p.MaxSuccessfulDownloads}";
                string br = string.IsNullOrWhiteSpace(p.BrNum) ? "" : $"BR {p.BrNum}";
                string msg = string.IsNullOrWhiteSpace(p.Message) ? "" : p.Message;

                ProgressText = $"{p.Stage} - {br} ({success}) {msg}".Trim();

                // add row live
                if (p.CompletedRow is not null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Rows.Add(p.CompletedRow);
                        OnCountsChanged();
                    });
                }
            });

            IReadOnlyList<DownloadStatusRow> rows =
            await runner.RunAsync(records, options, _cts.Token, progress);

            OnCountsChanged();
            StatusText = "Done.";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = "Failed.";
            MessageBox.Show(ex.ToString(), "Unexpected error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            IsRunning = false;
            if (StatusText == "Done.")
            {
                ProgressPercent = 100;
            }
        }
    }


    public void Cancel()
    {
        if (!IsRunning) return;
        _cts?.Cancel();
    }

    private static HttpClient CreateHttpClient()
    {
        HttpClientHandler handler = new HttpClientHandler
        {
            AllowAutoRedirect = true
        };

        HttpClient httpClient = new HttpClient(handler)
        {
            // Timeout is handled per request in the Core layer.
            Timeout = Timeout.InfiniteTimeSpan
        };

        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PdfDownloader/1.0");
        return httpClient;
    }

    private void OnCountsChanged()
    {
        OnPropertyChanged(nameof(DownloadedCount));
        OnPropertyChanged(nameof(FailedCount));
        OnPropertyChanged(nameof(SkippedCount));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    public void BrowseExcel()
    {
        Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Excel Files (*.xlsx)|*.xlsx",
            Title = "Select Excel file"
        };

        bool? result = dialog.ShowDialog();

        if (result == true)
        {
            XlsxPath = dialog.FileName;
        }
    }

    public void BrowseOutputFolder()
    {
        using System.Windows.Forms.FolderBrowserDialog dialog =
            new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select output folder"
            };

        System.Windows.Forms.DialogResult result = dialog.ShowDialog();

        if (result == System.Windows.Forms.DialogResult.OK)
        {
            OutputFolder = dialog.SelectedPath;
        }
    }
}