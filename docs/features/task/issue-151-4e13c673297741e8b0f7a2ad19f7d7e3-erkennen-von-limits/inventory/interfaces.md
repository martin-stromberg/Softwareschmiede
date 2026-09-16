# Interfaces

## `IPlugin`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IPlugin.cs`

| Methode / Property | Parameter | Rückgabewert | Zweck |
|--------------------|-----------|--------------|-------|
| `PluginName` | — | `string` | Anzeigename des Plugins |
| `PluginPrefix` | — | `string` | Eindeutiger Prefix; Basis für Credential-Store-Schlüssel `<PluginPrefix>.<FieldKey>` — und für `Aufgabe.KiPluginPrefix` |
| `GetSettingGroups` | — | `IReadOnlyList<PluginSettingGroup>` | Konfigurierbare Einstellungsgruppen |
| `PluginType` | — | `PluginType` | Typ für `PluginManager`-Registrierung |

## `IKiPlugin`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IKiPlugin.cs`

Erweitert `IPlugin`.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `StartCliAsync` | `localRepoPath`, `parameters?`, `ct` | `Task<ProcessStartInfo>` | Liefert `ProcessStartInfo` für den CLI-Prozess (Parameter z. B. Session-ID für `--continue`) |
| `GetProcessWindowTitle` | `aufgabeId` | `string` | Hinweis auf erwarteten Fenstertitel (optional) |
| `SupportsSessionContinuation` | — | `bool` | Ob das Plugin Session-Fortsetzung unterstützt (`KiSimulatorPlugin`: `false`) |
| `CheckHealthAsync` | `ct` | `Task<bool>` | Verfügbarkeitsprüfung |

## `IPluginManager`
Datei: `src/Softwareschmiede/Domain/Interfaces/IPluginManager.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetSourceCodeManagementPlugins` | — | `IReadOnlyList<IGitPlugin>` | Alle geladenen SCM-Plugins |
| `GetDevelopmentAutomationPlugins` | — | `IReadOnlyList<IKiPlugin>` | Alle geladenen KI-Plugins |
| `GetDefaultSourceCodeManagementPlugin` | — | `IGitPlugin` | Erstes SCM-Plugin |
| `GetDefaultDevelopmentAutomationPlugin` | — | `IKiPlugin` | Priorisiertes KI-Plugin (`copilot`-Präfix zuerst) |
| `GetIdePlugins` | — | `IReadOnlyList<IIdePlugin>` | Alle IDE-Plugins |
| `GetDefaultIdePlugin` | — | `IIdePlugin` | Erstes IDE-Plugin |

Implementiert von `PluginManager` (`src/Softwareschmiede/Infrastructure/Plugins/PluginManager.cs`).

## `IAktiveAufgabenService`
Datei: `src/Softwareschmiede/Application/Services/IAktiveAufgabenService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetAktiveAufgabenAsync` | `ct` | `Task<List<Aufgabe>>` | Aktive Aufgaben für die Seitenleisten-Anzeige |

Implementiert von `AufgabeService` (nutzt `IstAktivOderWartendPredicate`); konsumiert von `MainWindowViewModel` (5-s-Refresh) und indirekt vom `DashboardViewModel` (Zählung via `GetAktiveUndWartendeCountAsync`).

## `IRunningAutomationStatusSource`
Datei: `src/Softwareschmiede/Domain/Interfaces/IRunningAutomationStatusSource.cs`

| Methode / Event | Parameter | Rückgabewert | Zweck |
|-----------------|-----------|--------------|-------|
| `RunningCountChanged` | `Action<int,int>` (alt, neu) | Event | Änderung der Anzahl laufender Automatisierungen |
| `GetRunningCount` | — | `int` | Anzahl laufender Automatisierungen |
| `IsRunning` | `aufgabeId` | `bool` | Läuft aktuell eine Automatisierung für die Aufgabe |

Implementiert von `KiAusfuehrungsService`; verwendet von `AufgabeRecoveryService.ScanForRecoveryCandidatesAsync` zum Ausschluss tatsächlich laufender Prozesse.

## `ICliUpdateSafetyService`
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateInterfaces.cs` (Z. 34)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `CheckAsync` | `ct` | `Task<CliUpdateSafetyResult>` | Liefert riskante aktive CLI-Aufgaben vor einem Programmupdate |

Implementiert von `CliUpdateSafetyService` (Filter via `AufgabeLaufAktivitaet.IstAktiv`).

## `ITerminalOutputSink`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/ITerminalOutputSink.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `OnOutputChunk` | `ReadOnlySpan<byte>` | `void` | Roh-Chunk der Terminal-Ausgabe; Bytes sofort kopieren |
| `Complete` | — | `void` | Idempotenter Abschluss, Flush der Restdaten |
| `CompleteAsync` | `timeout`, `ct` | `Task` | Abschluss mit begrenztem Drain der Persistenz |

Implementiert von `CliOutputProtokollWriter` — die Senke, über die CLI-Ausgabezeilen (und damit der Rate-Limit-Marker) in `ProtokollService.AddCliOutputAsync` landen.
