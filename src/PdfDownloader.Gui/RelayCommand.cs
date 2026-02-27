using System;
using System.Windows.Input;

namespace PdfDownloader.Gui;

/// <summary>
/// Basic synchronous implementation of <see cref="ICommand"/>.
/// 
/// Responsibility:
/// - Execute a synchronous Action
/// - Delegate execution eligibility to a provided predicate
/// - Notify the UI when CanExecute changes
///
/// Use this for lightweight UI actions that do not require async/await.
/// For asynchronous operations, use AsyncRelayCommand instead.
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool> _canExecute;

    /// <summary>
    /// Creates a new command.
    /// </summary>
    /// <param name="execute">Action to execute when invoked.</param>
    /// <param name="canExecute">Predicate determining whether execution is allowed.</param>
    public RelayCommand(Action execute, Func<bool> canExecute)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    /// <summary>
    /// Raised when the command's execution eligibility changes.
    /// </summary>
    public event EventHandler? CanExecuteChanged;

    /// <summary>
    /// Determines whether the command can execute.
    /// </summary>
    public bool CanExecute(object? parameter) => _canExecute();

    /// <summary>
    /// Executes the associated action.
    /// </summary>
    public void Execute(object? parameter) => _execute();

    /// <summary>
    /// Triggers a UI re-evaluation of CanExecute.
    /// </summary>
    public void RaiseCanExecuteChanged()
        => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}