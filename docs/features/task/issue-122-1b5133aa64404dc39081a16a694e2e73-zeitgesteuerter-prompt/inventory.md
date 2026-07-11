# Bestandsaufnahme: Zeitgesteuerter Prompt

Analyse der bestehenden Codebasis bezogen auf die Anforderung „Zeitgesteuerter Prompt" (Issue #122). Der Fokus liegt auf bereits vorhandenen Komponenten für Prompt-Verwaltung, CLI-Versand und Terminal-Interaktion.

---

## Zusammenfassung

### Was ist bereits vorhanden

- **`PromptVorlage`-Entität:** Persistierte Promptvorlagen mit Name, Prompttext, Sortierung und Zeitstempel
- **`PromptVorlagenService`:** Service zum Verwalten, Abrufen und Initialisieren von Standardvorlagen
- **`PromptVorlagenPlatzhalterService`:** Service zur Auflösung von Platzhaltern im Prompttext (z. B. %TaskName%, %ProjectName%)
- **`TaskDetailViewModel`:** Umfassendes ViewModel mit Properties zur Verwaltung der Aufgabe, CLI-Status und Prompt-Versand
  - `PromptVorlagen` (ObservableCollection)
  - `SelectedPromptVorlage` (bindbar)
  - `KannPromptVorlageSenden` (Boolean)
  - `PromptVorlageAuswaehlenCommand` (AsyncRelayCommand)
  - `PromptVorlageGesendet` (Event)
  - Methode `GetPseudoConsoleSession()` zum Abrufen der aktiven CLI-Session
- **`PseudoConsoleSession`:** Verwaltung einer laufenden Terminal-Sitzung mit schreibbarem `InputStream` zum Versand von Prompts
- **`KiAusfuehrungsService`:** Singleton-Service zum Verwalten laufender CLI-Prozesse, Methode `GetPseudoConsoleSession(aufgabeId)` zum Abrufen der Session
- **UI: `TaskDetailView.xaml`:** Ribbon-Menü mit Promptvorlage-ComboBox, Eingabefelder, Buttons und Status-Text
- **Tests:** Grundgerüste für `TaskDetailViewModelTests`, `PromptVorlagenServiceTests`, `PseudoConsoleSessionTests`, `KiAusfuehrungsServiceTests`

### Was fehlt (noch zu implementieren)

- **`PromptZeitVersandService`:** Neuer Service für zeitgesteuerten Versand (Warteschlange, Timer, Versand bei Erreichen der Zielzeit)
- **`TaskDetailViewModel`-Erweiterungen:** Properties für Stunde/Minute-Eingaben, Statusanzeige und Command für zeitgesteuerten Versand
- **UI-Erweiterungen:** TextBox-Eingabefelder für Stunde und Minute, Button für zeitgesteuerten Versand, Statusanzeige
- **Tests:** Umfassende Unit-Tests für `PromptZeitVersandService` und `TaskDetailViewModel`-Erweiterungen
- **Validierungslogik:** Range-Validierung für Stunde (0–23) und Minute (0–59)

---

## Details

- [Datenmodell](inventory/models.md)
- [Logik](inventory/logic.md)
- [Tests](inventory/tests.md)
- [UI-Komponenten](inventory/ui.md)
