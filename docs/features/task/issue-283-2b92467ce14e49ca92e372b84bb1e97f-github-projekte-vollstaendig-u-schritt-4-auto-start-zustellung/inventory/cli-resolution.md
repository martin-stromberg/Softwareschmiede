# Bestandsaufnahme-Detail: CLI-Auflösung und Wartend-Verhalten

Zurück: [../inventory.md](../inventory.md)

## Auflösungskette

`PluginSelectionService.ResolveDevelopmentAutomationPluginWithProjectScopeAsync` (`src/Softwareschmiede/Application/Services/PluginSelectionService.cs:101-124`):

1. `aufgabenPluginPrefix` (`Aufgabe.KiPluginPrefix`) — direkt, ohne Aktivierungsprüfung.
2. `PluginDefaultSettingsService.GetProjectDefaultPluginPrefixAsync(projektId, PluginType.DevelopmentAutomation)` — `AppEinstellungen`-Schlüssel `plugins.default.project.{id}.developmentautomation` (Projekt-Default).
3. `GetDefaultPluginPrefixAsync(PluginType.DevelopmentAutomation)` — `plugins.default.developmentautomation` (globaler Default).
4. `null` → Aufrufer entscheidet (Orchestrator: Wartend; UI: Auswahldialog).

`ResolveDevelopmentAutomationPluginStrictAsync` (Z. 88-94) löst ausschließlich **aktivierte** Plugins auf (`PluginActivationService.GetEnabledDevelopmentAutomationPluginsAsync` — fehlender `plugins.enabled.*`-Eintrag bedeutet aktiviert, `PluginActivationService.cs:29-32`); sonst `InvalidOperationException` → im Dispatch `NextAttemptUtc = long.MaxValue` (manuelle Prüfung nötig).

Belegt: `ProzessStartenUndCliStartenAsync`/`CliNeustartenAsync` schreiben den tatsächlich verwendeten Prefix über `AufgabeService.UpdateAsync` in `Aufgabe.KiPluginPrefix` (`EntwicklungsprozessService.cs:181-186, 276-281`).

## Wartend-Verhalten ohne Standard-CLI

- Text: „Keine Standard-CLI konfiguriert. Ereignis öffnen und CLI auswählen." (`ScmEventOrchestrator.cs:143`), gesetzt via `WaitWithCommentAsync` → `Status="Wartend"`, `Reason`, plus `waiting`-Kommentar über die Outbox.
- `NextAttemptUtc` bleibt `null` → der 60-s-Poll versucht die Zustellung automatisch erneut, sobald eine CLI auflösbar ist (`ScmEventMonitoringService.cs:153-156`). „Erneut prüfen" (`RetryAsync`) ist der manuelle Sofortpfad.
- Bedienbarkeit in der SCM-Ereignisübersicht: `ScmEventOpen` ist aktiv solange `!IsProcessed && !IsBlocked && AttemptId == null` (`ScmEventQueueViewModel.cs:295`-Kontext, View-Logik über `ScmEventQueueItem`).

## UI: SCM-Ereignisübersicht

- `src/Softwareschmiede.App/Views/ScmEventQueueView.xaml` + `src/Softwareschmiede.App/ViewModels/ScmEventQueueViewModel.cs`.
- `ScmEventQueueItem.From` (`ScmEventQueueViewModel.cs:297-311`): projiziert Status in deutsche Texte; `IsBlocked` für beendete/archivierte/pausierte Aufgaben (dann `ScmEventOpen` gesperrt); `CanConfirmReady` nur bei AttemptId `null` + `CliRunId` + Reason „Eingabebereitschaft unbekannt".
- `OpenAsync` (Z. 219-246): bei lebender Session `BestaetigenDialog` „An laufende CLI übergeben…", sonst `ShowPluginSelectionDialogAsync` mit Option „als Projekt-Default speichern" → `OpenFromQueueAsync(eventId, select)`.
- „Erneut prüfen" (`ScmEventRetry`) → `RetryAsync`; hebt auch Checkpoint-Sperren auf.
- Zeilenanzeige: Spalte „Aufgabe" = `TaskTitle` (`ScmEventQueueView.xaml:81`) — für automatisch angelegte Aufgaben der Issue-/PR-Titel (z. B. „SCM Referenz 20" im E2E-Fixture).

## Statusmodell `ScmIncomingEvent.Status`

| Status | Erreicht über |
|--------|---------------|
| `Aufgenommen` | Inbox-Insert |
| `Wartend` | `WaitAsync`/`WaitWithCommentAsync` |
| `ZustellungVorbereitet` | Attempt persistiert (Z. 182) |
| `Zugestellt` | `WritePromptAsync` erfolgreich (Z. 195) |
| `InBearbeitung` | `accepted.json` korreliert |
| `Verarbeitet` | `complete.json` + alle Aktionen `Succeeded` |
| `ZustellungUngewiss` | Schreibversuch unbestätigt |
| `VerarbeitungUngeklärt` | Kanal-/Timeout-/Identitätsproblem |

## Entscheidungskontext: Anlage bei fehlender Standard-CLI

Ist-Zustand (bereits so implementiert): Die Anlage erfolgt in der Aufnahmephase des Monitors **vor** jeder CLI-Auflösung. Folgen:

- Ereignis geht mit `AufgabeId` in die Inbox → Dispatch → ohne CLI `Wartend` mit bedienbarer Aufforderung; `NextAttemptUtc = null` → automatischer Folgeversuch.
- Aufgabe ist sofort in der Aufgabenverwaltung sichtbar (`ProjektId`, Titel, Quellverweis im Auftragstext).
- `SourceKey`-/Unique-Index-Dedup verhindert Duplikate über Wartezeiten und Wiederholungen hinweg; kein Ereignis geht verloren (Inbox-Eintrag persistiert).
- Die Aufgabe wird bereits auf `Gestartet` gesetzt (Z. 132 vor der CLI-Auflösung) — gleiches Verhalten wie bei aufgabenverknüpften Ereignissen (`NoDefault_ActivatesTaskBeforeWaitingForSelection` belegt das bereits für den Bestandspfad).
