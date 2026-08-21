# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### AutonomAufgabeDetailViewModel.cs (AutonomAufgabeDetailViewModel)

- **Doppelter Code** — `StarteAgentAsync`, `StoppeAgentAsync` und `ResumeAgentAsync` enthalten jeweils denselben Null-Guard-Block:

  ```csharp
  if (_aufgabe is null)
  {
      ErrorMessage = "Aufgabe wurde nicht initialisiert.";
      return Task.CompletedTask;
  }
  ```

  Der Block ist wortgleich in allen drei Methoden dupliziert (Zeilen ~150-156, ~165-171, ~180-186).

  Empfehlung: In eine private Hilfsmethode extrahieren, z. B. `private bool IstInitialisiert() { if (_aufgabe is not null) return true; ErrorMessage = "Aufgabe wurde nicht initialisiert."; return false; }` und in allen drei Methoden per `if (!IstInitialisiert()) return Task.CompletedTask;` verwenden.

### AutonomAufgabeStartService.cs (AutonomAufgabeStartService)

- **Fehlerbehandlung** — In `StarteAsync` wurde der try/catch-Bereich gegenüber der Vorgängerversion (`AutonomAufgabeStartCoordinator`) erweitert und umschließt jetzt die gesamte Methode statt nur den Teil ab dem Anzeigen der Detail-Ansicht. Dadurch ist `aktualisierteAufgabe` im `catch`-Block nicht mehr im Scope, und im Fehlerfall wird `new AutonomAufgabeStartResult(null, ...)` zurückgegeben statt (wie vorher) die bereits geladene `aktualisierteAufgabe` mitzugeben. Konkrete Auswirkung: Schlägt `ShowAutonomAufgabeDetailAsync` fehl, nachdem `InitialisiereAsync` bereits erfolgreich war und den `AusfuehrungsStatus` der Aufgabe in der DB geändert hat, zeigt `TaskDetailViewModel` (das nur bei `AktualisierteAufgabe is not null` aktualisiert, siehe `TaskDetailViewModel.cs` Zeile ~1221) weiterhin den veralteten Stand an, obwohl in der DB bereits der neue Stand persistiert ist.

  Empfehlung: `aktualisierteAufgabe` außerhalb des try-Blocks deklarieren (z. B. `Aufgabe? aktualisierteAufgabe = null;` vor dem try) und im catch-Fall weiterhin mitgeben, analog zum bisherigen Verhalten in `AutonomAufgabeStartCoordinator`.

## Geprüfte Dateien

- `src/Softwareschmiede.App/App.xaml.cs`
- `src/Softwareschmiede.App/Services/AutonomAufgabeStartCoordinator.cs` (gelöscht)
- `src/Softwareschmiede.App/Services/AutonomAufgabeStartErgebnis.cs` (gelöscht)
- `src/Softwareschmiede.App/Services/AutonomAufgabeStartResult.cs` (neu)
- `src/Softwareschmiede.App/Services/AutonomAufgabeStartService.cs` (neu)
- `src/Softwareschmiede.App/Services/WpfDialogService.cs`
- `src/Softwareschmiede.App/ViewModels/AutonomAufgabeDetailViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/AutonomAufgabeInitialisierungsDialogViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
- `src/Softwareschmiede.App/Views/AutonomAufgabeInitialisierungsDialog.xaml`
- `src/Softwareschmiede.App/Views/AutonomAufgabeInitialisierungsDialog.xaml.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/AutonomAufgabeDetailViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/AutonomAufgabeInitialisierungsDialogViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/AutonomAufgabeInitialisierungsDialogViewModelTests_BranchUndVorlagen.cs` (neu)
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTestsBase.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_PluginAktivierung.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_ZeitgesteuerterPrompt.cs`
- `src/Softwareschmiede.Tests/Application/Services/AutonomAufgabenInitialisierungsServiceTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/ProjektleiterAgentServiceTests_Fehlerfaelle.cs` (neu)
- `src/Softwareschmiede.Tests/Application/Services/SessionManagementServiceTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/UnteragentGovernanceServiceTests.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_AutonomAufgabenInitialisierung.cs`
- `src/Softwareschmiede.Tests/Helpers/TaskDetailViewModelTestFactory.cs`
- `src/Softwareschmiede/Application/Services/AutonomAufgabenInitialisierungsService.cs`
- `src/Softwareschmiede/Application/Services/AutonomAufgabenOptions.cs`
- `src/Softwareschmiede/Application/Services/DirectoryAccessGuard.cs` (neu)
- `src/Softwareschmiede/Application/Services/GitKlonHelper.cs`
- `src/Softwareschmiede/Application/Services/ProjektleiterAgentService.cs`
- `src/Softwareschmiede/Application/Services/SessionManagementService.cs`
- `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContext.cs`
- `src/Softwareschmiede/Migrations/20260821160341_AddAutonomAufgabeMaxLengthConstraints.cs` (neu)
- `src/Softwareschmiede/Migrations/20260821160341_AddAutonomAufgabeMaxLengthConstraints.Designer.cs` (neu)
- `src/Softwareschmiede/Migrations/SoftwareschmiededDbContextModelSnapshot.cs`
- `src/Softwareschmiede/appsettings.json`

## Hinweise

- Die leeren `Up`/`Down`-Methoden in der neuen Migration `AddAutonomAufgabeMaxLengthConstraints` wurden geprüft und sind für SQLite korrekt: SQLite erzwingt keine `VARCHAR(n)`-Längenbegrenzung auf TEXT-Spalten, weshalb der EF-Core-SQLite-Migrationsgenerator für reine `HasMaxLength`-Änderungen keine SQL-Operationen erzeugt. Kein Befund.
- Umbenennung `AutonomAufgabeStartCoordinator`/`AutonomAufgabeStartErgebnis` → `AutonomAufgabeStartService`/`AutonomAufgabeStartResult`: alle Referenzen (Produktivcode, Tests, DI-Registrierung) wurden konsistent aktualisiert; keine verwaisten Referenzen gefunden.
- Entfernung der beiden `InitialisiereAsync`-Überladungen mit Einzelparametern aus `AutonomAufgabenInitialisierungsService`: es existiert nur noch ein Aufrufer (`AutonomAufgabeInitialisierungsDialogViewModel`), der bereits auf die `AutonomAufgabeInitialisierungsAnfrage`-Überladung umgestellt ist. Kein Befund.
- `DirectoryAccessGuard` (neu) und die vorbestehenden `GitKlonHelper`/`StateJsonHelper`-Hilfsklassen werden jetzt konsistent in `AutonomAufgabenInitialisierungsService` und `ProjektleiterAgentService` verwendet; keine verbleibende Duplikation der jeweiligen Try/Catch- bzw. State-Lese-/Schreiblogik gefunden.
