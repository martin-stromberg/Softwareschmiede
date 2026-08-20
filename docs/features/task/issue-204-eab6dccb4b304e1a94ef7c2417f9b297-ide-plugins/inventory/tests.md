# Tests

## Testklassen für IDE-Öffnen-Funktionalität

### `IdeOeffnenServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/IdeOeffnenServiceTests.cs`

Tests für den `IdeOeffnenService`.

**Verfügbare Testmethoden (Auszug):**
- `FindeSolutions_LiefertAlleSlnAlphabetischSortiert()` — Prüft, dass `.sln`-Dateien korrekt gefunden und sortiert werden
- (Weitere Testmethoden zur `OpenRepositoryInIdeAsync` wahrscheinlich vorhanden)

**Test-Fixtures:**
- `TestTempDirectoryFixture` — Verwaltet temporäre Verzeichnisse für Tests

---

### `TaskDetailViewModelTests` und verwandte
Datei: `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests*.cs`

Mehrere Test-Dateien für verschiedene Aspekte des `TaskDetailViewModel`:
- `TaskDetailViewModelTests.cs` — Haupt-Tests
- `TaskDetailViewModelTests_Arbeitsverzeichnis.cs` — Arbeitsverzeichnis-Funktionalität
- `TaskDetailViewModelTests_VisualStudioCode.cs` — IDE-spezifische Tests
- `TaskDetailViewModelTests_PluginAktivierung.cs` — Plugin-Aktivierung
- `TaskDetailViewModelTests_ZeitgesteuerterPrompt.cs` — Zeitgesteuerter Prompt-Versand
- `TaskDetailViewModelTests_Todos.cs` — To-Do-Verwaltung
- `TaskDetailViewModelTestsBase.cs` — Basis-Klasse mit gemeinsamen Setup und Hilfsmethoden

**Notizen:**
- Es gibt derzeit keine spezifischen Tests für `OeffneIdeAsync` oder die Split-Button-Funktionalität.
- Die Basis-Klasse bietet wahrscheinlich Mock-Objekte und Builder für `TaskDetailViewModel`.

---

## Test-Infrastruktur

### `TaskDetailViewModelTestsBase`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTestsBase.cs`

Basis-Test-Klasse, die gemeinsame Setup-, Tear-Down- und Hilfsmethoden für alle `TaskDetailViewModel`-Tests bereitstellt.

**Funktion:**
- Stellt Mock-Objekte für abhängige Services bereit (z. B. `IDialogService`, `IdeOeffnenService`).
- Bietet Builder-Methoden zum Erstellen von Test-`TaskDetailViewModel`-Instanzen.
- Wird von den spezialisierten Test-Klassen geerbt.

---

## Fehlende Tests für die neue Split-Button-Funktionalität

**Abgedeckt durch die Anforderung (aber noch nicht implementiert):**
- Tests für neue Commands / Properties (`OeffneIdeAuswahlCommand`, `KannIdeAuswaehlen`)
- Tests für Fallback-Verhalten (Aufruf von `OeffneIdeCommand` bei keinem/einem Einstiegspunkt)
- Tests für Auswahldialog-Anruf bei mehreren Einstiegspunkten
- E2E-Tests für Split-Button-Layout bei verschiedenen Einstiegspunkt-Anzahlen
- Interaktionstests (Haupt-Button öffnet direkt, Dropdown zeigt Dialog)

