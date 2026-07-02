# Bestandsaufnahme: Aktive Aufgaben im Menü

Diese Bestandsaufnahme analysiert den bestehenden Code der WPF-Desktopanwendung bezogen auf die Anforderung "Aktive Aufgaben im Menü" (Issue #81). Ziel ist die Anzeige von Aufgaben mit Status `Gestartet` oder `Wartend` in der Seitenleiste und im Dashboard.

---

## Zusammenfassung

### Vorhanden

- **Domain & Entities**
  - `Aufgabe` Entity mit allen notwendigen Properties (`AktiveRunId`, `LastHeartbeatUtc`, Status)
  - `AufgabeStatus` Enum mit Werten `Gestartet` und `Wartend`
  
- **Services**
  - `AufgabeService` mit umfangreichen Methoden zur Aufgabenverwaltung
  - Methode `GetAktiveUndWartendeCountAsync()` für Zähler (wird im Dashboard verwendet)
  - Heartbeat-Methoden: `UpdateHeartbeatAsync()`, `GetHeartbeatAgeMinutesAsync()`
  
- **ViewModels**
  - `MainWindowViewModel` mit Navigation und CurrentView-Handling
  - `DashboardViewModel` mit Statistik-Kacheln (Anzahl aktiver Aufgaben)
  - `TaskDetailViewModel` für Aufgabendetail-Ansicht
  - Command-Infrastruktur: `RelayCommand`, `AsyncRelayCommand` (mit und ohne Parameter)
  
- **Views & UI**
  - `MainWindow.xaml` mit Seitenleiste und Navigationsstruktur
  - `DashboardView.xaml` mit Statistik-Kacheln
  - `TaskDetailView.xaml` für Aufgabendetail-Anzeige
  - Existierende DataTemplate-Mappings für Navigation
  
- **Converter**
  - `BoolToVisibilityConverter` (für Standard-Sichtbarkeit)
  - `InverseBoolToVisibilityConverter` (für inverse Logik)
  - `BoolToWidthConverter` (für Seitenleisten-Breite)
  - `NullOrEmptyToVisibilityConverter` (für Fehlerbanners)

### Fehlend / Zu erweitern

- **Service-Methode (KRITISCH)**
  - `AufgabeService.GetAktiveAufgabenAsync(CancellationToken ct)` — Filtert und sortiert aktive Aufgaben
  
- **MainWindowViewModel Properties & Commands (KRITISCH)**
  - Property `AktiveAufgaben` : `ObservableCollection<Aufgabe>`
  - Property `IsDashboardVisible` : `bool` (computed)
  - Methode `AktiveAufgabenAktualisierenAsync(CancellationToken ct)` : `Task`
  - Command `NavigateZuAufgabeCommand` : `ICommand` mit `Guid aufgabeId`-Parameter
  
- **DashboardViewModel Property (WICHTIG)**
  - Erweiterung von `AktiveAufgaben` von `int` zu `ObservableCollection<Aufgabe>`
  - Erweiterung von `LadenAsync()` zur Befüllung dieser Collection
  
- **Converter (WICHTIG)**
  - `KiAusfuehrungsStatusConverter` — Konvertiert `Aufgabe` zu Status-String
  
- **XAML (MainWindow.xaml) (KRITISCH)**
  - Neue Sektion in Seitenleiste: "Aktive Aufgaben" mit ItemsControl
  - Border-Separator
  - ScrollViewer mit Höhen-Limit
  - Aufgabenkachel-Template mit Titel, Status-Anzeige und Navigation-Button
  - Sichtbarkeit basierend auf `IsDashboardVisible` (inverse Logik)
  
- **XAML (DashboardView.xaml) (WICHTIG)**
  - Neue Sektion mit aktiven Aufgaben (ähnlich wie Seitenleiste, ohne Höhen-Limit)
  
- **Tests (OPTIONAL aber empfohlen)**
  - Tests für `GetAktiveAufgabenAsync()`
  - Tests für neue ViewModel-Properties
  - Tests für `KiAusfuehrungsStatusConverter`

---

## Details

Siehe folgende Detaildokumente für tiefergehende Analyse:

- [Datenmodelle](inventory/models.md) — Entity `Aufgabe` mit Properties
- [Enums](inventory/enums.md) — `AufgabeStatus` mit Werten
- [Services](inventory/services.md) — `AufgabeService` mit Methoden
- [ViewModels](inventory/viewmodels.md) — `MainWindowViewModel`, `DashboardViewModel`, `TaskDetailViewModel`
- [Converter](inventory/converters.md) — Existierende und fehlende Value Converter
- [Views (XAML)](inventory/views.md) — `MainWindow.xaml`, `DashboardView.xaml`, relevante Struktur
- [Tests](inventory/tests.md) — Bestehende Test-Infrastruktur und empfohlene neue Tests

---

## Architektur-Hinweise

### Navigation
- Navigation erfolgt über Commands in `MainWindowViewModel` (z.B. `NavigateToDashboardCommand`)
- `CurrentView` wird gesetzt, welches durch DataTemplate-Mappings automatisch die richtige View rendert
- Neue Navigation zu Aufgaben-Details: Ähnliches Muster mit `NavigateZuAufgabeCommand`

### Data-Binding
- ObservableCollections werden verwendet für dynamische Listen (z.B. `RecoveryKandidaten`, `LetzteProjects`)
- ViewModels nutzen `INotifyPropertyChanged` (von `ViewModelBase` ererbt)
- Commands sind Relay-Implementierungen (synchron und asynchron)

### Async-Handling
- Asynchrone Operationen nutzen `AsyncRelayCommand` oder `AsyncRelayCommand<T>`
- CancellationToken wird durchgereicht
- `LadenAsync()` wird typischerweise beim View-Load oder bei Daten-Refresh aufgerufen

### UI-Themes
- DynamicResources für Farben: `BackgroundBrush`, `PrimaryTextBrush`, `SecondaryTextBrush`, `BorderBrush`, `SurfaceBrush`, `SuccessBrush`, `WarningBrush`, `ErrorBrush`
- Themes in `Themes/DarkTheme.xaml` und `Themes/LightTheme.xaml` definiert

---

## Implementierungs-Checkliste

- [ ] `AufgabeService.GetAktiveAufgabenAsync()` implementieren
- [ ] `MainWindowViewModel` erweitern: Properties + Commands
- [ ] `MainWindowViewModel.AktiveAufgabenAktualisierenAsync()` implementieren (ggf. mit Timer)
- [ ] `DashboardViewModel` erweitern: ObservableCollection + LadenAsync-Update
- [ ] `KiAusfuehrungsStatusConverter` implementieren
- [ ] `MainWindow.xaml` erweitern: Seitenleisten-Sektion
- [ ] `DashboardView.xaml` erweitern: Aufgabenliste
- [ ] Optional: UserControl `AktiveAufgabeKachel.xaml` erstellen
- [ ] Tests schreiben für neue Methoden und ViewModels
- [ ] App.xaml updaten: Converter registrieren
