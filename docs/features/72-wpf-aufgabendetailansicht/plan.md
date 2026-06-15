# Umsetzungsplan: Aufgabendetailansicht mit Ribbon-Menü und Status-abhängigem Content-Switching

## Übersicht

Die TaskDetailView wird erweitert um ein Ribbon-Menü (analog ProjectDetailView) mit Buttons für Speichern, Löschen, Starten und Beenden. Status-abhängiges Content-Switching zeigt unterschiedliche Panels: Edit-Panel für Status=Neu (mit TextBoxen für Titel und Anforderungsbeschreibung), CLI-Panel für laufende Status (Gestartet, InArbeit, Wartend) mit Toggle-Button zwischen CLI-Ansicht und Info-Panel, und Diff-Panel für Status=Beendet als Platzhalter. Neue Commands (SpeichernCommand, LoeschenCommand, InfoCliToggleCommand) mit validierter CanExecute-Logik sowie ein Value Converter für Sichtbarkeitsbindung.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Ribbon-Menü-Implementierung | Wiederverwendung bestehender `RibbonGroup` und `RibbonLargeButton` Controls aus ProjectDetailView | Konsistenz mit bestehender UI-Architektur, reduzierter Entwicklungsaufwand, Benutzervertrautheit mit einheitlicher Office-Stil-Navigation |
| Status-abhängiges Content-Switching | Kombination aus `AufgabeStatusToVisibilityConverter` + ViewModel-Properties (`ShowEditPanel`, `ShowCliPanel`, `ShowDiffPanel`) mit Binding-Logik | MVVM-konform, ViewModel steuert Sichtbarkeit über Properties, einfacheres Testing, WPF-native Implementierung |
| Edit-Modus und ViewModel-Commands | Vier neue Commands mit CanExecute-Logik basierend auf Status + CLI-Zustand | Konsistent mit existierenden Commands (CliStartenCommand, etc.), CanExecute verhindert ungültige Status-Übergänge auf UI-Ebene, Validierung auch Service-seitig (Defense-in-Depth) |
| Toggle-Button für Info/CLI-Ansicht | Neue Boolean-Property `IsInfoViewVisible` im ViewModel, Toggle sichtbar nur wenn Status ∈ {Gestartet, InArbeit, Wartend} | Einfach implementierbar, kein Datenverlust (nur UI-Zustand), klare Semantik: Info-Ansicht vs. CLI-Ansicht |
| Diff-Ansicht | Grid mit TextBlock "Diff wird hier angezeigt" als Platzhalter; später austauschbar mit Git-Diff-Visualisierung oder File-Tree-Control | Anforderung sagt "Implementierung folgt später", verhindert Blocking, View-Struktur ist bereits in Place, Service-Integration (GetLatestDiffResultIdAsync) ist vorhanden |
| Datenbankmigrationen | Keine neuen Tabellen/Spalten erforderlich | Existierende `Aufgabe.Titel`, `Aufgabe.AnforderungsBeschreibung` sind bereits vorhanden; AufgabeService.UpdateAsync() und DeleteAsync() existieren bereits; Status-Übergänge in ValidateStatusTransition() definiert |

## Programmabläufe

### UC1: Aufgabe erstellen und im Status "Neu" bearbeiten

1. User öffnet TaskDetailView mit einer neuen Aufgabe (Status=Neu)
2. View zeigt Edit-Panel (Titel + AnforderungsBeschreibung sind TextBox-Eingabefelder)
3. User füllt Felder aus und modifiziert über Two-Way-Binding
4. User klickt "Speichern"-Button im Ribbon (Gruppe "Aufgabe")
5. `SpeichernCommand.Execute()`:
   - IsLoading = true
   - `AufgabeService.UpdateAsync(Id, EditTitel, EditAnforderungsBeschreibung, CancellationToken)`
   - Bei Success: LadenAsync() aufrufen, Toast "Aufgabe gespeichert" anzeigen
   - Bei Exception: FehlerMeldung anzeigen
   - IsLoading = false
6. Nach Speichern: Aufgabe wird neu geladen, Status bleibt Neu, Button "Starten" wird sichtbar

Beteiligte Klassen/Komponenten: `TaskDetailViewModel`, `TaskDetailView.xaml`, `AufgabeService`, `EditTitel` Property, `EditAnforderungsBeschreibung` Property

### UC2: Aufgabe starten

1. User sieht Status=Neu, Button "Starten" ist sichtbar und enabled im Ribbon
2. User klickt Button "Starten"
3. `StatusGestartetSetzenCommand.Execute()`:
   - `AufgabeService.SetStatusAsync(Id, AufgabeStatus.Gestartet)`
   - Bei Success: LadenAsync() aufrufen
   - Property-Changed Event triggert für `AufgabeStatus`
4. ViewModel: Alle Compute-Properties aktualisieren (`ShowEditPanel` = false, `ShowCliPanel` = true, `ShowDiffPanel` = false)
5. View wechselt automatisch: Edit-Panel hidden → CLI-Panel sichtbar
6. KI-Plugin-Dropdown wird aktiv, CLI-Panel mit ProcessWindowHost bereit

Beteiligte Klassen/Komponenten: `TaskDetailViewModel`, `AufgabeService`, `ShowEditPanel`, `ShowCliPanel`, `ShowDiffPanel` Properties, `AufgabeStatusToVisibilityConverter`

### UC3: CLI starten und überwachen mit Info/CLI-Toggle

1. Status=Gestartet, KI-Plugin ist gewählt, CLI läuft noch nicht
2. User klickt Button "CLI Starten" (im Ribbon)
3. `CliStartenCommand.Execute()`:
   - `KiAusfuehrungsService.StartenAsync()` wird aufgerufen
   - IsCliRunning = true
   - ProcessWindowHost erhält Fenster-Handle
4. CLI-Panel ist sichtbar mit Toggle-Button
5. Toggle-Button ist sichtbar (Status ∈ {Gestartet, InArbeit, Wartend})
6. Wenn `IsInfoViewVisible` = false: ProcessWindowHost sichtbar (CLI-Fenster)
7. Wenn `IsInfoViewVisible` = true: Info-Panel sichtbar (Aufgabeeigenschaften + Protokolleinträge)
8. User klickt Toggle-Button → `InfoCliToggleCommand.Execute()`:
   - `IsInfoViewVisible = !IsInfoViewVisible`
   - ProcessWindowHost bzw. Info-Panel wird sichtbar/hidden
9. CLI läuft, Events werden abonniert (`KiAusfuehrungsService.CliProcessStatusChanged`)
10. User klickt "Stoppen" → `CliStoppenCommand.Execute()`:
    - IsCliRunning = false, ProcessWindowHost wird geleert

Beteiligte Klassen/Komponenten: `TaskDetailViewModel`, `KiAusfuehrungsService`, `IsInfoViewVisible` Property, `InfoCliToggleCommand`, ProcessWindowHost Control, Info-Panel UI

### UC4: Aufgabe abschließen

1. Status=InArbeit oder Wartend, CLI läuft nicht (IsCliRunning=false)
2. Button "Beenden" ist sichtbar und enabled im Ribbon
3. User klickt Button "Beenden"
4. `AufgabeAbschliessenCommand.Execute()`:
   - `EntwicklungsprozessService.AbschliessenAsync()`
   - Status wird zu Beendet geändert
   - AbschlussDatum wird gesetzt
5. LadenAsync() aufgerufen, ViewModel aktualisiert sich
6. Compute-Properties wechseln: `ShowCliPanel` = false, `ShowDiffPanel` = true
7. View wechselt: CLI-Panel hidden → Diff-Panel sichtbar (Platzhalter: "Diff wird hier angezeigt...")
8. Buttons "Speichern", "Löschen", "Starten" werden disabled (CanExecute prüft Status)

Beteiligte Klassen/Komponenten: `TaskDetailViewModel`, `EntwicklungsprozessService`, `ShowDiffPanel` Property, `AufgabeAbschliessenCommand`, `AufgabeStatusToVisibilityConverter`

### UC5: Aufgabe löschen mit Bestätigungsdialog

1. Status ≠ Beendet und ≠ Archiviert, Button "Löschen" ist sichtbar und enabled
2. User klickt Button "Löschen"
3. `LoeschenCommand.Execute()`:
   - `IDialogService.ShowConfirmationAsync()` wird aufgerufen
   - Dialog-Text: "Aufgabe '{AufgabeTitel}' wirklich löschen? Diese Aktion kann nicht rückgängig gemacht werden."
4. User klickt "Löschen" im Dialog (oder "Abbrechen"):
   - Wenn "Löschen": IsLoading = true, `AufgabeService.DeleteAsync(Id)` wird aufgerufen
   - Wenn "Abbrechen": Nichts geschieht, Dialog closed
5. Bei Success:
   - `ProjektListeAktualisierenCallback?.Invoke()` (optional, um Aufgaben-Listenansicht zu aktualisieren)
   - `ZurueckAction?.Invoke()` (Navigation zurück zur Projektdetailansicht)
   - Toast "Aufgabe gelöscht" anzeigen
6. Bei Exception (z.B. Status=Beendet):
   - `IDialogService.ShowErrorAsync("Aufgabe konnte nicht gelöscht werden: {exception.Message}")`
   - Aufgabe bleibt in DB

Beteiligte Klassen/Komponenten: `TaskDetailViewModel`, `AufgabeService`, `IDialogService`, `LoeschenCommand`, `KannLoeschen` Property, `ZurueckAction` Callback

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `AufgabeStatusToVisibilityConverter` | IValueConverter | Konvertiert `AufgabeStatus` zu `Visibility` basierend auf Vergleich mit ConverterParameter; wird für Content-Switching verwendet (zeigt Panels basierend auf Status) |

## Änderungen an bestehenden Klassen

### `TaskDetailViewModel` (ViewModels/TaskDetailViewModel.cs)

- **Neue Eigenschaften:**
  - `IsInfoViewVisible` (bool) — Steuert Sichtbarkeit zwischen Info-Panel und CLI-Fenster; Initial = false
  - `ShowEditPanel` (bool, computed) — True wenn `Aufgabe?.Status == AufgabeStatus.Neu`, sonst false
  - `ShowCliPanel` (bool, computed) — True wenn Status ∈ {Gestartet, InArbeit, Wartend}, sonst false
  - `ShowDiffPanel` (bool, computed) — True wenn `Aufgabe?.Status == AufgabeStatus.Beendet`, sonst false
  - `EditTitel` (string?) — Editable Kopie von `Aufgabe.Titel` für Edit-Modus (Two-Way-Binding)
  - `EditAnforderungsBeschreibung` (string?) — Editable Kopie von `Aufgabe.AnforderungsBeschreibung` für Edit-Modus
  - `KannSpeichern` (bool, computed) — CanExecute für SpeichernCommand: Status ∈ {Neu, Gestartet} && !IsCliRunning && Titel.Length > 0
  - `KannLoeschen` (bool, computed) — CanExecute für LoeschenCommand: Status ∉ {Beendet, Archiviert} && !IsCliRunning

- **Neue Commands:**
  - `SpeichernCommand` (ICommand) — Ruft `AufgabeService.UpdateAsync(Id, EditTitel, EditAnforderungsBeschreibung)` auf; CanExecute = KannSpeichern
  - `LoeschenCommand` (ICommand) — Zeigt Bestätigungsdialog, ruft `AufgabeService.DeleteAsync()` auf; CanExecute = KannLoeschen
  - `InfoCliToggleCommand` (ICommand) — Toggled `IsInfoViewVisible`; keine CanExecute-Prüfung

- **Änderungen an bestehenden Methoden:**
  - `LadenAsync()` — Nach Laden der Aufgabe: `EditTitel = Aufgabe.Titel;`, `EditAnforderungsBeschreibung = Aufgabe.AnforderungsBeschreibung;`, OnPropertyChanged für alle Compute-Properties aufrufen

- **Neue Event-Handler / Dependencies:**
  - `IDialogService` — Für Bestätigungsdialog beim Löschen (Dependency Injection im Constructor)

- **Private Fields (Support):**
  - `_isInfoViewVisible` (bool) — Backing-Field für IsInfoViewVisible
  - `_editTitel` (string?) — Backing-Field für EditTitel
  - `_editAnforderungsBeschreibung` (string?) — Backing-Field für EditAnforderungsBeschreibung

### `TaskDetailView.xaml` (Views/TaskDetailView.xaml)

- **Neue Grid-Struktur:**
  - Row 0: Ribbon-Menü (neu, analog ProjectDetailView)
  - Row 1: Fehler-Border (existiert, unverändert)
  - Row 2: Hauptinhalt mit Content-Switching zwischen 3 Panels (Neu)
  - Row 3: Statusleiste (existiert, unverändert)

- **Ribbon-Menü (Grid.Row="0", neu):**
  - Border mit SurfaceBrush Background, BorderBrush
  - StackPanel Horizontal mit RibbonGroups:
    - **RibbonGroup "Navigation":** Button "Zurück" → Command: `ZurueckCommand`
    - **RibbonGroup "Aufgabe":** Vier Buttons mit Icons:
      - "Speichern" (💾) → `SpeichernCommand`
      - "Löschen" (🗑) → `LoeschenCommand`
      - "Starten" (▶) → `StatusGestartetSetzenCommand`
      - "Beenden" (✓) → `AufgabeAbschliessenCommand`
    - Button-Visibility: Basierend auf CanExecute oder direkt auf ShowEditPanel/ShowCliPanel/ShowDiffPanel

- **Content-Switching Grid (Grid.Row="2", neu):**
  - Drei überlagerte Child-Grids mit Visibility-Binding:

  1. **Edit-Panel (Status=Neu):**
     - Visibility: `{Binding ShowEditPanel, Converter={StaticResource BoolToVisibilityConverter}}`
     - Kachel (Border mit CornerRadius=12, Padding=16)
     - Label "Aufgabe bearbeiten"
     - TextBlock "Titel"
     - TextBox: `Text={Binding EditTitel, UpdateSourceTrigger=PropertyChanged}`
     - TextBlock "Anforderungsbeschreibung"
     - TextBox (MultiLine): `Text={Binding EditAnforderungsBeschreibung, UpdateSourceTrigger=PropertyChanged}`
     - Hinweis: "Speichern-Button im Ribbon verwenden"

  2. **CLI-Panel (Status ∈ {Gestartet, InArbeit, Wartend}):**
     - Visibility: `{Binding ShowCliPanel, Converter={StaticResource BoolToVisibilityConverter}}`
     - Grid mit 2 Rows:
       - Row 0 (Auto): StackPanel mit Toggle
         - Label "Ansicht:"
         - ToggleButton: `IsChecked={Binding IsInfoViewVisible}` mit Content "Info"/"CLI"
       - Row 1 (*): Zwei überlagerte Panels:
         - **ProcessWindowHost (CLI-Fenster):**
           - Visibility: `{Binding IsInfoViewVisible, Converter={StaticResource InverseBoolToVisibilityConverter}}`
         - **Info-Panel (Aufgabeeigenschaften + Protokoll):**
           - Visibility: `{Binding IsInfoViewVisible, Converter={StaticResource BoolToVisibilityConverter}}`
           - Kachel (Border)
           - TextBlock: Aufgabe.Titel, Aufgabe.Status, Aufgabe.AnforderungsBeschreibung
           - ListBox: `ItemsSource={Binding Protokolleintraege}`, Scrolling aktiviert

  3. **Diff-Panel (Status=Beendet):**
     - Visibility: `{Binding ShowDiffPanel, Converter={StaticResource BoolToVisibilityConverter}}`
     - Kachel (Border)
     - TextBlock (Platzhalter): "Diff-Ansicht wird hier angezeigt... (Implementierung folgt später)"

- **Header-Bereich (anpassen):**
  - Buttons "CLI Starten", "Stoppen", "Beenden" aus Header entfernen (sind jetzt im Ribbon)
  - KI-Plugin-Dropdown bleibt (Visibility: !IsCliRunning)
  - Optional: Info-Label "Toggle Info/CLI" oder Icon bei aktiver Aufgabe

## Datenbankmigrationen

Keine. Alle erforderlichen Spalten existieren bereits:
- `Aufgabe.Titel` — für Edit-Modus vorhanden
- `Aufgabe.AnforderungsBeschreibung` — für Edit-Modus vorhanden
- `Aufgabe.Status` — für Status-abhängiges Content-Switching vorhanden
- `Aufgabe.DiffResults` — für Diff-Navigation vorhanden

Status-Enum `AufgabeStatus` hat alle 7 Werte: Neu, ArbeitsverzeichnisEingerichtet, Gestartet, InArbeit, Wartend, Beendet, Archiviert.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `SpeichernCommand.CanExecute` | Status ∈ {Neu, Gestartet} && !IsCliRunning && Titel.Length > 0 | Button disabled wenn Bedingung nicht erfüllt |
| `LoeschenCommand.CanExecute` | Status ∉ {Beendet, Archiviert} && !IsCliRunning | Button disabled wenn Status schon abgeschlossen |
| `StatusGestartetSetzenCommand.CanExecute` | Status == Neu && !IsCliRunning | Button disabled wenn nicht in Zustand "Neu" |
| `AufgabeAbschliessenCommand.CanExecute` | Status ∈ {Gestartet, InArbeit, Wartend} && !IsCliRunning | Button disabled wenn Status nicht aktiv |
| `AufgabeService.UpdateAsync()` | Service-Validierung: keine Status-Prüfung; kann immer aufgerufen werden | Exception: nur wenn DB-Fehler |
| `AufgabeService.DeleteAsync()` | Service-Validierung: wirft Exception wenn Status ∈ {Gestartet, InArbeit, Wartend, Beendet, Archiviert}; nur Neu erlaubt | Exception: "Aufgabe konnte nicht gelöscht werden..." |
| `AufgabeService.SetStatusAsync()` | Service-Validierung: Nutzt `ValidateStatusTransition()` → wirft Exception bei ungültigem Übergang | Exception: "Ungültiger Status-Übergang" |

## Konfigurationsänderungen

Keine. Verhalten ist fest definiert:
- Ribbon-Struktur ist statisch
- Content-Switching erfolgt automatisch
- Diff-Anzeige ist mit Platzhalter hardcoded

## Seiteneffekte und Risiken

- **TaskDetailView-Navigation:** Keine Änderung; Navigation von ProjectDetailView bleibt gleich; neue TaskDetailView ist nur erweitert, nicht umgebrochen
- **CLI-Start/Stop:** Keine Änderung; existierende Commands bleiben unverändert; neue Commands sind zusätzlich
- **Aufgaben-Listenansicht:** Keine Änderung; neue Properties/Commands beeinflussen nur TaskDetailView

| Risiko | Wahrscheinlichkeit | Mitigation |
|--------|-------------------|-----------|
| Toggle-Button während CLI-Prozess kann instabil wirken | Mittel | Test-Szenario UC3: Protokoll-Wechsel während laufen; IsInfoViewVisible ist nur UI-Zustand |
| Diff-Platzhalter wird vergessen, später nicht implementiert | Niedrig | TODO-Kommentar im Code; Anforderung referenzieren |
| Edit-Felder werden nach Status-Übergang nicht geleert | Niedrig | In LadenAsync: EditTitel und EditAnforderungsBeschreibung neu initialisieren |
| User versucht zu Speichern während CLI läuft | Niedrig | CanExecute verhindert UI-Klick; Service-Validierung auch vorhanden |

## Umsetzungsreihenfolge

1. **Converter `AufgabeStatusToVisibilityConverter` in AppConverters.cs hinzufügen**
   - Voraussetzungen: Keine
   - Beschreibung: Neuer IValueConverter mit Convert() und ConvertBack()-Methoden; Parameter wird komma-separiert oder einzelner Status-Wert; Status == Parameter → Visibility.Visible, sonst Collapsed

2. **TaskDetailViewModel erweitern: Private Fields und Compute-Properties**
   - Voraussetzungen: Converter-Datei existiert
   - Beschreibung: Private Fields (_isInfoViewVisible, _editTitel, _editAnforderungsBeschreibung) und Compute-Properties (ShowEditPanel, ShowCliPanel, ShowDiffPanel, KannSpeichern, KannLoeschen) mit OnPropertyChanged-Aufrufen

3. **TaskDetailViewModel erweitern: Neue Commands (SpeichernCommand, LoeschenCommand, InfoCliToggleCommand)**
   - Voraussetzungen: Step 2 abgeschlossen; IDialogService DI-Registrierung vorhanden
   - Beschreibung: Drei neue Commands als public ICommand properties; initialisiert im Constructor mit Lambda-Execute-Delegaten; CanExecute-Logik basierend auf Compute-Properties

4. **TaskDetailViewModel erweitern: LadenAsync()-Logik**
   - Voraussetzungen: Step 3 abgeschlossen
   - Beschreibung: Nach Laden der Aufgabe EditTitel und EditAnforderungsBeschreibung initialisieren; OnPropertyChanged für alle Compute-Properties aufrufen

5. **TaskDetailViewModel erweitern: SpeichernCommand.Execute()-Implementierung**
   - Voraussetzungen: Step 4 abgeschlossen
   - Beschreibung: AufgabeService.UpdateAsync() aufrufen; IsLoading gesetzt; Error-Handling; Toast anzeigen; Optional neu laden

6. **TaskDetailViewModel erweitern: LoeschenCommand.Execute()-Implementierung**
   - Voraussetzungen: Step 5 abgeschlossen; IDialogService injiziert
   - Beschreibung: Dialog anzeigen; nur bei "Löschen" AufgabeService.DeleteAsync() aufrufen; Navigation zurück; Error-Dialog bei Fehler

7. **TaskDetailViewModel erweitern: InfoCliToggleCommand.Execute()-Implementierung**
   - Voraussetzungen: Step 3 abgeschlossen
   - Beschreibung: IsInfoViewVisible = !IsInfoViewVisible; einfle Toggle-Logik

8. **Bestehende Commands überprüfen (StatusGestartetSetzenCommand, AufgabeAbschliessenCommand)**
   - Voraussetzungen: Step 7 abgeschlossen
   - Beschreibung: CanExecute-Logik überprüfen; optional korrigieren wenn nicht korrekt implementiert

9. **App.xaml aktualisieren: Converter registrieren**
   - Voraussetzungen: Step 1 abgeschlossen
   - Beschreibung: `<local:AufgabeStatusToVisibilityConverter x:Key="AufgabeStatusToVisibilityConverter" />` in App.xaml.Resources registrieren

10. **TaskDetailView.xaml: Ribbon-Menü implementieren**
    - Voraussetzungen: Step 3 abgeschlossen (Commands vorhanden)
    - Beschreibung: Grid.Row="0" mit Border + StackPanel + RibbonGroups (Navigation, Aufgabe) mit vier Buttons; Bindings zu Commands

11. **TaskDetailView.xaml: Header anpassen**
    - Voraussetzungen: Step 10 abgeschlossen
    - Beschreibung: Buttons aus Header in Ribbon verschieben; KI-Plugin-Dropdown Visibility überprüfen

12. **TaskDetailView.xaml: Content-Switching Grid implementieren**
    - Voraussetzungen: Step 10 abgeschlossen (Ribbon done); Step 9 abgeschlossen (Converter registriert)
    - Beschreibung: Drei Child-Grids in Grid.Row="2": Edit-Panel (Status=Neu), CLI-Panel (Status∈{Gestartet,InArbeit,Wartend}), Diff-Panel (Status=Beendet); Visibility-Bindings; Toggle-Button im CLI-Panel

13. **TaskDetailView.xaml: Info-Panel Design anpassen**
    - Voraussetzungen: Step 12 abgeschlossen
    - Beschreibung: Info-Panel (in CLI-Panel) mit Aufgabeeigenschaften + Protokoll-ListBox gestalten; Scrolling; Farben konsistent

14. **Unit-Tests schreiben: TaskDetailViewModelTests**
    - Voraussetzungen: Step 8 abgeschlossen (ViewModel vollständig)
    - Beschreibung: Neue Test-Klasse mit Setup; Tests für ShowEditPanel, ShowCliPanel, ShowDiffPanel, KannSpeichern, KannLoeschen, Commands (SpeichernCommand, LoeschenCommand, InfoCliToggleCommand, bestehende Status-Commands); >90% Coverage

15. **E2E-Tests schreiben: TaskDetailViewE2ETests (UC1-UC5 + Error Handling)**
    - Voraussetzungen: Step 13 abgeschlossen (UI vollständig); Step 14 abgeschlossen (Unit-Tests zeigen Patterns)
    - Beschreibung: Neue E2E-Test-Klasse; In-Memory DB + echte Services; Tests für alle 5 Use Cases + Button Visibility + Error Handling; >80% Feature-Coverage

16. **Code-Dokumentation (XML Comments)**
    - Voraussetzungen: Step 15 abgeschlossen
    - Beschreibung: Alle neuen Properties, Commands, Methoden mit /// comments dokumentieren; keine TODOs

17. **Feature-Dokumentation schreiben**
    - Voraussetzungen: Step 16 abgeschlossen
    - Beschreibung: Benutzer-Handbuch (Use Cases 1-5 mit Screenshots), Developer-Guide (Architektur, Classes), API-Referenz, Testing-Guide, bekannte Einschränkungen

18. **Code-Review und QA**
    - Voraussetzungen: Step 17 abgeschlossen
    - Beschreibung: Peer-Review, manuelle UI-Tests, Regression-Tests für bestehende Features, Documentation-Review

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `ShowEditPanel_IsTrue_WhenStatusNeu()` | TaskDetailViewModelTests | ShowEditPanel ist true wenn Status=Neu |
| `ShowCliPanel_IsTrue_WhenStatusGestartetOrInArbeitOrWartend()` | TaskDetailViewModelTests | ShowCliPanel ist true für laufende Status |
| `ShowDiffPanel_IsTrue_WhenStatusBeendet()` | TaskDetailViewModelTests | ShowDiffPanel ist true wenn Status=Beendet |
| `KannSpeichern_Tests (4 Tests)` | TaskDetailViewModelTests | CanExecute-Logik für SpeichernCommand prüfen |
| `KannLoeschen_Tests (4 Tests)` | TaskDetailViewModelTests | CanExecute-Logik für LoeschenCommand prüfen |
| `SpeichernCommand_Tests (5 Tests)` | TaskDetailViewModelTests | Command ruft UpdateAsync auf; IsLoading gesetzt; Error-Handling |
| `LoeschenCommand_Tests (7 Tests)` | TaskDetailViewModelTests | Dialog angezeigt; DeleteAsync aufgerufen; Navigation; Error-Dialog |
| `InfoCliToggleCommand_Tests (3 Tests)` | TaskDetailViewModelTests | IsInfoViewVisible toggled |
| `EditTitel/EditAnforderungsBeschreibung_Tests (4 Tests)` | TaskDetailViewModelTests | Edit-Properties initialisiert und bindbar |
| `AufgabeStatusToVisibilityConverter_Tests` | AppConvertersTests | Converter konvertiert Status zu Visibility korrekt |
| `E2E_CreateAndEditTask_InStatusNeu()` | TaskDetailViewE2ETests | UC1: Edit-Panel, Speichern, DB-Update, Toast |
| `E2E_StartTask_FromStatusNeu()` | TaskDetailViewE2ETests | UC2: Status-Übergang, Panel-Wechsel zu CLI |
| `E2E_RunCli_AndToggleInfoView()` | TaskDetailViewE2ETests | UC3: CLI-Start, Toggle funktioniert, Panel-Wechsel |
| `E2E_CompleteTask_ShowsDiffPanel()` | TaskDetailViewE2ETests | UC4: Status zu Beendet, Diff-Panel sichtbar |
| `E2E_DeleteTask_ShowsConfirmationDialog()` | TaskDetailViewE2ETests | UC5: Dialog, Löschen, Navigation, DB |
| `E2E_DeleteTask_ShowsErrorDialog_WhenStatusIsBeendet()` | TaskDetailViewE2ETests | Error-Handling: nicht löschbar |
| `E2E_Button_Visibility_Tests (4 Tests)` | TaskDetailViewE2ETests | Button-Sichtbarkeit je nach Status |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| Keine | Keine bestehenden Tests sollten brechen; neue Properties und Commands sind zusätzlich, nicht ersetzend; existierende Commands (StatusGestartetSetzenCommand, AufgabeAbschliessenCommand, CliStartenCommand) bleiben unverändert |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Aufgabe im Status Neu bearbeiten und speichern | TaskDetailViewE2ETests.E2E_CreateAndEditTask_InStatusNeu | "Aufgabe kann im Status Neu mit Ribbon-Buttons bearbeitet werden" |
| Aufgabe vom Status Neu zu Gestartet wechseln | TaskDetailViewE2ETests.E2E_StartTask_FromStatusNeu | "Edit-Panel wird ausgeblendet, CLI-Panel wird angezeigt" |
| CLI starten und Info/CLI-Ansicht toggen | TaskDetailViewE2ETests.E2E_RunCli_AndToggleInfoView | "Toggle-Button schaltet zwischen CLI-Fenster und Info-Panel um" |
| Aufgabe abschließen und Diff-Panel anzeigen | TaskDetailViewE2ETests.E2E_CompleteTask_ShowsDiffPanel | "Status Beendet zeigt Diff-Panel statt CLI-Panel" |
| Aufgabe löschen mit Bestätigungsdialog | TaskDetailViewE2ETests.E2E_DeleteTask_ShowsConfirmationDialog | "Löschen-Button zeigt Dialog, bestätigung löscht Aufgabe und navigiert zurück" |

Bestehende E2E-Tests, die betroffen sind: Keine bekannt. Existierende Tests für TaskDetailView müssen noch überprüft werden (falls vorhanden).

## Offene Punkte

Keine. Alle Fragen aus der Anforderung wurden beantwortet:

1. **Diff-Ansicht für beendete Aufgaben:** Platzhalter-TextBlock implementiert; Service-Integration (GetLatestDiffResultIdAsync) ist vorhanden
2. **Edit-Modus — nur Status Neu, oder auch später:** Edit-Mode nur Status=Neu; nach Status-Wechsel werden Edit-Felder hidden
3. **Bestätigungsdialog beim Löschen:** Ja, implementiert mit LoeschenCommand
4. **Toggle-Button Info/CLI immer sichtbar bei Status InArbeit:** Ja, Toggle-Button sichtbar in Status ∈ {Gestartet, InArbeit, Wartend}
5. **Pflichtfelder beim Speichern:** `Titel` ist Pflichtfeld (Length > 0); `AnforderungsBeschreibung` ist optional
6. **Recovery-Meldung beim Stoppen:** Nicht im Scope; kann später implementiert werden

