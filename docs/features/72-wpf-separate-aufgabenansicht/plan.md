# Umsetzungsplan: WPF separate Aufgabenansicht

## Übersicht

Feature 72 transformiert die Aufgabendetailansicht von einer inline eingebetteten Position in `ProjectDetailView` zu einer fensterumfassenden, separaten View. Nach dem Klick auf eine Aufgabe in der Aufgabenliste navigiert die App zu `TaskDetailView`, wobei `ProjectDetailView` nicht mehr sichtbar ist. Eine Zurück-Navigation bringt den Nutzer zur Projektdetailansicht zurück. Die Neuanlage von Aufgaben funktioniert analog: Doppelklick auf „Neue Aufgabe" öffnet `TaskDetailView` mit leerem Formular; nach dem Speichern wird die neue Aufgabe mit Status „Neu" persistiert und die Navigation kehrt zur `ProjectDetailView` zurück.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Navigationsmechanismus | Callback-basiert (Action<TaskDetailViewModel>, Action) statt zentraler INavigationService | Die bestehende Architektur nutzt bereits Callbacks (`ZurueckAction`, `AufgabeListeAktualisierenCallback`). `ProjectListViewModel` zeigt ein funktionierendes Muster. Ein zusätzlicher Service würde zu doppelter Verantwortung führen (Content-Control in MainWindow + Service). Konsistenz erhöht Wartbarkeit. |
| View-Wechsel Animation | Sofortiger Wechsel ohne Animation | Keine Fade-, Slide- oder andere Übergänge erforderlich. Sofortiger Wechsel zwischen ProjectDetailView und TaskDetailView. |
| Fehlerbehandlung beim Speichern | Fehlermeldung anzeigen, View bleibt offen | Wenn das Speichern fehlschlägt, wird dem Nutzer eine Fehlermeldung angezeigt und die TaskDetailView bleibt geöffnet zur Korrektur. Keine automatische Navigation. |
| Performance bei vielen Aufgaben | Einzel-Update statt vollständiger Reload | Nach dem Speichern wird nur das geänderte Element in der Aufgaben-Collection aktualisiert, nicht die gesamte Liste neu geladen. |

## Programmabläufe

### Aufgabe aus Liste öffnen

1. Nutzer doppelklickt auf Aufgabe in `ProjectDetailView.Aufgaben` ListBox
2. Code-Behind `ProjectDetailView.AufgabeDoubleClick()` wird ausgelöst
3. `ProjectDetailViewModel.AufgabeOeffnenCommand.Execute(aufgabeId)` wird aufgerufen
4. `ProjectDetailViewModel.OeffneAufgabe(id)` wird aufgerufen
5. Neues `TaskDetailViewModel` wird erstellt und konfiguriert:
   - `vm.ZurueckAction = () => NavigateBackToProject()` (Callback gesetzt)
   - `vm.AufgabeListeAktualisierenCallback = async () => await ReloadAufgabenListAsync()`
   - `vm.AufgabeId = id` setzt AufgabeId und triggert Laden
6. `NavigateToTaskViewCallback?.Invoke(vm)` wird aufgerufen (statt `SelectedTaskViewModel = vm`)
7. `ProjectListViewModel.ZeigeTaskDetailView(vm)` setzt `DetailViewModel = vm`
8. MainWindow DataTemplate matched `TaskDetailViewModel` Type und rendert `TaskDetailView`
9. `ProjectDetailView` wird nicht mehr angezeigt

Beteiligte Klassen/Komponenten: `ProjectDetailViewModel`, `TaskDetailViewModel`, `ProjectListViewModel`, `ProjectDetailView`, `MainWindow`

### Neue Aufgabe erstellen

1. Nutzer klickt "Neue Aufgabe" Button in `ProjectDetailView`
2. `ProjectDetailViewModel.AufgabeErstellenCommand` wird ausgelöst
3. `AufgabeService.CreateAsync()` erstellt neue Aufgabe mit Status `Neu` und ID
4. Neue Aufgabe wird in `Aufgaben` ObservableCollection hinzugefügt
5. `OeffneAufgabe(aufgabe.Id)` wird aufgerufen (siehe Ablauf 1, ab Schritt 4)
6. `TaskDetailView` zeigt Edit-Panel (weil Status == Neu)

Beteiligte Klassen/Komponenten: `ProjectDetailViewModel`, `AufgabeService`, `TaskDetailViewModel`

### Von Aufgabendetail zur Projektdetail zurück navigieren

1. Nutzer klickt "Zurück" oder "Abbrechen" Button in `TaskDetailView`
2. `TaskDetailViewModel.ZurueckCommand` wird ausgelöst
3. `ZurueckAction()` Callback wird aufgerufen (durch `ProjectDetailViewModel` gesetzt)
4. Callback führt `NavigateBackToProjectCallback()` auf `ProjectDetailViewModel` durch
5. `ProjectListViewModel.KehreZuProjectZurueck()` wird aufgerufen
6. `ProjectListViewModel.DetailViewModel` wird auf `ProjectDetailViewModel` zurück gesetzt
7. MainWindow zeigt `ProjectDetailView` wieder an

Beteiligte Klassen/Komponenten: `TaskDetailViewModel`, `ProjectDetailViewModel`, `ProjectListViewModel`, `MainWindow`

### Neue Aufgabe speichern und zurück navigieren

1. Nutzer füllt Titel und Beschreibung in `TaskDetailView` aus
2. Nutzer klickt "Speichern"
3. `TaskDetailViewModel.SpeichernAsync()` wird ausgelöst
4. `AufgabeService.UpdateAsync(id, titel, beschreibung, ...)` speichert Änderungen
5. **Fehlerfall:** Wenn Speichern fehlschlägt, wird eine Fehlermeldung in der View angezeigt und die TaskDetailView bleibt offen
6. **Erfolgsfall:** Aufgabe bleibt mit Status `Neu`
7. `AufgabeListeAktualisierenCallback()` wird aufgerufen, aber mit Einzel-Update: nur das geänderte Element wird in der Aufgaben-Collection aktualisiert (nicht vollständiger Reload)
8. `ZurueckAction()` wird aufgerufen
9. Navigation kehrt zu `ProjectDetailView` zurück (siehe Ablauf 3)

Beteiligte Klassen/Komponenten: `TaskDetailViewModel`, `AufgabeService`, `ProjectDetailViewModel`

## Neue Klassen

Keine — die bestehende Architektur ist ausreichend.

## Änderungen an bestehenden Klassen

### `ProjectDetailViewModel` (ViewModel)

- **Neue Properties:** `NavigateToTaskViewCallback` (Action<TaskDetailViewModel>?) — Wird von `ProjectListViewModel` gesetzt, um Navigation zu separater View zu triggern
- **Neue Properties:** `NavigateBackToProjectCallback` (Action?) — Wird von `ProjectListViewModel` gesetzt, um Navigation von Task zurück zu Project zu triggern
- **Neue Methoden:** `ReloadAufgabenListAsync()` — Lädt Aufgabenliste neu, wird als `AufgabeListeAktualisierenCallback` an `TaskDetailViewModel` übergeben
- **Geänderte Methoden:** `OeffneAufgabe(Guid id)` — Neues ViewModel erstellen und konfigurieren, dann `NavigateToTaskViewCallback?.Invoke(vm)` aufrufen statt `SelectedTaskViewModel = vm`
- **Entfernte Properties:** `SelectedTaskViewModel` — wird nicht mehr benötigt, da Navigation fensterumfassend erfolgt

### `TaskDetailViewModel` (ViewModel)

- Keine Änderungen erforderlich — das ViewModel bleibt unverändert. `ZurueckAction` und `AufgabeListeAktualisierenCallback` werden durch `ProjectDetailViewModel` gesetzt.

### `ProjectListViewModel` (ViewModel)

- **Neue Methoden:** `ZeigeTaskDetailView(TaskDetailViewModel vm)` — Setzt `DetailViewModel = vm`, wird von `NavigateToTaskViewCallback` aufgerufen
- **Neue Methoden:** `KehreZuProjectZurueck()` — Setzt `DetailViewModel` zurück zu aktuellem `ProjectDetailViewModel`, wird von `NavigateBackToProjectCallback` aufgerufen
- **Geänderte Methoden:** `InitDetailViewModel(ProjectDetailViewModel viewModel)` — Zusätzlich konfigurieren: `vm.NavigateToTaskViewCallback = ZeigeTaskDetailView;` und `vm.NavigateBackToProjectCallback = KehreZuProjectZurueck;`

### `ProjectDetailView.xaml` (XAML-View)

- **Entfernte Elemente:** Zeilen mit inline `<views:TaskDetailView DataContext="{Binding SelectedTaskViewModel}" .../>` (ca. Zeilen 229–231) — wird nicht mehr inline gerendert

## Datenbankmigrationen

Keine. Das Datenmodell ändert sich nicht. Die Entitäten `Aufgabe` und `Projekt` bleiben unverändert.

## Validierungsregeln

Keine. Bestehende Status-Übergänge und Validierungsregeln bleiben gültig.

## Konfigurationsänderungen

Keine. Navigation ist ein System-Verhalten und erfordert keine Konfiguration.

## Seiteneffekte und Risiken

- **Backward Compatibility:** `SelectedTaskViewModel` Property wird entfernt. Code, der direkt darauf zugreift, bricht. Gründliche Suche nach Nutzungen durchführen; Property mit `[Obsolete]` markieren vor Entfernung.
- **Navigation-State zwischen Sessions:** Wenn Session geschlossen wird, während Task-View aktiv ist, wird die App wieder bei Projekt-View geöffnet (kein State-Persistence). Dies ist erwartet und erfordert keine Mitigierung.
- **Performance-Optimierung:** Einzel-Update statt vollständiger Reload bei großen Aufgabenlisten wird implementiert. Die `AufgabeListeAktualisierenCallback` Methode muss intelligent zwischen vollständigem Reload und Einzel-Update unterscheiden.
- **Fehlerbehandlung in UI:** TaskDetailView muss eine Fehlerbehandlung implementieren (Fehlermeldung anzeigen) für gescheiterte Speichervorgänge.
- **Testing:** Neue Callback-Logik muss in Unit-Tests verprobt werden. Szenario-basierte Unit-Tests für alle Navigation-Flows sind erforderlich. Fehlerszenarien beim Speichern müssen getestet werden.

## Umsetzungsreihenfolge

1. **Callbacks in ProjectDetailViewModel hinzufügen**
   - Voraussetzungen: Keine
   - Beschreibung: Neue Properties `NavigateToTaskViewCallback` (Action<TaskDetailViewModel>?) und `NavigateBackToProjectCallback` (Action?) als public, databindbar; neue private Methode `ReloadAufgabenListAsync()` implementieren mit Unterstützung für Einzel-Update von Aufgaben

2. **ProjectListViewModel für Task-Navigation erweitern**
   - Voraussetzungen: Schritt 1 abgeschlossen
   - Beschreibung: Neue private Methoden `ZeigeTaskDetailView(TaskDetailViewModel vm)` und `KehreZuProjectZurueck()` implementieren; `InitDetailViewModel()` anpassen, um Callbacks zu setzen

3. **OeffneAufgabe-Methode anpassen**
   - Voraussetzungen: Schritte 1–2 abgeschlossen
   - Beschreibung: Statt `SelectedTaskViewModel = vm` aufzurufen: `NavigateToTaskViewCallback?.Invoke(vm)`, `TaskDetailViewModel` mit `ZurueckAction` und `AufgabeListeAktualisierenCallback` konfigurieren

4. **TaskDetailViewModel Fehlerbehandlung erweitern**
   - Voraussetzungen: Schritt 3 abgeschlossen
   - Beschreibung: `SpeichernAsync()` Methode anpassen, um bei Fehler eine Fehlermeldung zu zeigen (z.B. über Binding an ErrorMessage Property). Keine automatische Navigation bei Fehler. View bleibt offen.

5. **ProjectDetailView.xaml Fehlerbehandlung anpassen**
   - Voraussetzungen: Schritt 4 abgeschlossen
   - Beschreibung: TaskDetailView Error-Meldungsbereich hinzufügen (falls nicht vorhanden), um Fehlermeldungen beim Speichern anzuzeigen

6. **SelectedTaskViewModel Property entfernen**
   - Voraussetzungen: Schritt 3 abgeschlossen
   - Beschreibung: Property komplett entfernen aus `ProjectDetailViewModel`

7. **ProjectDetailView.xaml anpassen**
   - Voraussetzungen: Schritt 6 abgeschlossen
   - Beschreibung: Inline `<views:TaskDetailView>` Element entfernen (ca. Zeilen 229–231)

8. **Unit-Tests für Navigation schreiben**
   - Voraussetzungen: Schritte 1–7 abgeschlossen
   - Beschreibung: Tests für `AufgabeOeffnen_CallsNavigateToTaskViewCallback`, `NavigateToTaskViewCallback_SetsZurueckAction`, `ReloadAufgabenList_*`, `AufgabeErstellen_NavigatesToTask`, `ProjectListViewModel.ZeigeTaskDetailView_*`, `ProjectListViewModel.KehreZuProjectZurueck_*`, `SpeichernAsync_ShowsErrorMessage_WhenSaveFails`

9. **E2E-Tests implementieren**
   - Voraussetzungen: Schritte 1–8 abgeschlossen, Unit-Tests erfolgreich
   - Beschreibung: Tests für Aufgabe öffnen → TaskDetailView anzeigen, Zurück-Navigation, Neue Aufgabe erstellen und speichern, Fehlerbehandlung beim Speichern

10. **Manuelles Testen und Dokumentation**
    - Voraussetzungen: Schritte 1–9 abgeschlossen
    - Beschreibung: Navigationsflusses in echtem WPF-Fenster testen, Inline-Dokumentation hinzufügen, CHANGELOG updaten

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `AufgabeOeffnen_CallsNavigateToTaskViewCallback_WhenAufgabeSelected` | ProjectDetailViewModelTests | Prüft, dass `NavigateToTaskViewCallback` aufgerufen wird, wenn Aufgabe geöffnet wird |
| `NavigateToTaskViewCallback_SetsZurueckActionAndCallbackOnTaskVM` | ProjectDetailViewModelTests | Prüft, dass `ZurueckAction` und `AufgabeListeAktualisierenCallback` auf dem neuen `TaskDetailViewModel` gesetzt werden |
| `AufgabeErstellen_CreatesTaskWithStatusNeuAndNavigates` | ProjectDetailViewModelTests | Prüft, dass neue Aufgabe mit Status Neu erstellt wird und zu TaskDetailView navigiert wird |
| `ReloadAufgabenList_UpdatesSingleItemAsync_WhenCallbackInvoked` | ProjectDetailViewModelTests | Prüft, dass nur das geänderte Element in der Aufgabenliste aktualisiert wird (Einzel-Update) |
| `ProjectListViewModel_ZeigeTaskDetailView_SetsDetailViewModelToTask` | ProjectListViewModelTests | Prüft, dass `DetailViewModel` zu übergebenem `TaskDetailViewModel` gesetzt wird |
| `ProjectListViewModel_KehreZuProjectZurueck_RestoresProjectDetailView` | ProjectListViewModelTests | Prüft, dass `DetailViewModel` zurück zum `ProjectDetailViewModel` gesetzt wird |
| `SpeichernAsync_ShowsErrorMessage_WhenSaveFails` | TaskDetailViewModelTests | Prüft, dass bei Speicherfehler eine Fehlermeldung angezeigt wird und die View offen bleibt |
| `SpeichernAsync_DoesNotNavigateBack_WhenSaveFails` | TaskDetailViewModelTests | Prüft, dass `ZurueckAction` nicht aufgerufen wird, wenn Speichern fehlschlägt |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| ProjectDetailViewModelTests – alle Tests | Neue Callbacks müssen in Setup gemockt werden |
| Tests, die `SelectedTaskViewModel` prüfen | Diese Property wird entfernt; solche Tests müssen gelöscht oder angepasst werden |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Öffne Projekt, doppelklick auf Aufgabe, TaskDetailView wird fensterumfassend angezeigt | E2E_TaskDetailNavigation | Aufgabe kann über Double-Click geöffnet werden; TaskDetailView wird vollständig angezeigt |
| TaskDetailView zeigt korrekte Aufgabendaten an | E2E_TaskDetailNavigation | Aufgabentitel, -beschreibung und andere Felder werden korrekt angezeigt |
| Klick "Zurück" Button, ProjectDetailView wird angezeigt | E2E_TaskDetailNavigation | Navigation zurück zur Projektdetail funktioniert |
| Erstelle neue Aufgabe, fülle Titel ein, speichere, neue Aufgabe erscheint in Liste | E2E_CreateNewTaskNavigation | Neue Aufgabe mit Status "Neu" wird persistiert und in Liste angezeigt |
| Erstelle neue Aufgabe, klick Abbrechen, neue Aufgabe wird NICHT persistiert | E2E_CreateNewTaskNavigation | Abbrechen ohne Speichern verhindert Persistierung |

Welche bestehenden E2E-Tests müssen angepasst werden? **Keine** — bestehende Tests für Projektdetail sollten weiterhin funktionieren, da `ProjectDetailView` noch gerendert wird, nur nicht mehr inline mit `TaskDetailView`.

## Offene Punkte

Keine.
