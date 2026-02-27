using PdfDownloader.Core.Domain;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PdfDownloader.Gui;

/// <summary>
/// UI wrapper around <see cref="ReportRecord"/> that adds selection state
/// and supports WPF data binding.
///
/// Responsibility:
/// - Expose a Core domain model to the UI
/// - Add IsSelected state for checkbox binding
/// - Implement INotifyPropertyChanged for reactive UI updates
///
/// Important:
/// - This class contains no business logic.
/// - It adapts a domain model for presentation purposes only.
/// </summary>
public sealed class SelectableReportRecord : INotifyPropertyChanged
{
    private bool _isSelected;

    /// <summary>
    /// Creates a selectable wrapper for a report record.
    /// </summary>
    /// <param name="record">Underlying domain record (cannot be null).</param>
    /// <param name="isSelected">Initial selection state.</param>
    public SelectableReportRecord(ReportRecord record, bool isSelected)
    {
        Record = record ?? throw new ArgumentNullException(nameof(record));
        _isSelected = isSelected;
    }

    /// <summary>
    /// Raised when a bindable property changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Underlying domain record.
    /// </summary>
    public ReportRecord Record { get; }

    /// <summary>
    /// Indicates whether this record is selected in the UI.
    /// </summary>
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

    // Convenience properties to simplify XAML bindings
    // Prevents deep bindings like Record.BrNum in the view.

    public string BrNum => Record.BrNum ?? string.Empty;
    public string PrimaryUrl => Record.PrimaryUrl?.ToString() ?? string.Empty;
    public string FallbackUrl => Record.FallbackUrl?.ToString() ?? string.Empty;

    /// <summary>
    /// Notifies the UI that a property value has changed.
    /// CallerMemberName allows automatic property name resolution.
    /// </summary>
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}