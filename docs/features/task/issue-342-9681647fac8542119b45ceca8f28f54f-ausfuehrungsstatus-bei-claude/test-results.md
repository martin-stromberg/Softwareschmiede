# Test-Ergebnisse

## Ergebnis

**Status:** Fehler vorhanden

Hinweis (Iteration 3): Alle **ausgeführten** Tests sind fehlerfrei bestanden (0 Fehlschläge in 2.561 Tests, siehe Zusammenfassung). Der Status `Fehler vorhanden` resultiert ausschließlich daraus, dass (a) der geplante Pflicht-E2E-Test in dieser Sandbox nicht ausgeführt wurde (ConPTY/WPF-E2E sind hier bestätigt nicht lauffähig → `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` / `SOFTWARESCHMIEDE_SKIP_WPF_E2E_TESTS=1` → `Skipped`) und (b) die reale End-to-End-Abnahme der UI-Szenarien nicht durchführbar war. Nach den Regeln des `/run-tests`-Kommandos gelten nicht ausgeführte geplante E2E-Szenarien und unaufgelöste `Nicht durchführbar`-Abnahmen nicht als Erfolg. Die Entscheidung (manuelle Abnahme durch Anwender oder dokumentierter Verzicht) steht aus.

**Ausgeführte Kommandos (alle synchron, voller Build vorher):**

- `dotnet build Softwareschmiede.slnx -c Debug` → erfolgreich, 0 Warnungen, 0 Fehler
- `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface" --collect:"XPlat Code Coverage"` → 2224 bestanden, 1 übersprungen, 0 fehlgeschlagen
- `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 SOFTWARESCHMIEDE_SKIP_WPF_E2E_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category=OsInterface"` → 51 bestanden, 19 übersprungen, 0 fehlgeschlagen
- `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests` → 266 bestanden, 0 fehlgeschlagen
- `dotnet format Softwareschmiede.slnx --verify-no-changes` → sauber (Exit 0, keine Änderungen nötig)

**Verifikation der Iteration-3-Concurrency-Fixes (ungecommitted, selbstständig geprüft):**

- `AufgabeService.LaufGates` (`ConcurrentDictionary<Guid, SemaphoreSlim>`, `AufgabeService.cs:33`) serialisiert alle Laufdaten-Schreibpfade (`StartenAsync` ~Z. 779, `AbschliessenAsync` ~Z. 899, `StartZuruecksetzenAsync` ~Z. 960, `AktivenLaufSetzenAsync` ~Z. 1001, `AktivenLaufBeendenAsync` ~Z. 1046, `AktivenLaufSicherstellenAsync` ~Z. 1102).
- `AktivenLaufSicherstellenAsync` mit `Func<AufgabeLaufStatus?>? laufStatusProvider` + `Func<bool>? laeuftGuard` (beide innerhalb der Sperre ausgewertet, `AufgabeService.cs:1100–1114`).
- `_disposed`-Flag + `_lifecycleLock` im `CliProcessManager` (`CliProcessManager.cs:39–47`, gesetzt in `Dispose` Z. 463–467, geprüft in allen Timer-/Subscription-Pfaden).
- `UpdateHeartbeatAsync`, `AusfuehrungAktivSetzenAsync`, `AktualisiereLaufStatusAsync` produktiv entfernt (verbleibende Nennungen nur in erklärenden Test-/Dok-Kommentaren — Querprüfung per Volltextsuche).
- Neuer Helper `CliRuntimeStatusExtensions` (`src/Softwareschmiede/Infrastructure/Terminal/CliRuntimeStatusExtensions.cs`, `ToAufgabeLaufStatus`-Mapping) vorhanden.
- Neue Iteration-3-Tests alle bestanden: `LaufdatenSchreibpfade_SerialisierenProAufgabe`, `AktivenLaufSicherstellenAsync_WertetLaufStatusProvider_InDerSperreAus`, `AktivenLaufSicherstellenAsync_SchreibtNichts_WennLaeuftGuardNichtMehrLaeuft`, `Dispose_VerhindertNeuanlageVonTimerUndSubscription`.

## Fehlgeschlagene Tests

Kein einziger ausgeführter Test ist fehlgeschlagen. Die folgenden Punkte werden dennoch — den Kommandoregeln entsprechend — hier geführt, weil geplante Nachweise offen sind:

### E2E / Reale Abnahme (offen, Anwender-Entscheidung erforderlich)

- **`End2EndTest.ConPty_WeitereLebenszyklusSzenarien_E2E` (enthält den Pflicht-E2E `SeitenleistenKachel_AktualisiertStatusAutomatisch_OhneManuellesNeuladen_E2E`)** — Nicht ausgeführt: `Skipped` via `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` (bestätigte Sandbox-Limitation: ConPTY-Kindprozess wird in dieser Agent-Sandbox nicht an die Pseudo-Konsole isoliert; der Test läuft regulär in Visual Studio / interaktiver Desktop-Session, siehe CLAUDE.md und `plan.md` Abschnitt „E2E-Tests – Hinweis zur Ausführbarkeit").
- **Reale End-to-End-Abnahme der Akzeptanzszenarien 1–6 (siehe Abschnitt „Reale End-to-End-Abnahme")** — `Nicht durchführbar` in dieser Sandbox; erfordert manuelle Abnahme durch den Anwender oder dokumentierten Verzicht.

## E2E-Abdeckung

| Szenario | Test / Testklasse | Ergebnis |
|----------|-------------------|----------|
| Pflicht (plan.md): Start → Kachel „▶ Läuft" synchron zur Fußzeile; Session-Wiederanbindung (Detailansicht verlassen/erneut öffnen) → beide Anzeigen synchron; Stopp → „✓ Bereit" (Akzeptanzszenarien 1, 2, 6) | `SeitenleistenKachel_AktualisiertStatusAutomatisch_OhneManuellesNeuladen_E2E` (`src/Softwareschmiede.Tests/E2E/E2E_ArbeitsstatusAktualisierung.cs`), konsolidiert in `ConPty_WeitereLebenszyklusSzenarien_E2E` (`MainTest.cs`) | **Nicht ausgeführt** (Skipped: `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` — Sandbox-Limitation) |
| Optional (plan.md): Divergenz-Nachspiel — Laufdaten per DB-Zugriff während des Laufs entfernt (F1/H4-Bild) → Anzeige bleibt synchron / Selbstheilung stellt Laufdaten wieder her (Akzeptanzszenarien 3–5) | gleiche konsolidierte Methode | **Nicht ausgeführt** (Skipped: gleiche Begründung; Unit-Pendants `AktualisierungAsync_HeiltGeloeschteLaufdaten_BeiLaufendemProzess`, `AbgleichAsync_StelltLaufdatenSicher_FuerLaufendeAufgaben`, `CliLaufAktiv_StelltLaufdatenSicher_BeiWiederverwendung` bestanden) |
| F1: „CLI stoppen" bei nicht beendbarem Prozess über UI | Laut plan-check kein dedizierter E2E zwingend; Unit-Test `TaskDetailViewModelTests.CliStoppenAsync_BehaeltLaufdaten_BeiWeiterlaufendemProzess` | Bestanden (Unit-Ebene; UI-Gesamtfluss durch den Pflicht-E2E vorgesehen — dessen Ausführung offen) |

Alle geplanten neuen Tests aus `plan.md` (Abschnitt „Neue Tests") plus die Iteration-3-Ergänzungen wurden im Lauf `Category!=OsInterface` gefunden und bestanden — u. a. `AktivenLaufSicherstellenAsync_*` (7 Fälle inkl. Provider-/Guard-/Serialisierungs-Varianten), `ZweiterStart_FeuertCliLaufAktiv_StattGestartet`, `NotifyLaufAktiv_FeuertNur_BeiLaufendemProzess`, `GetLiveLaufStatus_BildetSessionStatusAb`, `GetLaufendeAufgabenIds_ListetNurLaufendeHandles`, `CliLaufAktiv_StelltLaufdatenSicher_BeiWiederverwendung`, `AktualisierungAsync_HeiltGeloeschteLaufdaten_BeiLaufendemProzess`, `OnRuntimeStatusChanged_HeiltFehlendeAktiveRunId`, `AbgleichAsync_StelltLaufdatenSicher_FuerLaufendeAufgaben`, `LaufdatenSicherstellenAsync_WithDisposedScopeFactory_Completes`, `Dispose_VerhindertNeuanlageVonTimerUndSubscription`, `LaufdatenSchreibpfade_SerialisierenProAufgabe`, die `LiveLaufStatus`-Klassifikationstests (`KiAusfuehrungsStatusConverterTests`/`KiAusfuehrungsStatusSymbolConverterTests`), `AktiveAufgabenAktualisierenAsync_ShouldMapLiveLaufStatus`, `CliStoppenAsync_BehaeltLaufdaten_BeiWeiterlaufendemProzess`, `StartCliAndUpdateStateAsync_SchreibtVolleLaufdaten_BeiWiederverwendeterSession` und `CliNeustartenAsync_MeldetLaufAktiv_BeiLebenderSession`.

## Reale End-to-End-Abnahme

Die Anforderung berührt die Benutzeroberfläche (Kachel-Statusanzeige ↔ Fußzeilen-Synchronisation) → reale Abnahme erforderlich. In dieser Agent-Sandbox **nicht durchführbar**: Es steht keine interaktive Windows-Desktop-Session zur Verfügung; WPF-Fenster-E2E-Tests und ConPTY-Prozessstarts sind hier nachweislich nicht lauffähig (vgl. CLAUDE.md, Abschnitt Testing — bestätigte Limitation, kein Code-Bug). Ein realer App-Start mit sichtbarer Kachel wäre genau der Weg, den der übersprungene Pflicht-E2E abbildet. Ergebnis je Akzeptanzszenario aus `plan.md` (unverändert gegenüber Iteration 1/2 — die Iteration-3-Concurrency-Fixes ändern kein sichtbares Szenario):

| Szenario | Durchgeführt über | Ergebnis |
|----------|-------------------|----------|
| 1 – Happy Path: Start ↔ Stopp synchron (Fußzeile „Ausführung läuft" ↔ Kachel „▶ Läuft"; nach Stopp beide „✓ Bereit"/„CLI inaktiv") | Vorgesehen: realer App-Start + Claude-CLI-Prozess (Pflicht-E2E `ConPty_WeitereLebenszyklusSzenarien_E2E`) | **Nicht durchführbar** (Sandbox: keine interaktive Desktop-Session, ConPTY nicht lauffähig) |
| 2 – Session-Wiederverwendung ohne `Gestartet` (H1): Detailansicht erneut öffnen bei lebender Session | E2E-Abschnitt `taskDetail.Reload()` in derselben Methode | **Nicht durchführbar** (gleiche Begründung; Mechanismus zusätzlich per `ZweiterStart_FeuertCliLaufAktiv_StattGestartet`, `CliNeustartenAsync_MeldetLaufAktiv_BeiLebenderSession` auf Unit-Ebene abgesichert) |
| 3 – Verschluckter Persistenzfehler (H2) → Selbstheilung | E2E-Divergenz-Nachspiel (DB-Löschung) | **Nicht durchführbar** (Sandbox; Unit-Pendants bestanden, s. o.) |
| 4 – Stopp ohne Prozessende (F1) | UI-Klick „CLI stoppen" bei überlebendem Prozess | **Nicht durchführbar** (Sandbox; Unit-Test `CliStoppenAsync_BehaeltLaufdaten_BeiWeiterlaufendemProzess` bestanden) |
| 5 – Heartbeat-Ausfall bei lebendem Prozess (H4) | E2E-Divergenz-Nachspiel / 30-s-Abgleich | **Nicht durchführbar** (Sandbox; Unit-Pendant `AbgleichAsync_StelltLaufdatenSicher_FuerLaufendeAufgaben` bestanden) |
| 6 – Tatsächlich beendeter/verwaister Lauf → „✓ Bereit" (kein falsches Wieder-Aktiv) | E2E-Stopp-Abschnitt + Heartbeat-Timeout | **Nicht durchführbar** (Sandbox; Unit-Pendants bestanden: `Convert_ShouldReturnBereitString_WhenPanelItemLiveLaufStatusNullUndBeendet`, `AktivenLaufSicherstellenAsync_SchreibtNichts_WennLaeuftGuardNichtMehrLaeuft` (neu in Iteration 3 — TOCTOU-Guard gegen Phantom-Wiederbelebung), bestehende Gestoppt/Fehler-Tests) |

Auflösungswege (Entscheidung liegt beim Anwender, nicht beim Agenten): **(a)** manuelle Abnahme — Anwender führt die Szenarien in einer realen Desktop-Session aus (z. B. `ConPty_WeitereLebenszyklusSzenarien_E2E` aus Visual Studio) und bestätigt die sichtbare Wirkung → `Bestanden (manuell, Datum)`; **(b)** dokumentierter Verzicht mit Begründung → `Verzichtet (Anwender, Datum, Begründung)`.

## Zusammenfassung

- Gesamt: 2561
- Bestanden: 2541
- Fehlgeschlagen: 0
- Übersprungen: 20

Aufschlüsselung: `Category!=OsInterface` 2225 (2224 bestanden, 1 übersprungen — `ArbeitsverzeichnisOeffnenServiceTests.Oeffne_AufNichtWindows_WirftPlatformNotSupportedException`, plattformbedingter Skip auf Windows, nicht issue-relevant) · `Category=OsInterface` 70 (51 bestanden, 19 übersprungen — 5 via `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`, 14 via `SOFTWARESCHMIEDE_SKIP_WPF_E2E_TESTS=1`, dokumentierte Sandbox-Skips) · `Softwareschmiede.IntegrationTests` 266 (alle bestanden).

## Testabdeckung

**Abdeckung:** 27,7 % Zeilenabdeckung gesamt (19 881 / 71 688 Zeilen; Branch: 58,9 %) — **Caveat:** Coverage nur in der stabilen Lane (`Category!=OsInterface`) erhoben; die OsInterface-Lane und die IntegrationTests-Lane (u. a. SCM-Services, Prozess-/Terminalcode) steuern zusätzliche Abdeckung bei, die hier nicht gemessen ist. Cobertura-Report: `src/Softwareschmiede.Tests/TestResults/00498a28-6b6f-4371-8449-2c26fe2a889f/coverage.cobertura.xml`.

Abdeckung der von diesem Issue geänderten Dateien (Zeilenbasis, je Datei aggregiert):

| Datei | Abdeckung |
|-------|-----------|
| `src/Softwareschmiede.App/Converters/AppConverters.cs` | 81,2 % (`KiAusfuehrungsStatusKlassifikation` inkl. Live-Zweig abgedeckt) |
| `src/Softwareschmiede.App/ViewModels/AktiveAufgabePanelItem.cs` | 100,0 % |
| `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs` | 81,5 % |
| `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs` | 79,1 % |
| `src/Softwareschmiede/Application/Services/AufgabeService.cs` | 82,7 % (`AktivenLaufSicherstellenAsync` inkl. `LaufGates`-Sperre/`laufStatusProvider`/`laeuftGuard` durch neue Tests abgedeckt) |
| `src/Softwareschmiede/Application/Services/CliProcessManager.cs` | 84,9 % (`LaufdatenSicherstellenAsync`, `AbgleichAsync`, `_lifecycleLock`/`_disposed`-Pfade abgedeckt) |
| `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs` | 82,1 % |
| `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs` | 56,9 % (neuer Code abgedeckt; niedrige Gesamtrate v. a. durch OsInterface-Lane-Code wie `HandleExitedCoreAsync`/`HandleProcessExitedAsync`, der in dieser Messung nicht läuft) |
| `src/Softwareschmiede/Infrastructure/Terminal/CliRuntimeStatusExtensions.cs` | 83,3 % |
| `src/Softwareschmiede/Domain/Interfaces/IRunningAutomationStatusSource.cs` | n/a (reines Interface) |

Alle neuen/geänderten Codepfade des Issues — inklusive der Iteration-3-Concurrency-Pfade (`LaufGates`, `laufStatusProvider`, `laeuftGuard`, `_lifecycleLock`/`_disposed`) — sind durch bestandene Tests abgedeckt. Projektweit liegen 247 Quelldateien unter 80 % (Liste im Cobertura-Report; hier nur die berührten Dateien aufgeführt).

## Fehlende Tests

Quelle: `Coverage-Daten`

Hinweis: 0 %-Werte stammen ausschließlich aus der stabilen Lane; mehrere der genannten Dateien werden durch die IntegrationTests-/OsInterface-Lane bzw. die in dieser Sandbox geskippten E2E-Tests abgedeckt (z. B. `ScmTaskCompletionMonitoringService`/`ScmEventMonitoringService` haben bestandene Integration-Tests, `Services/Testing/*` sind reine E2E-Fixture-Helfer). Von diesem Issue geänderte Dateien haben **keine** 0 %-Abdeckung.

Produktionsdateien mit 0 % Zeilenabdeckung in der stabilen Lane (generierte `obj/*.g.cs`, ~60 Migrationsdateien, reine `.xaml`-Einträge und `App/Services/Testing/*`-E2E-Fixtures ausgenommen; 61 Dateien):

- `src/Softwareschmiede.App/Behaviors/StatusUebergangsAnimation.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Controls/RibbonButtonBase.cs`, `RibbonButtonStyleHelper.cs`, `RibbonGroup.xaml.cs`, `RibbonLargeButton.xaml.cs`, `RibbonSmallButton.xaml.cs`, `RibbonSplitButton.xaml.cs` — 0 % Abdeckung (UI-Code-Behind)
- `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml.cs`, `DiffViewer.xaml.cs`, `PluginDetailPanel.xaml.cs`, `RecoveryBannerControl.xaml.cs` — 0 % Abdeckung (UI-Code-Behind)
- `src/Softwareschmiede.App/Services/PluginSelectionDialogService.cs`, `WpfApplicationShutdownService.cs`, `WpfAudioService.cs`, `WpfBannerService.cs`, `WpfDialogService.cs`, `WpfUpdateProgressDialogService.cs` — 0 % Abdeckung (WPF-/OS-nahe Dienste)
- `src/Softwareschmiede.App/ViewModels/PluginSelectionDialogViewModel.cs`, `SolutionSelectionDialogViewModel.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/*.xaml.cs` (Dialog-/View-Code-Behind, ~15 Dateien), `PluginSettingEntryEditHelper.cs`, `PluginSettingFieldTemplateSelector.cs` — 0 % Abdeckung
- `src/Softwareschmiede/Application/Services/DirectoryAccessGuard.cs`, `GitBranchNameValidator.cs`, `GitKlonHelper.cs`, `KiAufgabenAbschlussEreignis.cs`, `KiAufgabenBenachrichtigungsHub.cs` — 0 % Abdeckung
- `src/Softwareschmiede/Application/Services/ScmEventExchangeService.cs`, `ScmEventMonitoringService.cs`, `ScmEventTaskResolver.cs`, `ScmTaskCompletionMonitoringService.cs` — 0 % in dieser Lane (Integration-Tests vorhanden, dort abgedeckt)
- `src/Softwareschmiede/Domain/Entities/PluginKonfiguration.cs`, `ScmTaskLease.cs`; `Domain/Exceptions/DirectoryAccessException.cs`, `SoftwareschmiedeException.cs`; `Domain/ValueObjects/WorkspaceNodeRow.cs` — 0 % Abdeckung
- `src/Softwareschmiede/Infrastructure/Services/BenutzerkontextService.cs`, `CliRunner.cs`, `SystemProzessStarter.cs`, `SystemShutdownService.cs`, `WindowsCredentialStore.cs` — 0 % Abdeckung (OS-nahe Dienste)
- `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsole.cs`, `PseudoConsoleProcessStarter.cs`, `Win32PseudoConsoleProcessLauncher.cs` — 0 % Abdeckung (ConPTY/OS-nahe; in Sandbox nicht lauffähig)
- `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContextDesignTimeFactory.cs` — 0 % Abdeckung (Design-Time-Tooling)
- `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/AgentInfo.cs` — 0 % Abdeckung
