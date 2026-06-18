# Umsetzungsplan: SCM-Issues in Aufgabenlisten integrieren

## Übersicht

Die Aufgabenliste in der Projektdetailansicht wird um eine Vorschlagsfunktion für SCM-Issues erweitert. Nach dem Laden der persistierten Aufgaben wird geprüft, ob das Repository ein SCM-Plugin mit Issue-Support hat; falls ja, werden offene Issues abgerufen und als separate Abschnitt "Offene Anforderungen" angezeigt. Klickt der Anwender auf ein Issue, wird eine Bestätigung angefordert und dann automatisch eine neue persistierte `Aufgabe` mit Status `Neu` erstellt, die auf das Issue referenziert. In der Aufgabendetailansicht werden neue Ribbon-Buttons zur Issue-Verwaltung hinzugefügt: Issue zuweisen, Issue öffnen.

## Designentscheidungen

| Komponente | Gewählter Ansatz | Begründung |
|-----------|-----------------|-----------|
| Issue-Präsentation | Separater Abschnitt "Offene Anforderungen" in Aufgabenliste | Visuelle Differenzierung hilft Nutzern, neue Issues von existenten Tasks zu unterscheiden; kann später mit Filter kombiniert werden |
| Issue-Filterung | Nur offene Issues (GitHub Plugin Default `state: open`) | Weniger API-Last, fokussiert auf handhabbare Issues; GitHub Plugin lädt bereits mit diesem Filter |
| Caching/Aktualisierung | Einmalig beim `LadenAsync()`, Refresh über `LadenCommand` | Einfacher zu implementieren; Nutzer hat explizite Kontrolle über Refresh via Laden-Button |
| Fehlerbehandlung | Grace-Degradation (leere Liste, Logging) | Konsistent mit `GetIssuesAsync` Implementierung; User Experience nicht unterbrochen |
| Issue-Doppel-Handling | Ausblenden nach Konvertierung | Reduziert UI-Clutter; `IssueReferenz` in `Aufgabe` dokumentiert die Verknüpfung |
| TaskDetail Issue-Dialog | Zuweisung, Neuzuweisung und Löschung (via "Entfernen"-Button) | Dialog ermöglicht volle Kontrolle: Issue zuweisen, wechseln oder Zuweisung löschen (Issue-Referenz auf null setzen) |
| Namenskonvention | `IssueSelectionDialog` und `IssueSelectionDialogViewModel` | Konsistent mit `PluginSelectionDialog`; beschreibt Dialog-Typ (Was), nicht Kontext (Wofür) |
| Plugin-Capability-Prüfung | Try-Catch + Typ-Check `(gitPlugin is IGitPlugin)` vor `GetIssuesAsync()` | Kein neues Interface-Marker nötig; LocalDirectory wirft `NotSupportedException`, die wird gehandhabt |

## Programmabläufe

### Issue-Laden in ProjectDetailViewModel

1. Nutzer navigiert zu Projektdetailansicht oder klickt "Laden"
2. `ProjectDetailViewModel.LadenAsync()` wird aufgerufen
3. Aufgaben werden wie bisher via `AufgabeService.GetByProjektAsync()` geladen → `Aufgaben` Collection gefüllt
4. Nach erfolgreichen Aufgaben-Laden: Prüfung `if (SelectedRepository != null && SelectedRepository.SCMPlugin is IGitPlugin gitPlugin)`
5. Falls true: `IsLoadingIssues = true` setzen
6. Aufruf `await _gitPlugin.GetIssuesAsync(repositoryId, ct)` → Returns `IEnumerable<Issue>`
7. Filtern: Issues entfernen, deren `IssueNummer` bereits in `Aufgaben` als `IssueReferenz` vorhanden ist
8. Gefilterte Issues in `IssueVorschlaege` (ObservableCollection) einfügen
9. Bei Fehler (z.B. API-Limit, Netzwerk): Exception loggen, `IssueVorschlaege` bleibt leer, kein Fehler-Dialog
10. `IsLoadingIssues = false` setzen
11. UI aktualisiert automatisch via Binding auf `IssueVorschlaege`

Beteiligte Klassen/Komponenten: `ProjectDetailViewModel`, `AufgabeService`, `IGitPlugin` (GitHub Plugin), `Issue` (Value Object)

### Issue-zu-Aufgabe Konvertierung

1. Nutzer sieht "Offene Anforderungen" Abschnitt mit Issues
2. Nutzer klickt auf ein Issue (z.B. auf Title)
3. `AufgabeAusIssueErstellenCommand` wird ausgeführt mit dem `Issue`-Parameter
4. Bestätigungsdialog wird angezeigt: "Issue '{Issue.Titel}' als Aufgabe erstellen?"
5. Bei Nutzer-Bestätigung: `AufgabeService.CreateFromIssueAsync(ProjektId, issue, SelectedRepository?.GitRepositoryId, ct)` aufrufen
6. Service erstellt neue `Aufgabe` mit:
   - `Titel` = `Issue.Titel`
   - `AnforderungsBeschreibung` = `Issue.Body`
   - `Status` = `AufgabeStatus.Neu`
   - `IssueReferenz` mit `IssueNummer`, `Titel`, `Body`, `LabelsJson`, `Milestone`, `IssueUrl`
7. Aufgabe wird in DB gespeichert
8. Issue wird aus `IssueVorschlaege` entfernt (via LINQ-Filterung)
9. Neue Aufgabe wird zu `Aufgaben` Collection hinzugefügt
10. `AktualisiereGefilterteAufgaben()` wird aufgerufen um Filter anzuwenden
11. UI zeigt neue Aufgabe in persistierter Aufgabenliste

Beteiligte Klassen/Komponenten: `ProjectDetailViewModel`, `AufgabeService`, `Aufgabe`, `IssueReferenz`, `Issue`

### Issue-Zuweisung im TaskDetailViewModel

1. Nutzer ist in `TaskDetailView` und sieht Task-Details
2. Ribbon zeigt "Issue zuweisen" Button (nur sichtbar wenn `CanAssignIssue == true`)
3. Nutzer klickt "Issue zuweisen"
4. `IssueZuweisenAsync()` wird aufgerufen
5. `IssueSelectionDialogViewModel` wird via DI erzeugt: `_serviceProvider.GetRequiredService<IssueSelectionDialogViewModel>()`
6. Dialog-ViewModel lädt Issues: `await _gitPlugin.GetIssuesAsync(repositoryId, ct)`
7. Dialog wird modal angezeigt (via `IDialogService.ShowDialog()` oder direktes `ShowDialog()`)
8. Nutzer wählt Issue oder klickt Abbrechen
9. Bei Auswahl: Dialog schließt sich und gibt `Issue` zurück
10. `AufgabeService.UpdateAsync()` wird aufgerufen um `Aufgabe.IssueReferenz` zu aktualisieren
11. `Aufgabe` wird neu geladen via `LadenAsync()`
12. `CurrentIssueReferenz` Property ist jetzt `!= null`, triggert PropertyChanged
13. "Issue öffnen" Button wird sichtbar wenn `CurrentIssueReferenz?.IssueUrl != null`
14. Klick "Issue öffnen" öffnet URL via `Process.Start(url)`

Beteiligte Klassen/Komponenten: `TaskDetailViewModel`, `IssueSelectionDialog`, `IssueSelectionDialogViewModel`, `IGitPlugin`, `AufgabeService`, `Aufgabe`, `IssueReferenz`

## Neue Klassen

| Klasse | Typ | Namespace | Datei | Zweck |
|--------|-----|-----------|-------|-------|
| `IssueSelectionDialogViewModel` | ViewModel | `Softwareschmiede.App.ViewModels` | `ViewModels/IssueSelectionDialogViewModel.cs` | Verwaltet Dialog-Zustand: Issue-Liste laden, Auswahl ermöglichen, Dialog-Ergebnis zurückgeben |
| `IssueSelectionDialog` | UserControl (XAML+Code-Behind) | `Softwareschmiede.App.Views` | `Views/Dialogs/IssueSelectionDialog.xaml` (+ `.xaml.cs`) | UI-Darstellung des modalen Issue-Auswahl-Dialogs mit ListView/DataGrid, OK/Abbrechen Buttons |

## Änderungen an bestehenden Klassen

### `ProjectDetailViewModel` (ViewModel)

- **Neue Eigenschaften:**
  - `IssueVorschlaege: ObservableCollection<Issue>` — Collection von geladenen Issues aus dem SCM-Plugin, initialisiert in Constructor
  - `IsLoadingIssues: bool` — zeigt an, ob Issues gerade geladen werden (für Loading-Spinner in UI)
  - `KannIssuesLaden: bool` (computed/read-only) — `true` wenn `SelectedRepository?.SCMPlugin is IGitPlugin`

- **Neue Methoden:**
  - `private async Task LadenIssuesAsync(CancellationToken ct)` — lädt Issues vom SCM-Plugin, handhabt Fehler mit Grace-Degradation, filtert bereits konvertierte Issues
  - Wird aufgerufen am Ende von `LadenAsync()`, nachdem Aufgaben geladen sind

- **Neue Events/Commands:**
  - `AufgabeAusIssueErstellenCommand: AsyncRelayCommand<Issue>` — Parameter: Issue aus Vorschläge
    - Zeigt Bestätigungsdialog
    - Ruft `AufgabeService.CreateFromIssueAsync()` auf
    - Entfernt Issue aus `IssueVorschlaege`
    - Fügt neue Aufgabe zu `Aufgaben` hinzu
    - Ruft `AktualisiereGefilterteAufgaben()` auf

- **Änderungen an bestehenden Methoden:**
  - `LadenAsync()` wird angepasst: Am Ende `await LadenIssuesAsync(ct)` aufrufen (nach Aufgaben-Laden)

### `TaskDetailViewModel` (ViewModel)

- **Neue Eigenschaften:**
  - `CanAssignIssue: bool` (computed/read-only) — `true` wenn `Aufgabe != null && SelectedRepository?.SCMPlugin is IGitPlugin && !IsCliRunning`
  - `CurrentIssueReferenz: IssueReferenz?` (computed/read-only) — `return Aufgabe?.IssueReferenz`

- **Neue Methoden:**
  - `private async Task IssueZuweisenAsync(CancellationToken ct)` — öffnet Dialog zur Issue-Auswahl
    - Erstellt `IssueSelectionDialogViewModel` via DI
    - Zeigt Dialog modal
    - Bei Auswahl: ruft `AufgabeService.UpdateAsync()` auf mit neuer `IssueReferenz`
    - Lädt Aufgabe neu via `await LadenAsync()`

- **Neue Events/Commands:**
  - `IssueZuweisenCommand: AsyncRelayCommand` — triggert `IssueZuweisenAsync()`
    - CanExecute: `CanAssignIssue && !IsLoading`
  - `IssueBrowserOeffnenCommand: RelayCommand` — öffnet `IssueUrl` im Browser
    - CanExecute: `CurrentIssueReferenz?.IssueUrl != null`
    - Führe aus: `Process.Start(CurrentIssueReferenz.IssueUrl)`

- **Änderungen an bestehenden Properties:**
  - Beim Setzen von `Aufgabe`: `OnPropertyChanged(nameof(CanAssignIssue))` und `OnPropertyChanged(nameof(CurrentIssueReferenz))` aufrufen

## Datenbankmigrationen

Keine. `Aufgabe` und `IssueReferenz` sind bereits im EF-Modell konfiguriert mit 1:1-Relationship. Die Navigation `Aufgabe.IssueReferenz` ist bereits vorhanden.

## Validierungsregeln

Keine neuen Validierungsregeln erforderlich. Bestehende Validierungen für `Aufgabe` (Titel nicht leer, ProjektId erforderlich) gelten weiterhin. `IssueReferenz` wird automatisch via `CreateFromIssueAsync` mit allen erforderlichen Feldern befüllt.

## Konfigurationsänderungen

Keine erforderlich. Issue-Funktionalität wird vollständig durch Plugin-Capability bestimmt (ob `IGitPlugin` vorhanden ist und `GetIssuesAsync` implementiert).

## Seiteneffekte und Risiken

- **GitHub API Rate-Limit:** Bei vielen Issues kann `GetIssuesAsync` durch Rate-Limit eingeschränkt werden. Mitigation: Limit auf 100 Issues im GitHub Plugin ist bereits implementiert; Grace-Degradation handhabt Fehler.
- **Doppelte Issue-Konvertierung:** Falls Nutzer auf denselben Issue zweimal klickt, könnte es zu mehreren `Aufgabe`-Einträgen mit demselben Issue kommen. Mitigation: `IssueVorschlaege` wird nach Konvertierung aktualisiert; UI sollte Command während Ausführung deaktivieren.
- **LocalDirectory Plugin Inkompatibilität:** `LocalDirectoryPlugin.GetIssuesAsync()` wirft `NotSupportedException`. Mitigation: Try-Catch in `LadenIssuesAsync()` mit Logging; Issue-Buttons sind nur sichtbar wenn Plugin Issue-Support hat.
- **Performance bei vielen Aufgaben:** Filterung bereits konvertierter Issues (Abgleich `Issue.Nummer` mit allen `Aufgaben.IssueReferenz.IssueNummer`) könnte O(n*m) sein. Mitigation: Für typische Projekt-Größen (< 1000 Aufgaben) unkritisch; später evtl. als HashSet optimieren.
- **UI-Update-Race:** Wenn Issues geladen und gleichzeitig neue Aufgabe erstellt wird, könnte Filterung fehlschlagen. Mitigation: Semaphore oder Lock auf `IssueVorschlaege` während `LadenIssuesAsync()` ausgeführt wird.

## Umsetzungsreihenfolge

1. **`IssueSelectionDialogViewModel` implementieren**
   - Voraussetzungen: `IGitPlugin` Interface, `Issue` Value Object, MVVM-Infrastruktur (AsyncRelayCommand, PropertyChanged), DI-Container
   - Beschreibung: ViewModel mit Properties (`VerfuegbareIssues`, `SelectedIssue`, `IsLoading`), Commands (`BestaetigenCommand`, `AbbrechenCommand`), Event (`CloseRequested`) implementieren. `LoadAsync()` aufrufen via Constructor mit DI-Injection von `IGitPlugin`. Fehlerbehandlung mit Try-Catch.

2. **`IssueSelectionDialog.xaml` + `.xaml.cs` implementieren**
   - Voraussetzungen: `IssueSelectionDialogViewModel`, XAML-Infrastruktur (DataContext, Binding), WPF-Controls
   - Beschreibung: Modal Dialog mit ListView oder DataGrid für Issues (Spalten: Nummer, Titel, Body-Preview), OK- und Abbrechen-Buttons. Bindings zu ViewModel. Dialog-Result setzen auf OK/Cancel.

3. **`ProjectDetailViewModel` um Issue-Funktionalität erweitern**
   - Voraussetzungen: `IssueSelectionDialogViewModel` und Dialog sind implementiert, `IGitPlugin.GetIssuesAsync()` Methode vorhanden, Bestätigungsdialog-Infrastruktur (IDialogService oder MessageBox)
   - Beschreibung: `IssueVorschlaege` Collection, `IsLoadingIssues`, `KannIssuesLaden` Properties hinzufügen. `LadenIssuesAsync()` Methode implementieren mit Try-Catch, Filterung, Grace-Degradation. `AufgabeAusIssueErstellenCommand` implementieren. `LadenAsync()` anpassen.

4. **`TaskDetailViewModel` um Issue-Funktionalität erweitern**
   - Voraussetzungen: `IssueSelectionDialogViewModel` und Dialog sind implementiert, `IServiceProvider` für DI in TaskDetailViewModel
   - Beschreibung: `CanAssignIssue`, `CurrentIssueReferenz` Properties hinzufügen. `IssueZuweisenAsync()` Methode mit Dialog-Öffnung implementieren. `IssueZuweisenCommand`, `IssueBrowserOeffnenCommand` implementieren. Aufgabe-Property Setter anpassen.

5. **`ProjectDetailView.xaml` anpassen für Issue-Anzeige**
   - Voraussetzungen: `ProjectDetailViewModel` hat `IssueVorschlaege` Collection und Commands, XAML-Controls (DataGrid/ListView, Loading-Spinner)
   - Beschreibung: Neue Sektion in Aufgabenliste für "Offene Anforderungen". Binding zu `IssueVorschlaege`. Binding zu `IsLoadingIssues` für Spinner. Command-Binding zu `AufgabeAusIssueErstellenCommand`. Optional: Template-Selector um persistierte Aufgaben und Issues unterschiedlich zu rendern.

6. **`TaskDetailView.xaml` anpassen für Issue-Buttons im Ribbon**
   - Voraussetzungen: `TaskDetailViewModel` hat Issue-Commands und Properties, Ribbon-Infrastruktur
   - Beschreibung: Neue Buttons oder Gruppe im Ribbon: "Issue zuweisen", "Issue öffnen". Bindings zu `CanAssignIssue`, `CurrentIssueReferenz?.IssueUrl != null`, `CanExecute`. Command-Bindings zu `IssueZuweisenCommand`, `IssueBrowserOeffnenCommand`. Visuelle Kennzeichnung (Icon, Label).

7. **Unit-Tests für `ProjectDetailViewModel.LadenIssuesAsync()`**
   - Voraussetzungen: Mock-Infrastruktur für Tests (Moq, Xunit), Test-Base-Klassen
   - Beschreibung: Tests schreiben für: Issues laden wenn Plugin vorhanden, leere Liste wenn Plugin nicht vorhanden, Exception-Handling, Filterung bereits konvertierter Issues, `IsLoadingIssues` Flag Lifecycle.

8. **Unit-Tests für `TaskDetailViewModel.IssueZuweisenAsync()`**
   - Voraussetzungen: Mock-Infrastruktur, `IssueSelectionDialogViewModel` Mock
   - Beschreibung: Tests schreiben für: Dialog-Öffnung, Auswahl-Handling, Update via Service, Reload, `CanAssignIssue` CanExecute-Logic.

9. **Unit-Tests für `IssueSelectionDialogViewModel`**
   - Voraussetzungen: Mock-Infrastruktur
   - Beschreibung: Tests schreiben für: Issue-Laden, SelectedIssue-Änderung, Command-Ausführung (Bestätigung/Abbrechen), CloseRequested-Event.

10. **E2E-Tests für Issue-Workflows**
    - Voraussetzungen: E2E-Test-Infrastruktur (WinAppDriver oder ähnlich), Datenbankseeding mit Test-Projekten und Repositories
    - Beschreibung: Tests schreiben für: Issue-Abschnitt ist sichtbar bei GitHub-Repository, Issue-Klick öffnet Bestätigungsdialog, Bestätigung erstellt neue Aufgabe, Issue-Button im Task-Detail sichtbar/unsichtbar je nach Plugin, LocalDirectory-Plugin hat keine Issue-Buttons.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `LadenIssuesAsync_LoadsIssuesWhenRepositorySupportsIssues()` | `ProjectDetailViewModelTests` | Issues werden geladen wenn IGitPlugin vorhanden ist |
| `LadenIssuesAsync_ReturnsEmptyListWhenPluginDoesNotSupport()` | `ProjectDetailViewModelTests` | Leere Liste wenn SCMPlugin ist nicht IGitPlugin |
| `LadenIssuesAsync_HandlesExceptionGracefully()` | `ProjectDetailViewModelTests` | Exception wird geloggt, `IssueVorschlaege` bleibt leer, `IsLoadingIssues = false` |
| `LadenIssuesAsync_FiltersOutAlreadyConvertedIssues()` | `ProjectDetailViewModelTests` | Issues deren IssueNummer bereits in Aufgaben existiert, werden gefiltert |
| `AufgabeAusIssueErstellenAsync_CreatesAufgabeAndRemovesFromVorschlaege()` | `ProjectDetailViewModelTests` | Issue wird in Aufgabe konvertiert, aus IssueVorschlaege entfernt, zu Aufgaben hinzugefügt |
| `AufgabeAusIssueErstellenAsync_UserCancellation_DoesNothing()` | `ProjectDetailViewModelTests` | Wenn Nutzer Bestätigungsdialog abbricht, nichts passiert |
| `IssueZuweisenAsync_ShowsDialogAndUpdatesCurrentIssueReferenz()` | `TaskDetailViewModelTests` | Dialog wird geöffnet, bei Auswahl wird IssueReferenz aktualisiert |
| `IssueZuweisenAsync_UserAbortDoesNothing()` | `TaskDetailViewModelTests` | Wenn Dialog abgebrochen, IssueReferenz unverändert |
| `IssueBrowserOeffnenCommand_OpensUrlWhenAvailable()` | `TaskDetailViewModelTests` | `Process.Start()` wird mit korrekter URL aufgerufen |
| `IssueBrowserOeffnenCommand_CannotExecuteWhenUrlNull()` | `TaskDetailViewModelTests` | CanExecute gibt false zurück wenn IssueUrl null |
| `CanAssignIssue_TrueWhenAufgabeExistsAndPluginSupportsIssues()` | `TaskDetailViewModelTests` | CanAssignIssue ist true unter Bedingungen |
| `CanAssignIssue_FalseWhenCliRunning()` | `TaskDetailViewModelTests` | CanAssignIssue ist false wenn IsCliRunning == true |
| `LoadAsync_PopulatesVerfuegbareIssues()` | `IssueSelectionDialogViewModelTests` | Issues werden geladen und in Collection gefüllt |
| `LoadAsync_HandlesExceptionGracefully()` | `IssueSelectionDialogViewModelTests` | Exception bei GetIssuesAsync wird gehandhabt, `IsLoading = false` |
| `SelectedIssue_UpdatesKannBestaetigen()` | `IssueSelectionDialogViewModelTests` | Wenn Issue selektiert, `KannBestaetigen` wird true |
| `BestaetigenCommand_RaisesCloseRequestedWithSelectedIssue()` | `IssueSelectionDialogViewModelTests` | Befehl löst CloseRequested-Event mit true aus |
| `AbbrechenCommand_RaisesCloseRequestedWithFalse()` | `IssueSelectionDialogViewModelTests` | Befehl löst CloseRequested-Event mit false aus |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `ProjectDetailViewModelTests` (allgemein) | Setup muss `IGitPlugin` Mock bereitstellen; `LadenAsync` ruft jetzt auch `LadenIssuesAsync()` auf |
| `TaskDetailViewModelTests` (allgemein) | Setup muss `IServiceProvider` Mock bereitstellen; `Aufgabe`-Property Setter triggert neue PropertyChanged-Events |
| `AufgabeServiceTests` (Integration) | Keine Anpassung nötig; `CreateFromIssueAsync` wird bereits getestet |

### E2E-Tests (Pflicht)

| Szenario | Testklasse / Testdatei | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Issue-Vorschläge sind sichtbar bei GitHub-Plugin | `IssueIntegration_E2E.cs` | "Offene Anforderungen" Abschnitt ist sichtbar in Aufgabenliste |
| Issue-Klick öffnet Bestätigung | `IssueIntegration_E2E.cs` | Dialog mit Issue-Details wird angezeigt |
| Bestätigung erstellt neue Aufgabe | `IssueIntegration_E2E.cs` | Nach Bestätigung existiert neue Aufgabe mit korrektem Titel, Body, IssueReferenz |
| Konvertierte Issue verschwinden aus Vorschläge | `IssueIntegration_E2E.cs` | Nach Konvertierung ist Issue nicht mehr im "Offene Anforderungen" Abschnitt |
| Issue-Buttons sichtbar in Task-Detail | `IssueIntegration_E2E.cs` | Ribbon hat "Issue zuweisen" und "Issue öffnen" Buttons bei Aufgabe mit Issue-Support |
| Issue zuweisen öffnet Dialog | `IssueIntegration_E2E.cs` | Klick "Issue zuweisen" öffnet Modal mit Issue-Liste |
| Issue-Öffnen-Button funktioniert | `IssueIntegration_E2E.cs` | Klick "Issue öffnen" öffnet Browser mit korrekter URL |
| LocalDirectory-Plugin hat keine Issue-UI | `IssueIntegration_E2E.cs` | Mit LocalDirectory-Plugin sind Issue-Buttons nicht sichtbar, "Offene Anforderungen" ist leer |
| GitHub-Plugin zeigt Issue-UI | `IssueIntegration_E2E.cs` | Mit GitHub-Plugin sind Issue-Buttons sichtbar, Issues werden geladen |

Welche bestehenden E2E-Tests sind betroffen? Keine bekannten bestehenden E2E-Tests sollten durch diese Feature beeinträchtigt werden, da die Issue-Funktionalität optional und Plugin-abhängig ist. Alle Aufgaben-Workflows funktionieren auch ohne Issue-Support.

## Offene Punkte

Keine. Alle 8 ursprünglichen offenen Punkte wurden geklärt und sind als Designentscheidungen/Programmablauf-Spezifikationen dokumentiert:

1. ✓ **Visual Präsentation** — Dokumentiert: Getrennte Abschnitte "Aufgaben" und "Offene Anforderungen"
2. ✓ **Issue-Filterung** — Dokumentiert: Nur offene Issues (GitHub Plugin Default `state: open`)
3. ✓ **Caching/Aktualisierung** — Dokumentiert: Einmalig beim `LadenAsync()`, Refresh via `LadenCommand`
4. ✓ **Fehlerbehandlung** — Dokumentiert: Grace-Degradation mit Logging, kein Dialog
5. ✓ **Issue-Doppel-Handling** — Dokumentiert: Ausblenden nach Konvertierung
6. ✓ **Issue-Dialog Löschfunktion** — Dokumentiert: Ja, "Entfernen"-Button ermöglicht Löschen der Zuweisung
7. ✓ **Namenskonvention** — Dokumentiert: `IssueSelectionDialog` und `IssueSelectionDialogViewModel`
8. ✓ **GitHub Plugin Filter** — Dokumentiert: Default `state: open` wird beibehalten
