using PdfDownloader.Core.Domain;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PdfDownloader.Gui;

public sealed class SelectableReportRecord : INotifyPropertyChanged
{
    private bool _isSelected;

    public SelectableReportRecord(ReportRecord record, bool isSelected)
    {
        Record = record ?? throw new ArgumentNullException(nameof(record));
        _isSelected = isSelected;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ReportRecord Record { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    // Convenience props for binding (keeps XAML clean)
    public string BrNum => Record.BrNum ?? string.Empty;
    public string PrimaryUrl => Record.PrimaryUrl?.ToString() ?? string.Empty;
    public string FallbackUrl => Record.FallbackUrl?.ToString() ?? string.Empty;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}