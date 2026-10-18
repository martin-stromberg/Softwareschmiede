# Interfaces und Plugin-Basisklassen

## `IPlugin`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IPlugin.cs`

Gemeinsame Basis aller Plugins (SCM, KI, IDE).

| Methode / Property | Parameter | Rückgabewert | Zweck |
|--------------------|-----------|--------------|-------|
| `PluginName` | — | `string` | Eindeutiger Anzeigename (z. B. "GitHub", "GitHub Copilot") |
| `PluginPrefix` | — | `string` | Prefix für Credential-Store-Schlüssel und Persistenzfelder (`Aufgabe.KiPluginPrefix`, `GitRepository.PluginTyp`) |
| `GetSettingGroups()` | — | `IReadOnlyList<PluginSettingGroup>` | Einstellungsgruppen/-felder für die UI |
| `PluginType` | — | `PluginType` | Typ-Zuordnung (`SourceCodeManagement`/`DevelopmentAutomation`/`Ide`) |

**Nicht vorhanden:** ein Symbol-/Glyph-Member (für die Anforderung neu zu ergänzen; binärkompatible Erweiterung nötig, da Plugins separat kompiliert geladen werden).

## `IGitPlugin : IPlugin`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IGitPlugin.cs`

SCM-Vertrag. Relevant für die Anforderung: erweitert `IPlugin` um ~25 Member, davon mehrere mit **Default-Interface-Implementierungen** (z. B. `GetRepositoryLinkFields() => []` (Z. 15), `GetOpenPullRequestsAsync` → leere Liste (Z. 23–24), `GetPullRequestStatusAsync`/`GetPullRequestWorkflowRunsAsync`/`CheckoutPullRequestSourceAsync`/`GetRepositoryStructureAsync`/`MergeToSourceAsync` → `NotSupportedException`, `CompletePullRequestAsync` → `PullRequestCompletionResult.Failed`, `GetGitActionCapabilitiesAsync` → Defaults (Z. 137–144), `GetRepositoryStructureLoadResultAsync` → Exception-Mapping (Z. 169–188), `ResolveEffectiveRepositoryPathAsync` → `localPath` (Z. 200–201)). Das Muster "neue Member als Default-Implementierung" ist im Vertrag bereits etabliert.

## `IKiPlugin : IPlugin`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IKiPlugin.cs`

| Methode / Property | Parameter | Rückgabewert | Zweck |
|--------------------|-----------|--------------|-------|
| `StartCliAsync` | `localRepoPath`, `parameters?`, `ct` | `Task<ProcessStartInfo>` | Startet den CLI-Prozess |
| `GetProcessWindowTitle` | `aufgabeId` | `string` | Fenstertitel-Hinweis (optional) |
| `SupportsSessionContinuation()` | — | `bool` | Session-Fortsetzung möglich? |
| `CheckHealthAsync` | `ct` | `Task<bool>` | Verfügbarkeitsprüfung |
| `TerminalCapabilities` | — | `TerminalProviderCapabilities` | PTY-Unterstützung/-Bedarf |
| `GetTerminalStartSpecAsync` | `localRepoPath`, `parameters?`, `ct` | `Task<TerminalSessionStartSpec>` | Startbeschreibung für interaktives Terminal |

## `IIdePlugin : IPlugin`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IIdePlugin.cs`

IDE-Plugin-Vertrag (Visual Studio, VS Code; built-in registriert über `PluginManager.RegisterBuiltInIdePlugins`). Für die Anforderung nur indirekt relevant — ein Symbol-Member an `IPlugin` würde automatisch auch für IDE-Plugins gelten.

## `IPluginManager`

Datei: `src/Softwareschmiede/Domain/Interfaces/IPluginManager.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetSourceCodeManagementPlugins()` | — | `IReadOnlyList<IGitPlugin>` | Alle geladenen SCM-Plugins |
| `GetDevelopmentAutomationPlugins()` | — | `IReadOnlyList<IKiPlugin>` | Alle geladenen KI-Plugins |
| `GetDefaultSourceCodeManagementPlugin()` | — | `IGitPlugin` | Erstes SCM-Plugin (wirft bei leerer Liste) |
| `GetDefaultDevelopmentAutomationPlugin()` | — | `IKiPlugin` | Priorisiertes KI-Plugin |
| `GetIdePlugins()` | — | `IReadOnlyList<IIdePlugin>` | Alle IDE-Plugins |
| `GetDefaultIdePlugin()` | — | `IIdePlugin` | Erstes IDE-Plugin |

Wird von `MainWindowViewModel.ResolvePluginName` zur Prefix→Name-Auflösung genutzt; dort würde auch die Symbol-Auflösung ansetzen.

## `IAktiveAufgabenService`

Datei: `src/Softwareschmiede/Application/Services/IAktiveAufgabenService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetAktiveAufgabenAsync` | `ct` | `Task<List<Aufgabe>>` | Aktive Aufgaben für die Seitenleisten-Anzeige |

Implementiert von `AufgabeService`; im `MainWindowViewModel` injiziert (tests auch durch eigene Fakes: `VerzoegernderAktiveAufgabenService`, `ZaehlenderAktiveAufgabenService`).

## Basisklassen `GitPluginBase<TPlugin>` und `CliKiPluginBase`

Dateien: `src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/GitPluginBase.cs`, `src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/CliKiPluginBase.cs`

- `GitPluginBase<TPlugin>` (`abstract`, implementiert `IGitPlugin`, `IIssueCreateProvider`, `IIssueTemplateProvider`, `IScmAlertProvider`): `abstract` Member `PluginName`, `PluginPrefix`, `PluginType`, `GetSettingGroups`, `GetIssuesAsync`, `CloneRepositoryAsync`, `PushBranchAsync`, `PullAsync`, `CreatePullRequestAsync`, `CheckHealthAsync`, `GetRemoteBranchesAsync`, `GetDefaultBranchAsync`; **zahlreiche `virtual`-Member mit Defaults** (u. a. `GetRepositoryLinkFields`, `GetOpenPullRequestsAsync`, `GetAlertsAsync`, `CreateIssueAsync`, `GetIssueTemplatesAsync`, `GetPullRequestStatusAsync`, `CompletePullRequestAsync`, `GetGitActionCapabilitiesAsync`, `MergeToSourceAsync`, `GetRepositoryStructureAsync`, `ResolveEffectiveRepositoryPathAsync`, `CreateBranchAsync`, `CommitAsync`, `ResetAsync`, `CheckoutRemoteBranchAsync`, `CheckoutPullRequestSourceAsync`, `GetAvailableRepositoriesAsync`). Konstruktor nimmt `ICliRunner`. Geschützte Helfer: `RunGitAsync`, `AddAllAsync`, `EnsureGitRepositoryAsync`, `CheckoutPullRequestSourceCoreAsync`.
- `CliKiPluginBase` (`abstract`, implementiert `IKiPlugin`): `abstract` Member `ProviderDateiPraefix`, `PluginName`, `PluginPrefix`, `PluginType`, `GetSettingGroups`, `BuildProcessStartInfo`, `SupportsSessionContinuation`, `CheckHealthAsync`; `virtual`-Member `TerminalCapabilities` (Default `SupportsPty`), `PromptSubmitDelay` (Default `TimeSpan.Zero`), `GetProcessWindowTitle` (Default `string.Empty`), `GetCliHelpTextAsync`; konkrete Implementierungen `StartCliAsync`, `GetTerminalStartSpecAsync`, `BuildTerminalStartSpec`. Geschützte Helfer: `RunHelpCommandAsync`, `ResolveExecutablePath`, `AppendCommandLineParameters`, `CheckHealthWithVersionCommandAsync`, `BuildIssueTemplateFillPrompt`, `RunOneShotTextGenerationAsync`, `DiscoverAgents`.

Beide Basisklassen leben in `Softwareschmiede.Plugin.Contracts` und werden von allen Repo-Plugins genutzt — ein `virtual` Symbol-Property mit Fallback-Glyph wäre hier die binärkompatible Erweiterungsstelle.
