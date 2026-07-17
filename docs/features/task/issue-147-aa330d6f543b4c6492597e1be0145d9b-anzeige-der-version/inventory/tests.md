# Tests

## Testklassen

### `MainWindowViewModelTests`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelTests.cs`

Umfassende Unit-Tests für `MainWindowViewModel`. Die Testklasse nutzt in-memory EF Core Datenbank und Mocks.

**Relevante vorhandene Testmethoden (Auswahl):**
- `AktiveAufgabenAktualisierenAsync_ShouldFillObservableCollection_WhenCalled()` – testet Befüllung der aktiven Aufgaben
- `IsDashboardVisible_ShouldReturnTrue_WhenCurrentViewIsDashboardViewModel()` – testet Dashboard-Sichtbarkeit
- `NavigateZuAufgabeCommand_*()` – mehrere Tests für Navigation zur Aufgabendetailansicht
- `CurrentView_Setter_UsesFireAndForgetSafely()` – testet asynchrone Hintergrund-Aktualisierung ohne Awaiting
- Update-bezogene Tests: `Constructor_ShouldStartUpdateCheckAndExposeAvailableUpdate()`, `UpdateStartenCommand_*()` etc.

**Hilfsmethoden:**
- `CreateSut()` – Factory-Methode zum Erstellen von `MainWindowViewModel` Instanzen mit konfigurierbaren Mocks und Services

**Test-Setup:**
- `TestDbContextFactory.Create()` – erstellt In-Memory-Testdatenbank
- `TestKiAusfuehrungsServiceFactory.Create()` – erstellt Mock für KI-Service
- `TaskDetailViewModelTestFactory.Create()` – erstellt Mock für Task-Detail-ViewModel
- Mocks für `IServiceProvider`, `IDialogService`, `IRunningAutomationStatusSource`, `IPluginManager`

**Besonderheit:** Es existieren **KEINE Tests für `CurrentVersion`**, da die Property noch nicht implementiert ist.

### `ApplicationVersionProviderTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/Updates/ApplicationVersionProviderTests.cs`

Tests für `ApplicationVersionProvider`.

**Testmethoden:**
- `GetInstalledVersionAsync_ShouldReadValidVersionJson()` – testet erfolgreiche Deserialisierung einer gültigen `version.json`
- `GetInstalledVersionAsync_ShouldReturnNull_WhenVersionJsonIsInvalid()` – testet Fehlerbehandlung bei fehlenden/ungültigen Versionen (multiple InlineData-Varianten)

**Hilfsmethoden:**
- `TempDirectory` – Disposable-Klasse zum Erstellen/Löschen temporärer Test-Verzeichnisse

**Aktuelle Abdeckung:** Tests prüfen bereits den Happy Path (gültige version.json mit Normalisierung "v1.2.3" → "1.2.3") und mehrere Fehlerszenarien (fehlende Datei, ungültige Version, fehlende Version-Property).

## Weitere Test-Dateien (E2E)

Es gibt umfassende E2E-Tests im Verzeichnis `src/Softwareschmiede.Tests/E2E/`, z. B.:
- `E2E_CreateNewTaskNavigation.cs`
- `E2E_PluginSelectionDialog.cs`
- etc. (insgesamt ca. 20 E2E-Test-Dateien)

**Relevanz für diese Anforderung:** Ein optionaler E2E-Test könnte später UI-Validierung durchführen (z. B. prüfen, dass die Versionsnummer in der Fußzeile angezeigt wird), ist aber in der Anforderung optional deklariert.
