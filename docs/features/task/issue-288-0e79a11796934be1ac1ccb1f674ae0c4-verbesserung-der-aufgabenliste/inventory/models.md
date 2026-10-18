# Datenmodelle

## `AktiveAufgabePanelItem`

Datei: `src/Softwareschmiede.App/ViewModels/AktiveAufgabePanelItem.cs`

UI-Daten einer Aufgabe in der aktiven Aufgabenliste. Erbt `ViewModelBase`; alle Properties außer `IsAktiv` sind `init`-only (werden in `MainWindowViewModel.MapAktiveAufgabePanelItem` befüllt).

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Aufgaben-ID; wird als `CommandParameter` an `NavigateCommand` übergeben |
| `Titel` | `string` | Aufgabentitel (Kachel-Zeile 1; Basis aller UIA-Anker `…:{Titel}`) |
| `ProjektName` | `string` | Anzeigename des Projekts aus `aufgabe.Projekt?.Name` — **nicht** `GitRepository.RepositoryName` (Kachel-Zeile 2) |
| `ScmPluginName` | `string?` | Aufgelöster SCM-Plugin-Anzeigename oder gespeicherter Prefix als Fallback (Kachel-Zeile "SCM: …") |
| `KiPluginName` | `string?` | Aufgelöster KI-Plugin-Anzeigename oder gespeicherter Prefix als Fallback (Kachel-Zeile "KI: …") |
| `LaufStatus` | `AufgabeLaufStatus?` | Laufzeit-Substatus der CLI-Ausführung (für "▶ Läuft" vs. "⏸ Wartet") |
| `Status` | `AufgabeStatus` | Lebenszyklusstatus der Aufgabe |
| `AusfuehrungsStatus` | `AufgabeAusfuehrungsStatus` | Persistierter Status der KI-Ausführung (Default `NichtGestartet`) |
| `AktiveRunId` | `string?` | Aktive Lauf-ID der KI-Ausführung |
| `LastHeartbeatUtc` | `DateTimeOffset?` | Letzter Heartbeat einer aktiven Ausführung |
| `LetzterCliStartUtc` | `DateTimeOffset?` | Letzter echter CLI-Prozessstart (Sortierkriterium der Datenquelle) |
| `PausiertBisUtc` | `DateTimeOffset?` | Pausier-Ende; `null`/Vergangenheit = nicht pausiert |
| `IstPausiert` | `bool` | Abgeleitet: `PausiertBisUtc > DateTimeOffset.UtcNow` (steuert Opacity-Abblendung + UIA-Marker) |
| `HasScheduledPrompt` | `bool` | Zeitgesteuerter Prompt in Warteschlange (→ "⏳ Prompt in Wartestellung") |
| `OffeneTodoCount` | `int` | Anzahl offener To-Dos |
| `OffeneTodoLabelText` | `string` | Abgeleitet: `"1 Todo"` / `"{n} Todos"` (Button-Content) |
| `OffeneTodosAnzeigenCommand` | `ICommand?` | Öffnet den read-only To-do-Dialog |
| `IsAktiv` | `bool` | Setzbar; markiert die aktuell in der Detailansicht geöffnete Aufgabe (Accent-Rand) |

**Nicht vorhanden** (für die Anforderung relevant): Repository-Name, Plugin-Symbole, aufgelöster KI-Gruppenname, expliziter Sortierzeitpunkt (`LetzterCliStartUtc ?? ErstellungsDatum` — `ErstellungsDatum` ist nicht gemappt), Gruppenschlüssel.

## `Aufgabe`

Datei: `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`

Domain-Entität; für die Anforderung relevante Eigenschaften:

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige ID |
| `ProjektId` | `Guid` | Zugehöriges Projekt |
| `GitRepositoryId` | `Guid?` | **Optional** — Aufgaben ohne Repository-Verknüpfung möglich |
| `Titel` | `string` | Aufgabentitel |
| `Status` | `AufgabeStatus` | Lebenszyklus (Neu/Gestartet/Wartend/Beendet/Archiviert) |
| `AusfuehrungsStatus` | `AufgabeAusfuehrungsStatus` | KI-Ausführungsstatus (Default `NichtGestartet`) |
| `KiPluginPrefix` | `string?` | Zuletzt verwendetes KI-Plugin (gesetzt u. a. über `AufgabeService.UpdateAsync`) |
| `ErstellungsDatum` | `DateTimeOffset` | Fallback-Sortierkriterium |
| `AktiveRunId` | `string?` | Aktive Lauf-ID |
| `LastHeartbeatUtc` | `DateTimeOffset?` | Letzter Heartbeat |
| `LetzterCliStartUtc` | `DateTimeOffset?` | Primäres Sortierkriterium von `GetAktiveAufgabenAsync` |
| `LaufStatus` | `AufgabeLaufStatus?` | Laufzeit-Substatus |
| `PausiertBisUtc` | `DateTimeOffset?` | Pausier-Ende |
| `Projekt` | `Projekt` | Navigation (in `GetAktiveAufgabenAsync` inkludiert) |
| `GitRepository` | `GitRepository?` | Navigation (ebenfalls inkludiert) |

## `GitRepository`

Datei: `src/Softwareschmiede/Domain/Entities/GitRepository.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige ID |
| `ProjektId` | `Guid` | Zugehöriges Projekt |
| `PluginTyp` | `string` | Gespeicherter SCM-Plugin-Prefix (z. B. "Softwareschmiede.GitHub"); Auflösungsquelle für `ScmPluginName` |
| `RepositoryUrl` | `string` | Repository-URL |
| `RepositoryName` | `string` | **Anzeigename des Repositories** — voraussichtlicher Untertitel/Gruppenschlüssel der Anforderung |
| `Aktiv` | `bool` | Aktiv-Flag (Default `true`) |

## `AppEinstellung`

Datei: `src/Softwareschmiede/Domain/Entities/AppEinstellung.cs`

Globale Schlüssel/Wert-Einstellungstabelle (`Schluessel`, `Wert`, `AktualisiertAm`). Potenzieller Ablageort, falls der gewählte Aufgabenlisten-Modus persistiert werden soll (in der Anforderung als offene Frage geführt).
