namespace Softwareschmiede.App.Services;

/// <summary>Erstellt eine <see cref="Action{Action}"/> zum Marshallen auf den UI-Thread mit Fallback auf den WPF-Dispatcher.</summary>
public static class DispatcherInvokeFactory
{
    /// <summary>Gibt <paramref name="explizit"/> zurück, falls vorhanden, sonst einen auf <see cref="System.Windows.Application.Current"/> basierenden Dispatcher-Aufruf oder direkte Ausführung, falls kein Dispatcher verfügbar ist.</summary>
    /// <param name="explizit">Ein explizit übergebener Dispatcher-Aufruf, z. B. für Tests. Wenn null, wird ein Fallback ermittelt.</param>
    /// <returns>Eine <see cref="Action{Action}"/>, die Aktionen auf den UI-Thread marshallt oder direkt ausführt.</returns>
    public static Action<Action> Create(Action<Action>? explizit)
    {
        if (explizit != null)
            return explizit;

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        return dispatcher != null
            ? action => dispatcher.Invoke(action)
            : action => action();
    }
}
