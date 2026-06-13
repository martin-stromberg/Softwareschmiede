# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### ProjectDetailViewModelTests.cs (ProjectDetailViewModelTests)

- **Testqualität: Timing-abhängige Tests** — Die Tests verwenden `await Task.Delay(100/200/300ms)` als Synchronisationsmechanismus für asynchrone Befehle (z. B. Zeilen 54, 71, 100, 200). Fällt die Ausführung des Befehls in einer langsamen Umgebung länger als das Delay aus, schlägt der Test nicht-deterministisch fehl.

  Empfehlung: Den `AsyncRelayCommand` um eine `ExecuteAsync`-Methode (oder eine `Task`-Eigenschaft `ExecutingTask`) ergänzen, die im Test direkt awaited werden kann, statt auf ein festes Delay zu warten. Alternativ die `Execute`-Methode so testen, dass das zugrunde liegende `Task`-Delegate direkt aufgerufen wird.

- **Testqualität: Test prüft mehrere fachliche Fälle** — `RepositoryOeffnenAsync_Success_OeffnetRepositoryUrl` (Zeile 217) verifiziert zwei konzeptuell getrennte Dinge: (1) `CanExecute == false` ohne Repository, (2) `CanExecute == true` nach Laden eines Projekts mit Repository. Der Test deckt dabei auch den Lade-Pfad ab, obwohl sein Name nur das Öffnen der URL beschreibt.

  Empfehlung: Zwei separate Tests anlegen — einen für `CanExecute == false` ohne Repository, einen für `CanExecute == true` nach dem Laden — und den zweiten Test korrekt benennen (`RepositoryOeffnenCommand_CanExecuteTrue_WennRepositoryGeladen`).

---

### WpfTestBase.cs (WpfTestBase)

- **Toter Code / Fehlende Aufrufe: `DeleteTestDatabase` wird in `Dispose` aufgerufen, aber Datenbankisolation ist unvollständig** — `LaunchApp` setzt die Umgebungsvariable `SOFTWARESCHMIEDE_TEST_DB_PATH` mit `Environment.SetEnvironmentVariable` (Zeile 54). Das setzt den Wert prozessweit, also für alle parallel laufenden E2E-Tests in derselben Test-Collection. Da `[Collection("E2E")]` nur sequenziell ausführt, ist das kein akutes Problem; es ist aber ein stilles Risiko, das nicht dokumentiert ist.

  Empfehlung: Einen Kommentar hinzufügen, der erklärt, dass die Umgebungsvariable prozessweit gilt und deshalb die `[Collection("E2E")]`-Annotation zwingend erforderlich ist, um parallele Überschneidungen zu verhindern.

- **Fehlerbehandlung: Zu breiter Exception-Handler in `Dispose`** — `Dispose` enthält drei separate `catch (Exception)` Blöcke (Zeilen 71, 82, 92), die Ausnahmen nur auf `Console.WriteLine` protokollieren und dann stillschweigend schlucken. Fehler beim Schließen der Anwendung oder beim Löschen der Testdatenbank bleiben unsichtbar im Testlauf.

  Empfehlung: Das ist in `Dispose`-Implementierungen ein bekanntes Muster und weitgehend akzeptabel. Die Ausgabe sollte jedoch auf `TestContext` (oder `ITestOutputHelper` in xUnit) umgestellt werden, damit die Meldungen im Testergebnis erscheinen und nicht nur in der Konsole verschwinden.

- **Temporäres Feld: `_application` und `_automation` sind nullable, werden aber außerhalb von `LaunchApp` nicht auf Initialisierung geprüft** — Die Properties `Automation` und `FlaUiApp` (Zeilen 19–20) verwenden den Null-Forgiving-Operator (`!`). Wird ein Testmethode aufgerufen, ohne `LaunchApp` vorher aufzurufen, entsteht eine `NullReferenceException` zur Laufzeit statt einer klaren Fehlermeldung.

  Empfehlung: In `Automation` und `FlaUiApp` statt `!` eine explizite Prüfung mit `throw new InvalidOperationException("LaunchApp muss vor dem Zugriff aufgerufen werden.")` einfügen.

---

### ProjectDetailE2ETests.cs (ProjectDetailE2ETests)

- **Doppelter Code: `LaunchApp` + `NavigateToProjecten` in jedem Test wiederholt** — Alle sieben Testmethoden beginnen mit identischen drei Zeilen (LaunchApp, GetMainWindow, NavigateToProjecten). Sechs der sieben rufen zusätzlich `CreateAndOpenProject` auf.

  Empfehlung: Setup-Logik in eine `Initialize`/`SetupProject`-Hilfsmethode verschieben, oder den gemeinsamen Pfad `LaunchApp → NavigateToProjecten` als Xunit-Fixture oder in `[Collection("E2E")]`-Setup zentralisieren. Mindestens die drei Startzeilen in eine private Hilfsmethode `StartAndNavigateToProjects()` auslagern, die `mainWindow` zurückgibt.

- **Toter Code: `FlaUiApp`-Property in `WpfTestBase` wird in keinem Test verwendet** — Die Property `FlaUiApp` (Zeile 20 in `WpfTestBase`) ist zwar vorhanden, wird aber in keiner der aktuellen Testklassen referenziert. Stattdessen wird `LaunchApp()` direkt assigned.

  Empfehlung: Property entfernen oder einen Test/Kommentar ergänzen, der den vorgesehenen Verwendungszweck zeigt.

---

### ProjectDetailViewModel.cs (ProjectDetailViewModel)

- **Temporäres Feld: `_disposed`** — Das Feld `_disposed` ist am Ende der Klasse deklariert (Zeile 397), obwohl es in der `Dispose`-Methode und in `RepositoryZuweisenAsync` verwendet wird. Das erschwert die Lesbarkeit, weil das Feld weit von seiner Verwendung entfernt ist.

  Empfehlung: `_disposed` zusammen mit den anderen privaten Feldern zu Beginn der Klasse deklarieren (nach Zeile 38).

- **Fehlende Kapselung: Fehlermeldungs-Pattern wiederholt sich** — Das Muster `FehlerMeldung = $"Fehler: {ex.Message}"` taucht in `LadenAsync` (Zeile 237), `AufgabeErstellenAsync` (Zeile 270), `ProjektSpeichernAsync` (Zeile 306), `ProjektLoeschenAsync` (Zeile 331) und `RepositoryZuweisenAsync` (Zeile 369) sowie `RepositoryOeffnen` (Zeile 394) auf — sechs Mal identisch.

  Empfehlung: Eine private Hilfsmethode `SetFehler(Exception ex)` anlegen, die `FehlerMeldung = $"Fehler: {ex.Message}"` setzt, und alle sechs Stellen darauf umstellen.

---

### ProjectListViewModel.cs (ProjectListViewModel)

- **Doppelter Code: `ZeigeDetail` und `ZeigeDetailErstellungsFormular` duplizieren `InitDetailViewModel`-Aufruf** — Beide Methoden (Zeilen 165–170 und 173–181) rufen `InitDetailViewModel(viewModel)` auf und setzen danach `DetailViewModel = viewModel`. Der Unterschied liegt nur in der initialen `ProjektId`. Das ist strukturell in Ordnung, aber `ZeigeDetailErstellungsFormular` setzt `ProjektBeschreibung = string.Empty` manuell (Zeile 179), während der Konstruktor des ViewModels `_projektBeschreibung` bereits auf `null` initialisiert. Das Überschreiben auf `string.Empty` unterscheidet sich semantisch von `null` und könnte zu inkonsistentem Verhalten führen (z. B. in der Validierung).

  Empfehlung: Prüfen, ob das Setzen von `ProjektBeschreibung = string.Empty` in `ZeigeDetailErstellungsFormular` fachlich beabsichtigt ist. Falls ja, kommentieren. Falls nein, Zeile 179 entfernen, um die Initialisierung konsistent zu halten.

- **Lazy Class: `LadenProjekteInternAsync`** — Die private Methode `LadenProjekteInternAsync` (Zeilen 104–110) enthält nur drei Zeilen und wird genau zweimal aufgerufen (`LadenAsync` und `NeuesProjektHinzufuegen`). Ihr Mehrwert gegenüber einem direkten Aufruf ist gering; die Methode macht den Aufrufpfad aber lesbarer und verhindert, dass `IsLoading`/`FehlerMeldung` in `NeuesProjektHinzufuegen` fehlen. Das ist akzeptabel; kein zwingender Befund.

---

### ViewModelBase.cs / AppConverters.cs

Keine Befunde. Die Implementierungen sind klar, klein und korrekt.

## Geprüfte Dateien

- `src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/ProjectListViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/ViewModelBase.cs`
- `src/Softwareschmiede.App/Converters/AppConverters.cs`
- `src/Softwareschmiede.App/Views/ProjectDetailView.xaml`
- `src/Softwareschmiede.App/Views/RepositoryAssignDialog.xaml.cs`
- `src/Softwareschmiede.App/Views/SettingsView.xaml`
- `src/Softwareschmiede.Tests/App/ViewModels/ProjectDetailViewModelTests.cs`
- `src/Softwareschmiede.Tests/E2E/ProjectDetailE2ETests.cs`
- `src/Softwareschmiede.Tests/E2E/WpfE2EPlaceholderTests.cs`
- `src/Softwareschmiede.Tests/E2E/WpfTestBase.cs`
