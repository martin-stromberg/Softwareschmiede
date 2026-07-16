# Tests

## Testklassen

### `UpdateProgressViewModelTests`

Datei: `src/Softwareschmiede.Tests/Application/Services/Updates/UpdateProgressViewModelTests.cs`

Namespace: `Softwareschmiede.Tests.Application.Services.Updates`

Testklasse für `UpdateProgressViewModel`.

**Bestehende Testmethoden:**

- **`Apply_ShouldUpdateProgressState()`** [Fact]
  - **Was wird getestet:** `Apply(UpdatePreparationProgress)` aktualisiert korrekt `PhaseText`, `Percent`, `IsIndeterminate` und `Message`
  - **Beispieltest:** Ruft `Apply()` mit Phase `Download` und Prozentwert 42 auf; erwartet `PhaseText = "Download"`, `Percent = 42`, `IsIndeterminate = false`, `Message = "Lädt"`

- **`SetError_ShouldEnableClose()`** [Fact]
  - **Was wird getestet:** `SetError(string)` setzt Fehler-Flag und Schließen-Flag korrekt
  - **Beispieltest:** Ruft `SetError("Fehler")` auf; erwartet `HasError = true`, `CanClose = true`, `Message = "Fehler"`

- **`CancelCommand_ShouldInvokeCancellationAndDisableCancel()`** [Fact]
  - **Was wird getestet:** `CancelCommand.Execute()` ruft die Cancellation-Action auf und deaktiviert Abbruch
  - **Beispieltest:** Erstellt ViewModel mit `cancelAction`, führt Command aus; erwartet dass Action aufgerufen wurde, `CanCancel = false`, `Message` aktualisiert

- **`MarkUpdaterStarting_ShouldEnableCloseAndDisableCancel()`** [Fact]
  - **Was wird getestet:** `MarkUpdaterStarting()` bereitet Dialog korrekt auf Updater-Start vor
  - **Beispieltest:** Ruft `MarkUpdaterStarting()` auf; erwartet `CanCancel = false`, `CanClose = true`, `Message = "Update wird gestartet. Die Anwendung wird beendet."`

**Abhängigkeiten und Frameworks:**
- FluentAssertions für Assertions
- Fact-Tests (xUnit)
- Direkte Instanziierung von `UpdateProgressViewModel` (keine Mocks)
