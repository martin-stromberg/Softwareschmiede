# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs (TaskDetailViewModel)

- **Toter Code / redundante Fehlerbehandlung** — In der Methode, die `AutonomAufgabeInitialisieren` behandelt (um `_autonomAufgabeStartCoordinator.StarteAsync(_aufgabe, ct)`), wurde ein neuer `try { ... } catch (OperationCanceledException) { throw; } catch (Exception ex) { ...; FehlerMeldung = ...; }`-Block ergänzt. `AutonomAufgabeStartService.StarteAsync` (`src/Softwareschmiede.App/Services/AutonomAufgabeStartService.cs`, Zeilen 33–67) fängt jedoch bereits selbst **alle** Exceptions außer `OperationCanceledException` ab und gibt sie als `AutonomAufgabeStartResult.FehlerMeldung` zurück, statt sie zu werfen. Der neue `catch (Exception ex)`-Block in `TaskDetailViewModel` ist dadurch im Normalfall unerreichbar (Dead Code) — dieselbe Fehlerbedingung wird an zwei Stellen redundant behandelt, was beim nächsten Refactoring leicht auseinanderlaufen kann.

  Empfehlung: Fehlerbehandlung auf eine Schicht konzentrieren. Entweder `AutonomAufgabeStartService.StarteAsync` weiterhin alle Exceptions abfangen lassen und in `TaskDetailViewModel` nur noch `ergebnis.FehlerMeldung` auswerten (den neuen `try/catch` dort entfernen), oder umgekehrt `AutonomAufgabeStartService` nicht mehr fangen lassen und die Fehlerbehandlung ausschließlich im ViewModel belassen.

### src/Softwareschmiede/Application/Services/AutonomAufgabenInitialisierungsService.cs (AutonomAufgabenInitialisierungsService) / src/Softwareschmiede/Application/Services/ProjektleiterAgentService.cs (ProjektleiterAgentService)

- **Doppelter Code** — Der Block `catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { throw new DirectoryAccessException(pfad, ex); }` kommt jetzt dreimal identisch vor: zweimal in `AutonomAufgabenInitialisierungsService` (`ErstelleArbeitsverzeichnisStrukturAsync`, Zeile ~127, und neu in `InitialisiereAsync` beim Schreiben von `permissions.json`/`state.json`, Zeile ~59) sowie neu in `ProjektleiterAgentService.SteuereUnteragentAsync` beim `Directory.CreateDirectory(unteragent.AgentDirectory)` (Zeile ~80). Das Projekt hat für vergleichbare Wiederholungen bereits Hilfsklassen (`GitKlonHelper`, `StateJsonHelper`) eingeführt; dieses Muster fehlt hier.

  Empfehlung: Eine gemeinsame Hilfsmethode extrahieren, z. B. eine statische Helper-Methode `DirectoryAccessGuard.RunAsync(string pfad, Func<Task> aktion)` (oder synchron für `Directory.CreateDirectory`), die den Catch-Filter und das Wrapping in `DirectoryAccessException` kapselt, und diese an allen drei Stellen verwenden.

### src/Softwareschmiede.App/ViewModels/AutonomAufgabeInitialisierungsDialogViewModel.cs (AutonomAufgabeInitialisierungsDialogViewModel)

- **Kopplung/Konsistenz — optionale DI-Abhängigkeiten ohne fachlichen Grund** — Der Konstruktor nimmt `IPluginManager? pluginManager = null`, `PromptVorlagenService? promptVorlagenService = null` und `PromptVorlagenPlatzhalterService? promptVorlagenPlatzhalterService = null` als nullable Parameter mit Default `null` entgegen. Alle drei Services sind aber reguläre DI-Registrierungen in `App.xaml.cs` (`services.AddScoped<PromptVorlagenService>()` Zeile 205, `services.AddSingleton<PromptVorlagenPlatzhalterService>()` Zeile 207, `services.AddSingleton<IPluginManager>(...)` Zeile 254) und werden in `TaskDetailViewModel` (`src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`, Konstruktorparameter Zeilen ~581–585) für exakt dieselben Services als **nicht-nullable Pflichtparameter ohne Default** injiziert. Die Optionalität in diesem neuen ViewModel dient erkennbar nur der bequemeren Testkonstruktion, sorgt in Produktion aber dafür, dass eine fehlerhafte DI-Registrierung nicht mehr fehlschlägt, sondern das Feature (Branch-Auswahl, Promptvorlagen) still deaktiviert wird (`LadeProjektBranchesAsync` fällt z. B. lautlos auf manuelle Eingabe zurück, `LadePromptVorlagenAsync` gibt lautlos `return` zurück). Das widerspricht dem in `TaskDetailViewModel` etablierten Fail-Fast-Verhalten für dieselben Abhängigkeiten.

  Empfehlung: `IPluginManager`, `PromptVorlagenService` und `PromptVorlagenPlatzhalterService` als reguläre (nicht-nullable, ohne Default) Konstruktorparameter deklarieren, analog zu `TaskDetailViewModel`. In Tests explizite Mocks/Instanzen übergeben statt sich auf die Default-`null`-Bequemlichkeit zu verlassen.

### src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContext.cs (SoftwareschmiededDbContext)

- **Migration/Model-Snapshot-Drift** — Es wurden neue Spaltenconstraints ergänzt: `UnteragentSpezifikation.AgentId`/`TaskId`/`AgentScope` erhalten jetzt `.HasMaxLength(255)`, `SkillDefinition.SkillName` `.HasMaxLength(255)` und `SkillDefinition.SkillVersion` `.HasMaxLength(64)`. Die zugehörige (bereits eingecheckte) Migration `src/Softwareschmiede/Migrations/20260820175118_AddAutonomAufgabeModels.cs` sowie `src/Softwareschmiede/Migrations/SoftwareschmiededDbContextModelSnapshot.cs` wurden dabei **nicht** angepasst — beide definieren diese Spalten weiterhin ohne Längenbegrenzung. Damit weicht das EF-Core-Modell von der letzten Migration/vom Snapshot ab; ein nachfolgendes `dotnet ef migrations add` würde "pending model changes" melden, und bestehende Installationen erhalten die neue Constraint nie über eine Migration appliziert (Upgrade-Sicherheit, siehe auch die `entityframework-database`-Richtlinien dieses Projekts).

  Empfehlung: Für die geänderten Constraints eine neue EF-Core-Migration erzeugen (bzw. `SoftwareschmiededDbContextModelSnapshot.cs` und ggf. die bestehende Migration konsistent nachziehen, falls sie noch nicht veröffentlicht wurde), sodass Modell, Migration und Snapshot wieder synchron sind.

### src/Softwareschmiede.App/ViewModels/AutonomAufgabeDetailViewModel.cs (AutonomAufgabeDetailViewModel)

- **Fehlende Rückmeldung bei No-Op** — `StarteAgentAsync` wurde um eine neue Vorbedingungsprüfung ergänzt: Ist `_aufgabe is null` (d. h. `Initialize` wurde nicht aufgerufen), wird `Task.CompletedTask` zurückgegeben, ohne `ErrorMessage` zu setzen oder zu loggen (Zeilen 150–161). Das entspricht zwar dem bereits vorhandenen Muster von `StoppeAgentAsync`/`ResumeAgentAsync`, führt aber dazu, dass ein Klick auf „Start“ in diesem Zustand für den Anwender wirkungslos bleibt, ohne jede Rückmeldung, warum nichts passiert.

  Empfehlung: In allen drei Methoden (`StarteAgentAsync`, `StoppeAgentAsync`, `ResumeAgentAsync`) im `_aufgabe is null`-Fall zusätzlich eine aussagekräftige `ErrorMessage` setzen (z. B. „Aufgabe wurde nicht initialisiert.“) statt lautlos zurückzukehren.

## Geprüfte Dateien

- `src/Softwareschmiede.App/App.xaml.cs`
- `src/Softwareschmiede.App/Services/WpfDialogService.cs`
- `src/Softwareschmiede.App/Services/AutonomAufgabeStartService.cs` (neu)
- `src/Softwareschmiede.App/Services/AutonomAufgabeStartResult.cs` (neu)
- `src/Softwareschmiede.App/Services/AutonomAufgabeStartCoordinator.cs` (gelöscht)
- `src/Softwareschmiede.App/Services/AutonomAufgabeStartErgebnis.cs` (gelöscht)
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
- `src/Softwareschmiede.Tests/Application/Services/SessionManagementServiceTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/UnteragentGovernanceServiceTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/ProjektleiterAgentServiceTests_Fehlerfaelle.cs` (neu)
- `src/Softwareschmiede.Tests/E2E/E2E_AutonomAufgabenInitialisierung.cs`
- `src/Softwareschmiede.Tests/Helpers/TaskDetailViewModelTestFactory.cs`
- `src/Softwareschmiede/Application/Services/AutonomAufgabenInitialisierungsService.cs`
- `src/Softwareschmiede/Application/Services/AutonomAufgabenOptions.cs`
- `src/Softwareschmiede/Application/Services/GitKlonHelper.cs`
- `src/Softwareschmiede/Application/Services/ProjektleiterAgentService.cs`
- `src/Softwareschmiede/Application/Services/SessionManagementService.cs`
- `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContext.cs`
- `src/Softwareschmiede/appsettings.json`
