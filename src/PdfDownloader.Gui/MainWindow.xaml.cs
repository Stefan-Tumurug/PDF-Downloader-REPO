using System.Windows;

namespace PdfDownloader.Gui;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly AsyncRelayCommand _startCommand;
    private readonly RelayCommand _cancelCommand;
    private readonly RelayCommand _browseExcelCommand;
    private readonly RelayCommand _browseOutputCommand;

    public MainWindow()
    {
        InitializeComponent();

        _vm = new MainViewModel();

        _startCommand = new AsyncRelayCommand(_vm.StartAsync, () => _vm.CanStart);
        _cancelCommand = new RelayCommand(_vm.Cancel, () => _vm.CanCancel);
        _browseExcelCommand = new RelayCommand(_vm.BrowseExcel, () => true);
        _browseOutputCommand = new RelayCommand(_vm.BrowseOutputFolder, () => true);

        _vm.PropertyChanged += (_, __) =>
        {
            _startCommand.RaiseCanExecuteChanged();
            _cancelCommand.RaiseCanExecuteChanged();
        };

        DataContext = new
        {
            Vm = _vm,
            StartCommand = _startCommand,
            CancelCommand = _cancelCommand,
            BrowseExcelCommand = _browseExcelCommand,
            BrowseOutputCommand = _browseOutputCommand
        };
    }
}