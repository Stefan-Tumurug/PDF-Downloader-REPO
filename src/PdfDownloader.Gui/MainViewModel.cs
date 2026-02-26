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
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;


namespace PdfDownloader.Gui;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private string _xlsxPath = string.Empty;
    private string _outputFolder = string.Empty;
    private int _maxSuccessfulDownloads = 10;
    public ObservableCollection<SelectableReportRecord> Records { get; } = new();
    private bool _isRunning;
    private bool _overwriteExisting;
    private string _statusText = "Ready.";
    private CancellationTokenSource? _cts;
    public bool CanLoadRows => !IsRunning && File.Exists(XlsxPath);
    public event PropertyChangedEventHandler? PropertyChanged;

    // --- Preview range (1-based start, inclusive end) ---
    private const int MaxPreviewRows = 20000;

    private string _previewStartText = "1";
    public string PreviewStartText
    {
        get => _previewStartText;
        set
        {
            if (_previewStartText == value) return;
            _previewStartText = value;
            OnPropertyChanged();

            if (TryReadInt(value, out int parsed))
            {
                PreviewStart = parsed;
            }
        }
    }

    private string _previewEndText = "200";
    public string PreviewEndText
    {
        get => _previewEndText;
        set
        {
            if (_previewEndText == value) return;
            _previewEndText = value;
            OnPropertyChanged();

            if (TryReadInt(value, out int parsed))
            {
                PreviewEnd = parsed;
            }
        }
    }

    private int _previewStart = 1;
    public int PreviewStart
    {
        get => _previewStart;
        private set
        {
            int clamped = Clamp(value, min: 1, max: MaxPreviewRows);
            if (_previewStart == clamped) return;
            _previewStart = clamped;
            OnPropertyChanged();

            // Hold teksten i sync hvis user skrev noget "skævt"
            if (_previewStartText != clamped.ToString())
            {
                _previewStartText = clamped.ToString();
                OnPropertyChanged(nameof(PreviewStartText));
            }
        }
    }

    private int _previewEnd = 200;
    public int PreviewEnd
    {
        get => _previewEnd;
        private set
        {
            int clamped = Clamp(value, min: 1, max: MaxPreviewRows);
            if (_previewEnd == clamped) return;
            _previewEnd = clamped;
            OnPropertyChanged();

            if (_previewEndText != clamped.ToString())
            {
                _previewEndText = clamped.ToString();
                OnPropertyChanged(nameof(PreviewEndText));
            }
        }
    }

    public int PreviewCount
    {
        get
        {
            int start = PreviewStart;
            int end = PreviewEnd;

            if (end < start) return 0;
            return end - start + 1;
        }
    }

    // Small helpers (kun én gang i klassen)
    private static bool TryReadInt(string? text, out int value)
        => int.TryParse(text, out value);

    private static int Clamp(int value, int min, int max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }


    public string XlsxPath
    {
        get => _xlsxPath;
        set
        {
            _xlsxPath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanLoadRows));
        }
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
        if (Records.Count == 0)
        {
            await LoadRecordsAsync();
        }

        IReadOnlyList<ReportRecord> records = GetSelectedRecordsOrShowError();
        if (records.Count == 0)
        {
            StatusText = "No rows selected.";
            return;
        }
        IsRunning = true;
        StatusText = "Running...";
        ProgressPercent = 0;
        ProgressText = "Starting...";
        Rows.Clear();
        OnCountsChanged();

        _cts = new CancellationTokenSource();
        // Ensure records are loaded for selection UI
        if (Records.Count == 0)
        {
            await LoadRecordsAsync();
        }
        try
        {
            ExcelReportSource source = new ExcelReportSource(XlsxPath);
            // Rename the inner 'records' variable to avoid shadowing
            IReadOnlyList<ReportRecord> allRecords = source.ReadAll();

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

            // Use the outer 'records' variable here
            IReadOnlyList<DownloadStatusRow> rows =
            await runner.RunAsync(records, options, _cts.Token, progress);

            OnPropertyChanged(nameof(CanStart));
            OnPropertyChanged(nameof(CanCancel));

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
    public void SelectAllRecords()
    {
        foreach (SelectableReportRecord r in Records)
        {
            r.IsSelected = true;
        }
    }

    public void ClearSelection()
    {
        foreach (SelectableReportRecord r in Records)
        {
            r.IsSelected = false;
        }
    }
    private IReadOnlyList<ReportRecord> GetSelectedRecordsOrShowError()
    {
        List<ReportRecord> selected = Records
            .Where(r => r.IsSelected)
            .Select(r => r.Record)
            .ToList();

        if (selected.Count == 0)
        {
            MessageBox.Show("No rows selected. Select at least one row.", "Selection", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        return selected;
    }
    public async Task LoadRecordsAsync()
    {
        if (!File.Exists(XlsxPath))
        {
            MessageBox.Show("Excel file not found.", "Input error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            StatusText = "Reading Excel...";
            Records.Clear();

            ExcelReportSource source = new ExcelReportSource(XlsxPath);
            IReadOnlyList<ReportRecord> records = source.ReadAll();

            int total = records.Count;

            int startIndexZeroBased = PreviewStart - 1; // 1-based UI -> 0-based list
            if (startIndexZeroBased >= total)
            {
                StatusText = $"Preview start ({PreviewStart}) is beyond total rows ({total}).";
                return;
            }

            int count = PreviewCount;
            if (count <= 0)
            {
                StatusText = $"Invalid range: {PreviewStart}-{PreviewEnd}.";
                return;
            }

            IEnumerable<ReportRecord> slice =
                records
                    .Skip(startIndexZeroBased)
                    .Take(count);

            foreach (ReportRecord record in slice)
            {
                Records.Add(new SelectableReportRecord(record, isSelected: true));
            }

            int loadedFrom = PreviewStart;
            int loadedTo = Math.Min(PreviewEnd, total);

            StatusText = $"Loaded {Records.Count}/{total} rows into UI (range: {loadedFrom}-{loadedTo}).";
        }
        catch (Exception ex)
        {
            StatusText = "Failed to load Excel rows.";
            MessageBox.Show(ex.ToString(), "Excel load failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        await Task.CompletedTask;
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
    public async Task BrowseExcelAsync()
    {
        OpenFileDialog dialog = new OpenFileDialog
        {
            Filter = "Excel Files (*.xlsx)|*.xlsx",
            Title = "Select Excel file"
        };

        bool? result = dialog.ShowDialog();

        if (result != true) return;

        XlsxPath = dialog.FileName;
        await LoadRecordsAsync();
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