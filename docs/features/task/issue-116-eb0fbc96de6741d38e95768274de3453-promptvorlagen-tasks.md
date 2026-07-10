# Tasks: Promptvorlagen

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | Entity `PromptVorlage` mit `Id`, `Name`, `Prompttext`, `Sortierung`, `ErstelltAm` und `AktualisiertAm` anlegen | Offen | Service-/DbContext-Tests nach Implementierung |
| 2 | Persistenz | `SoftwareschmiededDbContext` um `DbSet<PromptVorlage>` und Mapping inklusive Pflichtfeldern und Sortierungsindex erweitern | Offen | Migration/DbContext-Test nach Implementierung |
| 3 | Persistenz | EF-Core-Migration fuer die Tabelle `PromptVorlagen` erstellen | Offen | `dotnet test` bzw. Migration-Anwendung beim Start |
| 4 | Service | `PromptVorlagenService` mit Laden, Erstellen, Aktualisieren, Loeschen und sortierter Rueckgabe implementieren | Offen | Neue `PromptVorlagenServiceTests` |
| 5 | Initialdaten | `EnsureInitialPromptVorlagenAsync` idempotent implementieren und exakt die drei geforderten Initialvorlagen anlegen | Offen | Tests fuer erstmaliges Seeding, kein Duplikat, kein Ueberschreiben |
| 6 | Platzhalter | `PromptVorlagenPlatzhalterService` fuer `%ProjectName%`, `%TaskName%` und `%RepositoryUrl%` implementieren | Offen | Resolver-Tests fuer vollstaendige und fehlende Kontextwerte |
| 7 | Platzhalter | Fehlende Werte, insbesondere fehlende `RepositoryUrl`, deterministisch als leeren Text aufloesen; unbekannte Platzhalter unveraendert lassen | Offen | Resolver-Test `FehlendesRepository_ShouldUseEmptyText` |
| 8 | DI/Startup | Neue Services in `App.xaml.cs` registrieren und Initialdaten nach `db.Database.MigrateAsync()` seeden | Offen | App-/Service-Test oder Startverifikation |
| 9 | Settings ViewModel | `SettingsViewModel` um Promptvorlagen-Collection, Add-/Delete-Commands und Dirty-State fuer Vorlagen erweitern | Offen | `SettingsViewModelTests` fuer Laden, Hinzufuegen und Loeschen |
| 10 | Settings Speichern | Speichern/Verwerfen fuer Promptvorlagen in `SettingsViewModel` integrieren und Pflichtfelder validieren | Offen | `SettingsViewModelTests` fuer Speichern, Verwerfen und Validierung |
| 11 | Settings UI | `SettingsView.xaml` um Tab `Promptvorlagen` mit Liste, Name-Feld, mehrzeiligem Prompttext und Hinzufuegen-/Loeschen-Aktionen erweitern | Offen | Manuelle Sichtpruefung oder UI-Test |
| 12 | TaskDetail ViewModel | `TaskDetailViewModel` laedt verfuegbare Promptvorlagen beim Oeffnen der Aufgabe | Offen | `TaskDetailViewModelTests` fuer geladene Auswahl |
| 13 | CLI-Versand | `TaskDetailViewModel` sendet bei Vorlagenauswahl den aufgeloesten Prompt plus Zeilenende sofort an die aktive `PseudoConsoleSession` | Offen | `TaskDetailViewModelTests` fuer InputStream-Inhalt |
| 14 | CLI-Stabilitaet | Versand ohne laufende CLI-Session oder mit leerem Prompt stabil abbrechen, ohne Exception | Offen | `TaskDetailViewModelTests` fuer fehlende Session |
| 15 | TaskDetail UI | `TaskDetailView.xaml` um Ribbon-Auswahlbox fuer Promptvorlagen in der CLI-Gruppe erweitern | Offen | Manuelle Sichtpruefung oder UI-Test |
| 16 | TaskDetail UI | Auswahl nach erfolgreichem Versand zuruecksetzen und deaktivieren, wenn keine Vorlagen oder keine CLI-Session verfuegbar sind | Offen | ViewModel-Test fuer Reset/CanExecute, manuelle Sichtpruefung |
| 17 | Testinfrastruktur | `TaskDetailViewModelTestFactory` und bestehende `SettingsViewModelTests` an neue Konstruktorabhaengigkeiten anpassen | Offen | Bestehende Tests kompilieren und laufen gruen |
| 18 | Regression | Gesamte Test-Suite ausfuehren und relevante manuelle UI-Pruefung fuer Settings-Tab und Ribbon-Auswahl dokumentieren | Offen | `dotnet test` und dokumentierte Sichtpruefung |
