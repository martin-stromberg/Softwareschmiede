using Microsoft.Extensions.Logging;

namespace Softwareschmiede.Application.Services;

/// <summary>Erweiterungsmethoden für die sichere Ausführung von Fire-and-Forget-Tasks.</summary>
public static class AsyncTaskExtensions
{
    /// <summary>
    /// Registriert einen Callback, der Exceptions des übergebenen Tasks loggt, ohne sie zum Aufrufer zu propagieren.
    /// </summary>
    /// <param name="task">Der asynchron auszuführende Task.</param>
    /// <param name="logger">Logger für Fehler- und Abbruchmeldungen.</param>
    /// <param name="operationName">Bezeichnung der Operation für die Log-Ausgabe.</param>
    public static void SafeFireAndForget(this Task task, ILogger logger, string operationName = "Fire-and-Forget Task")
    {
        _ = task.ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                logger.LogError(t.Exception, "Unerwarteter Fehler in {OperationName}", operationName);
            }
            else if (t.IsCanceled)
            {
                logger.LogInformation("Operation {OperationName} wurde abgebrochen", operationName);
            }
        }, TaskScheduler.Default);
    }
}
