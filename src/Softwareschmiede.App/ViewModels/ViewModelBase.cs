using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Softwareschmiede.App.ViewModels;

/// <summary>Basisklasse für alle ViewModels mit manueller INotifyPropertyChanged-Implementierung.</summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Löst PropertyChanged aus.</summary>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>Setzt ein Feld und löst PropertyChanged aus, wenn sich der Wert geändert hat.</summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>Setzt ein Feld, löst PropertyChanged aus und ruft eine Aktion auf, wenn sich der Wert geändert hat.</summary>
    protected bool SetProperty<T>(ref T field, T value, Action onChanged, [CallerMemberName] string? propertyName = null)
    {
        if (!SetProperty(ref field, value, propertyName))
            return false;

        onChanged();
        return true;
    }
}

/// <summary>Einfacher Relay-Command ohne Parameter.</summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    /// <inheritdoc cref="RelayCommand"/>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    /// <inheritdoc/>
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    /// <inheritdoc/>
    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    /// <inheritdoc/>
    public void Execute(object? parameter) => _execute();

    /// <summary>Erzwingt die Neuauswertung von CanExecute.</summary>
    public static void Refresh() => CommandManager.InvalidateRequerySuggested();
}

/// <summary>Relay-Command mit Parameter.</summary>
public sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    /// <inheritdoc cref="RelayCommand{T}"/>
    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    /// <inheritdoc/>
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    /// <inheritdoc/>
    public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;

    /// <inheritdoc/>
    public void Execute(object? parameter) => _execute((T?)parameter);
}

/// <summary>Asynchroner Relay-Command ohne Parameter.</summary>
public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<CancellationToken, Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _isExecuting;
    private CancellationTokenSource? _cts;

    /// <inheritdoc cref="AsyncRelayCommand"/>
    public AsyncRelayCommand(Func<CancellationToken, Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    /// <inheritdoc/>
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    /// <summary>Gibt an, ob der Command gerade ausgeführt wird.</summary>
    public bool IsExecuting => _isExecuting;

    /// <inheritdoc/>
    public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute?.Invoke() ?? true);

    /// <inheritdoc/>
    public async void Execute(object? parameter)
    {
        if (_isExecuting)
            return;

        _cts = new CancellationTokenSource();
        _isExecuting = true;
        CommandManager.InvalidateRequerySuggested();

        try
        {
            await _execute(_cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Abgebrochen - kein Fehler
        }
        catch (Exception ex)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is not null && !dispatcher.HasShutdownStarted)
            {
                var capturedEx = ex;
                _ = dispatcher.BeginInvoke(new Action(() =>
                    throw new InvalidOperationException(
                        $"Unbehandelte Ausnahme in AsyncRelayCommand: {capturedEx.Message}", capturedEx)));
            }
        }
        finally
        {
            _isExecuting = false;
            _cts?.Dispose();
            _cts = null;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>Bricht die laufende Ausführung ab.</summary>
    public void Cancel() => _cts?.Cancel();
}
