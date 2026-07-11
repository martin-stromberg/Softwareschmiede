# Tests

## Testklassen

### `TaskDetailViewModelTests`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`

Tests für `TaskDetailViewModel`. Setzt ein vollständiges Test-Setup mit Datenbank, Services und Mocks auf.

Relevante Test-Fixtures / Setup-Code:
- `_promptVorlagenService` – Service zum Laden von Promptvorlagen
- `_kiService` – Service zum Verwalten laufender CLI-Prozesse
- `_promptVorlagenPlatzhalterService` – Service zum Auflösen von Platzhaltern im Prompttext
- `_aufgabeService`, `_protokollService`, `_entwicklungsprozessService` – weitere unterstützende Services

Keine bestehenden Tests für zeitgesteuerten Prompt-Versand (Feature ist neu).

---

### `PromptVorlagenServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/PromptVorlagenServiceTests.cs`

Tests für `PromptVorlagenService`:

- `EnsureInitialPromptVorlagenAsync_LegtDreiStandardvorlagenAn` – Prüft, dass initial drei Standardvorlagen angelegt werden
- `EnsureInitialPromptVorlagenAsync_MitBestehenderVorlage_LegtKeineDuplikateAn` – Prüft, dass keine Duplikate angelegt werden, wenn bereits Vorlagen existieren
- `PromptVorlagenPlatzhalterService_Resolve_ErsetztBekanntePlatzhalter` – Prüft Platzhalter-Auflösung im Prompttext

Keine bestehenden Tests für zeitgesteuerten Versand.

---

### `PseudoConsoleSessionTests`
Datei: `src/Softwareschmiede.Tests/Infrastructure/Terminal/PseudoConsoleSessionTests.cs`

Tests für `PseudoConsoleSession`. Keine relevanten Details für zeitgesteuerten Prompt-Versand vorhanden (Feature ist neu).

---

### `KiAusfuehrungsServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/KiAusfuehrungsServiceTests.cs`

Tests für `KiAusfuehrungsService`. Prüfen das Starten, Stoppen und Verwalten laufender CLI-Prozesse.

Keine bestehenden Tests für zeitgesteuerten Versand.

---

## Hilfsmethoden

### `TaskDetailViewModelTestFactory`
Datei: `src/Softwareschmiede.Tests/Helpers/TaskDetailViewModelTestFactory.cs`

Hilfsfunktionen zum Erzeugen von Test-`TaskDetailViewModel`-Instanzen mit vollständigem Setup.

---

## Datenbank-Test-Fixtures

### `TestDbContextFactory`
Bietet In-Memory-Datenbank-Kontexte für Unit-Tests, mit initialen Daten für `Projekte`, `GitRepositories`, `Aufgaben` etc.

