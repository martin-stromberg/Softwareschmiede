# Umsetzungsplan: Aktive Aufgaben im Menü

## Übersicht

Die WPF-Desktopanwendung wird um die Anzeige aktuell aktiver Aufgaben in der Navigations-Seitenleiste erweitert. Aufgaben mit Status `Gestartet` oder `Wartend` werden als gerahmte Kacheln mit Titel und KI-Ausführungsstatus angezeigt. Die Sektion ist automatisch verborgen, wenn das Dashboard aktiv ist, um Redundanz zu vermeiden. Das Dashboard zeigt die gleiche Aufgabenliste ohne Höhenbeschränkung. Die Umsetzung erfordert eine neue Service-Methode, ViewModel-Eigenschaften und Commands, einen neuen Converter, sowie XAML-Erweiterungen in Seitenleiste und Dashboard.

---

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| **Aufgabenkachel-Rendering** | DataTemplate inline in MainWindow.xaml und DashboardView.xaml | Kein separater UserControl nötig — einfacher, weniger Dateien, direkte Bindung an ViewModel-Properties möglich |
| **DashboardViewModel Aufgabenliste** | Neue Property `AktiveAufgabenListe` (zusätzlich zu bestehendem `AktiveAufgaben: int`) | Vermeidet Breaking Change für Dashboard-Statistik-Kachel; alte `int`-Property bleibt für Zähler bestehen |
| **Aktualisierungslogik** | On-demand bei Navigation (keine Timer-Logik) | Erfüllt Anforderung, ist minimal invasiv, Performance-freundlich; Timer kann später optional hinzugefügt werden |
| **Sortierung aktiver Aufgaben** | Nach `LastHeartbeatUtc` absteigend, mit Fallback auf `ErstellungsDatum` | Zeigt zuletzt aktive Aufgaben zuerst; Fallback für Aufgaben ohne Heartbeat |
| **KI-Status-Ermittlung** | Im Converter, nicht im Service | Conversion von Entity zu UI-Text ist View-Concern; Service bleibt datenfokussiert |
| **Repository-Pattern** | Service-Layer nutzt bestehende `DbContext`-Queries | Folgt bestehendem Muster (analog zu `GetAktiveUndWartendeCountAsync`) |

---

## Programmabläufe

### Seitenleisten-Anzeige (aktive Aufgaben)

1. **App-Start / Seitenleisten-Render:**
   - `MainWindow.xaml` wird initialisiert
   - `MainWindowViewModel` wird instantiiert
   - `AktiveAufgabenAktualisierenAsync()` wird aufgerufen (z.B. im Constructor oder als Initialisierung)

2. **Aufgaben-Abruf:**
   - `AktiveAufgabenAktualisierenAsync()` ruft `AufgabeService.GetAktiveAufgabenAsync()` auf
   - Service filtert Aufgaben mit Status `Gestartet` oder `Wartend`
   - Sortiert nach `LastHeartbeatUtc` (desc), Fallback `ErstellungsDatum` (desc)
   - Rückgabe als `List<Aufgabe>` (optional mit Limit, z.B. 20)

3. **Binding & Rendering:**
   - `ObservableCollection<Aufgabe>` wird befüllt
   - `ItemsControl` in Seitenleiste rendert DataTemplate für jede Aufgabe
   - Für jede Aufgabe: `KiAusfuehrungsStatusConverter` ermittelt Status-String aus `Aufgabe`
   - Sichtbarkeit der Sektion: `IsDashboardVisible = false` → sichtbar; `true` → verborgen

4. **Navigation zu Aufgabendetails:**
   - User klickt Navigation-Button (→) auf Aufgabenkachel
   - `NavigateZuAufgabeCommand` wird ausgelöst mit `CommandParameter="{Binding Id}"`
   - Command erstellt neue `TaskDetailViewModel`-Instanz mit `AufgabeId`
   - Setzt `MainWindowViewModel.CurrentView` auf neue Instanz
   - `MainWindow.xaml` rendert `TaskDetailView` via DataTemplate-Mapping

### Dashboard-Anzeige (aktive Aufgaben)

1. **Dashboard-Navigation:**
   - User navigiert zum Dashboard via `NavigateToDashboardCommand`
   - `MainWindowViewModel.CurrentView` wird auf `DashboardViewModel` gesetzt
   - `IsDashboardVisible` wird aktualisiert (computed: `CurrentView is DashboardViewModel`) → `true`
   - Seitenleisten-Sektion wird verborgen

2. **Dashboard-LadenAsync:**
   - `DashboardViewModel.LadenAsync()` wird aufgerufen (z.B. über `LadenCommand`)
   - Ruft `AufgabeService.GetAktiveAufgabenAsync()` auf (gleiche Logik wie Menü)
   - Befüllt neue `ObservableCollection<Aufgabe>` Property (`AktiveAufgabenListe`)
   - Befüllt auch weiterhin `AktiveAufgaben: int` für Statistik-Kachel

3. **Dashboard-Rendering:**
   - Dashboard zeigt Aufgabenliste unter Statistik-Kacheln
   - Kacheln-Template identisch mit Seitenleiste (oder ähnlich, ohne Höhenlimit)
   - Keine Sichtbarkeits-Bedingung (immer sichtbar wenn auf Dashboard)

### Berechnung KI-Ausführungsstatus (Converter)

1. **Input:** `Aufgabe`-Objekt

2. **Logik:**
   - Wenn `AktiveRunId != null` UND `LastHeartbeatUtc` ist bekannt UND (`Jetzt - LastHeartbeatUtc`) < 5 Minuten:
     - Output: "▶ Läuft"
   - Wenn Status == `AufgabeStatus.Wartend`:
     - Output: "⏸ Wartet"
   - Sonst (Status == `Gestartet` aber `AktiveRunId` null oder Heartbeat zu alt):
     - Output: "✓ Bereit" oder Status-String

Beteiligte Klassen/Komponenten: `MainWindowViewModel`, `DashboardViewModel`, `AufgabeService`, `KiAusfuehrungsStatusConverter`, `MainWindow.xaml`, `DashboardView.xaml`

---

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `KiAusfuehrungsStatusConverter` | Value Converter (`IValueConverter`) | Konvertiert `Aufgabe`-Objekt zu Status-String (z.B. "▶ Läuft", "⏸ Wartet") für UI-Anzeige |

---

## Änderungen an bestehenden Klassen

### `AufgabeService` (Service)

- **Neue Methode:** `GetAktiveAufgabenAsync(CancellationToken ct = default)` → `Task<List<Aufgabe>>`
  - Filtert Aufgaben mit Status `Gestartet` ODER `Wartend`
  - Sortiert nach `LastHeartbeatUtc` absteigend (neueste zuerst), Fallback auf `ErstellungsDatum`
  - Optional: Limit auf erste 20 Ergebnisse (oder konfigurierbar)
  - Nutzt `AsNoTracking()` für Performance
  - Nutzt `ToListAsync()` für async Datenbank-Abruf

---

### `MainWindowViewModel` (ViewModel)

- **Neue Property:** `AktiveAufgaben` : `ObservableCollection<Aufgabe>` (public)
  - Read-only, initialisiert im Constructor
  - Befüllt via `AktiveAufgabenAktualisierenAsync()`
  - Binding-Quelle für ItemsControl in Seitenleiste

- **Neue Property:** `IsDashboardVisible` : `bool` (public, computed)
  - Gibt `true` zurück wenn `CurrentView is DashboardViewModel`
  - Gibt `false` zurück sonst
  - Berechnet bei jeder `CurrentView`-Änderung (via OnPropertyChanged oder Binding Trigger)

- **Neue Methode:** `AktiveAufgabenAktualisierenAsync(CancellationToken ct = default)` : `Task`
  - Aufrufer: Constructor, Navigation-Commands
  - Ruft `AufgabeService.GetAktiveAufgabenAsync()` auf
  - Clear und Befüllt `AktiveAufgaben` ObservableCollection
  - Error-Handling: Silent (keine Exception nach oben, optional Logging)

- **Neue Command-Property:** `NavigateZuAufgabeCommand` : `ICommand` (public)
  - Typ: `AsyncRelayCommand<Guid>` (oder `RelayCommand<Guid>` wenn async nicht nötig)
  - Parameter: `Guid aufgabeId` (CommandParameter aus Binding)
  - Execute:
    1. Erstellt neue `TaskDetailViewModel`-Instanz mit `AufgabeId`
    2. Setzt `CurrentView` auf neue Instanz
    3. Optional: Setzt `TaskDetailViewModel.ZurueckAction` für Navigation zurück
  - CanExecute: Immer `true`

- **Geänderte Methoden:**
  - Navigation-Methoden (`NavigateToDashboard()`, etc.): Rufen nach `CurrentView`-Setzen `AktiveAufgabenAktualisierenAsync()` auf (optional, oder nur bei Rückkehr zum Menü)

---

### `DashboardViewModel` (ViewModel)

- **Neue Property:** `AktiveAufgabenListe` : `ObservableCollection<Aufgabe>` (public)
  - Read-only, initialisiert im Constructor
  - Befüllt via `LadenAsync()`
  - Binding-Quelle für ItemsControl in DashboardView
  - **Hinweis:** Bestehende `AktiveAufgaben: int` bleibt unverändert (für Statistik-Kachel)

- **Geänderte Methode:** `LadenAsync(CancellationToken ct)`
  - Bestehende Logik bleibt erhalten (Projekte, Recovery, letzte Projekte, Zähler)
  - Neue Zeile: Ruft `AufgabeService.GetAktiveAufgabenAsync()` auf
  - Befüllt neue `AktiveAufgabenListe` mit Ergebnis
  - Error-Handling: Bestehend (`FehlerMeldung`), optional auch für neue Methode

---

## Datenbankmigrationen

Keine.

---

## Validierungsregeln

Keine.

---

## Konfigurationsänderungen

Keine erforderlich. Optional zukünftig:

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `ActiveTasksMaxDisplayCount` | `int` | `20` | Maximale Anzahl aktiver Aufgaben in Seitenleiste (Limit gegen UI-Überlastung) |
| `ActiveTasksRefreshIntervalMs` | `int` | `5000` | Intervall (ms) für periodische Aktualisierung (falls Timer implementiert) |
| `ActiveTaskHeartbeatThresholdMinutes` | `int` | `5` | Schwelle (min) für "Läuft"-Status in KI-Ausführungsstatuskonverter |

**Anmerkung:** Diese Konfigurationen sind OPTIONAL und können später hinzugefügt werden. Der Plan setzt Standardwerte fest ohne externe Konfiguration (hardcoded oder als Konstanten).

---

## Seiteneffekte und Risiken

- **DashboardViewModel Property-Umbenennung:** Neue `AktiveAufgabenListe` Property ist zusätzlich. Alte `AktiveAufgaben: int` bleibt, daher kein Breaking Change. XAML-Binding in Statistik-Kachel ist nicht betroffen.

- **MainWindow.xaml Layout-Änderung:** Seitenleiste wird um neue Sektion erweitert. Könnte bestehende Layout-Tests betreffen (z.B. E2E-Tests für Navigation-UI).

- **Performance:** Wiederkehrende Aufrufe von `GetAktiveAufgabenAsync()` — sollte durch `AsNoTracking()` und Indexing auf Status-Spalte optimiert werden. Kein bekanntes Risiko für aktuelle Datenmenge.

- **Heartbeat-Logik im Converter:** Converter-Code sollte keine side-effects haben. Ist stateless, daher unbedenklich.

---

## Umsetzungsreihenfolge

1. **`KiAusfuehrungsStatusConverter` implementieren**
   - Voraussetzungen: `Aufgabe`-Entity (vorhanden), `AufgabeStatus`-Enum (vorhanden)
   - Beschreibung: Neue Klasse `KiAusfuehrungsStatusConverter` im `Converters`-Verzeichnis. Implementiert `IValueConverter`. Input: `Aufgabe`. Output: Status-String basierend auf `AktiveRunId`, `LastHeartbeatUtc`, Status. Test schreiben für Converter.

2. **`AufgabeService.GetAktiveAufgabenAsync()` implementieren**
   - Voraussetzungen: `AufgabeService` (vorhanden), `SoftwareschmiededDbContext` (vorhanden), Entity Framework (vorhanden)
   - Beschreibung: Neue Methode in `AufgabeService`. Filtert `Aufgaben` nach Status `Gestartet` oder `Wartend`. Sortiert nach `LastHeartbeatUtc` desc, Fallback `ErstellungsDatum` desc. Optional Limit (20). Unit-Tests schreiben.

3. **`MainWindowViewModel` erweitern: Properties & Commands**
   - Voraussetzungen: `MainWindowViewModel` (vorhanden), `ObservableCollection<T>` (vorhanden), `ViewModelBase` (vorhanden), `AsyncRelayCommand<T>` (vorhanden)
   - Beschreibung: 
     - Neue Property `AktiveAufgaben: ObservableCollection<Aufgabe>`
     - Neue computed Property `IsDashboardVisible: bool`
     - Neue Methode `AktiveAufgabenAktualisierenAsync()`
     - Neuer Command `NavigateZuAufgabeCommand: AsyncRelayCommand<Guid>`
     - Constructor-Update: Initialisiert `AktiveAufgaben`, ruft `AktiveAufgabenAktualisierenAsync()` auf
     - Tests schreiben für neue Properties und Commands

4. **`DashboardViewModel` erweitern: ObservableCollection & LadenAsync**
   - Voraussetzungen: `DashboardViewModel` (vorhanden), `AufgabeService.GetAktiveAufgabenAsync()` (Schritt 2 erledigt)
   - Beschreibung:
     - Neue Property `AktiveAufgabenListe: ObservableCollection<Aufgabe>`
     - `LadenAsync()` erweitern: Ruft `GetAktiveAufgabenAsync()` auf, befüllt `AktiveAufgabenListe`
     - Tests aktualisieren oder neue Tests schreiben für neue Property

5. **`App.xaml` aktualisieren: Converter registrieren**
   - Voraussetzungen: `KiAusfuehrungsStatusConverter` (Schritt 1 erledigt), `App.xaml` (vorhanden)
   - Beschreibung: `KiAusfuehrungsStatusConverter` in `Application.Resources` als `x:Key="KiAusfuehrungsStatusConverter"` registrieren

6. **`MainWindow.xaml` erweitern: Seitenleisten-Sektion**
   - Voraussetzungen: `MainWindow.xaml` (vorhanden), `MainWindowViewModel.AktiveAufgaben` (Schritt 3 erledigt), `MainWindowViewModel.IsDashboardVisible` (Schritt 3 erledigt), `KiAusfuehrungsStatusConverter` (Schritt 1 + 5 erledigt), `InverseBoolToVisibilityConverter` (vorhanden)
   - Beschreibung:
     - Neue `Border` als Separator unterhalb Navigationseinträge
     - Neue `TextBlock "Aktive Aufgaben"`
     - Neue `ScrollViewer` mit `MaxHeight="300"`
     - Neue `ItemsControl ItemsSource="{Binding AktiveAufgaben}"`
     - DataTemplate für Aufgabenkachel:
       - `Border` mit abgerundeten Ecken, Rahmen
       - `Grid` mit 2 Spalten: Text-Content + Navigation-Button
       - Column 0: StackPanel mit Titel-TextBlock + Status-TextBlock (via Converter)
       - Column 1: Button "→" mit `NavigateZuAufgabeCommand` und `CommandParameter="{Binding Id}"`
     - Visibility: `"{Binding IsDashboardVisible, Converter={StaticResource InvertedBoolToVisibilityConverter}}"`

7. **`DashboardView.xaml` erweitern: Aufgabenliste**
   - Voraussetzungen: `DashboardView.xaml` (vorhanden), `DashboardViewModel.AktiveAufgabenListe` (Schritt 4 erledigt), `KiAusfuehrungsStatusConverter` (Schritt 1 + 5 erledigt), Navigation zu `TaskDetailView` (vorhanden)
   - Beschreibung:
     - Neue Sektion (Grid.Row=?) unterhalb Statistik-Kacheln oder in separater Panel
     - Titel: "Aktive Aufgaben"
     - `ItemsControl ItemsSource="{Binding AktiveAufgabenListe}"`
     - DataTemplate: Identisch oder ähnlich wie MainWindow.xaml, ohne Höhenlimit (oder größeres Limit, z.B. 600)
     - Navigation: Bindet auf `MainWindowViewModel.NavigateZuAufgabeCommand` (via `RelativeSource` Ancestor)

8. **Unit-Tests schreiben**
   - Voraussetzungen: Test-Framework (vorhanden), TestDbContextFactory (vorhanden), Converter-Testklasse (kann neu angelegt werden)
   - Beschreibung:
     - `AufgabeServiceTests`: Test für `GetAktiveAufgabenAsync()` (verschiedene Status, Sortierung, Limit)
     - `KiAusfuehrungsStatusConverterTests`: Tests für Status-String-Berechnung
     - `MainWindowViewModelTests`: Tests für `AktiveAufgabenAktualisierenAsync()`, `IsDashboardVisible`, `NavigateZuAufgabeCommand`
     - `DashboardViewModelTests`: Tests für `AktiveAufgabenListe`, `LadenAsync()`

9. **E2E-Tests / Integration-Tests schreiben**
   - Voraussetzungen: E2E-Test-Framework (vorhanden oder neu), Test-Daten (aufgaben mit verschiedenen Status)
   - Beschreibung:
     - Test: Navigation vom Menü zu Aufgabendetail via Aufgabenkachel
     - Test: Seitenleisten-Sektion ist verborgen wenn Dashboard aktiv
     - Test: Dashboard zeigt aktive Aufgaben
     - Test: Aufgabenliste wird aktualisiert nach Navigation

---

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `GetAktiveAufgabenAsync_ShouldReturnAufgabenWithStatusGestartetOrWartend_WhenCalled` | `AufgabeServiceTests` | Filtert korrekt nach Status; testet nur `Gestartet` und `Wartend` |
| `GetAktiveAufgabenAsync_ShouldSortByLastHeartbeatDescThenByErstellungsDatum_WhenCalled` | `AufgabeServiceTests` | Sortierung nach Heartbeat (desc), Fallback ErstellungsDatum (desc) |
| `GetAktiveAufgabenAsync_ShouldLimitTo20Results_WhenMoreThan20Exist` | `AufgabeServiceTests` | Optional: Limit wird eingehalten (oder unbegrenzt, je nach Implementierung) |
| `Convert_ShouldReturnLaeuftString_WhenAktiveRunIdPresentAndHeartbeatRecent` | `KiAusfuehrungsStatusConverterTests` | Converter gibt "▶ Läuft" wenn Heartbeat < 5 min |
| `Convert_ShouldReturnWartetString_WhenStatusIstWartend` | `KiAusfuehrungsStatusConverterTests` | Converter gibt "⏸ Wartet" bei Status Wartend |
| `Convert_ShouldReturnBereitOrStatusFallback_WhenNoActiveRunOrOldHeartbeat` | `KiAusfuehrungsStatusConverterTests` | Fallback-Logik |
| `AktiveAufgabenAktualisierenAsync_ShouldFillObservableCollection_WhenCalled` | `MainWindowViewModelTests` | `AktiveAufgaben` Collection wird befüllt |
| `IsDashboardVisible_ShouldReturnTrue_WhenCurrentViewIsDashboardViewModel` | `MainWindowViewModelTests` | Computed Property korrekt |
| `IsDashboardVisible_ShouldReturnFalse_WhenCurrentViewIsNotDashboard` | `MainWindowViewModelTests` | Computed Property korrekt |
| `NavigateZuAufgabeCommand_ShouldCreateTaskDetailViewModelAndSetCurrentView_WhenExecutedWithAufgabeId` | `MainWindowViewModelTests` | Command navigiert korrekt, neue ViewModel-Instanz wird erstellt |
| `LadenAsync_ShouldFillAktiveAufgabenListe_WhenCalled` | `DashboardViewModelTests` | Dashboard-LadenAsync aktualisiert neue Collection |
| (Optional) `AktiveAufgabenAktualisierenAsync_ShouldBeCalledOnNavigation_E2E` | E2E-Tests | Integration: Navigation triggert Aktualisierung |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `DashboardViewModelTests` (falls vorhanden) | `LadenAsync()` wurde erweitert; Tests müssen berücksichtigen, dass `AktiveAufgabenListe` befüllt wird (neue Assertion oder Setup-Anpassung) |
| `ProjectDetailViewModelTests` (falls Tests Navigation testen) | Möglicherweise betroffen wenn Navigation zu TaskDetailView getestet wird; keine Änderung nötig wenn Tests noch passen |

---

## E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| **Happy Path: Menü-Anzeige**<br/>Aktive Aufgaben werden in Seitenleiste angezeigt wenn nicht auf Dashboard | `E2ETests/MenuActiveTasksTests` oder ähnlich | Akzeptanzkriterium: "Seitenleiste zeigt aktive Aufgaben mit Titel und Status" |
| **Happy Path: Navigation zu Aufgabe**<br/>User klickt auf Aufgabenkachel in Menü und navigiert zu Aufgabendetail | `E2ETests/NavigationToTaskDetailTests` | Akzeptanzkriterium: "Navigation-Button führt zu TaskDetailView" |
| **Happy Path: Dashboard-Anzeige**<br/>Dashboard zeigt aktive Aufgaben, Menü-Sektion ist verborgen | `E2ETests/DashboardActiveTasksTests` | Akzeptanzkriterium: "Dashboard zeigt aktive Aufgaben, Menü-Sektion verborgen wenn Dashboard aktiv" |
| **Status-Anzeige: KI Läuft**<br/>Aufgabe mit aktuellem Heartbeat zeigt "▶ Läuft" | `E2ETests/KiStatusDisplayTests` | Akzeptanzkriterium: "KI-Status wird korrekt angezeigt" |
| **Status-Anzeige: Wartet**<br/>Aufgabe mit Status Wartend zeigt "⏸ Wartet" | `E2ETests/KiStatusDisplayTests` | Akzeptanzkriterium: "Status Wartend wird angezeigt" |
| **Sichtbarkeits-Toggle**<br/>Menü-Sektion verschwindet beim Navigieren zum Dashboard, erscheint wieder beim Verlassen | `E2ETests/VisibilityTests` | Akzeptanzkriterium: "Menü-Sektion ist abhängig von Dashboard-Sichtbarkeit" |

**Welche bestehenden E2E-Tests müssen angepasst werden?**

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| Navigation-Tests (generisch) | Möglicherweise müssen Selektoren aktualisiert werden wenn neue XAML-Elemente die DOM-Struktur ändern |
| Seitenleisten-Layout-Tests | Neue Sektion in Seitenleiste könnte Größe/Position beeinflussen — Tests können angepasst werden wenn sie spezifische Größen asserten |
| Dashboard-E2E-Tests | Wenn Tests die Dashboard-Struktur überprüfen, könnte neue Aufgabenliste Layout-Änderungen bedingen (wahrscheinlich minor) |

---

## Offene Punkte

Keine.

**Hinweis:** Die in der Anforderung formulierten offenen Punkte (1–8) werden durch die Anforderung und Bestandsaufnahme implizit beantwortet:

- **(1) KI-Ausführungsstatus Präzision:** → Converter nutzt `AktiveRunId` + `LastHeartbeatUtc` (< 5 min) für "Läuft", ansonsten Status
- **(2) Refresh-Verhalten:** → On-demand bei Navigation (einfach, performant)
- **(3) Scrolling & Limit:** → ScrollViewer mit `MaxHeight="300"` in Menü; optional Limit 20 im Service
- **(4) Navigation zu Aufgabendetail:** → `TaskDetailView` ersetzt `CurrentView` (analog zu Architektur)
- **(5) Definition "aktive Aufgaben":** → Status `Gestartet` oder `Wartend`
- **(6) Status "Beendet" / "Archiviert":** → Ausgeblendet (nur aktive in Liste)
- **(7) Darstellung Aufgabenkachel:** → Titel + KI-Status (kompakt)
- **(8) Fehlerbehandlung:** → Silent fallback auf leere Liste (wie in Dashboard üblich)
