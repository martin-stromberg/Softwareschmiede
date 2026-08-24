# Umsetzungsplan: UI-Integration Autonomer Aufgaben

## Übersicht

Der bisherige separate Dialog für Autonome Aufgaben (`AutonomAufgabeDetailDialog`) wird aus dem Fenster-Lifecycle entfernt und sein Inhalt als neue Registerkarte "Automatisierung" in die bestehende `TaskDetailView` integriert. Die drei Aktionsbuttons "Start", "Stop", "Resume" migrieren vom Dialog-Fenster in das Ribbon-Menü der Aufgaben-Detailansicht. Die Integration erfolgt durch Erweiterung von `TaskDetailViewModel` um ein neues `DetailAnsicht`-Enum-Wert, neue Properties und Commands sowie durch Anpassung der Service-Integration in `AutonomAufgabeStartService`.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Ribbon-Button-Sichtbarkeit | Start/Stop/Resume-Buttons sind sichtbar, wenn `ShowAutomatisierungPanel == true`, unabhängig von der gerade aktiven Registerkarte. | Konsistent mit anderen Ribbon-Buttons (z.B. CLI-Start/Stop sind auch sichtbar, bevor Benutzer zur CLI-Ansicht wechselt). Bessere UX: Benutzer kann Start-Button direkt klicken. |
| Verhalten beim Aufgabenwechsel | Beim Wechsel zu einer anderen Aufgabe wird `_autonomAufgabeDetailViewModel` auf `null` gesetzt und die "Automatisierung"-Registerkarte ausgeblendet (via `WaehleStandardAnsicht()`). | Konsistent mit bestehenden Registerkarten (Diff-Ansicht wird auch ausgeblendet, wenn Status != Beendet). Verhindert Verwirrung bei mehreren parallelen Autonomen Aufgaben — jede Aufgabe zeigt ihre eigene Autonome Aufgabe. |
| Dialog-Fallback | `ShowAutonomAufgabeDetailAsync()` wird vollständig aus `IDialogService` / `WpfDialogService` entfernt. | Folgt CLAUDE.md-Grundsatz: Keine Features/Code für hypothetische Zukunftsanforderungen. Nach der Integration wird der Dialog nicht mehr benötigt. Bei zukünftiger Anforderung kann er leicht wieder implementiert werden. |
| Service-Integration (Abhängigkeits-Auflösung) | **Korrigiert gegenüber ursprünglichem Entwurf** (siehe Begründung): `AutonomAufgabeStartService` bekommt KEINE `TaskDetailViewModel`-Abhängigkeit. Stattdessen liefert `StarteAsync()` das erzeugte `AutonomAufgabeDetailViewModel` über den bestehenden Rückgabewert `AutonomAufgabeStartResult` (neue Property `AutonomAufgabeDetailViewModel? DetailViewModel`) zurück. Die aufrufende `TaskDetailViewModel.AutonomAufgabeInitialisierenAsync()` (die bereits `_autonomAufgabeStartService.StarteAsync(_aufgabe, ct)` direkt awaited, siehe `TaskDetailViewModel.cs:1219`) ruft danach selbst `await SetzeAutonomAufgabeDetailViewAsync(ergebnis.DetailViewModel)` auf `this` auf. | **Der ursprünglich vorgesehene Ansatz (`TaskDetailViewModel` als Constructor-Parameter von `AutonomAufgabeStartService`) ist fehlerhaft und wurde verworfen:** `AutonomAufgabeStartService` ist `AddScoped` registriert, `TaskDetailViewModel` hingegen `AddTransient` (siehe `App.xaml.cs:212` bzw. `:274`). Eine Constructor-Injection von `TaskDetailViewModel` in `AutonomAufgabeStartService` würde vom DI-Container eine **neue, andere** Transient-Instanz auflösen als diejenige, die tatsächlich an die gerade offene `TaskDetailView` gebunden ist — der Aufruf von `SetzeAutonomAufgabeDetailViewAsync()` hätte dann schlicht keine sichtbare Wirkung in der UI. Die gewählte Lösung (Rückgabe über den bestehenden Result-Record, Aufruf durch die bereits aufrufende ViewModel-Instanz) vermeidet dieses Problem vollständig, benötigt keine neue Abhängigkeit und ist kleiner als der ursprüngliche Entwurf. |
| Automatische Initialisierung beim Laden | Nein: Keine automatische Überprüfung beim Laden. Nur wenn Benutzer explizit "Autonome Aufgabe starten" klickt. | Konsistent mit anderen Registerkarten (PR-Liste wird nicht automatisch aktualisiert). Sicherer: Verhindert unerwartete Agent-Starts. |
| Responsive Design / Spacing | `AutonomAufgabeDetailView.xaml` behält `Margin="24"` (Dialog-Nutzung). Im neuen Container in `TaskDetailView` wird ein Wrapper-Element mit angepasstem Padding für Registerkarten-Konsistenz verwendet. | Ermöglicht Wiederverwendung der View als Dialog später. Spacing-Anpassung erfolgt ohne View-Änderung. |

## Programmabläufe

### Ablauf 1: Autonome Aufgabe starten (von TaskDetailView aus)

1. Benutzer klickt "Autonome Aufgabe starten" Button im Ribbon
2. `AutonomAufgabeInitialisierenCommand` in `TaskDetailViewModel` wird ausgeführt
3. `AutonomAufgabeInitialisierenAsync()` wird aufgerufen, die `_autonomAufgabeStartService.StarteAsync()` direkt awaited
4. `AutonomAufgabeStartService.StarteAsync()` zeigt Initialisierungsdialog
5. Bei Erfolg: Service erstellt `AutonomAufgabeDetailViewModel` und gibt es als `DetailViewModel`-Property im zurückgegebenen `AutonomAufgabeStartResult` mit zurück (kein Dialog-Aufruf mehr)
6. `AutonomAufgabeInitialisierenAsync()` ruft mit dem Ergebnis `await SetzeAutonomAufgabeDetailViewAsync(ergebnis.DetailViewModel)` auf `this` auf; diese Methode speichert das ViewModel, triggert PropertyChanged-Events, wechselt zur Automatisierung-Ansicht
7. TaskDetailView zeigt neue Registerkarte mit eingebettetem `AutonomAufgabeDetailView`
8. Benutzer sieht Automatisierung-Tab mit Start/Stop/Resume-Buttons (auch im Ribbon sichtbar)

Beteiligte Klassen/Komponenten: `TaskDetailViewModel`, `AutonomAufgabeStartService`, `AutonomAufgabeDetailViewModel`, `TaskDetailView`

### Ablauf 2: Ansicht-Umschaltung (Automatisierung-Registerkarte)

1. Benutzer klickt "Automatisierung" Button in der Ansicht-Button-Reihe (oder Ribbon-Button triggert gleich Ansicht)
2. `AutomatisierungViewCommand` wird ausgeführt
3. `WaehleAnsicht(DetailAnsicht.Automatisierung)` wird aufgerufen
4. `WaehleAnsicht()` validiert: Falls `ShowAutomatisierungPanel == false`, wechsel zu Info-Ansicht statt
5. `_ausgewaehlteAnsicht` wird gesetzt, `OnPropertyChanged(nameof(IsAutomatisierungViewSelected))` triggert
6. TaskDetailView bindet auf `IsAutomatisierungViewSelected` und zeigt Container mit `AutonomAufgabeDetailView` an

Beteiligte Klassen/Komponenten: `TaskDetailViewModel`, `TaskDetailView`, `AutonomAufgabeDetailView`

### Ablauf 3: Aufgabenwechsel (mit Cleanup)

1. Benutzer klickt andere Aufgabe in Liste
2. `TaskDetailViewModel.Aufgabe` setter wird mit neuer Aufgabe aufgerufen
3. Setter triggert `WaehleStandardAnsicht()`
4. `WaehleStandardAnsicht()` entfernt alte `_autonomAufgabeDetailViewModel` (setzt auf `null`), triggert PropertyChanged
5. `WaehleAnsicht()` wird mit Standard-Ansicht (Info/Cli/Diff) aufgerufen
6. Falls neue Aufgabe autonom ist und Benutzer war in Automatisierung-Ansicht, wird alte ViewModel gelöscht und neue Ansicht geladen später (nur wenn Benutzer wieder auf "Autonome Aufgabe starten" klickt)

Beteiligte Klassen/Komponenten: `TaskDetailViewModel`, `WaehleStandardAnsicht()`, `WaehleAnsicht()`

### Ablauf 4: Start/Stop/Resume-Commands im Ribbon

1. Benutzer klickt "Start"/"Stop"/"Resume" Button im Ribbon (Gruppe "Autonome Aufgabe")
2. Button bindet auf `AutonomAufgabeDetailViewModel.StartCommand` / `StopCommand` / `ResumeCommand`
3. Diese Commands sind weiterhin in `AutonomAufgabeDetailViewModel` implementiert (keine Änderungen)
4. Ribbon-Buttons sind nur sichtbar wenn `ShowAutomatisierungPanel == true` (Visibility-Binding)

Beteiligte Klassen/Komponenten: `TaskDetailViewModel`, `AutonomAufgabeDetailViewModel`, `TaskDetailView.xaml`

## Neue Klassen

Keine neuen Klassen erforderlich.

## Änderungen an bestehenden Klassen

### `TaskDetailViewModel` (ViewModel)

**Neue Enum-Werte:**
- `DetailAnsicht.Automatisierung` — Neue Registerkarte für Autonome Aufgaben

**Neue Eigenschaften:**
- `AutonomAufgabeDetailViewModel?` — Hält Referenz zum ViewModel der Automatisierung-Registerkarte (null wenn keine Autonome Aufgabe aktiv)
- `IsAutomatisierungViewSelected { get; }` (bool) — True wenn `_ausgewaehlteAnsicht == DetailAnsicht.Automatisierung`
- `ShowAutomatisierungPanel { get; }` (bool) — True wenn `AutonomAufgabeDetailViewModel != null` (d.h. eine Autonome Aufgabe wurde initialisiert)

**Neue Commands:**
- `AutomatisierungViewCommand` (ICommand) — Ruft `WaehleAnsicht(DetailAnsicht.Automatisierung)` auf; CanExecute: `ShowAutomatisierungPanel`

**Neue Methode:**
- `SetzeAutonomAufgabeDetailViewAsync(AutonomAufgabeDetailViewModel? vm)` (public, async Task) — Wird von `AutonomAufgabeStartService` aufgerufen; speichert ViewModel, triggert PropertyChanged-Events (`ShowAutomatisierungPanel`, `AutonomAufgabeDetailViewModel`), wechselt zur Automatisierung-Ansicht

**Änderung an `WaehleAnsicht()`:**
- Neue Validierung: Falls `ansicht == DetailAnsicht.Automatisierung && !ShowAutomatisierungPanel`, setze auf `DetailAnsicht.Info`
- Neue PropertyChanged-Notification: `nameof(IsAutomatisierungViewSelected)`

**Änderung an `WaehleStandardAnsicht()`:**
- Vor Auswahl der Standard-Ansicht: `_autonomAufgabeDetailViewModel = null;` (Cleanup beim Aufgabenwechsel)
- Trigger PropertyChanged für `ShowAutomatisierungPanel`

**Änderung am Aufgabe-Property Setter:**
- Stellt sicher, dass `WaehleStandardAnsicht()` aufgerufen wird, um alte Autonome Aufgabe zu bereinigen

### `AutonomAufgabeStartService` (Service)

**Keine neue Abhängigkeit.** (Korrektur gegenüber ursprünglichem Entwurf — siehe Designentscheidungen: Eine Abhängigkeit auf `TaskDetailViewModel` wäre wegen unterschiedlicher DI-Lifetimes fehlerhaft.)

**Änderung an `StarteAsync()`:**
- Zeile `await _dialogService.ShowAutonomAufgabeDetailAsync(detailVm, ct);` (Zeile ~59) wird ersatzlos entfernt (kein Dialog-Aufruf mehr)
- `return new AutonomAufgabeStartResult(aktuelleAufgabe, null);` (Zeile 60) wird zu `return new AutonomAufgabeStartResult(aktuelleAufgabe, null, detailVm);` — das erzeugte `detailVm` wird im Result mitgegeben

### `AutonomAufgabeStartResult` (Record)

**Neue Property:**
- `AutonomAufgabeDetailViewModel? DetailViewModel` — das bei erfolgreicher Initialisierung erzeugte ViewModel; `null` bei Fehlern oder Abbruch. Record-Definition erweitert sich von `(Aufgabe? AktualisierteAufgabe, string? FehlerMeldung)` auf `(Aufgabe? AktualisierteAufgabe, string? FehlerMeldung, AutonomAufgabeDetailViewModel? DetailViewModel)`.

### `TaskDetailViewModel.AutonomAufgabeInitialisierenAsync()` (bestehende Methode, Zeile 1212–1234)

**Änderung:** Nach dem bestehenden Block (`Aufgabe = ergebnis.AktualisierteAufgabe` / `FehlerMeldung = ergebnis.FehlerMeldung`) wird ergänzt:
```csharp
if (ergebnis.DetailViewModel is not null)
{
    await SetzeAutonomAufgabeDetailViewAsync(ergebnis.DetailViewModel);
}
```

### `TaskDetailView.xaml` (View)

**Neue Ribbon-Buttons (Gruppe "Autonome Aufgabe", nach bestehendem "Autonome Aufgabe starten" Button):**
- Button "Start" — `ButtonCommand="{Binding AutonomAufgabeDetailViewModel.StartCommand}"`, `Visibility="{Binding ShowAutomatisierungPanel, Converter={StaticResource BoolToVisibilityConverter}}"`
- Button "Stop" — analog
- Button "Resume" — analog

**Neue Ansicht-Button-Reihe (nach "Todos" Button, Zeile ~312):**
- Button "Automatisierung" — `Command="{Binding AutomatisierungViewCommand}"`, `Visibility="{Binding ShowAutomatisierungPanel, Converter={StaticResource BoolToVisibilityConverter}}"`

**Neuer Container für Automatisierung-Ansicht (Grid.Row="1", nach anderen Registerkarten-Containern):**
- `ScrollViewer` oder `StackPanel` mit `Visibility="{Binding IsAutomatisierungViewSelected, Converter={StaticResource BoolToVisibilityConverter}}"`
- Enthält: `<views:AutonomAufgabeDetailView DataContext="{Binding AutonomAufgabeDetailViewModel}" />`
- Wrapper mit angepasstem Padding (z.B. `Padding="12"`) für Konsistenz mit anderen Registerkarten-Inhalten

### `IDialogService` (Interface)

**Entfernung:**
- Methode `ShowAutonomAufgabeDetailAsync(AutonomAufgabeDetailViewModel viewModel, CancellationToken ct)` wird entfernt

### `WpfDialogService` (Service Implementation)

**Entfernung:**
- Implementierung von `ShowAutonomAufgabeDetailAsync()` wird entfernt

### `AutonomAufgabeDetailDialog.xaml(.cs)` (View/CodeBehind)

**Status:** Kann entfernt oder archiviert werden. Wird nach dieser Anforderung nicht mehr verwendet.
- Falls Entfernung: Diese Datei und `.xaml.cs` werden gelöscht

### `AutonomAufgabeDetailViewModel` (ViewModel)

**Keine Breaking Changes** — ViewModel bleibt unverändert. Commands (`StartCommand`, `StopCommand`, `ResumeCommand`) werden weiterhin von Ribbon-Buttons gebunden statt vom Dialog.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine neuen Validierungen erforderlich.

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **DI-Lifetime-Falle vermieden:** Der ursprünglich erwogene Ansatz (`AutonomAufgabeStartService` erhält `TaskDetailViewModel` als Constructor-Abhängigkeit) wurde verworfen, da `AutonomAufgabeStartService` `AddScoped` und `TaskDetailViewModel` `AddTransient` registriert sind — eine Constructor-Injection hätte eine andere Instanz als die tatsächlich angezeigte geliefert (siehe Designentscheidungen). Die gewählte Lösung (Rückgabe über `AutonomAufgabeStartResult`, Aufruf durch die bereits aufrufende ViewModel-Instanz) hat kein Zirkularitäts- oder Lifetime-Risiko.

- **Betroffene bestehende Registerkarte-Navigation:** Die neue Automatisierung-Ansicht muss in der bestehenden Ansicht-Umschalte-Logik berücksichtigt werden. Andere Code-Stellen, die auf `DetailAnsicht` passen (z.B. mit `switch`-Statements), kössen das neue Enum-Wert vergessen.

- **Tests:** Alle bestehenden Tests, die `IDialogService.ShowAutonomAufgabeDetailAsync()` mocken, müssen angepasst werden. Tests für `AutonomAufgabeStartService` müssen `TaskDetailViewModel`-Dependency mocken und `SetzeAutonomAufgabeDetailViewAsync()` aufrufe verifizieren.

- **Dialog-Migration:** Wenn Benutzer aktuell die separate Dialog-UI nutzen, werden sie diese UI nach der Integration nicht mehr sehen. Das ist gewünscht, aber muss benutzerfreundlich kommuniziert werden.

## Umsetzungsreihenfolge

1. **Enum-Wert hinzufügen**
   - Voraussetzungen: Keine
   - Beschreibung: `DetailAnsicht.Automatisierung` zu `TaskDetailViewModel` hinzufügen (Zeile 26–34)

2. **TaskDetailViewModel erweitern: Properties und Commands**
   - Voraussetzungen: Schritt 1 abgeschlossen
   - Beschreibung: Neue Properties `IsAutomatisierungViewSelected`, `ShowAutomatisierungPanel`, `AutonomAufgabeDetailViewModel?` hinzufügen; neuen Command `AutomatisierungViewCommand` initialisieren

3. **TaskDetailViewModel erweitern: Neue Methode `SetzeAutonomAufgabeDetailViewAsync()`**
   - Voraussetzungen: Schritt 2 abgeschlossen
   - Beschreibung: Neue public async Task-Methode, die ViewModel speichert, PropertyChanged triggert, Ansicht wechselt

4. **TaskDetailViewModel erweitern: `WaehleAnsicht()` und `WaehleStandardAnsicht()` anpassen**
   - Voraussetzungen: Schritt 3 abgeschlossen
   - Beschreibung: `WaehleAnsicht()` mit Validierung für Automatisierung-Ansicht erweitern; `WaehleStandardAnsicht()` um Cleanup von `_autonomAufgabeDetailViewModel` ergänzen

5. **TaskDetailView erweitern: XAML-Container für Automatisierung-Ansicht**
   - Voraussetzungen: Schritte 1-4 abgeschlossen
   - Beschreibung: Neuer ScrollViewer/StackPanel-Container mit `AutonomAufgabeDetailView`, Visibility-Binding auf `IsAutomatisierungViewSelected`

6. **TaskDetailView erweitern: Ansicht-Button und Ribbon-Buttons**
   - Voraussetzungen: Schritt 5 abgeschlossen
   - Beschreibung: Button "Automatisierung" in Ansicht-Button-Reihe; Buttons "Start", "Stop", "Resume" in Ribbon-Gruppe "Autonome Aufgabe"

7. **AutonomAufgabeStartResult erweitern, AutonomAufgabeStartService und AutonomAufgabeInitialisierenAsync anpassen**
   - Voraussetzungen: Schritt 3 abgeschlossen
   - Beschreibung: `AutonomAufgabeStartResult` um Property `DetailViewModel` erweitern; in `AutonomAufgabeStartService.StarteAsync()` den Dialog-Aufruf (Zeile ~59) entfernen und `detailVm` stattdessen im Result zurückgeben; in `TaskDetailViewModel.AutonomAufgabeInitialisierenAsync()` nach Erhalt des Ergebnisses `SetzeAutonomAufgabeDetailViewAsync(ergebnis.DetailViewModel)` aufrufen

8. **Aus `IDialogService` entfernen: `ShowAutonomAufgabeDetailAsync()` Signatur**
   - Voraussetzungen: Schritt 7 abgeschlossen (keine Aufrufer mehr vorhanden)
   - Beschreibung: Methoden-Signatur aus Interface entfernen

9. **Aus `WpfDialogService` entfernen: `ShowAutonomAufgabeDetailAsync()` Implementierung**
   - Voraussetzungen: Schritt 8 abgeschlossen
   - Beschreibung: Methoden-Implementierung löschen

10. **`AutonomAufgabeDetailDialog.xaml(.cs)` löschen**
    - Voraussetzungen: Schritte 8-9 abgeschlossen (keine Verweise mehr)
    - Beschreibung: Dateien aus Repo entfernen

11. **Unit-Tests für TaskDetailViewModel: Neue Tests**
    - Voraussetzungen: Schritte 1-6 abgeschlossen, Test-Infrastruktur vorhanden
    - Beschreibung: Tests für neue Properties, Commands, Ansicht-Umschaltung, Visibility-Bindung

12. **Unit-Tests für AutonomAufgabeStartService: Anpassung**
    - Voraussetzungen: Schritt 7 abgeschlossen
    - Beschreibung: Tests für neue `TaskDetailViewModel`-Abhängigkeit, Mock für `SetzeAutonomAufgabeDetailViewAsync()`, Verifikation des Aufrufs

13. **E2E-Tests: Neue Tests für UI-Integration**
    - Voraussetzungen: Schritte 1-10 abgeschlossen, Anwendung läuft
    - Beschreibung: Tests für Automatisierung-Registerkarte Start/Stop/Resume im Ribbon, Ansicht-Umschaltung

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `AutomatisierungViewSelected_WhenDetailsViewModelSet` | TaskDetailViewModelTests | Prüft, dass `IsAutomatisierungViewSelected` true ist, wenn eine Automatisierung-Ansicht aktiv ist |
| `AutomatisierungViewCommand_ChangesViewToAutomatisierung` | TaskDetailViewModelTests | Prüft, dass `AutomatisierungViewCommand` Ansicht zu Automatisierung wechselt |
| `ShowAutomatisierungPanel_TrueWhenViewModelSet_FalseWhenNull` | TaskDetailViewModelTests | Prüft, dass `ShowAutomatisierungPanel` korrekt auf `AutonomAufgabeDetailViewModel != null` reagiert |
| `SetzeAutonomAufgabeDetailViewAsync_StorpsViewModelAndSwitchesToView` | TaskDetailViewModelTests | Prüft, dass Methode ViewModel speichert, PropertyChanged triggert, Ansicht wechselt |
| `WaehleAnsicht_RejectsAutomatisierungIfPanelNotShown` | TaskDetailViewModelTests | Prüft, dass `WaehleAnsicht(Automatisierung)` zu Info fällt wenn `ShowAutomatisierungPanel == false` |
| `WaehleStandardAnsicht_CleansUpAutonomAufgabeViewModelOnTaskSwitch` | TaskDetailViewModelTests | Prüft, dass beim Aufgabenwechsel `_autonomAufgabeDetailViewModel` auf null gesetzt wird |
| `StarteAsync_GibtErstelltesDetailViewModelImResultZurueck_OhneDialogAufruf` | AutonomAufgabeStartServiceTests (bzw. bestehende Testklasse für den Service) | Prüft, dass `StarteAsync()` das erzeugte `AutonomAufgabeDetailViewModel` über `AutonomAufgabeStartResult.DetailViewModel` zurückgibt und `IDialogService.ShowAutonomAufgabeDetailAsync()` nicht mehr aufgerufen wird |
| `AutonomAufgabeInitialisierenAsync_RuftSetzeAutonomAufgabeDetailViewAsync_MitErgebnis` | TaskDetailViewModelTests | Prüft, dass `TaskDetailViewModel.AutonomAufgabeInitialisierenAsync()` nach erfolgreichem `StarteAsync()`-Aufruf `SetzeAutonomAufgabeDetailViewAsync(ergebnis.DetailViewModel)` auf sich selbst aufruft und die Automatisierung-Ansicht dadurch aktiv wird |
| `E2E_AutonomAufgabe_StartenZeigtRegisterkarte` | E2E_AutonomAufgabenInitialisierung | E2E: Benutzer klickt "Autonome Aufgabe starten", Registerkarte "Automatisierung" wird angezeigt |
| `E2E_AutonomAufgabe_RibbonStartButtonFunctional` | E2E_AutonomAufgabenAgentExecution | E2E: "Start"-Button im Ribbon ist sichtbar und funktioniert |
| `E2E_AutonomAufgabe_ViewSwitchToAutomatisierung` | E2E_AutonomAufgabenInitialisierung | E2E: Benutzer kann zur "Automatisierung"-Registerkarte wechseln |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `AutonomAufgabeStartServiceTests` — Tests, die `IDialogService.ShowAutonomAufgabeDetailAsync()` moggen/verifizieren | `StarteAsync()` ruft diese Methode nicht mehr auf; Tests müssen stattdessen prüfen, dass `AutonomAufgabeStartResult.DetailViewModel` bei Erfolg gesetzt ist. Keine neue Abhängigkeit nötig (siehe korrigierte Designentscheidung), daher keine neuen Mocks für `TaskDetailViewModel`. |
| `TaskDetailViewModelTests` — Tests für Ansicht-Umschaltung | Neue Enum-Wert `Automatisierung` muss in `switch`-Statement-Tests berücksichtigt werden |
| `AutonomAufgabeDetailViewModelTests` — alle Tests | Keine Breaking Changes, aber Tests sollten überprüft werden ob Bindings noch funktionieren wenn ViewModel in Registerkarte eingebettet ist |
| `E2E_AutonomAufgabenInitialisierung` — Dialog-bezogene Tests | Tests, die Dialog-Fenster suchen, müssen angepasst werden um neue Registerkarte statt Dialog zu suchen |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Autonome Aufgabe starten, neue Registerkarte erscheint | E2E_AutonomAufgabenInitialisierung | "Automatisierung"-Registerkarte ist nach erfolgreicher Initialisierung sichtbar |
| Registerkarte-Umschaltung funktioniert | E2E_AutonomAufgabenInitialisierung | Benutzer kann zwischen Info, CLI, Diff, etc. und Automatisierung wechseln |
| Ribbon-Buttons "Start", "Stop", "Resume" sind sichtbar | E2E_AutonomAufgabenAgentExecution | Buttons sind in Ribbon-Gruppe "Autonome Aufgabe" sichtbar wenn Automatisierung-Ansicht aktiv |
| Ribbon-Buttons funktionieren | E2E_AutonomAufgabenAgentExecution | Klick auf "Start"/"Stop"/"Resume" führt zu korrektem Verhalten (Agent startet/stoppt) |
| Aufgabenwechsel bereinigt Automatisierung-Ansicht | E2E_AutonomAufgabenInitialisierung | Wenn zu andere Aufgabe gewechselt wird, verschwindet "Automatisierung"-Registerkarte |

Welche bestehenden E2E-Tests müssen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `E2E_AutonomAufgabenInitialisierung` — Tests für Dialog-UI | Diese Tests müssen von FlaUI-Dialog-Suche zu Registerkarten-UI wechseln (FlaUI-Element-Suche im TaskDetailView) |
| `E2E_AutonomAufgabenAgentExecution` — Agent-Start-Tests | Tests müssen überprüfen dass neue Ribbon-Buttons verwendet werden können statt Dialog-Buttons |

## Offene Punkte

Keine — alle 6 Designfragen wurden durch begründete Standardentscheidungen geklärt und sind in den "Designentscheidungen" dokumentiert.
