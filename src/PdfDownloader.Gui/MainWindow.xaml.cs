using System.Windows;

namespace PdfDownloader.Gui;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    private readonly AsyncRelayCommand _startCommand;
    private readonly RelayCommand _cancelCommand;
    private readonly AsyncRelayCommand _browseExcelCommand;
    private readonly RelayCommand _browseOutputCommand;

    private readonly AsyncRelayCommand _loadCommand;
    private readonly RelayCommand _selectAllCommand;
    private readonly RelayCommand _clearSelectionCommand;

    public MainWindow()
    {
        InitializeComponent();

        _vm = new MainViewModel();

        // Commands (kun én gang)
        _startCommand = new AsyncRelayCommand(_vm.StartAsync, () => _vm.CanStart);
        _cancelCommand = new RelayCommand(_vm.Cancel, () => _vm.CanCancel);
        _browseExcelCommand = new AsyncRelayCommand(_vm.BrowseExcelAsync, () => _vm.CanStart);
        _browseOutputCommand = new RelayCommand(_vm.BrowseOutputFolder, () => true);

        _loadCommand = new AsyncRelayCommand(_vm.LoadRecordsAsync, () => _vm.CanLoadRows);
        _selectAllCommand = new RelayCommand(_vm.SelectAllRecords, () => _vm.CanStart && _vm.Records.Count > 0);
        _clearSelectionCommand = new RelayCommand(_vm.ClearSelection, () => _vm.CanStart && _vm.Records.Count > 0);

        // Hooks
        _vm.Records.CollectionChanged += (_, __) =>
        {
            _selectAllCommand.RaiseCanExecuteChanged();
            _clearSelectionCommand.RaiseCanExecuteChanged();
            _loadCommand.RaiseCanExecuteChanged();
        };

        _vm.PropertyChanged += (_, __) =>
        {
            _startCommand.RaiseCanExecuteChanged();
            _cancelCommand.RaiseCanExecuteChanged();
            _loadCommand.RaiseCanExecuteChanged();
            _selectAllCommand.RaiseCanExecuteChanged();
            _clearSelectionCommand.RaiseCanExecuteChanged();
        };

        DataContext = new
        {
            Vm = _vm,
            StartCommand = _startCommand,
            CancelCommand = _cancelCommand,
            BrowseExcelCommand = _browseExcelCommand,
            BrowseOutputCommand = _browseOutputCommand,
            LoadCommand = _loadCommand,
            SelectAllCommand = _selectAllCommand,
            ClearSelectionCommand = _clearSelectionCommand
        };
    }
}