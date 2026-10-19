using System.Windows.Input;
using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.App.ViewModels;

/// <summary>UI-Daten einer Aufgabe in der aktiven Aufgabenliste.</summary>
public sealed class AktiveAufgabePanelItem : ViewModelBase
{
    private bool _isAktiv;

    /// <summary>Eindeutige ID der Aufgabe.</summary>
    public Guid Id { get; init; }

    /// <summary>Titel der Aufgabe.</summary>
    public string Titel { get; init; } = string.Empty;

    /// <summary>Name des zugehörigen Repositorys (<see cref="Domain.Entities.GitRepository.RepositoryName"/>), Fallback Projektname.</summary>
    public string RepositoryName { get; init; } = string.Empty;

    /// <summary>Anzeigename des SCM-/SCI-Plugins oder gespeicherter Prefix als Fallback.</summary>
    public string? ScmPluginName { get; init; }

    /// <summary>Unicode-Glyph des SCM-Plugins (ToolTip: <see cref="ScmPluginName"/>).</summary>
    public string? ScmPluginSymbol { get; init; }

    /// <summary>Anzeigename des KI-Plugins oder gespeicherter Prefix als Fallback.</summary>
    public string? KiPluginName { get; init; }

    /// <summary>Unicode-Glyph des KI-Plugins (ToolTip: <see cref="KiPluginName"/>).</summary>
    public string? KiPluginSymbol { get; init; }

    /// <summary>Gruppenname im Repository-Modus (Repository-Name oder <see cref="AufgabenListenAnsicht.OhneRepository"/>).</summary>
    public string RepositoryGruppenName { get; init; } = string.Empty;

    /// <summary><see langword="true"/>, wenn <see cref="RepositoryGruppenName"/> die Fallback-Gruppe ist (sortiert ans Ende).</summary>
    public bool RepositoryGruppeIstFallback { get; init; }

    /// <summary>Gruppenname im KI-Modus (Plugin-Name, roher Prefix oder <see cref="AufgabenListenAnsicht.NichtAusgefuehrt"/>).</summary>
    public string KiGruppenName { get; init; } = string.Empty;

    /// <summary><see langword="true"/>, wenn <see cref="KiGruppenName"/> die Fallback-Gruppe ist (sortiert ans Ende).</summary>
    public bool KiGruppeIstFallback { get; init; }

    /// <summary>Sortierzeitpunkt der Liste (letzte Ausführung bzw. Erstellung).</summary>
    public DateTimeOffset SortierZeitpunkt { get; init; }

    /// <summary>Optionaler Laufzeit-Substatus der aktiven CLI-Ausführung.</summary>
    public AufgabeLaufStatus? LaufStatus { get; init; }

    /// <summary>Aktueller Aufgabenstatus.</summary>
    public AufgabeStatus Status { get; init; }

    /// <summary>Persistierter Lebenszyklusstatus der KI-Ausführung.</summary>
    public AufgabeAusfuehrungsStatus AusfuehrungsStatus { get; init; } = AufgabeAusfuehrungsStatus.NichtGestartet;

    /// <summary>Aktive Lauf-ID der KI-Ausführung.</summary>
    public string? AktiveRunId { get; init; }

    /// <summary>Zeitstempel des letzten Heartbeats einer aktiven Ausführung.</summary>
    public DateTimeOffset? LastHeartbeatUtc { get; init; }

    /// <summary>Zeitstempel des letzten echten CLI-Prozessstarts.</summary>
    public DateTimeOffset? LetzterCliStartUtc { get; init; }

    /// <summary>Optionaler UTC-Zeitpunkt, bis zu dem die Aufgabe pausiert ist (null oder Vergangenheit = nicht pausiert).</summary>
    public DateTimeOffset? PausiertBisUtc { get; init; }

    /// <summary>Gibt an, ob die Aufgabe aktuell pausiert ist (<see cref="PausiertBisUtc"/> liegt in der Zukunft).</summary>
    public bool IstPausiert => PausiertBisUtc > DateTimeOffset.UtcNow;

    /// <summary>Gibt an, ob für diese Aufgabe aktuell ein zeitgesteuerter Prompt in der Warteschlange steht.</summary>
    public bool HasScheduledPrompt { get; init; }

    /// <summary>Anzahl offener To-Dos dieser Aufgabe.</summary>
    public int OffeneTodoCount { get; init; }

    /// <summary>Anzeigetext für die offenen To-Dos.</summary>
    public string OffeneTodoLabelText => OffeneTodoCount == 1 ? "1 Todo" : $"{OffeneTodoCount} Todos";

    /// <summary>Öffnet den read-only Dialog mit den offenen To-Dos dieser Aufgabe.</summary>
    public ICommand? OffeneTodosAnzeigenCommand { get; init; }

    /// <summary>Gibt an, ob diese Aufgabe aktuell im Inhaltsbereich angezeigt wird.</summary>
    public bool IsAktiv
    {
        get => _isAktiv;
        set => SetProperty(ref _isAktiv, value);
    }
}
