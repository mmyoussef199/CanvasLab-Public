using System.Windows.Input;

namespace CanvasLab.ViewModels;

public sealed class AsyncRelayCommand(Func<Task> executeAsync, Func<bool>? canExecute = null) : ICommand
{
    private bool _running;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => !_running && (canExecute?.Invoke() ?? true);
    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;
        try { _running = true; RaiseCanExecuteChanged(); await executeAsync(); }
        finally { _running = false; RaiseCanExecuteChanged(); }
    }
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

