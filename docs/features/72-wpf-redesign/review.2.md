# Plan-Review: Projektdetailansicht

## Ergebnis

**Status:** Offene Aufgaben vorhanden

## Umgesetzte Planelemente

### ViewModel-Eigenschaften
- [x] `ProjektName` (string) — Eigenschaft vorhanden und bearbeitbar
- [x] `ProjektBeschreibung` (string?) — Eigenschaft vorhanden und bearbeitbar
- [x] `SelectedRepository` (GitRepository?) — Eigenschaft vorhanden
- [x] `AufgabenFilter` (AufgabenFilterTyp) — Eigenschaft vorhanden
- [x] `IsFilterOverlayVisible` (bool) — Eigenschaft vorhanden (für Filter-Overlay-Steuerung)

### ViewModel-Commands
- [x] `ZurueckCommand` — Command vorhanden, implementiert als RelayCommand
- [x] `SpeichernCommand` — Command vorhanden, implementiert als AsyncRelayCommand mit Validierung
- [x] `LoeschenCommand` — Command vorhanden, implementiert als AsyncRelayCommand
- [x] `FilterCommand` — Command vorhanden, implementiert als RelayCommand
- [x] `RepositoryZuweisenCommand` — Command vorhanden, implementiert als AsyncRelayCommand
- [x] `RepositoryOeffnenCommand` — Command vorhanden, implementiert als AsyncRelayCommand

### ViewModel-Methoden
- [x] `LadenAsync` (erweitert) — Lädt Projekt, Aufgaben und Repositories, setzt bearbeitbare Eigenschaften
- [x] `ProjektSpeichernAsync` — Speichert neues oder bestehendes Projekt
- [x] `ProjektLoeschenAsync` — Löscht Projekt mit Bestätigungsabfrage (LoeschenBestaetigenFunc)
- [x] `RepositoryZuweisenAsync` — Öffnet Repository-Zuweisungs-Dialog und ruft AddRepositoryAsync auf
- [x] `RepositoryOeffnenAsync` — Öffnet Repository-URL im Standard-Browser mit Process.Start

### UI-Komponenten
- [x] `ProjectDetailView.xaml` — Ribbon-Menü mit 4 Gruppen (Navigation, Projekt, Aufgaben, Repository)
- [x] `Projekt-Kachel` — Border mit TextBox für Name und Beschreibung, Emoji-Icon 📁
- [x] `Aufgaben-Kachel` — ListBox mit Aufgabenliste und Doppelklick-Handler
- [x] `Filter-Overlay-Panel` — RadioButton-Auswahl für Alle/Aktiv/Archiviert
- [x] Einfache Aufgabenliste wurde durch Kachel-Layout ersetzt

### Neue Klassen
- [x] `RepositoryAssignDialog` (UserControl) — Dialog mit ListBox und Bestätigung/Abbruch-Buttons
- [x] `RepositoryAssignDialog.xaml.cs` — Code-behind mit DataContext und CloseRequested-Event
- [x] `RepositoryAssignViewModel` — ViewModel mit VerfuegbareRepositories, SelectedRepository und Commands

### Sonstige
- [x] `AufgabenFilterTyp`-Enum mit Werten: Alle, Aktiv, Archiviert
- [x] Navigation über ZurueckCommand mit ZurueckAction-Callback

## Offene Aufgaben

### Unit- und Integrationstests
- [ ] `ProjektSpeichernAsync_Success` Test — bereits vorhanden als `ProjektSpeichernAsync_ErstelltNeuesProjekt_WennIdLeer` und `ProjektSpeichernAsync_AktualisiertBestehendesProjekt_WennIdVorhanden`
  - Status: **Teilweise abgedeckt** — Tests vorhanden, aber nicht mit den exakten Namen aus dem Plan. Hinzugefügt: `ProjektSpeichernAsync_Success_RuftProjektHinzugefuegtCallbackAuf`
  
- [ ] `ProjektSpeichernAsync_ValidationError` Test — **Vorhanden** als `ProjektSpeichernAsync_ValidationError_CanExecuteFalse_WhenNameLeer` und `ProjektSpeichernAsync_ValidationError_CanExecuteFalse_WhenNameNurLeerzeichen`

- [ ] `ProjektLoeschenAsync_Success` Test — **Vorhanden** als `ProjektLoeschenAsync_Success_RuftDeleteAsyncUndZurueckActionAuf`

- [ ] `ProjektLoeschenAsync_Aborted` Test — **Vorhanden** als `ProjektLoeschenAsync_Aborted_RuftDeleteAsyncNichtAuf`

- [ ] `RepositoryZuweisenAsync_Success` Test — **Vorhanden** als `RepositoryZuweisenAsync_Success_RuftAddRepositoryAsyncAuf`

- [ ] `RepositoryOeffnenAsync_Success` Test — **Vorhanden** als `RepositoryOeffnenAsync_Success_OeffnetRepositoryUrl`

### E2E-Tests
Alle E2E-Tests sind mit `Skip = SkipReason` Attribut übersprungen und befinden sich in `WpfE2EPlaceholderTests.cs`, nicht in einer dedizierten `ProjectDetailE2ETests`-Klasse:

- [ ] Projekt bearbeiten und speichern E2E-Test — **Fehlt vollständig** — nicht in `ProjectDetailE2ETests` implementiert
- [ ] Projekt löschen E2E-Test — **Fehlt vollständig**
- [ ] Aufgabe neu anlegen E2E-Test — **Fehlt vollständig**
- [ ] Aufgaben filtern E2E-Test — **Fehlt vollständig**
- [ ] Repository zuweisen E2E-Test — **Fehlt vollständig**
- [ ] Repository öffnen E2E-Test — **Fehlt vollständig**
- [ ] Zurück zur Übersicht E2E-Test — **Fehlt vollständig**

**Hinweis:** Die bestehenden E2E-Placeholder-Tests (`WpfE2EPlaceholderTests.cs`) sind nicht spezifisch für die Projektdetailansicht und alle übersprungen.

## Hinweise

1. **Unit-Tests sind vorhanden, folgen aber anderen Namenskonventionen**: Die Tests wurden teilweise mit beschreibenden Namen implementiert (z.B. `ErstelltNeuesProjekt_WennIdLeer` statt `Success`). Dies ist eigentlich eine bessere Praxis, aber weicht vom Plan ab.

2. **E2E-Tests fehlen**: Der Plan sieht 7 E2E-Tests für spezifische Szenarien vor (Projektdetailansicht, nicht die allgemeinen Dashboard-Tests). Diese sollten in einer neuen `ProjectDetailE2ETests`-Klasse mit FlaUI implementiert werden.

3. **RepositoryZuweisenAsync-Dialog-Limitation**: Der Dialog kann nicht vollständig in Unit-Tests getestet werden, da er einen GUI-Thread benötigt. Der vorhandene Test simuliert nur den Service-Aufruf nach erfolgreicher Auswahl.

4. **Abhängigkeiten**: Die Unit-Tests verwenden korrekt TestDbContextFactory und Mocks für IServiceProvider. Die E2E-Tests würden die Anwendung mit FlaUI starten und benötigen eine Debug-Build der WPF-App.

5. **Bestätigungsdialog**: Die Lösch-Bestätigung ist durch `LoeschenBestaetigenFunc` (ein public Func mit MessageBox als Default) implementiert, was testbar macht.

6. **Filter-Implementierung**: Der Filter selbst ist im ViewModel vorhanden (AufgabenFilter-Eigenschaft), aber die tatsächliche Filterlogik (nur gefilterte Aufgaben in der ListBox anzeigen) ist im View nicht sichtbar implementiert. Die ListBox zeigt alle Aufgaben an, der Filter ist nur im UI vorhanden.
