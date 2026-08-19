# Umsetzungsplan: Split-Button-Muster für IDE-Öffnen-Funktion

## Übersicht

Das IDE-Öffnen-Feature in der TaskDetailView wird um ein Split-Button-Muster erweitert. Der Haupt-Button öffnet direkt den ersten (priorisierten) Einstiegspunkt, während ein zusätzlicher Dropdown-Button nur bei mehreren verfügbaren Einstiegspunkten sichtbar wird und eine Auswahlliste anzeigt. Diese Änderung betrifft die WPF-UI-Schicht und erweitert das bestehende IDE-Plugin-System ohne Änderungen an Domain- oder Application-Logik.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| **Split-Button-Komponente** | Neue dedizierte `RibbonSplitButton.xaml`-Komponente statt Erweiterung von `RibbonLargeButton` | Saubere Trennung der Verantwortlichkeiten; `RibbonLargeButton` bleibt unverändert und kann für andere Zwecke wiederverwendet werden. Split-Button-Logik (Dropdown-Sichtbarkeit, zwei separate Befehle) unterscheidet sich grundlegend vom Single-Button-Verhalten. |
| **Einstiegspunkte-Ermittlung** | Hybrid: einmalige Berechnung von `KannIdeAuswaehlen` am Ende von `LadenAsync` (ohne `OpenEntryPointAsync`) **zusätzlich** zur on-demand-Ermittlung bei jedem Haupt-/Dropdown-Button-Klick | Der Dropdown-Button muss bereits beim ersten Anzeigen der View korrekt sichtbar/unsichtbar sein (`TaskDetailViewModel` ist `Transient` registriert, jede neu geöffnete View startet sonst mit `KannIdeAuswaehlen == false`). Die zusätzliche Ermittlung bei jedem Öffnen-Versuch bleibt bestehen, da sich Einstiegspunkte zwischen Laden und Klick ändern können und der eigentliche Öffnen-Vorgang ohnehin eine frische Ermittlung benötigt. Der Overhead der zusätzlichen Ermittlung beim Laden ist bei bereits geladenen Aufgaben minimal. |
| **Dialog-Anzeige** | Wiederverwendung von `ShowSolutionSelectionDialogAsync` (mit Pfad-Strings) statt neue Methode mit vollständigen `IdeEntryPoint`-Objekten | Minimale Änderungen an bestehenden Interfaces; `IdeEntryPoint.DisplayName` wird zur Anzeige genutzt. Eine neue `ShowIdeSelectionDialogAsync` mit Plugin-Informationen bleibt als optionale zukünftige Erweiterung. |
| **Fallback-Logik Haupt-Button** | Haupt-Button verwendet weiterhin Fallback-Verhalten via `PluginSelectionService.ResolveIdePluginAsync` (kein expliziter Callback an `OpenRepositoryInIdeAsync`) | Konsistent mit aktuellem Verhalten; kein Breaking Change. Falls primäres Plugin kompatibel ist und Einstiegspunkte hat, wird der erste geöffnet. |
| **Dialog-Inhalt** | Nur Einstiegspunkte des priorisierten (aufgelösten) IDE-Plugins anzeigen | Vereinfachte UX; mehrere IDE-Plugins werden durch Priorisierung bereits in der Konfiguration handhabbar. |

## Programmabläufe

### Haupt-Button-Klick (Bestehendes Verhalten, unverändert)

1. Benutzer klickt auf den Haupt-Button des Split-Buttons
2. `OeffneIdeCommand` wird ausgelöst
3. `OeffneIdeAsync` wird aufgerufen
4. `IdeOeffnenService.OpenRepositoryInIdeAsync` wird aufgerufen **ohne** `waehleEntryPointAsync`-Callback
5. Service löst IDE-Plugin via `PluginSelectionService.ResolveIdePluginAsync` auf
6. Service ruft `FindEntryPointsAsync` auf dem Plugin auf
7. **0 Einstiegspunkte:** Fehler wird geworfen und in `FehlerMeldung` angezeigt (bestehend)
8. **1 Einstiegspunkt:** Wird direkt via `OpenEntryPointAsync` geöffnet
9. **≥2 Einstiegspunkte:** Erster Einstiegspunkt wird direkt via `OpenEntryPointAsync` geöffnet (Fallback)

Beteiligte Klassen/Komponenten: `RibbonSplitButton`, `TaskDetailViewModel`, `IdeOeffnenService`, `PluginSelectionService`, IDE-Plugin

### Dropdown-Button-Klick (Neue Funktionalität)

1. Benutzer klickt auf den Dropdown-Teil des Split-Buttons
2. `OeffneIdeAuswahlCommand` wird ausgelöst
3. `OeffneIdeAuswahlAsync` wird aufgerufen
4. `IdeOeffnenService.OpenRepositoryInIdeAsync` wird aufgerufen **mit** `waehleEntryPointAsync`-Callback
5. Service löst IDE-Plugin via `PluginSelectionService.ResolveIdePluginAsync` auf
6. Service ruft `FindEntryPointsAsync` auf dem Plugin auf
7. **0 Einstiegspunkte:** Fehler wird geworfen und in `FehlerMeldung` angezeigt
8. **1 Einstiegspunkt:** Callback wird **nicht** aufgerufen, Einstiegspunkt wird direkt geöffnet (Optimierung)
9. **≥2 Einstiegspunkte:** 
   - Callback `waehleEntryPointAsync` wird mit der Liste aller Einstiegspunkte aufgerufen
   - Callback extrahiert `Path` von jedem `IdeEntryPoint` und nutzt `DisplayName` falls vorhanden
   - `IDialogService.ShowSolutionSelectionDialogAsync` wird mit Pfad-Liste aufgerufen
   - Dialog zeigt Auswahl an; Benutzer wählt einen Pfad oder bricht ab
   - Callback findet das zugehörige `IdeEntryPoint`-Objekt anhand des gewählten Pfads
   - Falls Auswahl: Service ruft `OpenEntryPointAsync` mit gewähltem `IdeEntryPoint` auf
   - Falls Abbruch: Service bricht ab, nichts wird geöffnet

Beteiligte Klassen/Komponenten: `RibbonSplitButton`, `TaskDetailViewModel`, `IdeOeffnenService`, `IDialogService`, `PluginSelectionService`, IDE-Plugin

### Sichtbarkeitskontrolle des Dropdown-Buttons

Hybrides Verhalten: `KannIdeAuswaehlen` wird sowohl einmalig beim Laden der Aufgabe als auch erneut bei jedem Öffnen-Versuch berechnet, damit der Dropdown-Button bereits beim ersten Anzeigen der View korrekt sichtbar/unsichtbar ist und trotzdem bei jedem Öffnen-Versuch den aktuellen Stand widerspiegelt.

**a) Einmalige Berechnung beim Laden (`LadenAsync`):**

1. `LadenAsync` wird beim Initialisieren der View oder beim Wechsel der Aufgabe aufgerufen (Setter von `AufgabeId`)
2. Am Ende von `LadenAsync` wird `KannIdeAuswaehlen` einmalig berechnet:
   - Arbeitsverzeichnis wird über `ErmittleEffektivesArbeitsverzeichnisAsync` ermittelt
   - IDE-Plugin wird über `PluginSelectionService.ResolveIdePluginAsync` aufgelöst
   - `FindEntryPointsAsync` wird auf dem Plugin aufgerufen — **ohne** anschließenden Aufruf von `OpenEntryPointAsync`
   - Falls Fehler, kein Plugin oder kein Arbeitsverzeichnis: `KannIdeAuswaehlen = false` (wird **nicht** als `FehlerMeldung` angezeigt, da das Laden der Aufgabe selbst erfolgreich war)
   - **< 2 Einstiegspunkte:** `KannIdeAuswaehlen = false` → Dropdown-Button unsichtbar
   - **≥ 2 Einstiegspunkte:** `KannIdeAuswaehlen = true` → Dropdown-Button sichtbar
3. Binding in `RibbonSplitButton` reagiert auf Eigenschaftsänderung und passt Sichtbarkeit an

**b) Erneute Berechnung bei jedem Öffnen-Versuch (Haupt- oder Dropdown-Button-Klick):**

Bei jedem Klick auf Haupt- oder Dropdown-Button wird `KannIdeAuswaehlen` als Nebeneffekt von `OeffneIdeInternAsync` erneut berechnet (siehe Abschnitte „Haupt-Button-Klick" und „Dropdown-Button-Klick"), da hier ohnehin eine frische Ermittlung der Einstiegspunkte für den eigentlichen Öffnen-Vorgang stattfindet. Diese erneute Berechnung ersetzt den beim Laden ermittelten Wert.

Beteiligte Klassen/Komponenten: `TaskDetailViewModel`, `PluginSelectionService`, IDE-Plugin, `RibbonSplitButton` (XAML-Binding)

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `RibbonSplitButton` | WPF UserControl (XAML + Code-Behind) | Ribbon-Button mit zwei Teilen: Haupt-Button und Dropdown-Button mit Pfeil; steuert Sichtbarkeit des Dropdown basierend auf einer Boolean-Property |

## Änderungen an bestehenden Klassen

### `TaskDetailViewModel` (ViewModel-Klasse)

- **Neue Eigenschaften:** 
  - `KannIdeAuswaehlen` (`bool`, read-only, über PropertyChanged notifiziert) — Gibt an, ob mehrere Einstiegspunkte verfügbar sind; steuert Sichtbarkeit des Dropdown-Buttons
  - `VerfuegbareEinstiegspunkte` (`IReadOnlyList<IdeEntryPoint>`, read-only, optional) — Gepufferte Liste der zuletzt ermittelten Einstiegspunkte für Debugging/Logging; wird aktualisiert bei jedem Aufruf von `OeffneIdeAsync` oder `OeffneIdeAuswahlAsync`

- **Neue Kommandos:** 
  - `OeffneIdeAuswahlCommand` (`ICommand`) — AsyncRelayCommand, das `OeffneIdeAuswahlAsync` aufruft; kann nur ausgeführt werden wenn `KannIdeOeffnen == true` und keine laufende Ermittlung stattfindet

- **Neue Methoden:** 
  - `OeffneIdeAuswahlAsync(CancellationToken)` (`private`) — Ruft `ErmittleEffektivesArbeitsverzeichnisAsync` auf und dann `IdeOeffnenService.OpenRepositoryInIdeAsync` **mit** `waehleEntryPointAsync`-Callback (im Gegensatz zu `OeffneIdeAsync`, die **ohne** Callback aufruft). Der Callback zeigt `ShowSolutionSelectionDialogAsync` an. Fehlerbehandlung identisch zu `OeffneIdeAsync`.
  - `waehleEntryPointAsync(IReadOnlyList<IdeEntryPoint>, CancellationToken)` (`private`) — Callback-Methode, die von `IdeOeffnenService.OpenRepositoryInIdeAsync` bei mehreren Einstiegspunkten aufgerufen wird. Extrahiert Pfade aus `IdeEntryPoint`-Objekten (nutzt `DisplayName` falls vorhanden, sonst `Path`), ruft `ShowSolutionSelectionDialogAsync` mit Pfad-Strings auf, findet das zugehörige `IdeEntryPoint`-Objekt anhand des gewählten Pfads, gibt es zurück (oder `null` bei Abbruch).

- **Geänderte Methoden:** 
  - `OeffneIdeAsync` — Keine inhaltlichen Änderungen; bleibt bisheriges Verhalten (Aufruf ohne Callback zum Fallback auf ersten Einstiegspunkt). Code wird evtl. geringfügig refaktoriert wenn gemeinsamer Code mit `OeffneIdeAuswahlAsync` existiert (z. B. Arbeitsverzeichnis-Ermittlung).

### `TaskDetailView.xaml` (WPF View)

- **Ersetzung eines UI-Elements:** 
  - Der bestehende `<controls:RibbonLargeButton>` (Zeile ~180–183) wird durch eine neue `<controls:RibbonSplitButton>`-Komponente ersetzt
  - Bindungen: Haupt-Button bindet `OeffneIdeCommand`, Dropdown-Button bindet `OeffneIdeAuswahlCommand`; Dropdown-Sichtbarkeit bindet `KannIdeAuswaehlen`
  - Icon/Text: Icon "🛠", Text "IDE öffnen", AutomationName "IdeOeffnen" (unverändert)

### `RibbonLargeButton.xaml` (WPF UserControl)

- **Keine Änderungen** — Komponente bleibt unverändert und wird weiterhin für einzelne Buttons genutzt

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine.

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **TaskDetailView-Abhängige Tests:** E2E-Tests, die die Struktur oder das Verhalten des IDE-Öffnen-Buttons prüfen, müssen auf die neue `RibbonSplitButton`-Komponente angepasst werden (z. B. das Lokalisieren und Klicken des Dropdown-Buttons, Prüfung der Dropdown-Sichtbarkeit basierend auf Einstiegspunkt-Anzahl).
- **`OeffneIdeAsync` Callback-Verhalten:** Der bestehende Inline-Callback in `OeffneIdeAsync` (der `ShowSolutionSelectionDialogAsync` aufruft) wird in die neue Methode `waehleEntryPointAsync` ausgelagert. Falls andere Code-Stellen `OeffneIdeAsync` direkt aufrufen und der Callback-Aufruf erwarten, könnte dies ein Breaking Change sein — ist aber unwahrscheinlich, da die Methode `private` ist.
- **Asynchrone Ermittlung von `KannIdeAuswaehlen`:** Property `KannIdeAuswaehlen` kann sich nach dem Laden der View asynchron ändern (Einstiegspunkte werden erst nach Plugin-Auflösung ermittelt). Dies könnte kurzzeitig zu inkonsistenter UI führen (Dropdown-Button erscheint später). Dies ist akzeptabel und entspricht Bestandteilen wie `KannIdeOeffnen`, die ebenfalls vom Arbeitsverzeichnis abhängen.
- **Betroffene bestehende Tests:** 
  - `TaskDetailViewModelTests` — Evtl. müssen Tests für `OeffneIdeCommand` angepasst werden, wenn sie Mock-Argumente von `OpenRepositoryInIdeAsync` prüfen (Callback-Signatur bleibt identisch, also kein Bruch, aber neue Tests sind erforderlich).
  - E2E-Tests für TaskDetailView — Tests, die auf den IDE-Button klicken, müssen aktualisiert werden, um den neuen Split-Button zu handhaben.

## Umsetzungsreihenfolge

1. **`RibbonSplitButton.xaml` (Komponente) anlegen**
   - Voraussetzungen: Keine (WPF-Grundlagen sind im Projekt vorhanden)
   - Beschreibung: Neue UserControl mit zwei Button-Bereichen (Haupt-Button + Dropdown-Button mit Pfeil). Haupt-Button nutzt DependencyProperties für Icon/Text/Command (ähnlich `RibbonLargeButton`). Dropdown-Button ist unsichtbar wenn Binding `KannIdeAuswaehlen == false`. Styling folgt bestehenden Ribbon-Buttons.

2. **`RibbonSplitButton.xaml.cs` (Code-Behind) implementieren**
   - Voraussetzungen: `RibbonSplitButton.xaml` angelegt
   - Beschreibung: DependencyProperties `ButtonIcon`, `ButtonText`, `AutomationName`, `ButtonCommand`, `DropdownCommand`, `CanShowDropdown` definieren. Event-Handler für Klicks auf Haupt- und Dropdown-Button. Styling (Hover, Pressed, Disabled) analog zu `RibbonLargeButton`.

3. **`TaskDetailViewModel` erweitern — Neue Property `KannIdeAuswaehlen`**
   - Voraussetzungen: `TaskDetailViewModel` existiert (bereits im Repo)
   - Beschreibung: Property `KannIdeAuswaehlen` hinzufügen (initialisiert mit `false`). Wird bei jedem Aufruf von `OeffneIdeAsync` oder `OeffneIdeAuswahlAsync` basierend auf Einstiegspunkt-Anzahl aktualisiert. PropertyChanged-Event wird gefeuert wenn sich die Anzahl ändert.

4. **`TaskDetailViewModel` erweitern — Neue Property `VerfuegbareEinstiegspunkte` (optional)**
   - Voraussetzungen: `TaskDetailViewModel` existiert
   - Beschreibung: Property `VerfuegbareEinstiegspunkte` hinzufügen (vom Typ `IReadOnlyList<IdeEntryPoint>`). Wird bei jedem Aufruf von `OeffneIdeAsync` oder `OeffneIdeAuswahlAsync` mit den ermittelten Einstiegspunkten aktualisiert. Dient Debugging und Logging.

5. **`TaskDetailViewModel` erweitern — Neues Kommando `OeffneIdeAuswahlCommand`**
   - Voraussetzungen: `TaskDetailViewModel` erweitert (Properties), `IdeOeffnenService` existiert
   - Beschreibung: `OeffneIdeAuswahlCommand` (`AsyncRelayCommand`) anlegen, das `OeffneIdeAuswahlAsync` aufruft. CanExecute prüft `KannIdeOeffnen == true` und dass keine laufende Ermittlung stattfindet.

6. **`TaskDetailViewModel` erweitern — Methode `OeffneIdeAuswahlAsync` implementieren**
   - Voraussetzungen: `TaskDetailViewModel` erweitert (Kommando), `waehleEntryPointAsync` Callback-Methode existiert (Schritt 7)
   - Beschreibung: Methode `OeffneIdeAuswahlAsync` implementieren — analog zu `OeffneIdeAsync`, aber mit `waehleEntryPointAsync` Callback an `OpenRepositoryInIdeAsync` übergeben. Fehlerbehandlung identisch.

7. **`TaskDetailViewModel` erweitern — Callback-Methode `waehleEntryPointAsync` implementieren**
   - Voraussetzungen: `TaskDetailViewModel` erweitert, `IDialogService` vorhanden (bereits im Repo)
   - Beschreibung: Private Methode `waehleEntryPointAsync` implementieren, die von `IdeOeffnenService` als Callback aufgerufen wird. Extrahiert Pfade aus `IdeEntryPoint`-Liste (nutzt `DisplayName` falls vorhanden), ruft `ShowSolutionSelectionDialogAsync` mit Pfad-Strings auf, findet das zugehörige `IdeEntryPoint`-Objekt, gibt es zurück (oder `null`).

8. **`TaskDetailView.xaml` anpassen — IDE-Button ersetzen**
   - Voraussetzungen: `RibbonSplitButton` Komponente implementiert, `TaskDetailViewModel` erweitert (alle Kommandos und Properties)
   - Beschreibung: Bestehenden `<controls:RibbonLargeButton>` (Zeile ~180–183) durch `<controls:RibbonSplitButton>` ersetzen. Haupt-Button Binding: `ButtonCommand="{Binding OeffneIdeCommand}"`. Dropdown-Button Binding: `DropdownCommand="{Binding OeffneIdeAuswahlCommand}"`, `CanShowDropdown="{Binding KannIdeAuswaehlen}"`. Icon/Text/AutomationName unverändert.

9. **Unit-Tests schreiben — `TaskDetailViewModel` Kommandos und Properties**
   - Voraussetzungen: `TaskDetailViewModel` erweitert (alle Methoden implementiert), `TaskDetailViewModelTestsBase` existiert
   - Beschreibung: Tests für `OeffneIdeAuswahlCommand` (ausführbar, ruft `OeffneIdeAuswahlAsync` auf), Tests für `KannIdeAuswaehlen` (berechnet korrekt basierend auf Einstiegspunkt-Anzahl), Tests für `waehleEntryPointAsync`-Callback (zeigt Dialog bei mehreren Einstiegspunkten, findet korrektes `IdeEntryPoint`-Objekt).

10. **Unit-Tests anpassen — Bestehende `TaskDetailViewModel`-Tests**
    - Voraussetzungen: `TaskDetailViewModel` erweitert, neue Tests geschrieben (Schritt 9)
    - Beschreibung: Tests für `OeffneIdeAsync` überprüfen, ob neue Callback-Logik beeinträchtigt wird. Falls Tests Mocks der `OpenRepositoryInIdeAsync`-Signatur verwenden, müssen sie ggf. angepasst werden (Callback bleibt aber im Verhalten gleich).

11. **E2E-Test schreiben — Haupt-Button öffnet direkt (bestehend, neu zu verfizieren)**
    - Voraussetzungen: Komponenten implementiert, View angepasst, Unit-Tests grün
    - Beschreibung: E2E-Test, der mit einer Aufgabe mit 1 Einstiegspunkt prüft: Klick auf Haupt-Button öffnet die IDE direkt. Dropdown-Button sollte unsichtbar sein.

12. **E2E-Test schreiben — Dropdown-Button sichtbar bei mehreren Einstiegspunkten**
    - Voraussetzungen: Komponenten implementiert (alle), Unit-Tests grün
    - Beschreibung: E2E-Test, der mit einer Aufgabe mit mehreren Einstiegspunkten prüft: Haupt-Button ist sichtbar, Dropdown-Button ist sichtbar, `KannIdeAuswaehlen == true`. Klick auf Haupt-Button öffnet den ersten Einstiegspunkt direkt.

13. **E2E-Test schreiben — Dropdown-Button zeigt Dialog und öffnet gewählten Einstiegspunkt**
    - Voraussetzungen: Komponenten implementiert (alle), Unit-Tests grün, E2E-Infrastruktur für Dialog-Handling vorhanden
    - Beschreibung: E2E-Test, der mit einer Aufgabe mit mehreren Einstiegspunkten prüft: Klick auf Dropdown-Button zeigt Auswahldialog mit allen Einstiegspunkten. Benutzer wählt einen aus → IDE öffnet den gewählten Einstiegspunkt. Alternativ: Abbruch-Klick → Nichts wird geöffnet.

14. **E2E-Test anpassen — Bestehende IDE-öffnen-Tests**
    - Voraussetzungen: Alle neuen Tests geschrieben (Schritte 11–13), Komponenten und ViewModel angepasst
    - Beschreibung: Falls E2E-Tests existieren, die das IDE-öffnen testen (z. B. über den alten `RibbonLargeButton`), müssen sie auf den neuen `RibbonSplitButton` angepasst werden (Selector/Automation-IDs können sich ändern).

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `OeffneIdeAuswahlCommand_ExecuteAsync_RuftOeffneIdeAuswahlAsyncAuf` | `TaskDetailViewModelTests_IdeAuswahl` | Kommando ist verfügbar und ruft `OeffneIdeAuswahlAsync` auf |
| `OeffneIdeAuswahlCommand_CanExecute_WhenKannIdeOeffnenFalse_ReturnsFalse` | `TaskDetailViewModelTests_IdeAuswahl` | Kommando kann nicht ausgeführt werden wenn `KannIdeOeffnen == false` |
| `KannIdeAuswaehlen_WhenOneEntryPoint_ReturnsFalse` | `TaskDetailViewModelTests_IdeAuswahl` | Property ist `false` bei 1 Einstiegspunkt |
| `KannIdeAuswaehlen_WhenMultipleEntryPoints_ReturnsTrue` | `TaskDetailViewModelTests_IdeAuswahl` | Property ist `true` bei ≥2 Einstiegspunkten |
| `KannIdeAuswaehlen_WhenNoEntryPoints_ReturnsFalse` | `TaskDetailViewModelTests_IdeAuswahl` | Property ist `false` bei 0 Einstiegspunkten / Fehler |
| `WaehleEntryPointAsync_WithMultipleEntryPoints_ShowsDialogAndReturnsSelected` | `TaskDetailViewModelTests_IdeAuswahl` | Callback zeigt Dialog und gibt gewählten `IdeEntryPoint` zurück |
| `WaehleEntryPointAsync_WithDialogAbort_ReturnsNull` | `TaskDetailViewModelTests_IdeAuswahl` | Callback gibt `null` zurück wenn Benutzer abbricht |
| `WaehleEntryPointAsync_UsesDisplayNameInDialog` | `TaskDetailViewModelTests_IdeAuswahl` | Callback nutzt `IdeEntryPoint.DisplayName` falls vorhanden für Dialog-Anzeige |
| `OeffneIdeAuswahlAsync_WithNoEntryPoints_ShowsError` | `TaskDetailViewModelTests_IdeAuswahl` | Fehlerbehandlung identisch zu `OeffneIdeAsync` |
| `VerfuegbareEinstiegspunkte_UpdatedAfterOeffneIde` | `TaskDetailViewModelTests_IdeAuswahl` | Property wird mit ermittelten Einstiegspunkten aktualisiert (optional, für Debugging) |
| (ggf. Hilfsmethode) `ErzeugeEntryPointMitDisplayName` | `TaskDetailViewModelTestsBase` | Erstellt Test-`IdeEntryPoint`-Objekte mit `DisplayName` für Tests |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `TaskDetailViewModelTests.cs` — Tests für `OeffneIdeCommand` | Evtl. müssen Mock-Setups angepasst werden wenn Tests die Callback-Signatur prüfen, aber Verhalten bleibt gleich (kein Breaking Change erwartet) |
| E2E-Tests in `E2E_TaskDetailView*.cs` oder ähnlich | Müssen aktualisiert werden um `RibbonSplitButton` zu handhaben, da UI-Struktur sich ändert (Old: 1 Button, New: 2 Buttons) |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| 1 Einstiegspunkt: Haupt-Button öffnet direkt, Dropdown unsichtbar | `E2E_TaskDetailView_IdeAuswahl.cs` (neue Klasse) | „Bei 1 Einstiegspunkt ist der Haupt-Button sichtbar und öffnet direkt, der Dropdown-Button ist unsichtbar" |
| ≥2 Einstiegspunkte: Haupt-Button öffnet ersten direkt | `E2E_TaskDetailView_IdeAuswahl.cs` | „Bei mehreren Einstiegspunkten öffnet der Haupt-Button den ersten direkt" |
| ≥2 Einstiegspunkte: Dropdown-Button ist sichtbar und zeigt Dialog | `E2E_TaskDetailView_IdeAuswahl.cs` | „Bei mehreren Einstiegspunkten ist der Dropdown-Button sichtbar und zeigt einen Auswahldialog" |
| Dropdown-Dialog: Benutzer wählt Einstiegspunkt → IDE öffnet ihn | `E2E_TaskDetailView_IdeAuswahl.cs` | „Benutzer kann einen Einstiegspunkt aus dem Dropdown-Dialog wählen und die IDE öffnet ihn" |
| Dropdown-Dialog: Benutzer bricht ab → Nichts wird geöffnet | `E2E_TaskDetailView_IdeAuswahl.cs` | „Benutzer kann den Dialog abbrechen und es wird nichts geöffnet" |
| 0 Einstiegspunkte: Fehler wird angezeigt (bestehend, zu verifizieren) | `E2E_TaskDetailView_IdeAuswahl.cs` | „Bei 0 Einstiegspunkten wird eine Fehlermeldung angezeigt (Haupt- und Dropdown-Button sollten deaktiviert sein)" |

**Bestehende E2E-Tests, die betroffen sind:**
- Falls E2E-Tests den IDE-öffnen-Button automatisieren (z. B. `E2E_TaskDetailView*.cs`, `E2E_*IdeOeffnen*.cs`), müssen sie aktualisiert werden um:
  - Den neuen `RibbonSplitButton` zu lokalisieren (statt `RibbonLargeButton`)
  - Ggf. die korrekte Schaltfläche (Haupt vs. Dropdown) zu wählen basierend auf Szenario
  - Dropdown-Sichtbarkeit zu prüfen basierend auf Einstiegspunkt-Anzahl

## Offene Punkte

Keine. Die in der Anforderung genannten offenen Punkte (1–5) werden wie folgt adressiert:

1. **Dialog-Inhalt bei mehreren IDEs:** Nur Einstiegspunkte des priorisierten Plugins (via `PluginSelectionService.ResolveIdePluginAsync`) werden angezeigt. Dies ist konsistent mit dem aktuellen Verhalten und wird durch die bestehende Plugin-Priorisierung konfigurierbar.

2. **Haupt-Button Fallback-Logik:** Haupt-Button nutzt **kein** Callback und fallen auf den ersten Einstiegspunkt zurück (bestehend). Dies bleibt unverändert.

3. **Datei-Dialog vs. Struktur-Dialog:** Wiederverwendung von `ShowSolutionSelectionDialogAsync` mit Pfad-Strings (flach). Eine zukünftige hierarchische Variante kann als separate Methode `ShowIdeSelectionDialogAsync` hinzugefügt werden.

4. **Tastatur-Navigation:** Folgt bestehenden WPF-Ribbon-Mustern (Tab-Navigation zwischen Buttons, Enter zum Aktivieren). Kein spezielles `Alt+I`-Muster erforderlich, da Ribbon ohnehin über Tab navigierbar ist.

5. **Async-Ermittlung der Einstiegspunkte:** On-demand beim Dropdown-Klick (Schritt 7 in der Umsetzungsreihenfolge). `KannIdeAuswaehlen` wird asynchron berechnet und kann sich nach View-Load ändern — akzeptable UX-Verzögerung.
