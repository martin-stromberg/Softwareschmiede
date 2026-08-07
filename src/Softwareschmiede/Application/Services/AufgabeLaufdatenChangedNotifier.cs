namespace Softwareschmiede.Application.Services;

/// <summary>UI-neutraler Notifier fuer persistierte Aenderungen an Aufgaben-Laufdaten.</summary>
public sealed class AufgabeLaufdatenChangedNotifier
{
    /// <summary>Wird ausgeloest, nachdem Laufdaten einer Aufgabe erfolgreich persistiert wurden.</summary>
    public event Action<Guid>? LaufdatenChanged;

    /// <summary>Meldet geaenderte Laufdaten fuer die angegebene Aufgabe.</summary>
    /// <param name="aufgabeId">ID der Aufgabe mit geaenderten Laufdaten.</param>
    public void NotifyLaufdatenChanged(Guid aufgabeId)
    {
        LaufdatenChanged?.Invoke(aufgabeId);
    }
}
