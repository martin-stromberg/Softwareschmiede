# Bestandsaufnahme: Promptvorlagen

Analysiert wurden die vorhandenen Einstellungen, die Aufgabendetailansicht mit Ribbon und Terminal, die CLI-Ausfuehrung sowie das Projekt-/Aufgaben-/Repository-Datenmodell mit Persistenzmechanismen.

## Zusammenfassung

- Die WPF-Anwendung nutzt `SettingsViewModel` und `SettingsView.xaml` fuer allgemeine, SCM- und KI-Plugin-Einstellungen; ein Bereich fuer Promptvorlagen existiert dort noch nicht.
- `TaskDetailView` besitzt ein Ribbon mit Gruppen fuer Navigation, Aufgabe, CLI und Issue; eine Auswahlbox fuer Promptvorlagen ist noch nicht vorhanden.
- CLI-Eingaben werden aktuell vom `TerminalControl` direkt als Bytes in `PseudoConsoleSession.InputStream` geschrieben. Eine ViewModel- oder Service-Methode zum Senden eines fertigen Prompttexts an die laufende CLI existiert noch nicht.
- Kontextdaten fuer Platzhalter sind im geladenen Aufgabendetail vorhanden: `Aufgabe.Projekt.Name`, `Aufgabe.Titel` und `Aufgabe.GitRepository.RepositoryUrl`.
- Persistenz erfolgt ueber EF Core/SQLite mit Migrationen beim App-Start. Es gibt `AppEinstellung` als Key-Value-Speicher und eigene Tabellen fuer Projekte, Repositories und Aufgaben; ein `PromptVorlage`-Modell, DbSet oder Seed-Mechanismus fuer Promptvorlagen ist nicht vorhanden.
- Vorhandene Tests decken Settings-ViewModel, TaskDetail-ViewModel, CLI-Session-Anbindung, Terminaleingaben und aeltere Blazor-Folgeprompt-Logik ab.

## Details

- [Datenmodell](inventory/models.md)
- [Logik und UI](inventory/logic.md)
- [Enums](inventory/enums.md)
- [Interfaces](inventory/interfaces.md)
- [Tests](inventory/tests.md)
