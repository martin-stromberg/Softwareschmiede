# Datenmodell — Bestandsaufnahme

## `ScheduledPromptInfo`
Datei: `src/Softwareschmiede/Application/Services/ScheduledPromptInfo.cs`

`sealed record` — Datencontainer für einen zeitgesteuert zu versendenden Prompt. Wird in `PromptZeitVersandService` erzeugt (Zeile 72) und über `GetScheduledPromptStatus` nach außen gereicht (u. a. `TaskDetailViewModel` Zeile 1835, `MainWindowViewModel` Zeile 406). Nicht persistiert.

| Positionsparameter | Typ | Beschreibung / Zweck |
|--------------------|-----|----------------------|
| `AufgabeId` | `Guid` | ID der Aufgabe, deren Session den Prompt erhalten soll. |
| `PromptText` | `string` | Der bereits platzhalteraufgelöste Prompttext (Auflösung vorher in `TaskDetailViewModel.SchedulePromptAsync` via `PromptVorlagenPlatzhalterService.Resolve`, Zeile 1825). |
| `TargetTime` | `DateTimeOffset` | Zeitpunkt, zu dem der Prompt versendet werden soll. |

## `ScheduledPromptEntry` (private geschachtelte Klasse)
Datei: `src/Softwareschmiede/Application/Services/PromptZeitVersandService.cs` (Zeilen 14–18)

Interner Warteschlangen-Eintrag des `PromptZeitVersandService`; Elemente liegen in `Dictionary<Guid, ScheduledPromptEntry> _scheduledPrompts` (Zeile 24), Schlüssel ist die `AufgabeId` (max. ein Eintrag pro Aufgabe).

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Info` | `ScheduledPromptInfo` (required, init) | Geplanter Prompt (Text + Zielzeit). |
| `Timer` | `ITimer` (required, init) | Per-Entry-Timer aus `TimeProvider.CreateTimer`; wird beim Entfernen disposed. |

## `Aufgabe` (nur anforderungsrelevante Eigenschaften)
Datei: `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`

Persistierte Entität (`DbSet<Aufgabe> Aufgaben` im `SoftwareschmiededDbContext`). Für die Anforderung relevante Eigenschaften:

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Primärschlüssel; zugleich Schlüssel der In-Memory-Warteschlange und der Session-Map `_handles`. |
| `Status` | `AufgabeStatus` | Aktueller Status der Aufgabe (u. a. `Neu`, `Gestartet`, `Wartend`, `Beendet`). |
| `AusfuehrungsStatus` | `AufgabeAusfuehrungsStatus` | Persistierter Status der KI-Ausführung (Standard `NichtGestartet`). |
| `VorschlagPrompt` | `string?` | Persistierter Vorschlag für den nächsten Prompt (Zeile 92) — anderer fachlicher Zweck als der zeitgesteuerte Versand; in der Anforderung als mögliche Anknüpfungsstelle genannt (offene Frage 8). |
| `VorschlagAusfuehrenAbUtc` | `DateTimeOffset?` | Geplanter Ausführungszeitpunkt des Vorschlagsprompts (Zeile 95); gespeichert via `NullableUnixMillisConverter` (DbContext Zeile 194). |
| `PausiertBisUtc` | `DateTimeOffset?` | UTC-Ende einer Pause (Zeilen 98–103); blockiert Starts und verschiebt den Versand zeitgesteuerter Prompts (gelesen von `PromptZeitVersandService.GetPausiertBisUtcAsync`). |
| `AktiveRunId` | `string?` | Aktive Lauf-ID einer KI-Ausführung (Anzeige-Kontext). |
| `LastHeartbeatUtc` | `DateTimeOffset?` | Letzter Heartbeat (Anzeige-Kontext). |
| `LetzterCliStartUtc` | `DateTimeOffset?` | Letzter CLI-Prozessstart (Sortier-/Anzeige-Kontext). |
| `LaufStatus` | `AufgabeLaufStatus?` | Laufzeit-Substatus (Anzeige-Kontext). |

Schreibpfade der Vorschlags-Felder: `AufgabeService.SavePromptVorschlagAsync` (Zeile 788) und `ClearPromptVorschlagAsync` (Zeile 808). `PausiertBisUtc` wird via `AufgabeService.SetPauseAsync` (Zeile 831) gesetzt.

## `AktiveAufgabePanelItem` (nur anforderungsrelevante Eigenschaften)
Datei: `src/Softwareschmiede.App/ViewModels/AktiveAufgabePanelItem.cs`

UI-Projection für die aktive Aufgabenliste (Seitenleiste/Dashboard).

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `HasScheduledPrompt` | `bool` (init) | `true`, wenn für die Aufgabe ein Prompt geplant ist; wird in `MainWindowViewModel` (Zeile 406) aus `PromptZeitVersandService.GetScheduledPromptStatus` befüllt und steuert den `⏳ Prompt in Wartestellung`-Status (`AppConverters.cs` Zeilen 170–173, 220, 252). |
| `PausiertBisUtc` | `DateTimeOffset?` | Pausenende; die Pausiert-Anzeige hat Vorrang vor `PromptInWartestellung` (`AppConverters.cs` Zeilen 162–168). |

## `EntwicklungsprozessServiceOptions`
Datei: `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs` (Zeilen 21–27)

`sealed record` mit optionalen Abhängigkeiten des `EntwicklungsprozessService`; relevant: `PromptZeitVersandService? PromptZeitVersandService = null` — wird beim Aufgabenabschluss zum Stornieren genutzt (Zeile 438).

## Persistenz-Kontext und vorhandene Muster
Datei: `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContext.cs`

- Es existiert **kein** `DbSet` und keine `OnModelCreating`-Konfiguration für geplante/zeitgesteuerte Prompts.
- Konventionen: `DateTimeOffset` → `UnixMillisConverter`, `DateTimeOffset?` → `NullableUnixMillisConverter` (Zeilen 13–19); Enum-Properties als `string`; Aufgabenbezug per FK mit `DeleteBehavior.Cascade` (Muster bei `IssueReferenz`, `AlertReferenz`, `PullRequestReferenz`, `Protokolleintrag`, `DiffResult`, `Todo`, Zeilen 206–229).
- Als Referenz für warteschlangenartige persistierte Einträge nennt die Anforderung u. a. `ScmOutgoingAction` (`DbSet` Zeile 97, Mapping Zeilen 118–122 mit Status-Index) sowie `PromptVorlage` (`DbSet` Zeile 64, Mapping Zeilen 366–377).
- Migrationen liegen unter `src/Softwareschmiede/Migrations/` (neueste: `20261028142449_RemoveAutonomAufgabeModels`); sie werden beim App-Start via `db.Database.MigrateAsync()` angewendet (`App.xaml.cs` Zeile 96).
