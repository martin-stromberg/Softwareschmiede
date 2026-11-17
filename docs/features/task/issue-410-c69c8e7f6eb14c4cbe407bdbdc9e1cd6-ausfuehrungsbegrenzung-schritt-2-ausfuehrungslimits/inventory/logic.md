# Logik (Ist-Stand)

## `ScmEventMonitoringService`
Datei: `src/Softwareschmiede/Application/Services/ScmEventMonitoringService.cs` — Singleton `PeriodicBackgroundService` (`ScmEventOptions.PollInterval`).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `RunOnceAsync` | public override | Beobachtungs-/Enumerations-/Inbox-Phase; Dispatch-Phase Z. 229-248: `upperSequence = Max(Sequence)`, Batches à 100 Ereignisse (`Status != "Abgebrochen"` und [`Status != "Verarbeitet"` und fällig] ODER [offene Outbox-Aktion fällig]), `OrderBy(Sequence)`, je Item neuer Scope → `ScmEventOrchestrator.DispatchAsync`. |
| `EnumerateNotificationsAsync` | private | Inbox des separaten Kontos → abgeleitete Referenzen (unverändert). |

Abonniert: — (läuft zeitgesteuert). Publiziert: `ScmEventChangeNotifier.Notify()` indirekt.

## `ScmEventOrchestrator`
Datei: `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs` — scoped.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `DispatchAsync` | public | Aufgabenlease (`ScmTaskCoordinator.Shared`) → `DispatchCoreAsync`. Einstieg für Poll und `CheckStatusCommand`. |
| `OpenFromQueueAsync` | public | UI-Öffnen mit Plugin-/Sitzungsauswahl; Claim+Renewal; `DeliveryAttemptId != null` → Core (Reconcile); sonst BlockReason/Status-Setzung, `select(...)` für Prefix, `NextAttemptUtc = null` → Core mit explizitem Prefix. |
| `DispatchCoreAsync` | private | Z. 74-281. Startprüfung relevante Reihenfolge: Abgebrochen-Return → Plugin/Responder → Hostaktionen (`receipt`, „waiting"/„attachments"-Outbox) → `Verarbeitet`-Return → `DeliveryAttemptId`-Reconcile + Return → `NextAttemptUtc`-Guard → älteres-offenes-Ereignis-Sperre (`WaitWithCommentAsync`) → Aufgabe laden → `BlockReason` → try{ Remote-Validierung (`GetEventRepositoryAsync`, Schreibrecht, Trigger-Revalidierung, `BereitsReagiert`) → `Neu→Gestartet` → `GetScmPromptSession` → Prefix-Konflikt-/Terminal-Checks → **`adapter is null || !IsAlive`: Prefix-Auflösung (explizit `explicitPluginPrefix` oder `ResolveEnabledDevelopmentAutomationPluginWithProjectScopeAsync`) → Plugin-Wartegründe (Schritt 1) → `ResolveDevelopmentAutomationPluginStrictAsync` → `CheckHealthAsync` → CLI-Start via `EntwicklungsprozessService`** → Session-Freshness → `BlockReason`-Recheck → `WaitForInputReadyAsync` → `CheckExchangeAccessAsync` → `exchange.PrepareAsync` → Zustellung } catch ScmProviderException → Retry/Sperre; catch InvalidOperationException/IOException/UnauthorizedAccess → `NextAttemptUtc = long.MaxValue` + Wartegrund. |
| `ReconcileAttemptAsync` | private | Nur begonnene Versuche (Exchange-Kanal lesen, Aktionen abgleichen, Abschluss erkennen, Attachments-Outbox, PR-Verknüpfung). **Kein neuer CLI-Start** → unbegrenzt. |
| `RetryAsync`, `ConfirmReadyAsync`, `ResendAsync`, `CancelAsync`, `ActivateForOpenAsync` | public | Mutations-Einstiege (UI): `MutateAndDispatchAsync` → `DispatchCoreAsync` (UI-ausgelöste Starts laufen durch denselben Kern). |
| `BlockReason` | private static | `Status Beendet/Archiviert` → „Aufgabe beendet/archiviert; Start gesperrt."; `PausiertBisUtc > UtcNow` → „Aufgabe pausiert; Start gesperrt.". |
| `WaitAsync`/`WaitWithCommentAsync` | private | `Status="Wartend"`+`Reason`; WithComment legt deterministische „waiting"-Outbox-Aktion an und führt sie aus (unbegrenzt). |
| `EnsureHostActionAsync` | private | Deterministische Host-Outbox-Aktion (Kollisionserkennung). |

## `KiAusfuehrungsService`
Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs` — Singleton, `IRunningAutomationStatusSource`.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetRunningCount` | public | Anzahl Handles mit `!Process.HasExited`. |
| `GetLaufendeAufgabenIds` | public | Aufgaben-IDs mit laufendem Handle (ein Handle je Aufgabe). |
| `IsRunning`, `GetRunningProcess`, `GetLiveLaufStatus`, `NotifyLaufAktiv` | public | Einzelabfragen. |
| `StartCliAsync`/`StartTerminalSessionAsync` | public | Prozessstart über `ScmTaskCoordinator` + `_startLock`; `_handles` wird vor Methodenende befüllt → sequenzielle Dispatches sehen neue Läufe sofort. Bei Terminal-Session entsteht `ScmPromptSessionAdapter` (mit `kiPlugin.PluginPrefix`). |
| `GetTerminalSession`/`GetScmPromptSession` | public | Sitzungszugriff; Adapter liefert `PluginPrefix`, `IsAlive`, `CliRunId`, `CliRecognition`, `IsBusy`. |

Publiziert: `CliProcessStatusChanged`, `CliLaufAktiv`, `RunningCountChanged`.

## `PluginSelectionService`
Datei: `src/Softwareschmiede/Application/Services/PluginSelectionService.cs` — scoped.

| Methode | Kurzbeschreibung |
|---------|------------------|
| `ResolveEnabledDevelopmentAutomationPluginWithProjectScopeAsync(aufgabenPluginPrefix, projektId, ct)` | Schritt-1-Auflösung: Aufgaben-Prefix → Projekt-Default → Global-Default → `ki.plugin.default`; nur aktivierte/installierte Prefixe; Ergebnis `KiPluginResolution(PluginPrefix?, IgnoredPluginPrefixes)`. |
| `ResolveDevelopmentAutomationPluginStrictAsync(prefix)` | Streng: wirft `InvalidOperationException` bei deaktiviert/nicht installiert (explizite Benutzerwahl). |
| `ResolveDevelopmentAutomationPluginWithProjectScopeAsync` | Ältere Variante ohne Aktivierungsprüfung (nicht im SCM-Pfad). |

## `AppEinstellungService`
`GetSettingAsync`/`GetIntSettingAsync`/`GetBoolSettingAsync`/`GetSettingsAsync(keys)` (Batch), `SetSettingAsync`/`SetIntSettingAsync`/`SetBoolSettingAsync`. Kein Parallel-Limit vorhanden.

## `KiPluginLimitService` (Referenz/Kollision)
`SessionLimitKeyPrefix = "plugins.sessionlimit."` → persistiert Rate-Limit-Reset-`DateTimeOffset` je Prefix + pausiert betroffene Aufgaben. **Schlüsselfamilie für Parallel-Limits nicht verwendbar.**

## `ScmEventStore` (relevante Teile)
`AcceptBatchAsync`: Delay-Direktive → `Status="Verzögert"` + `NextAttemptUtc` + `Reason` + „waiting"-Outbox-Kommentar + `PauseForDelayAsync` (Aufgabenpause bis Ziel, Max-Semantik).

## `ScmDelayDirective`
`#verzögert`-Parser; `Status="Verzögert"` ist seine einzige Quelle → Klasse-2-Markierung.

## `ScmTaskCoordinator`/`ScmTaskLeases`
Serialisierung pro Aufgabe; unverändert. `LaufendeAutomatisierungenZaehlung` zählt fachlich laufende Automatisierungen für Auto-Shutdown — getrennter Zweck, unverändert.
