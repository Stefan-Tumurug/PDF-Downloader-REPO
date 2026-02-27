using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PdfDownloader.Gui;

/// <summary>
/// WPF ICommand implementation that supports asynchronous execution.
///
/// Responsibility:
/// - Execute an async Task delegate.
/// - Prevent re-entrancy while the command is running.
/// - Notify the UI when CanExecute changes.
///
/// Design notes:
/// - async void is acceptable here because ICommand.Execute
///   requires a void signature.
/// - Re-entrancy is blocked via the _isExecuting flag.
/// - The ViewModel controls business logic; this class only
///   coordinates command execution state.
/// </summary>
public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool> _canExecute;
    private bool _isExecuting;

    /// <summary>
    /// Creates a new asynchronous command.
    /// </summary>
    /// <param name="execute">Async operation to execute.</param>
    /// <param name="canExecute">Predicate determining whether execution is allowed.</param>
    public AsyncRelayCommand(Func<Task> execute, Func<bool> canExecute)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    /// <summary>
    /// Raised when execution eligibility changes.
    /// </summary>
    public event EventHandler? CanExecuteChanged;

    /// <summary>
    /// Command can execute only if:
    /// - Not currently executing
    /// - External canExecute condition returns true
    /// </summary>
    public bool CanExecute(object? parameter)
        => !_isExecuting && _canExecute();

    /// <summary>
    /// Executes the async delegate.
    ///
    /// While running:
    /// - Disables the command
    /// - Re-enables it when finished
    /// </summary>
    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;

        try
        {
            _isExecuting = true;
            RaiseCanExecuteChanged();

            await _execute();
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// Triggers a UI re-evaluation of CanExecute.
    /// </summary>
    public void RaiseCanExecuteChanged()
        => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}