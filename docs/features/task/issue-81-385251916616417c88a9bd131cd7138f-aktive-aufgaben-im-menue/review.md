# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

---

## Umgesetzte Planelemente

### Neue Klassen

- [x] `KiAusfuehrungsStatusConverter` (Value Converter) — implementiert in `src/Softwareschmiede.App/Converters/AppConverters.cs`
  - Konvertiert `Aufgabe` zu Status-String: "▶ Läuft", "⏸ Wartet", "✓ Bereit"
  - Logik gemäß Plan: AktiveRunId + Heartbeat < 5 min = "Läuft"; Status Wartend = "Wartet"; sonst "Bereit"
  - In App.xaml registriert als `KiAusfuehrungsStatusConverter`

### Service-Methoden

- [x] `AufgabeService.GetAktiveAufgabenAsync()` — implementiert
  - Filtert Aufgaben mit Status `Gestartet` ODER `Wartend`
  - Sortiert nach `LastHeartbeatUtc ?? ErstellungsDatum` absteigend
  - Limit: 20 Ergebnisse via `Take(20)`
  - Nutzt `AsNoTracking()` für Performance

### MainWindowViewModel Erweiterungen

- [x] Property `AktiveAufgaben: ObservableCollection<Aufgabe>` — implementiert
  - Read-only, initialisiert im Constructor
  - Binding-Quelle für ItemsControl in Seitenleiste

- [x] Property `IsDashboardVisible: bool` — implementiert
  - Computed Property: `CurrentView is DashboardViewModel`
  - Triggert `OnPropertyChanged()` bei `CurrentView`-Änderung

- [x] Methode `AktiveAufgabenAktualisierenAsync(CancellationToken ct)` — implementiert
  - Aufrufer: Constructor, Navigation-Commands
  - Ruft `AufgabeService.GetAktiveAufgabenAsync()` auf
  - Befüllt `AktiveAufgaben` ObservableCollection
  - Error-Handling: Silent (Seitenleiste bleibt leer bei Fehler)

- [x] Command `NavigateZuAufgabeCommand: AsyncRelayCommand<Guid>` — implementiert
  - Erstellt neue `TaskDetailViewModel`-Instanz mit AufgabeId
  - Setzt `CurrentView` auf neue Instanz
  - Setzt `ZurueckAction` auf `NavigateToDashboard`

- [x] Methode `NavigateZuAufgabeAsync()` — implementiert
  - Private Hilfsmethode für Command-Ausführung
  - Initialisiert `TaskDetailViewModel` mit Aufgaben-ID

- [x] Initialisierung in Constructor — implementiert
  - Ruft `NavigateToDashboard()` auf
  - Ruft `AktiveAufgabenAktualisierenAsync()` auf

- [x] Navigation-Methoden erweitert — implementiert
  - `NavigateToDashboard()`: Ruft `AktiveAufgabenAktualisierenAsync()` auf
  - `NavigateToProjectList()`: Ruft `AktiveAufgabenAktualisierenAsync()` auf
  - `NavigateToSettings()`: Ruft `AktiveAufgabenAktualisierenAsync()` auf

### DashboardViewModel Erweiterungen

- [x] Property `AktiveAufgabenListe: ObservableCollection<Aufgabe>` — implementiert
  - Read-only, initialisiert im Constructor
  - Binding-Quelle für ItemsControl in DashboardView
  - Bestehende `AktiveAufgaben: int` bleibt unverändert (für Statistik-Kachel)

- [x] Methode `LadenAsync()` erweitert — implementiert
  - Bestehende Logik bleibt erhalten
  - Neue Zeilen: Ruft `GetAktiveAufgabenAsync()` auf und befüllt `AktiveAufgabenListe`
  - Error-Handling: Integriert in bestehende `try-catch`

### XAML Änderungen

- [x] `MainWindow.xaml` erweitert: Seitenleisten-Sektion — implementiert
  - Neue Sektion "Aktive Aufgaben" nach Navigationsbuttons
  - `Border` als Separator
  - `TextBlock` mit Label (mit Sichtbarkeitslogik)
  - `ScrollViewer` mit `MaxHeight="300"`
  - `ItemsControl ItemsSource="{Binding AktiveAufgaben}"`
  - DataTemplate für Aufgabenkacheln:
    - Titel in Column 0
    - Status via `KiAusfuehrungsStatusConverter` in Column 0
    - Navigation-Button "→" in Column 1 mit `NavigateZuAufgabeCommand`
  - Visibility: `"{Binding IsDashboardVisible, Converter={StaticResource InverseBoolToVisibilityConverter}}"`

- [x] `DashboardView.xaml` erweitert: Aufgabenliste — implementiert
  - Neue Sektion "Aktive Aufgaben" in Grid.Row=2 (unter Statistik-Kacheln)
  - `TextBlock` mit Titel
  - `ItemsControl ItemsSource="{Binding AktiveAufgabenListe}"`
  - DataTemplate für Aufgabenkacheln (identisches Layout wie Seitenleiste, ohne Höhenlimit)
  - Navigation-Button mit `DataContext.NavigateZuAufgabeCommand`

- [x] `App.xaml` aktualisiert: Converter registriert — implementiert
  - `KiAusfuehrungsStatusConverter` in Application.Resources registriert (Zeile 19)

### Tests

- [x] `KiAusfuehrungsStatusConverterTests` — implementiert
  - `Convert_ShouldReturnLaeuftString_WhenAktiveRunIdPresentAndHeartbeatRecent` (Test: Heartbeat < 5 min)
  - `Convert_ShouldReturnWartetString_WhenStatusIstWartend` (Test: Status Wartend)
  - `Convert_ShouldReturnBereitOrStatusFallback_WhenNoActiveRunOrOldHeartbeat` (Test: Fallback-Logik)
  - `Convert_ShouldReturnEmptyString_WhenValueIsNotAufgabe` (Test: Fehlerbehandlung)
  - `ConvertBack_ShouldThrowNotSupportedException` (Test: ConvertBack wirft Exception)

- [x] `AufgabeServiceTests` erweitert — implementiert
  - `GetAktiveAufgabenAsync_ShouldReturnAufgabenWithStatusGestartetOrWartend_WhenCalled` (Test: Filterung nach Status)
  - `GetAktiveAufgabenAsync_ShouldSortByLastHeartbeatDescThenByErstellungsDatum_WhenCalled` (Test: Sortierung)
  - `GetAktiveAufgabenAsync_ShouldLimitTo20Results_WhenMoreThan20Exist` (Test: Limit auf 20)

- [x] `MainWindowViewModelTests` erweitert — implementiert
  - `AktiveAufgabenAktualisierenAsync_ShouldFillObservableCollection_WhenCalled` (Test: Collection-Befüllung)
  - `IsDashboardVisible_ShouldReturnTrue_WhenCurrentViewIsDashboardViewModel` (Test: Computed Property)
  - `IsDashboardVisible_ShouldReturnFalse_WhenCurrentViewIsNotDashboard` (Test: Computed Property false)
  - `NavigateZuAufgabeCommand_ShouldCreateTaskDetailViewModelAndSetCurrentView_WhenExecutedWithAufgabeId` (Test: Navigation und ViewModel-Erstellung)

- [x] `DashboardViewModelTests` erweitert — implementiert
  - `LadenAsync_ShouldFillAktiveAufgabenListe_WhenCalled` (Test: Collection-Befüllung)

---

## Hinweise

### Implementierungsqualität

- **Service-Logik:** Korrekt implementiert mit Performance-Optimierungen (`AsNoTracking()`, Limit auf 20)
- **ViewModel-Pattern:** Folgt bestehendem Pattern (Commands, Properties, Error-Handling)
- **XAML-Binding:** Korrekte Binding-Syntax mit Converter und `RelativeSource`
- **Sichtbarkeitskontrolle:** Inverse Logik korrekt umgesetzt (`IsDashboardVisible=true` → Sektion verborgen)
- **Navigation:** `AsyncRelayCommand<Guid>` korrekt als Parameter-Command implementiert

### Testabdeckung

- **Converter-Tests:** 5 Tests abdecken alle Fälle (Läuft, Wartet, Bereit, Error)
- **Service-Tests:** 3 Tests für Filterung, Sortierung und Limit
- **ViewModel-Tests:** Tests für Collection-Befüllung, Computed Property, Navigation
- **Dashboard-Tests:** Test für `AktiveAufgabenListe`-Befüllung

### Bekannte Aspekte

- Die Methode `AktiveAufgabenAktualisierenAsync()` wird aufgerufen bei Navigation zu Dashboard, Projekte und Einstellungen — dies ist correct und erfüllt die Anforderung "on-demand bei Navigation"
- Das 5-Minuten-Heartbeat-Schwellwert ist in `KiAusfuehrungsStatusConverter` hardcoded (`TimeSpan.FromMinutes(5)`) — gemäß Plan ist dies korrekt
- Das Limit von 20 Aufgaben ist mit `Take(20)` in der Service-Methode implementiert — gemäß Plan korrekt
- Die Sortierung nutzt `LastHeartbeatUtc ?? ErstellungsDatum` (Null-Coalescing), was die Fallback-Logik elegant implementiert

### Keine Lücken gefunden

Alle Planelemente sind vollständig umgesetzt. Die Implementierung entspricht exakt dem Umsetzungsplan.
