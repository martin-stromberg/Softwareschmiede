# Interfaces

## `IRunningAutomationStatusSource`
Datei: `src/Softwareschmiede/Domain/Interfaces/IRunningAutomationStatusSource.cs` (impl. `KiAusfuehrungsService`)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `RunningCountChanged` (Event) | `(int alt, int neu)` | – | Zählungsänderung; für Schritt-3-Trigger relevant, hier nur Bestand. |
| `GetRunningCount` | – | `int` | Laufende Automatisierungen. |
| `IsRunning` | `Guid aufgabeId` | `bool` | Einzelabfrage. |
| `GetLiveLaufStatus` | `Guid aufgabeId` | `AufgabeLaufStatus?` | Live-Substatus für UI-Klassifikation. |

→ Für die Pro-Plugin-Attribution nicht ausreichend (kein Zugriff auf Aufgaben-IDs/Sessions); `KiAusfuehrungsService` liefert `GetLaufendeAufgabenIds` + `GetScmPromptSession` öffentlich konkret.

## `IScmPromptSessionAdapter`
Datei: `src/Softwareschmiede/Application/Services/ScmPromptSessionAdapter.cs` (oberer Teil)

| Member | Typ | Zweck |
|--------|-----|-------|
| `PluginPrefix` | `string` | **Tatsächlich gestartetes** Plugin der lebenden SCM-Sitzung (Attribution vor `Aufgabe.KiPluginPrefix`, sobald `IsAlive`). |
| `IsAlive` | `bool` | Sitzung lebt (Handle + Prozess). |
| `CliRunId` | `Guid` | Laufkorrelation. |
| `CliRecognition`, `IsBusy`, `ReadyStatus`, `WorkingDirectory` | diverse | Zustellungsentscheidungen. |
| `WaitForInputReadyAsync`, `CheckExchangeAccessAsync`, `WritePromptAsync`, `ConfirmReady`, `MarkInputStarted` | Methoden | Zustellung. |

## `IPluginManager`
`GetSourceCodeManagementPlugins()`, `GetDevelopmentAutomationPlugins()`, `GetIdePlugins()`, Default-Resolver — Plugin-Listen für Limits/Einstellungen.

## `IKiPlugin` (Plugin-Vertrag)
`PluginPrefix`, `PluginName`, `CheckHealthAsync`, `StartCliAsync`, `GetTerminalStartSpecAsync`, `GetSettingGroups` — plugindeklarierte Einstellungsgruppen (persistieren via `PluginSettingsService`/`ICredentialStore`, **nicht** `AppEinstellung`).

## `IScmEventSource`/`IScmEventResponder`/`IScmNotificationSource`
SCM-Plugin-Fähigkeiten (Beobachtung, Antworten, Inbox) — vom Limit unberührt; Hostaktionen laufen unbegrenzt.

## `ScmEventChangeNotifier`
`Notify()` — UI-/Wiederholungs-Signal; unverändert.

## Bemerkung Testbarkeit
`KiAusfuehrungsService` ist eine konkrete Singleton-Klasse ohne Interface für Handle-/Session-Zugriff. Für deterministische Unit-Tests der Limit-Entscheidung empfiehlt sich die Trennung: Belegung ermitteln (Service-Methode) vs. Entscheidung bewerten (statisch/pur) — Alternativ eine schmale Abstraktion (`GetLaufendeAufgabenIds` + `GetScmPromptSession`) hinter einem Interface kapseln.
