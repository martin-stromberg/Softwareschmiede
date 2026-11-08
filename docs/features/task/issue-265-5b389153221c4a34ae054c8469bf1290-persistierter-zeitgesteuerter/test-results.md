# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

Kein einziger ausgeführter Test ist fehlgeschlagen (0 Fehlschläge in 2450 ausgeführten Tests).
Das geplante Pflicht-E2E-Szenario `ZeitgesteuerterPrompt_NachPlanen_ZeigtWartestellungStatus_E2E`
(läuft innerhalb von `ConPty_WeitereLebenszyklusSzenarien_E2E`) konnte in dieser Sandbox **nicht
ausgeführt** werden (dokumentierte ConPTY-Isolations-Limitation, `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`
→ sauberer `Skipped` statt Timeout) und die reale End-to-End-Abnahme der Akzeptanzszenarien war hier
nicht durchführbar. Der Anwender hat am 2026-11-07 ausdrücklich dokumentiert **verzichtet**
(Begründung: E2E- und reale Abnahme laufen nur in echten interaktiven Sessions; auch die CI skipt
diese Lane; alle 2450 ausführbaren Tests bestanden, Changeset-Abdeckung auf Service-Ebene 79–100 %).
Beides waren Umgebungsrestriktionen, keine Produktivcode- oder Testfehler — mit dem dokumentierten
Verzicht ist der Endzustand je Szenario erreicht.

Build vorab: `dotnet build Softwareschmiede.slnx` — **0 Fehler, 0 Warnungen** (~6 s, inkrementell).

Ausgeführte Lanes (jeweils synchron/foreground, mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`):

- `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface" --logger "console;verbosity=normal" --collect:"XPlat Code Coverage"` — **2193 Tests: 2192 bestanden, 0 fehlgeschlagen, 1 übersprungen** (3 m 4 s)
- `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category=OsInterface&FullyQualifiedName~ConPty_WeitereLebenszyklusSzenarien"` — **1 Test: übersprungen** (ConPTY-Sandbox-Skip, `SkipWennConPtyNichtVerfuegbar`)
- `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category=OsInterface&FullyQualifiedName~General_RepositoryUndAufgaben"` — **1 Test: bestanden** (2 m 13 s) — enthält u. a. die Sofortversand-Regression `SessionLimit_MarkerPauseProtokollUndUpdateSicherheit_E2E`; die App startete real per FlaUI
- `dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj --filter "Category!=OsInterface"` — **257 Tests, alle bestanden** (~32 s); enthält `ScmTaskCompletionMonitoringServiceTests` (nutzt `PromptZeitVersandService`), u. a. `Monitor_MergedPullRequest_CancelsScheduledPromptOnComplete`

Alle im Plan neu geforderten Tests liefen und bestanden, u. a.:
`PromptZeitVersandServiceTests.{SchedulePromptAsync_SchreibtDatensatz, SchedulePromptAsync_ErneutesPlanen_ErsetztDatensatz, Versand_LoeschtDatensatz, CancelScheduledPromptAsync_LoeschtDatensatz, Timer_OhneSession_BehaeltPlanUndWartetAufSession, Sendefehler_BehaeltPlanUndWartetAufSession, CliGestartet_MitWartendemPlan_ZustelltNach, Nachzustellung_SchreibtSystemMeldung, Versand_OhneAbonnent_SchreibtSystemMeldung, Versand_MitAbonnent_KeinProtokollEintrag, ProtokollEintrag_BeiGeloeschterAufgabe_BrichtVersandNicht, Pause_Verschiebung_AktualisiertDatensatz, Restore_ZukuenftigerPlan_ArmiertTimer, Restore_UeberfaelligerPlan_WartetAufSession, Restore_VerwaisterDatensatz_WirdGeloescht, Restore_NachNeustart_LiefertBeiSessionStartZu}`,
`TaskDetailViewModelTests_ZeitgesteuerterPrompt.{LadenAsync_MitGeplantemPrompt_ZeigtWartestellungStatus, ScheduledPromptChanged_AktualisiertAnzeige, CancelScheduledPromptCommand_EntferntPlanUndAnzeige, Dispose_LaesstGeplantenPlanBestehen, OnCliProcessStatusChanged_CliGestoppt_BehaeltGeplantenPrompt}` sowie der angepasste
`EntwicklungsprozessServiceTests.AbschliessenAsync_ShouldCancelScheduledPrompt_WhenPromptGeplant`.

## Fehlgeschlagene Tests

Keine echte Fehlschläge (0 von 2450 ausgeführten Tests). Der bekannte Flake
`TerminalControlTests`-Clipboard-`COMException CLIPBRD_E_CANT_OPEN` ist **nicht** aufgetreten.

### Nicht ausführbar in dieser Sandbox — durch Anwender dokumentiert verzichtet

- **End2EndTest.ConPty_WeitereLebenszyklusSzenarien_E2E** (`MainTest.cs:226`) — per
  `SkipWennConPtyNichtVerfuegbar()` übersprungen, da `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` gesetzt
  ist. ConPTY-Kindprozesse werden in dieser Sandbox nachweislich nicht zur Pseudo-Konsole isoliert
  (dokumentierte Restriktion in `CLAUDE.md`); ohne die Env-Variable würde der Test minutenlang in
  einen `TimeoutException`-Lauf gehen. Enthält das geplante Pflicht-E2E-Szenario
  `ZeitgesteuerterPrompt_NachPlanen_ZeigtWartestellungStatus_E2E` —
  **Verzichtet (Anwender, 2026-11-07, nur in echter interaktiver Session ausführbar; CI skipt diese Lane ebenfalls)**.
- **Reale End-to-End-Abnahme der Akzeptanzszenarien** — `Nicht durchführbar` in dieser Sandbox
  (WPF-E2E-Fluss benötigt eine echte ConPTY-CLI-Session; ein realer App-Start mit planbarer
  CLI-Session ist hier nicht möglich).
  **Verzichtet (Anwender, 2026-11-07)** — siehe Abschnitt „Reale End-to-End-Abnahme".

## E2E-Abdeckung

Geplante Szenarien aus `plan.md` („E2E-Tests (primärer Funktionsnachweis)") und `plan-check.md` („E2E-Abdeckung"):

| Szenario | Test / Testklasse | Ergebnis |
|----------|-------------------|----------|
| Plan über UI anlegen → „Prompt in Wartestellung" + Storno-Button → Detailansicht verlassen → Datensatz bleibt in DB → Wiederöffnen → Wartestellung weiterhin sichtbar → `CancelScheduledPrompt()` → Anzeige weg + Datensatz gelöscht | `ZeitgesteuerterPrompt_NachPlanen_ZeigtWartestellungStatus_E2E` (`E2E_ZeitgesteuerterPrompt.cs`, aufgerufen in `ConPty_WeitereLebenszyklusSzenarien_E2E`, `MainTest.cs:233`) | Verzichtet (Anwender, 2026-11-07, Sandbox ohne ConPTY-Isolation; sauberer Skip) |
| Versand im Hintergrund bei geschlossener Detailansicht (Happy-Path-Kern) | E2E-UI-Anteil im obigen Szenario; Hintergrundversand ohne ConPTY nicht E2E-treibbar — Service-Nachweise: `Versand_LoeschtDatensatz`, `Versand_OhneAbonnent_SchreibtSystemMeldung`, `Timer_OhneSession_BehaeltPlanUndWartetAufSession` | Verzichtet (Anwender, 2026-11-07, E2E); Service-Ebene **bestanden** |
| App-Neustart → Re-Arm → Zustellung | kein echter FlaUI-Neustart geplant (zweiter App-Launch + ConPTY in Sandbox nicht lauffähig); Nachweis auf Service-Ebene: `Restore_NachNeustart_LiefertBeiSessionStartZu`, `Restore_ZukuenftigerPlan_ArmiertTimer`, `Restore_UeberfaelligerPlan_WartetAufSession`, `Restore_VerwaisterDatensatz_WirdGeloescht` | Bestanden (Service-Ebene); E2E nicht erforderlich mit Begründung (plan-check.md) |
| Sofortversand-Regression | `SessionLimit_MarkerPauseProtokollUndUpdateSicherheit_E2E` (Phase in `General_RepositoryUndAufgaben_E2E`, `MainTest.cs:36`) | **Bestanden** (2 m 13 s, reale App per FlaUI) |

## Reale End-to-End-Abnahme

Erforderlich (UI + Persistenz betroffen). In dieser Sandbox **nicht durchführbar**: Der Akzeptanzfluss
setzt eine real laufende CLI-Session voraus; ConPTY-Kindprozesse werden hier nicht zur Pseudo-Konsole
isoliert (dokumentierte Umgebungsrestriktion, `CLAUDE.md`). Die App selbst ist per FlaUI startbar
(`General_RepositoryUndAufgaben_E2E` lief real), aber der Plan-/Versand-Fluss benötigt eine echte
CLI-Session. Ergebnis je Szenario aus `plan.md`:

| # | Szenario | Durchgeführt über | Ergebnis |
|---|----------|-------------------|----------|
| 1 | Happy Path: Planen → Ansicht verlassen → Versand im Hintergrund | nicht durchgeführt (keine ConPTY-CLI-Session; automatisierter Nachweis nur Service-Ebene + geskipptes E2E) | Verzichtet (Anwender, 2026-11-07, Sandbox ohne ConPTY-CLI-Session) |
| 2 | App-Neustart mit zukünftigem Plan (Re-Arm) | nicht durchgeführt (App-Neustart-Szenario nicht lauffähig; Service-Nachweis `Restore_*` bestanden) | Verzichtet (Anwender, 2026-11-07, Sandbox ohne ConPTY-CLI-Session) |
| 3 | Überfälliger Plan / keine Session → Nachzustellung bei CLI-Start | nicht durchgeführt (ConPTY); Service-Nachweise bestanden | Verzichtet (Anwender, 2026-11-07, Sandbox ohne ConPTY-CLI-Session) |
| 4 | CLI-Stopp vor Fälligkeit → Plan bleibt → Zustellung beim Neustart | nicht durchgeführt (ConPTY); Unit-Nachweise bestanden | Verzichtet (Anwender, 2026-11-07, Sandbox ohne ConPTY-CLI-Session) |
| 5 | Aufgabenabschluss storniert Plan inkl. Datensatz | nicht durchgeführt als UI-Abnahme; Unit-Test `AbschliessenAsync_ShouldCancelScheduledPrompt_WhenPromptGeplant` + Integration-Test `Monitor_MergedPullRequest_CancelsScheduledPromptOnComplete` bestanden | Verzichtet (Anwender, 2026-11-07, Sandbox ohne ConPTY-CLI-Session) |
| 6 | Wiederöffnen zeigt persistierten Plan | nicht durchgeführt (UI-Abnahme); Unit-Test `LadenAsync_MitGeplantemPrompt_ZeigtWartestellungStatus` bestanden | Verzichtet (Anwender, 2026-11-07, Sandbox ohne ConPTY-CLI-Session) |
| 7 | Pausierte Aufgabe → Zielzeitverschiebung auf Pausenende | nicht durchgeführt (UI-Abnahme); Unit-Test `Pause_Verschiebung_AktualisiertDatensatz` bestanden | Verzichtet (Anwender, 2026-11-07, Sandbox ohne ConPTY-CLI-Session) |
| 8 | Manueller Storno („Wartestellung aufheben") | nicht durchgeführt (UI-Klick im E2E-Szenario geskippt); Unit-Test `CancelScheduledPromptCommand_EntferntPlanUndAnzeige` + `CancelScheduledPromptAsync_LoeschtDatensatz` bestanden | Verzichtet (Anwender, 2026-11-07, Sandbox ohne ConPTY-CLI-Session) |
| 9 | Sendefehler → Plan behalten + Retry beim nächsten Session-Start | nicht durchgeführt (ConPTY); Unit-Test `Sendefehler_BehaeltPlanUndWartetAufSession` bestanden | Verzichtet (Anwender, 2026-11-07, Sandbox ohne ConPTY-CLI-Session) |
| 10 | Dauerhaft wartender Plan (kein TTL) | nicht durchgeführt; Festlegung, implizit über `Restore_*`-Tests abgedeckt | Verzichtet (Anwender, 2026-11-07, Sandbox ohne ConPTY-CLI-Session) |

**Hinweis:** Der Anwender hat am 2026-11-07 für alle Szenarien ausdrücklich dokumentiert verzichtet
(Begründung: WPF-/ConPTY-E2E ist in dieser Sandbox und auch in der CI nicht ausführbar — nur in
echten interaktiven Sessions; sämtliche ausführbaren Unit-, Service- und Integrationstests sowie
das FlaUI-Szenario `General_RepositoryUndAufgaben_E2E` bestanden). Damit ist der dokumentierte
Endzustand je Szenario erreicht.

## Zusammenfassung

- Gesamt: 2452
- Bestanden: 2450
- Fehlgeschlagen: 0
- Übersprungen: 2 (`End2EndTest.ConPty_WeitereLebenszyklusSzenarien_E2E` — ConPTY-Sandbox-Skip;
  `ArbeitsverzeichnisOeffnenServiceTests.Oeffne_AufNichtWindows_WirftPlatformNotSupportedException` —
  plattformbedingter Skip, bereits im Ausgangslauf vorhanden)

## Testabdeckung

**Abdeckung:** 27,5 % (Zeilen, gesamte Solution, Cobertura; die WPF-App-/Plugin-Schicht ist ohne die
weitgehend geskippte `OsInterface`/FlaUI-Lane kaum erreichbar — daher niedriger Gesamtwert)

Coverage-Datei: `src/Softwareschmiede.Tests/TestResults/b777ef2f-01e5-4e21-9a1d-b9249c9477b8/coverage.cobertura.xml`

Changeset-relevante Dateien (Zeilenabdeckung, dateiweise aggregiert):

| Datei | Abdeckung |
|-------|-----------|
| `src/Softwareschmiede/Application/Services/PromptZeitVersandService.cs` | 87,1 % |
| `src/Softwareschmiede/Application/Services/ZeitgesteuerterPromptStore.cs` | 89,7 % |
| `src/Softwareschmiede/Domain/Entities/ZeitgesteuerterPrompt.cs` | 100,0 % |
| `src/Softwareschmiede/Application/Services/ScheduledPromptInfo.cs` | 100,0 % |
| `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs` | 82,1 % |
| `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContext.cs` | 98,8 % |
| `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs` | 79,0 % |
| `src/Softwareschmiede.App/Views/TaskDetailView.xaml.cs` | 20,2 % |
| `src/Softwareschmiede.App/Views/TaskDetailView.xaml` | 0,0 % |
| `src/Softwareschmiede.App/App.xaml.cs` | 4,0 % |
| `src/Softwareschmiede/Migrations/20261107211018_AddZeitgesteuerterPrompt*.cs`, `SoftwareschmiededDbContextModelSnapshot.cs` | 0,0 % (generiert — ignoriert) |

## Fehlende Tests

Quelle: `Coverage-Daten`

- `src/Softwareschmiede.App/Views/TaskDetailView.xaml` — 0 % Abdeckung; neue Storno-Button-Verdrahtung
  (`WartestellungAufheben`) ist nur über das geskippte FlaUI-E2E-Szenario erreichbar
- `src/Softwareschmiede.App/App.xaml.cs` — 4,0 % Abdeckung; der Aufruf von
  `RestoreScheduledPromptsAsync` im Startup-Pfad ist nur über App-Start-E2E erreichbar (Service-Logik
  selbst zu 85,7–98,8 % auf Service-Ebene abgedeckt)
- Hinweis: Solution-weit liegen 198 Dateien bei 0 % Zeilenabdeckung — nahezu vollständig
  WPF-UI-/Plugin- und E2E-Hilfscode, der ausschließlich über die in dieser Sandbox nicht bzw. nur
  teilweise lauffähige `Category=OsInterface`-Lane erreichbar ist. Das ist eine
  Umgebungs-/Ausführungslücke, keine neue Testlücke des Changesets.
