namespace Softwareschmiede.Application.Services;

/// <summary>Intervalle und Fristen der dauerhaften SCM-Verarbeitung.</summary>
public sealed class ScmEventOptions
{
    /// <summary>Abstand zwischen Remoteabfragen.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(60);
    /// <summary>Frist für die korrelierte Annahme.</summary>
    public TimeSpan AcceptanceTimeout { get; set; } = TimeSpan.FromMinutes(2);
    /// <summary>Frist für den korrelierten Abschluss.</summary>
    public TimeSpan CompletionTimeout { get; set; } = TimeSpan.FromMinutes(30);
}

/// <summary>Benachrichtigt Ansichten erst nach einer dauerhaften Änderung.</summary>
public sealed class ScmEventChangeNotifier
{
    /// <summary>Einträge oder Integrationsdiagnosen haben sich geändert.</summary>
    public event Action? Changed;
    /// <summary>Veröffentlicht eine Änderung.</summary>
    public void Notify() => Changed?.Invoke();
}

/// <summary>Hält neue Hintergrunddienste bis nach der Datenbankmigration an.</summary>
public sealed class ScmEventStartupGate
{
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    /// <summary>Gibt die Ereignisdienste nach der Migration frei.</summary>
    public void Ready() => _ready.TrySetResult();
    /// <summary>Wartet abbrechbar auf die initialisierte Datenbank.</summary>
    public Task WaitAsync(CancellationToken ct) => _ready.Task.WaitAsync(ct);
}
