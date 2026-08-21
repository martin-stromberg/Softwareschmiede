# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### TaskDetailViewModel.cs, TaskDetailViewModelTests.cs, TaskDetailViewModelTestsBase.cs, TaskDetailViewModelTests_PluginAktivierung.cs, TaskDetailViewModelTests_ZeitgesteuerterPrompt.cs, TaskDetailViewModelTestFactory.cs

- **Namenskonventionen und Einheitlichkeit** — Die Klasse `AutonomAufgabeStartCoordinator` wurde bereits in einer früheren Iteration konsistent zu `AutonomAufgabeStartService` umbenannt (Namenskonvention `...Service`-Suffix). Das dazugehörige Feld/der Konstruktorparameter in `TaskDetailViewModel.cs` sowie die lokale Variable in allen vier `TaskDetailViewModelTests*`-Dateien und in `TaskDetailViewModelTestFactory.cs` heißen jedoch weiterhin `_autonomAufgabeStartCoordinator` / `autonomAufgabeStartCoordinator` und tragen damit noch den alten Klassennamen, obwohl der Typ `AutonomAufgabeStartService` ist:
  - `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs` Zeile 50 (Feld), 592 (Parameter), 610 (Zuweisung), 1215 (Verwendung)
  - `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs` Zeile 176
  - `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTestsBase.cs` Zeile 134
  - `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_PluginAktivierung.cs` Zeile 81
  - `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_ZeitgesteuerterPrompt.cs` Zeile 94
  - `src/Softwareschmiede.Tests/Helpers/TaskDetailViewModelTestFactory.cs` Zeile 57

  Empfehlung: Feld, Konstruktorparameter und lokale Variablen an allen genannten Stellen von `_autonomAufgabeStartCoordinator`/`autonomAufgabeStartCoordinator` auf `_autonomAufgabeStartService`/`autonomAufgabeStartService` umbenennen, damit der Bezeichner wieder zum tatsächlichen Typnamen passt.

### ProjektleiterAgentServiceTests_Fehlerfaelle.cs (ProjektleiterAgentServiceTests_Fehlerfaelle)

- **Doppelter Code** — Die private Methode `ErstelleKonfigurationAsync()` (Zeilen 47-80) ist inhaltlich nahezu identisch mit `ErstelleAutonomeAufgabeAsync()` in der bestehenden `src/Softwareschmiede.Tests/Application/Services/ProjektleiterAgentServiceTests.cs` (Zeilen 75-108): beide legen dieselbe `Aufgabe`/`AutonomAufgabeKonfiguration`-Kombination mit denselben Werten an und schreiben dieselben drei Dateien (`plan.md`, `progress.md`, `state.json`) in `_testRoot`. Ebenso ist `ErstelleUnteragent(...)` in `ProjektleiterAgentServiceTests_Fehlerfaelle.cs` (Zeilen 82-93) eine private Kopie ohne Gegenstück in der Schwesterklasse, aber vom gleichen Muster.

  Empfehlung: Die gemeinsame Arrange-Logik (Aufgabe+Konfiguration anlegen, Basisdateien schreiben, Unteragent-Objekt bauen) in eine gemeinsam genutzte Test-Helper-Klasse (z. B. `ProjektleiterAgentServiceTestHelper` oder eine `partial class`-Basis analog zu `TaskDetailViewModelTestsBase`) extrahieren, die von beiden `ProjektleiterAgentServiceTests`-Klassen verwendet wird.

## Geprüfte Dateien

- `src/Softwareschmiede.App/App.xaml.cs`
- `src/Softwareschmiede.App/Services/AutonomAufgabeStartCoordinator.cs` (gelöscht)
- `src/Softwareschmiede.App/Services/AutonomAufgabeStartErgebnis.cs` (gelöscht)
- `src/Softwareschmiede.App/Services/AutonomAufgabeStartResult.cs`
- `src/Softwareschmiede.App/Services/AutonomAufgabeStartService.cs`
- `src/Softwareschmiede.App/Services/WpfDialogService.cs`
- `src/Softwareschmiede.App/ViewModels/AutonomAufgabeDetailViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/AutonomAufgabeInitialisierungsDialogViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
- `src/Softwareschmiede.App/Views/AutonomAufgabeInitialisierungsDialog.xaml`
- `src/Softwareschmiede.App/Views/AutonomAufgabeInitialisierungsDialog.xaml.cs`
- `src/Softwareschmiede.Tests/App/Services/AutonomAufgabeStartServiceTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/AutonomAufgabeDetailViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/AutonomAufgabeInitialisierungsDialogViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/AutonomAufgabeInitialisierungsDialogViewModelTests_BranchUndVorlagen.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTestsBase.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_PluginAktivierung.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_ZeitgesteuerterPrompt.cs`
- `src/Softwareschmiede.Tests/Application/Services/AutonomAufgabenInitialisierungsServiceTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/ProjektleiterAgentServiceTests_Fehlerfaelle.cs`
- `src/Softwareschmiede.Tests/Application/Services/SessionManagementServiceTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/UnteragentGovernanceServiceTests.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_AutonomAufgabenInitialisierung.cs`
- `src/Softwareschmiede.Tests/Helpers/TaskDetailViewModelTestFactory.cs`
- `src/Softwareschmiede/Application/Services/AutonomAufgabenInitialisierungsService.cs`
- `src/Softwareschmiede/Application/Services/AutonomAufgabenOptions.cs`
- `src/Softwareschmiede/Application/Services/DirectoryAccessGuard.cs`
- `src/Softwareschmiede/Application/Services/GitKlonHelper.cs`
- `src/Softwareschmiede/Application/Services/ProjektleiterAgentService.cs`
- `src/Softwareschmiede/Application/Services/SessionManagementService.cs`
- `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContext.cs`
- `src/Softwareschmiede/Migrations/20260821160341_AddAutonomAufgabeMaxLengthConstraints.cs`
- `src/Softwareschmiede/Migrations/20260821160341_AddAutonomAufgabeMaxLengthConstraints.Designer.cs`
- `src/Softwareschmiede/Migrations/SoftwareschmiededDbContextModelSnapshot.cs`
- `src/Softwareschmiede/appsettings.json`

## Hinweise

- Die beiden Befunde aus dem vorherigen Review (`review-code.4.md`) wurden korrekt behoben und beim erneuten Review verifiziert:
  - `AutonomAufgabeDetailViewModel.cs`: Der dreifach duplizierte Null-Guard-Block in `StarteAgentAsync`/`StoppeAgentAsync`/`ResumeAgentAsync` wurde in `PruefeAufgabeInitialisiert()` (mit `[MemberNotNullWhen(true, nameof(_aufgabe))]`) extrahiert; alle drei Methoden nutzen sie konsistent, die Nullability-Narrowing von `_aufgabe` bleibt für die nachfolgende Verwendung erhalten.
  - `AutonomAufgabeStartService.cs`: `StarteAsync` deklariert `aktuelleAufgabe` jetzt vor dem try-Block, aktualisiert sie nach jedem `GetDetailAsync`-Aufruf mit Fallback auf den zuletzt bekannten Stand und gibt sie im catch-Block über `new AutonomAufgabeStartResult(aktuelleAufgabe, ...)` zurück statt `null`. Der neue Regressionstest `AutonomAufgabeStartServiceTests.StarteAsync_GibtBereitsGeladeneAufgabeZurueck_BeiFehlerWaehrendInitialisierung` deckt den Fehlerpfad ab und ist sauber nach Arrange-Act-Assert strukturiert.
- `DirectoryAccessGuard`, `GitKlonHelper.KloneFallsNichtVorhandenAsync` und `StateJsonHelper` werden konsistent in `AutonomAufgabenInitialisierungsService`/`ProjektleiterAgentService`/`SessionManagementService` verwendet; keine verbleibende Duplikation der jeweiligen Try/Catch- bzw. State-Lese-/Schreiblogik gefunden.
- Die Entfernung der beiden Convenience-Overloads von `AutonomAufgabenInitialisierungsService.InitialisiereAsync` ist unauffällig: Es existiert nur noch ein Aufrufer (`AutonomAufgabeInitialisierungsDialogViewModel.BestaetigenAsync`), der bereits auf die `(Aufgabe, AutonomAufgabeInitialisierungsAnfrage, ct)`-Überladung umgestellt ist; keine verwaisten Referenzen auf die entfernten Überladungen gefunden.
- Umbenennung `MaxConcurrentSubagents`→`MaxConcurrentUnteragenten`, `SkillAutoGenerationEnabled`→`SkillAutogenerationEnabled` sowie die neuen `MaxClones`/`MaxFeatureBranches`-Konfigurationswerte sind zwischen `AutonomAufgabenOptions.cs`, `appsettings.json` und allen Verwendungsstellen konsistent; keine verwaisten Referenzen auf die alten Namen gefunden.
- Alle neuen/erweiterten Testmethoden in den geprüften Testdateien sind einzelfallbezogen (ein Fact prüft einen fachlichen Fall) und folgen der Arrange-Act-Assert-Struktur.
