# Tests — Bestandsaufnahme

## Test-Ausgangszustand vor der Umsetzung

- **Zeitpunkt (mit Zeitzone):** 2026-11-07, ca. 21:44–21:48 Uhr CET (UTC+01:00)
- **Branch und Commit-ID:** `task/issue-265-5b389153221c4a34ae054c8469bf1290-persistierter-zeitgesteuerter` @ `5779f6d7641db62bafb539dd67f7ed6fbee86ce1` („Backmerge from main to staging (#330)", 2026-11-07 13:25:57 +0100)
- **Uncommittete Änderungen im getesteten Stand:** Keine Produktivcode-/Teständerungen. Als untracked vorhanden: das Feature-Dokumentationsverzeichnis `docs/features/task/issue-265-5b389153221c4a34ae054c8469bf1290-persistierter-zeitgesteuerter/` (`requirement.md`, `todo.md` sowie die durch diese Bestandsaufnahme erzeugten `inventory*`-Dateien und Logs). Diese Dateien beeinflussen das Testergebnis nicht.
- **Testumgebung und Runtime-/SDK-Versionen:** Windows, Git Bash; .NET SDK `10.0.403`; Debug-Build (`net10.0` / `net10.0-windows10.0.17763.0`). Sandbox-Eigenschaften laut `CLAUDE.md`: ConPTY-Kindprozesse werden nicht korrekt isoliert → `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` gesetzt; FlaUI-E2E erfordert eine interaktive Desktop-Session.
- **Ermittelte Testsuiten und Quellen der Testbefehle:** Aus `CLAUDE.md` und `.github/workflows/pr-staging-ci.yml` (Zeilen 128, 132, 222, 226) sowie `E2E`-Datei-Headern: (1) stabile Lane `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"`, (2) OS-Interface-Lane `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category=OsInterface"`, (3) `Softwareschmiede.IntegrationTests`-Projekt (in CI ebenfalls nach `OsInterface` getrennt). Hinweis: Die FlaUI-E2E-Suite (`End2EndTest`) trägt `[OsInterface]` auf Klassenebene (`MainTest.cs` Zeile 10) und gehört damit vollständig zur `Category=OsInterface`-Lane; der `Category=E2E`-Trait ist eine zusätzliche Kennzeichnung.

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Build | `dotnet build Softwareschmiede.slnx` | Repo-Root | 0 (0 Warnungen, 0 Fehler, ~15,5 s) | — | — | — | [dotnet-build.log](test-results/dotnet-build.log) |
| Reguläre Lane | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"` | Repo-Root | 0 | 2166 | 0 | 1 | [dotnet-test-regular.log](test-results/dotnet-test-regular.log) |

Dauer des Testlaufs: 2 min 48 s. Übersprungener Test: `Softwareschmiede.Tests.Application.Services.ArbeitsverzeichnisOeffnenServiceTests.Oeffne_AufNichtWindows_WirftPlatformNotSupportedException` (plattformbedingter Skip).

Hinweis zur Ausführung: Der `dotnet test`-Befehl wurde synchron gestartet; die Ausführungsumgebung hat den laufenden Befehl nach ~12,5 s ohne Ausgabe in den Hintergrund verlagert, worauf der Lauf über Ausgabe-Polling vollständig abgewartet wurde. Ein Build-Lock-/Hook-Problem trat nicht auf (in diesem Klon existiert kein `.claude/`-Verzeichnis).

### Nachgewiesene bestehende Testfehler

Keine. Der reguläre Lauf endete mit 0 Fehlschlägen; es liegen keine nachgewiesenen bestehenden Testfehler vor.

### Testlücken und Ausführungsprobleme

- **`Category=OsInterface`-Lane nicht ausgeführt** (`dotnet test ... --filter "Category=OsInterface"`). Umfang: ~45 `[OsInterfaceFact]`/`[OsInterfaceTheory]`-Tests (Clipboard-/Tastatur-Tests unter `App/Controls`, echte Prozessstarts in `KiAusfuehrungsServiceTests*`, `SimulatedPseudoConsoleProcessLauncherTests`, `CliEmbeddingServiceIntegrationTests`) sowie alle `[OsInterface]`-markierten E2E-Klassen — darunter die gesamte FlaUI-Suite `End2EndTest` (`MainTest.cs` mit 12 `[SkippableFact]`-Sammeltests, `E2E_RepositoryManagementTests`, `E2E_RepositoryInitialisierung*`, `E2E_ScmEvents`, `ProjectDetailE2ETests`, `AppStartupLogInspectorTests`, `WpfE2EPlaceholderTests`). Die Anforderung erlaubt den Verzicht ausdrücklich („nur ausführen, wenn … Zeit es erlaubt"); Begründung: sehr lange Gesamtlaufzeit (jeder E2E-Test startet die echte App per FlaUI) und hohe Umgebungssensitivität (interaktive Desktop-Session, in dieser Sandbox bekanntermaßen defekte ConPTY-Isolation). **Konsequenz für den Nachweis:** Die anforderungsrelevanten E2E-Szenarien `ZeitgesteuerterPrompt_NachPlanen_ZeigtWartestellungStatus_E2E` (Teil von `ConPty_WeitereLebenszyklusSzenarien_E2E`) und der zeitgesteuerte Sofortversand in `SessionLimit_MarkerPauseProtokollUndUpdateSicherheit_E2E` (Teil von `General_RepositoryUndAufgaben_E2E`) wurden im Ausgangslauf **nicht ausgeführt** — ihr Ausgangszustand ist unbekannt, sie gelten weder als erfolgreich noch als belegt fehlerhaft.
- **`Softwareschmiede.IntegrationTests`-Projekt nicht ausgeführt.** In der CI läuft es separat; die Anforderung nannte nur die `Softwareschmiede.Tests`-Lane als verpflichtend. Ausgangszustand unbekannt.
- **ConPTY-abhängige Tests:** Mit gesetztem `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` würden ConPTY-Tests sauber als „Skipped" gemeldet statt zu timen — im regulären Lauf kam kein ConPTY-Test zur Ausführung bzw. zum Skip, da sämtliche ConPTY-E2E-Tests zur `OsInterface`-Lane gehören.
- **Keine Infrastruktur-/Buildfehler** aufgetreten: Der volle Solution-Build lief ohne Fehler durch; es musste kein Teil-Build wegen einer gesperrten `Softwareschmiede.App.exe` gewählt werden.

## Testklassen

### `PromptZeitVersandServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/PromptZeitVersandServiceTests.cs` — Unit-Tests mit `FakeTimeProvider`; Sessions werden per Reflection in `KiAusfuehrungsService._handles` registriert.

- `SchedulePromptAsync_ZielzeitInVergangenheit_SendetSofort` — Vergangene Zielzeit sendet sofort und hinterlässt keinen Warteschlangen-Eintrag.
- `SchedulePromptAsync_ZielzeitInZukunft_PuffertPrompt` — Zukünftige Zielzeit erzeugt Eintrag; `GetScheduledPromptStatus` liefert Text und Zielzeit.
- `Timer_BeiErreichenDerZielzeit_SendetPromptAutomatisch` — `Advance` auf die Zielzeit feuert den Timer, sendet und entfernt den Eintrag.
- `CancelScheduledPrompt_EntferntGeplantenPrompt` — Storno verhindert Versand auch nach Ablauf der Zielzeit.
- `SchedulePromptAsync_ZweiterPromptFuerSelbeAufgabe_ErsetztErsten` — Ersetzungsregel: zweiter Plan ersetzt ersten Timer.
- `Timer_OhneSession_VerwirftPromptStill` — Fälligkeit ohne Session: kein `PromptSent`, keine Exception, Eintrag entfernt.
- `SchedulePromptAsync_AktivePause_VerschiebtZielzeitAufPausenende` — Aktive Pause beim Planen verschiebt die Zielzeit auf das Pausenende (Issue 151).
- `Timer_BeiNachPlanungGesetzterPause_VerschiebtVersandAufPausenende` — Nachträglich gesetzte Pause plant den Timer auf das Pausenende um und versendet erst danach.

### `TaskDetailViewModelTests_ZeitgesteuerterPrompt`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_ZeitgesteuerterPrompt.cs`

- `ScheduledPromptTargetHours_Binding_SetztProperty` — Binding der Zeitfelder inkl. `PropertyChanged`.
- `SchedulePrompt_LeereFelder_KeinScheduling` — Leere Zeitfelder ⇒ `KannPromptPlanen`/`CanExecute` false.
- `SchedulePrompt_GueltigeZeit_RuftServiceAuf` — Gültige Zeit + Vorlage ⇒ Service-Eintrag + „Prompt in Wartestellung"-Status, Felder werden geleert.
- `SchedulePrompt_UngueltigeStunde_SetztFehlerMeldung` — Stunde 25 ⇒ `FehlerMeldung`, kein Eintrag.
- `Dispose_StorniertGeplantePrompts` — `Dispose` storniert den geplanten Prompt (heutiges Verhalten, das die Anforderung ändern will).
- `OnCliProcessStatusChanged_CliGestoppt_StorniertGeplantenPrompt` — CLI-Stopp storniert den geplanten Prompt (heutiges Verhalten).
- `IsCliRunning_Aendert_LoestKannPromptPlanenPropertyChangedAus` — CLI-Statuswechsel feuert `PropertyChanged(KannPromptPlanen)`.
- `SelectedPromptVorlage_Aendert_LoestKannPromptPlanenPropertyChangedAus` — Vorlagenwechsel feuert `PropertyChanged(KannPromptPlanen)`.

### `MainWindowViewModelTests_ZeitgesteuerterPrompt`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelTests_ZeitgesteuerterPrompt.cs`

- `AktiveAufgabenAktualisierenAsync_MitGeplantemPrompt_SetztHasScheduledPrompt` — Panel-Item trägt `HasScheduledPrompt=true`.
- `AktiveAufgabenAktualisierenAsync_OhneGeplantenPrompt_HasScheduledPromptBleibtFalse` — Ohne Plan bleibt `HasScheduledPrompt=false`.

### `KiAusfuehrungsStatusConverterTests_ZeitgesteuerterPrompt`
Datei: `src/Softwareschmiede.Tests/App/Converters/KiAusfuehrungsStatusConverterTests_ZeitgesteuerterPrompt.cs`

- `Convert_ShouldReturnPromptInWartestellung_WhenHasScheduledPromptIsTrue` — „⏳ Prompt in Wartestellung" bei `HasScheduledPrompt`.
- `Convert_ShouldReturnPromptInWartestellung_EvenWhenCliIsRunning` — Wartestellung hat Vorrang vor „▶ Läuft".
- `Convert_ShouldReturnWartetString_WhenHasScheduledPromptIsFalse` — Unverändertes Statusverhalten ohne Plan.

### `TaskDetailViewModelTests_Pause` (relevanter Ausschnitt)
Datei: `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_Pause.cs`

- `PromptGuards_False_WhenAufgabePausiert` — Bei aktiver Pause sind `KannPromptVorlageSenden` und `KannPromptPlanen` false (Issue 151).

### `EntwicklungsprozessServiceTests` (relevanter Ausschnitt)
Datei: `src/Softwareschmiede.Tests/Application/Services/EntwicklungsprozessServiceTests.cs`

- `AbschliessenAsync_ShouldCancelScheduledPrompt_WhenPromptGeplant` (Zeile 540) — Aufgabenabschluss ruft `CancelScheduledPrompt`.

### `End2EndTest` — E2E-Szenarien (nicht im Ausgangslauf ausgeführt, `Category=OsInterface`)
- `ZeitgesteuerterPrompt_NachPlanen_ZeigtWartestellungStatus_E2E` (`E2E/E2E_ZeitgesteuerterPrompt.cs`, aufgerufen in `MainTest.ConPty_WeitereLebenszyklusSzenarien_E2E` Zeile 233) — Plant über die UI eine Zielzeit und erwartet die „Prompt in Wartestellung"-Anzeige ohne Fehlerbanner.
- `SessionLimit_MarkerPauseProtokollUndUpdateSicherheit_E2E` (`E2E/E2E_SessionLimitPause.cs` Zeile 51, aufgerufen in `MainTest.General_RepositoryUndAufgaben_E2E` Zeile 36) — Nutzt den zeitgesteuerten Versand mit vergangener Zielzeit (Sofortversand) zum Senden eines Rate-Limit-Markers.

## Hilfsmethoden

### `PromptZeitVersandServiceTests` (interne Hilfen)
- `StartCliSessionAsync` / `RegisterSession(Guid)` — registriert eine `PseudoConsoleSession` (`TestPseudoConsoleSessionFactory.Create`) per `CliProcessHandle` direkt in `KiAusfuehrungsService._handles` via Reflection, damit `GetTerminalSession` eine Session liefert.
- `CreatePausedTaskProviderAsync(DateTimeOffset? pausiertBisUtc, Guid aufgabeId)` — baut einen `ServiceProvider` mit InMemory-`SoftwareschmiededDbContext` + `AufgabeService` und seedet Projekt/Aufgabe mit `PausiertBisUtc`; liefert die `IServiceScopeFactory` für den Service.

### `TestKiAusfuehrungsServiceFactory` / `TestPseudoConsoleSessionFactory` / `TestDbContextFactory` / `TaskDetailViewModelTestFactory` / `ScmDispatchFixture`
Dateien unter `src/Softwareschmiede.Tests/Helpers/`
- `TestKiAusfuehrungsServiceFactory.Create(...)` — erzeugt einen `KiAusfuehrungsService` für Unit-Tests.
- `TestPseudoConsoleSessionFactory.Create(Stream, Stream)` — simulierte Terminal-Session.
- `TestDbContextFactory.Create()` — InMemory-`SoftwareschmiededDbContext`.
- `TaskDetailViewModelTestFactory.CreateStub()` / `CreateArbeitsverzeichnisOeffnenService()` — ViewModel-Hilfen; erzeugt u. a. einen `PromptZeitVersandService` mit `TimeProvider.System` (Zeilen 40–67).
- `ScmDispatchFixture` — registriert `PromptZeitVersandService` als Singleton und verdrahtet ihn in `EntwicklungsprozessServiceOptions` (Zeilen 134, 156).

### `TaskDetailView` (E2E-View-Pattern)
Datei: `src/Softwareschmiede.Tests/E2E/Views/TaskDetailView.cs`
- `SetScheduledPromptTime(int hour, int minute)` — befüllt die Automation-Elemente `ScheduledPromptStunde`/`ScheduledPromptMinute`.
- `SendScheduledPrompt()` — wartet auf `ScheduledPromptStatus` und löst den Versand aus.
- `SelectPromptTemplate(string name)` — wählt eine Vorlage in `PromptVorlagenAuswahl`.

### `WpfTestBase` (E2E-Basisklasse)
- `SkipWennConPtyNichtVerfuegbar()` — `Skip.If` bei `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` (via `ConPtyEnvironmentProbe`), für Tests mit ConPTY-CLI-Prozess.
- `SkipWennWpfE2ENichtVerfuegbar()` — `Skip.If` bei `SOFTWARESCHMIEDE_SKIP_WPF_E2E_TESTS=1` (via `WpfE2EEnvironmentProbe`), zusätzlich zentral in `LaunchApp` geprüft.
- `OpenTestDbContext()` / `WartenBisAsync` / `ConfirmLocalDirectoryGitInitInSourceDirectory` / `SetupProjectMitNeuerAufgabe` — DB-Zugriff, Polling und Projekt-/Aufgaben-Setup für E2E-Szenarien.
