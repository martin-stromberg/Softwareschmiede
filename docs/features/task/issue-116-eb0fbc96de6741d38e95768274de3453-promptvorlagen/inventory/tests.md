# Tests

## Testklassen

### `SettingsViewModelTests`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/SettingsViewModelTests.cs`

- `ScmPluginSelectedCommand_LaeadtSettingsGroups_FuerAusgewaehltesPlugin` - prueft Laden von SCM-Plugin-Setting-Gruppen.
- `ScmPluginSelectedCommand_WithMultipleFields_LoadsAllValues` - prueft mehrere Feldtypen in SCM-Settings.
- `KiPluginSelectedCommand_LaeadtSettingsGroups_FuerAusgewaehltesPlugin` - prueft Laden von KI-Plugin-Setting-Gruppen.
- `LadenAsync_LaedtDefaultPlugine_UndInitialeSettings` - prueft Default-Plugin-Laden und Initialisierung.
- `LadenAsync_VerwendetErstesPlugin_WennGespeicherterNameNichtExistiert` - prueft Fallback auf erstes Plugin.
- `SpeichernAsync_SpeichertDefaultPlugine_Und_EinstellungswerteFuerScm` - prueft Speichern von SCM-Settings.
- `SpeichernAsync_SpeichertDefaultPlugine_Und_EinstellungswerteFuerKi` - prueft Speichern von KI-Settings.
- `SpeichernAsync_ValidierungFehlgeschlagen_ZeigtFehlerMeldung` - prueft Pflichtfeldvalidierung.
- `SpeichernAsync_BooleanFelder_KonvertiertCorrect` - prueft Boolean-Konvertierung.

### `TaskDetailViewModelTests`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`

- Tests zu `AufgabeBranchName`, `AufgabeId` und den Sichtbarkeiten `ShowEditPanel`, `ShowCliPanel`, `ShowDiffPanel`.
- Tests zu `KannSpeichern`, `KannLoeschen`, `SpeichernCommand`, `LoeschenCommand` und `ZurueckCommand`.
- Tests zu `InfoCliToggleCommand`.
- Tests zum Starten der Aufgabe inklusive Plugin-Auswahl, Speichern eines Projekt-Defaults, Statuswechsel und laufender CLI.
- `StartenAsync_FiresPseudoConsoleSessionGestartet_NachErfolgreichemStart` - prueft Session-Event fuer Terminalbindung.
- `GetPseudoConsoleSession_ReturnsSession_AfterAutoRestartInLadenAsync` - prueft Session-Verfuegbarkeit nach Auto-Restart.
- `NachNavigateBack_WiederoeffnenFindetLaufendeSessionUndSetzIsCliRunning` - prueft weiterlaufende CLI ueber Navigation hinweg.
- Tests zum `PluginAendernCommand`.
- Tests zum automatischen CLI-Neustart in `LadenAsync`.
- Tests zur Issue-Zuweisung und Issue-URL-Oeffnung.

### `AufgabeDetailFolgePromptTests`
Datei: `src/Softwareschmiede.Tests/Components/Pages/Aufgaben/AufgabeDetailFolgePromptTests.cs`

- Testet eine vorhandene Blazor-Komponente `AufgabeDetail.razor`, nicht die aktuelle WPF-`TaskDetailView`.
- Enthalten sind Tests fuer KI-Anfrage-Markup, Agenten-/Plugin-Auswahl, gespeicherte `VorschlagPrompt`-Vorbelegung, Folgekontextmodus, Startparameter und Scroll-Verhalten.
- Die Klasse zeigt vorhandene Testmuster fuer Prompt- und Kontextlogik, ist aber nicht direkt an das WPF-Ribbon gebunden.

### `TerminalControlTests` und `TerminalControlTests.ClipboardPaste`
Dateien:

- `src/Softwareschmiede.Tests/App/Controls/TerminalControlTests.cs`
- `src/Softwareschmiede.Tests/App/Controls/TerminalControlTests.ClipboardPaste.cs`

- Pruefen Terminal-Rendering, Session-Bindung, Tastatureingaben und Clipboard-Paste in den Terminal-Input-Stream.

### `CliProcessManagerTests` und `KiAusfuehrungsServiceTests`
Dateien:

- `src/Softwareschmiede.Tests/Application/Services/CliProcessManagerTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/CliProcessManagerTests_AktiverLauf.cs`
- `src/Softwareschmiede.Tests/Application/Services/CliProcessManagerTests_LaufStatus.cs`
- `src/Softwareschmiede.Tests/Application/Services/KiAusfuehrungsServiceTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/KiAusfuehrungsServiceTests_WorkingDirectory.cs`
- `src/Softwareschmiede.Tests/Application/Services/KiAusfuehrungsServiceTests_WorkingDirectory_InSourceDirectory.cs`

- Decken CLI-Prozessstart, Laufstatus, aktiven Lauf, Arbeitsverzeichnisauflosung und Prozessverwaltung ab.

### `RateLimitDetectionServiceIntegrationTests`
Datei: `src/Softwareschmiede.Tests/ServiceIntegration/RateLimitDetectionServiceIntegrationTests.cs`

- `VorschlagPrompt_WirdGespeichert_UndStatusWirdWartend` - prueft Persistenz eines aufgabenspezifischen Vorschlagsprompts.
- `ClearPromptVorschlag_EntferntVorschlagUndZeitstempel` - prueft Entfernen dieses Vorschlags.

## Hilfsmethoden

### `TaskDetailViewModelTestFactory`
Datei: `src/Softwareschmiede.Tests/Helpers/TaskDetailViewModelTestFactory.cs`

- `Create` - erzeugt ein `TaskDetailViewModel` mit In-Memory-DbContext, Mock-Services, `KiAusfuehrungsService`, `ProtokollService`, `PluginSelectionService` und `EntwicklungsprozessService`.

### `TestDbContextFactory`
Datei: `src/Softwareschmiede.Tests/Helpers/TestDbContextFactory.cs`

- Erstellt testgeeignete `SoftwareschmiededDbContext`-Instanzen fuer Service- und ViewModel-Tests.
