# Logik: Autonome Aufgaben vs. gemeinsam genutzte Dienste

## Ausschließlich autonom (würden entfernt)

## `AutonomAufgabenInitialisierungsService`
Datei: `src/Softwareschmiede/Application/Services/AutonomAufgabenInitialisierungsService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `InitialisiereAsync(Aufgabe, AutonomAufgabeInitialisierungsAnfrage, ct)` | public | Prüft Feature-Flag, legt Arbeitsverzeichnis, Klon, Projektbranch, `state.json`, `permissions.json` und `AutonomAufgabeKonfiguration` an |
| `ErstelleProjektbranchAsync` | private/intern | Legt Projektbranch über `IGitPlugin` an (Tests: `ErstelleProjektbranchAsync_*`) |
| `ErstelleArbeitsverzeichnisStrukturAsync` | intern | Verzeichnisstruktur (`plan.md`, `progress.md`, `governance.md`, `skills/`, `clones/`, `tasks/`, `logs/`) |
| `BuildPermissionsJson`, `BuildStateJson`, `ValidiereAnfrage` | private | JSON-Erzeugung und Validierung (Token-Budget 1 … 5 000 000, Laufzeit 60 … 1 440 Minuten) |

Abhängigkeiten: `AppEinstellungService` (Feature-Flag), `UnteragentGitProvisioningService`, `GitBranchHelper`, `AutonomAufgabenOptions`.

## `ProjektleiterAgentService`
Datei: `src/Softwareschmiede/Application/Services/ProjektleiterAgentService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `StarteAgentAsync(konfiguration, resumePrompt?, ct)` | public | Startet Projektleiter-CLI über `KiAusfuehrungsService`, sendet verzögert Initialprompt |
| `StarteAgenNachAppNeustartAsync(aufgabeId, resumePrompt)` | public | Wird aus `App.xaml.cs` beim Start aufgerufen; respektiert `ExplizitGestoppt` |
| `StoppeAgenExplizitAsync(aufgabeId)` | public | Setzt `ExplizitGestoppt` |
| `SteuereUnteragentAsync(unteragent, ct)` | public | Validiert Scope/Branch/Pfade, provisioniert Unteragent-Klon |
| `IntegriereErgebnisseAsync(konfiguration, unteragent, ct)` | public | Schreibt Ergebnisse in `plan.md`/`progress.md` |
| `BuildDefaultProjektleiterSkill` | private static | Erzeugt Standard-Skill-Text |
| `ValidiereUnteragent` | private static | Eingabevalidierung |

Abhängigkeiten: `UnteragentGovernanceService`, `UnteragentGitProvisioningService`, `AppEinstellungService`, `AutonomAufgabenOptions`, `StateJsonHelper`, `KiAusfuehrungsService`.

## `UnteragentGitProvisioningService`
Datei: `src/Softwareschmiede/Application/Services/UnteragentGitProvisioningService.cs`
- `ProvisioniereAsync(unteragent, repoMainPfad, ct)`: legt Verzeichnis, Branch (über `GitBranchHelper`) und Klon an.
- Aufrufer: `AutonomAufgabenInitialisierungsService`, `ProjektleiterAgentService`, `App.xaml.cs` (DI).

## `GitBranchHelper` (intern)
Datei: `src/Softwareschmiede/Application/Services/GitBranchHelper.cs`
- `ErstelleLokalenBranchAsync(...)`: führt `git branch` ohne Checkout aus. Dokumentiert ausdrücklich nur für Unteragenten-Branches.
- Einziger Aufrufer: `UnteragentGitProvisioningService` (per Grep verifiziert). Ausschließlich autonom.

## `StateJsonHelper` (intern)
Datei: `src/Softwareschmiede/Application/Services/StateJsonHelper.cs`
- `LeseAsync`, `SchreibeAsync` für `state.json` im Arbeitsverzeichnis.
- Aufrufer: `ProjektleiterAgentService`, `SessionManagementService`. Ausschließlich autonom.

## `SessionManagementService`
Datei: `src/Softwareschmiede/Application/Services/SessionManagementService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `PauseAufgabeBeiBudgetLimitAsync` | public | Setzt `AutonomAufgabeKonfiguration.SessionPauseUtc` (Token-Budget) und aktualisiert `state.json` |
| `SetzeFortAsync` | public | Hebt Session-Pause auf, sendet Weitermachen-Prompt |
| `PruefeAusfuehrungAsync` | public | Erkennt Unterbrechung anhand Heartbeat |

Aufrufer: `AutonomAufgabeDetailViewModel` (über `TaskDetailViewModel` und `AutonomAufgabeStartService`). Ausschließlich autonom (Budget-Session-Pause). Nicht zu verwechseln mit `Aufgabe.PausiertBisUtc`.

## `UnteragentGovernanceService`
Datei: `src/Softwareschmiede/Application/Services/UnteragentGovernanceService.cs`
- `VerifiziereBerechtigung(unteragent, aktion, zielPfad)`: prüft Arbeitsbereich und verbotene Aktionen.
- `ValidiereFehlerBedingungAsync(unteragent, ct)`: prüft Token- und Laufzeitlimit, wirft `UnteragentAbbruchException`.
- Aufrufer: `ProjektleiterAgentService`, `UnteragentGovernanceMonitoringService`. Ausschließlich autonom.

## `UnteragentGovernanceMonitoringService`
Datei: `src/Softwareschmiede/Application/Services/UnteragentGovernanceMonitoringService.cs`
- `PeriodicBackgroundService`, prüft aktive Unteragenten (`AufgabeAusfuehrungsStatus.Aktiv`, `SessionPauseUtc == null`).
- Registriert in `src/Softwareschmiede.App/App.xaml.cs` (Z. ~334–335) als Hosted Service. Ausschließlich autonom.

## `AutonomAufgabeStartService` (App)
Datei: `src/Softwareschmiede.App/Services/AutonomAufgabeStartService.cs`
- `StarteAsync(aufgabe, ct)`: öffnet Initialisierungsdialog, liefert `AutonomAufgabeStartResult`. Aufrufer: `TaskDetailViewModel.AutonomAufgabeInitialisierenAsync`. Ausschließlich autonom.

## `AutonomAufgabeDetailViewModel` (App)
Datei: `src/Softwareschmiede.App/ViewModels/AutonomAufgabeDetailViewModel.cs`
- Befehle `StartCommand`, `StopCommand`, `ResumeCommand`; lädt `plan.md`, `progress.md`, `governance.md`. Erstellt in `TaskDetailViewModel.StelleAutonomAufgabeDetailViewModelWiederHerAsync` (Z. ~1636). Ausschließlich autonom.

## `AutonomAufgabeInitialisierungsDialogViewModel` (App)
Datei: `src/Softwareschmiede.App/ViewModels/AutonomAufgabeInitialisierungsDialogViewModel.cs`
- Dialog-Logik für Initialisierung (Branch-Auswahl, Vorlagen, Ressourcenlimits). Ausschließlich autonom.

## Gemeinsam genutzt (bleiben erhalten, aber bereinigen)

## `AufgabeService`
Datei: `src/Softwareschmiede/Application/Services/AufgabeService.cs` (1119 Zeilen)

| Methode / Stelle | Gemeinsam? | Autonomer Anteil |
|------------------|-----------|------------------|
| `GetDetailAsync` (Z. ~102–103) | Ja | `.Include(a => a.AutonomKonfiguration)` nur für `IstAutonom()` in der UI; Include entfällt |
| `SetPauseAsync` (Z. ~836–850) | Ja (Pausen-Mechanismus) | Prüfung `if (aufgabe.IstAutonom()) throw ...` entfällt |
| `SetPauseAsync` Z. ~842 `.Include(a => a.AutonomKonfiguration)` und Z. ~846 `IstAutonom()`-Prüfung | Ja | Include und Prüfung entfallen zusammen |
| `AbschliessenAsync`, Protokollierung | Ja | unverändert |

Weitere Pausen-/Start-Methoden (`StartenAsync`, `GetAktiveUndWartendeCountAsync`) sind gemeinsam.

## `AufgabeRecoveryService`
Datei: `src/Softwareschmiede/Application/Services/AufgabeRecoveryService.cs` (239 Zeilen)
- `RecoverManuellAsync` (Z. ~72–84): `.Include(a => a.AutonomKonfiguration)` und `IstRecoveryStatus(..., aufgabe.IstAutonom())`.
- Kandidatensuche (Z. ~54): `&& a.AutonomKonfiguration == null` (Filter, Recovery nur für reguläre Aufgaben).
- Gemeinsam: Heartbeat-Recovery, Pausen-Prüfung (`PausiertBisUtc`), `RecoverManuellAsync` für regulär.
- Aufrufer: `DashboardViewModel` (App), `App.xaml.cs` (DI). Die Recovery-Logik selbst ist gemeinsam.

## `KiPluginLimitService`
Datei: `src/Softwareschmiede/Application/Services/KiPluginLimitService.cs` (188 Zeilen)
- `VerarbeiteRateLimitAsync(aufgabeId, resetUtc, ct)`: persistiert Session-Limit je Plugin, pausiert via `PausiertBisUtc` aktive Aufgaben.
- Kandidatenfilter (Z. ~85): `&& a.AutonomKonfiguration == null`. Autonomer Anteil: nur dieser Filter.
- Aufrufer: `CliOutputProtokollWriter` (Z. ~221), `CliUpdateSafetyService`. **Gemeinsam und für ereignisgetriebene Bearbeitung relevant** (Plugin-Limit).
- Tests: `KiPluginLimitServiceTests` (8 Tests, keine autonomen Verweise).

## `AppEinstellungService`
Datei: `src/Softwareschmiede/Application/Services/AppEinstellungService.cs`
- Autonom: `AutonomAufgabenEnabledKey = "autonomeaufgaben.enabled"` (Z. ~43), `GetAutonomAufgabenEnabledAsync(bool deploymentDefault, ct)` (Z. ~179–182).
- Gemeinsam: `GetScmMonitoringEnabledAsync`, `GetBoolSettingAsync`, `SetSettingAsync`, Plugin-Einstellungen. **Bleibt erhalten.**
- Entfernung: nur Schlüssel und Methode.

## `TaskDetailViewModel` (App)
Datei: `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs` (rund 1770 Zeilen)
- Gemeinsam: Starten, Pausieren (`AufgabePausierenDialog`), CLI-Anzeige, Protokoll, ScmEvent-Ansicht.
- Autonom: Felder `_autonomAufgabeStartService`, `_autonomAufgabenOptions`, `_autonomAufgabeDetailViewModel`, `_isAutonomAufgabenEnabled`; Properties `IsAutonomAufgabe`, `CanAutonomAufgabeInitialisieren`, `ShowAutonomAufgabeRibbonGruppe`, `ShowAutomatisierungPanel`, `IsAutonomAufgabenEnabled`, `AutonomAufgabeDetailViewModel`; Commands `AutonomAufgabeInitialisierenCommand`; Methoden `AutonomAufgabeInitialisierenAsync`, `AktualisiereIsAutonomAufgabenEnabledAsync`, `StelleAutonomAufgabeDetailViewModelWiederHerAsync`, `SetzeAutonomAufgabeDetailViewAsync`.
- Mehrere Prädikate verwenden `!_aufgabe.IstAutonom()` (Z. ~251, 488, 578, 756). Bei Entfernung ist der Ausdruck zu vereinfachen, nicht zu löschen.

## `App.xaml.cs` (Startup und DI)
Datei: `src/Softwareschmiede.App/App.xaml.cs`
- Autonom: Startup-Recovery für `AutonomAufgabeKonfigurationen` (Z. ~114–136), `ErstelleResumePromptNachAppNeustart` (Z. ~158), `services.Configure<AutonomAufgabenOptions>` (Z. ~234), Registrierungen Z. 241–245, 299, 334–335, 397.
- Gemeinsam (bleibt): `SessionManagementService` ist ausschließlich autonom (Registrierung Z. 244 entfällt); `KiPluginLimitService` (Z. 285) und `AufgabeRecoveryService` (Z. 290) bleiben.

## `SettingsViewModel` (App)
Datei: `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs`
- Autonom: `IsAutonomAufgabenEnabled` (Z. ~224–228), Ladevorgang (Z. ~438), Speichern (Z. ~513–514), `IOptions<AutonomAufgabenOptions>` (Z. ~29, 343, 355).
- Gemeinsam: alle übrigen Einstellungen (SCM-Monitoring, Plugins usw.).
- Hinweis: Der Abschnitt „Automatisierung“ in `SettingsView.xaml` (Z. ~284–286) enthält nur diese Checkbox (siehe offene Frage 5 der Anforderung).

## Ereignisgetriebene Bearbeitung (ohne Abhängigkeit zum autonomen Feature)

Die folgenden Dienste enthalten **keine** Verweise auf `AutonomKonfiguration`, `IstAutonom`, `AutonomAufgabe*`, `ProjektleiterAgent`, `UnteragentGovernance` (per Grep über `src/` verifiziert). Sie bleiben vollständig erhalten:
- `ScmEventMonitoringService`, `ScmEventOrchestrator`, `ScmEventStore`, `ScmEventOutbox`, `ScmEventExchangeService`, `ScmEventExchangeProtocol`, `ScmEventValidationService`, `ScmEventTaskResolver`, `ScmPromptAppendix`, `ScmPromptSessionAdapter`, `ScmRestrictedProcessAccess`, `ScmCliAccessEvaluator`, `ScmCliWorkspaceTrust`
- `ScmTaskCompletionMonitoringService`, `ScmBackmergeService`
- `EntwicklungsprozessService` (einfacher Aufgabenstart, Z. ~958–961 prüft `PausiertBisUtc`)
- `GitOrchestrationService` (`BasisBranchName`)
- `PromptZeitVersandService` (`PausiertBisUtc`)

Ergebnis der Abgrenzung: Die einzigen Kopplungspunkte zwischen autonomem Feature und gemeinsamen Pfaden sind
1. die Navigation `Aufgabe.AutonomKonfiguration` und `IstAutonom()` (Pause, Recovery, UI-Prädikate, `AufgabeService.GetDetailAsync`),
2. der Filter `a.AutonomKonfiguration == null` in `AufgabeRecoveryService` und `KiPluginLimitService`,
3. `App.xaml.cs` (Startup-Recovery, DI),
4. der Feature-Flag-Pfad in `AppEinstellungService` und `SettingsViewModel`.

Der Pausen-Mechanismus (`PausiertBisUtc`) ist **nicht** autonom und wird von der ereignisgetriebenen Bearbeitung und dem Session-Limit benötigt. Die Anforderung führt ihn unter „Pausen-Mechanismus“ beim autonomen Feature; dieser Teil ist daher zu erhalten.
