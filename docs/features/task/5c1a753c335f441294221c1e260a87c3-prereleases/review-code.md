# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

Die Befunde aus `review-code.1.md` und `review-code.2.md` sind weitgehend behoben:
`UpdateReleaseLookupResult` trennt Fehlgeschlagen/KeinTreffer/Gefunden sauber, die
Asset-URL wird nur noch per HTTPS akzeptiert, `CancelLaufendenUpdateAblauf` fängt
allgemeine Exceptions, `Anfuegen` nutzt denselben Retry wie `Schreibe`, das
Stream-Gate unterscheidet Timeout von Cancellation, `CreateSqlite` wird verwendet,
`RoutingHttpHandler`/`StaticHttpHandler`/`TempDirectory` sind zentralisiert, und die
gemeinsame Basisklasse `MainWindowViewModelUpdateTestBase` beseitigt das Setup-Duplikat.
Die folgenden Rest- und Neubefunde bleiben bestehen.

## Befunde

### MainWindowViewModel.cs (MainWindowViewModel)

- **Lange Parameterliste** — Der Konstruktor (Zeilen 160–172) umfasst weiterhin 12
  Parameter, davon fünf optionale, überwiegend testgetriebene Seams
  (`dispatcherInvoke`, `dialogService`, `versionProvider`,
  `laufdatenChangedNotifier`, `updateDienste`). Die Bündelung der Update-Dienste in
  `MainWindowUpdateDienste` hat die Liste verkürzt, der Schwellwert wird aber weiterhin
  deutlich überschritten.
  Empfehlung: Die verbleibenden optionalen Seams in ein weiteres Parameter-Objekt
  bündeln (z. B. `MainWindowTestSeams`) oder per Property-Injection/DI-Auflösung
  aus dem Konstruktor nehmen.

- **Duplizierter Code** — Das Versuch-Gerüst ist in drei nahezu identischen Varianten
  vorhanden: `InitializeUpdatesAfterWindowReadyAsync` (Zeilen 447–501),
  `UpdatePruefenAsync` (Zeilen 503–547) und `UpdateStartenAsync` (Zeilen 549–599)
  wiederholen jeweils Gate-Akquise (`_updateGate.WaitAsync(0)`),
  `BeginneVersuch`/`BeendeVersuch`, Busy-Flag, Generations-Snapshot, Settings-Read mit
  `UpdateEinstellungenLesefehlerAnzeigen`, den `UpdateMode.Aus`-Guard,
  `IstUpdateVersuchAktuell`-Prüfung und den `catch (OperationCanceledException)`/
  `finally`-Rahmen. Zusätzlich ist der Block „Settings erneut lesen → bei `null`
  Fehlerhinweis, bei geändertem Snapshot `UpdateAngebotEntfernen` +
  `SetError(UpdateEinstellungenGeaendertHinweis)`" in `InstalliereUpdateAsync`
  zweimal wörtlich wiederholt (Zeilen 624–638 und 647–661).
  Empfehlung: Eine gemeinsame Methode
  `FuehreUpdateVersuchAsync(string versuchArt, Func<UpdateSettings, int, CancellationToken, Task> kern)`
  extrahieren, die Gate, Protokoll-Beginn/-Ende, Generations-Snapshot, Settings-Read und
  den Aus-Guard kapselt; den Aktualitätsnachweis in `InstalliereUpdateAsync` als
  `PruefeSettingsAktualitaetAsync(...)` auslagern.

- **Struktur/Verantwortlichkeiten** — Die Klasse umfasst 814 Zeilen; allein der
  Update-Fluss (Startautomatik, Prüfen, Installieren, Settings-Reaktion, Cancellation,
  Gates, Generations-Invalidierung; grob Zeilen 280–312 und 447–718) macht rund
  350 Zeilen aus und ist mit `MainWindowUpdateDienste` bereits als eigenes
  Abhängigkeitsbündel abgegrenzt.
  Empfehlung: Den Update-Ablauf in einen eigenen Koordinator (z. B.
  `MainWindowUpdateFlow`) auslagern, dem das ViewModel nur noch Commands, Properties
  und den Settings-Saved-Einstieg delegiert.

### SettingsViewModel.cs (SettingsViewModel)

- **Primitive Obsession / hartkodierte Werte** — Der Update-Modus wird als
  Anzeige-Label-String modelliert: `SelectedUpdateMode` ist `string`,
  `TryParseUpdateModus` (Zeilen 425–441) bildet Label-Texte per `switch` zurück auf
  `UpdateMode`. Dieselben drei Label-Texte sind zusätzlich in
  `E2E_UpdateSettings.cs` (Zeilen 25–27) dupliziert und werden in
  `MainWindowViewModelTests_UpdateSettingsReadFailure.cs` (z. B. Zeile 102) sowie
  `MainWindowViewModelTests_UpdateStartup.cs` (Zeilen 136, 189, 226) als Literale
  übergeben. Eine Label-Umbenennung erfordert vier synchrone Änderungen; ein Tippfehler
  in einem Testliteral erzeugt nur den generischen Pfad „ungültig".
  Empfehlung: `UpdateModusOptionen` als `(UpdateMode Modus, string Label)`-Paare bzw.
  direkt als `UpdateMode`-Auswahl mit ValueConverter modellieren und die Labels aus
  einer einzigen Quelle beziehen, sodass Tests den Enum-Wert statt des Anzeigetexts
  setzen können.

### MainWindowViewModelTests_UpdateSettingsReadFailure.cs (MainWindowViewModelTests_UpdateSettingsReadFailure)

- **Switch-/String-Dispatch auf Magic Strings** —
  `UpdateSettingsReadFailure_InitialStopsBeforeReleaseRequest` (Zeilen 39–76) wählt den
  Act-Schritt über `[InlineData]`-Strings (`"startautomatik"`, `"manuellPruefen"`,
  `"manuellInstallieren"`) per `if` (Zeile 54) und `switch` (Zeilen 65–76) ohne
  `default`-Fall. Ein falsch geschriebener String überspringt die Act-Phase kommentarlos;
  der anschließende `SettingsReadCount`-Assert schlägt dann mit einer irreführenden
  Ursachenmeldung fehl.
  Empfehlung: Die Einstiegspunkte als `[MemberData]` mit
  `Func<MainWindowViewModel, Task>`-Delegaten übergeben oder in drei separate
  Testmethoden zerlegen.

### E2E_UpdateSettings.cs (End2EndTest)

- **God-Methode** — `Startup_SafetyCancelAndErrorsRemainUsable` (Zeilen 605–892,
  ~287 Zeilen) deckt drei konzeptionell getrennte Aspekte ab: abgelehnter
  Sicherheitsdialog, Dialog-Abbruch sowie Fehler-/Timeout-Pfade an mehreren Grenzen.
  Die Methodenkonsolidierung zum Sparen von App-Starts ist projektvorgabe-konform, die
  interne Struktur lässt sich aber ohne zusätzliche App-Instanzen zerlegen.
  Empfehlung: Die drei Teilflüsse in eigene Hilfsmethoden (analog zu
  `LesefehlerStartupVarianteStartenAsync`) extrahieren und die Szenario-Methode als
  kurze Sequenz dieser Schritte führen.

- **Still geschluckte Exceptions** — `SetzeUpdateSteuerungZurueckAsync`
  (Zeilen 1223–1267) enthält sechs leere `catch { }`-Blöcke ohne Begründung
  (Zeilen 1226–1228 und 1264–1266), während die benachbarten Blöcke den Zweck
  kommentieren. Die Blöcke schlucken sämtliche Ausnahmetypen.
  Empfehlung: Entweder die erwarteten Typen gezielt fangen (`IOException`,
  `InvalidOperationException`) oder — wie in den Nachbarblöcken — einen kurzen
  Kommentar ergänzen, der das bewusste Ignorieren im Cleanup begründet.

- **Irreführender Methodenname / redundanter Alias** — `WarteAufSpeichernAbgeschlossen`
  (Zeile 1217) delegiert 1:1 an `WarteAufSpeichernAktiviert`. Der Name suggeriert das
  Warten auf den abgeschlossenen Speichervorgang (dafür existiert
  `SettingsView.WaitForSettingsSaved`, das auf die Bestätigung
  „Einstellungen gespeichert." wartet); tatsächlich wird nur auf die erneute
  Aktivierung des Speichern-Buttons gewartet.
  Empfehlung: Den Alias entfernen und Aufrufer direkt `WarteAufSpeichernAktiviert`
  nutzen lassen — oder `WarteAufSpeichernAbgeschlossen` auf die echte
  Speicher-Bestätigung warten lassen.

### UpdateFixtureHttpMessageHandlerTests.cs (UpdateFixtureHttpMessageHandlerTests)

- **Fehlerbehandlung** — `Dispose` (Zeile 42) fängt beim rekursiven
  `Directory.Delete` nur `IOException`. Dieselbe Operation kann auch
  `UnauthorizedAccessException` werfen (schreibgeschützte Dateien/Handles); der
  Vergleichscode `UpdateE2EFixture.Dispose` (Zeilen 390–406) behandelt beide Fälle.
  Ein solcher Cleanup-Fehler würde den eigentlichen Testausgang maskieren.
  Empfehlung: `UnauthorizedAccessException` zusätzlich fangen oder den Catch auf
  `catch (Exception)` erweitern.

## Geprüfte Dateien

- `src/Softwareschmiede.App/App.xaml.cs`
- `src/Softwareschmiede.App/Services/IUpdateVersuchProtokoll.cs`
- `src/Softwareschmiede.App/Services/Testing/FixtureCliUpdateSafetyService.cs`
- `src/Softwareschmiede.App/Services/Testing/ProtokollierenderUpdateService.cs`
- `src/Softwareschmiede.App/Services/Testing/RecordingApplicationShutdownService.cs`
- `src/Softwareschmiede.App/Services/Testing/RecordingUpdateProcessLauncher.cs`
- `src/Softwareschmiede.App/Services/Testing/UpdateE2EProtokoll.cs`
- `src/Softwareschmiede.App/Services/Testing/UpdateE2ESzenario.cs`
- `src/Softwareschmiede.App/Services/Testing/UpdateE2ETestConfiguration.cs`
- `src/Softwareschmiede.App/Services/Testing/UpdateE2ETestKontext.cs`
- `src/Softwareschmiede.App/Services/Testing/UpdateFixtureHttpMessageHandler.cs`
- `src/Softwareschmiede.App/Services/Testing/UpdateSettingsReadFailureInterceptor.cs`
- `src/Softwareschmiede.App/ViewModels/MainWindowUpdateDienste.cs`
- `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/UpdateProgressViewModel.cs`
- `src/Softwareschmiede.App/Views/MainWindow.xaml`
- `src/Softwareschmiede.App/Views/MainWindow.xaml.cs`
- `src/Softwareschmiede.App/Views/SettingsView.xaml`
- `src/Softwareschmiede.App/Views/UpdateProgressDialog.xaml`
- `src/Softwareschmiede.Tests/App/Services/Testing/UpdateFixtureHttpMessageHandlerTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelTests_UpdateSettingsReadFailure.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelTests_UpdateStartup.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelUpdateTestBase.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/SettingsViewModelTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/AppEinstellungServiceTests_UpdateSettings.cs`
- `src/Softwareschmiede.Tests/Application/Services/Updates/ApplicationVersionProviderTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/Updates/UpdatePackageServiceTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/Updates/UpdateProgressViewModelTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/Updates/UpdateScriptServiceTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/Updates/UpdateServiceTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/Updates/UpdateServiceTests_Options.cs`
- `src/Softwareschmiede.Tests/Application/Services/Updates/UpdateServiceTests_PrereleaseChain.cs`
- `src/Softwareschmiede.Tests/Application/Services/Updates/UpdateVersionComparerTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/Updates/UpdateVersionComparerTests_SemVer.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_AutonomAufgabenAgentExecution.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_AutonomAufgabenInitialisierung.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_ConPtyLifecycle.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_RepositoryInitialisierungAusfuehrungTests.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_RepositoryInitialisierungConfigTests.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_SettingsCommandLineParameters.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_SettingsKiPluginPersistence.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_UpdateFixture.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_UpdateSettings.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_ViewPattern.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_WorkingDirectory.cs`
- `src/Softwareschmiede.Tests/E2E/ElementWaitHelper.cs`
- `src/Softwareschmiede.Tests/E2E/MainTest.cs`
- `src/Softwareschmiede.Tests/E2E/ProjectDetailE2ETests.cs`
- `src/Softwareschmiede.Tests/E2E/UpdateE2EFixture.cs`
- `src/Softwareschmiede.Tests/E2E/Views/AutonomAufgabeDetailView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/Dialogs/ArbeitsverzeichnisBearbeitenDialogView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/Dialogs/AutonomAufgabeInitialisierungsDialogView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/Dialogs/DeleteConfirmationDialogView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/Dialogs/HelpTextDialogView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/Dialogs/PluginSelectionDialogView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/Dialogs/RepositoryAssignDialogView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/Dialogs/SolutionSelectionDialogView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/Dialogs/UpdateProgressDialogView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/Dialogs/UpdateSafetyDialogView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/FileExplorerView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/MenuView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/ProjectDetailView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/ProjectListView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/SettingsView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/TaskDetailView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/TodoListView.cs`
- `src/Softwareschmiede.Tests/E2E/WpfTestBase.cs`
- `src/Softwareschmiede.Tests/Helpers/RoutingHttpHandler.cs`
- `src/Softwareschmiede.Tests/Helpers/StaticHttpHandler.cs`
- `src/Softwareschmiede.Tests/Helpers/TempDirectory.cs`
- `src/Softwareschmiede.Tests/Helpers/TestDbContextFactory.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Data/DatenbankPfadResolverTests.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Services/GitHubReleaseClientTests.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Services/GitHubReleaseClientTests_Filters.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Services/GitHubReleaseClientTests_Pagination.cs`
- `src/Softwareschmiede/Application/Services/AppEinstellungService.cs`
- `src/Softwareschmiede/Application/Services/Updates/SemanticUpdateVersion.cs`
- `src/Softwareschmiede/Application/Services/Updates/UpdateInterfaces.cs`
- `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs`
- `src/Softwareschmiede/Application/Services/Updates/UpdateService.cs`
- `src/Softwareschmiede/Application/Services/Updates/UpdateVersionComparer.cs`
- `src/Softwareschmiede/Infrastructure/Services/Updates/GitHubReleaseClient.cs`
