# Bestandsaufnahme: Views (XAML)

## `MainWindow.xaml`
Datei: `src/Softwareschmiede.App/Views/MainWindow.xaml`

### Struktur
- **Window-Größe:** 1280x800 (Min: 900x600)
- **Layout:** 2-spaltige Grid (Seitenleiste + Inhaltsbereich)

### Seitenleiste (Sidebar)
- **Breite:** Dynamisch basierend auf `IsNavigationExpanded` (via `BoolToWidthConverter`)
- **MinWidth:** 48 (icons only), **ExpandedWidth:** 240 (mit Text)
- **Bestandteile:**
  - Toggle-Button (☰) zum Ein-/Ausklappen
  - Navigations-Buttons (Dashboard, Projekte)
  - Spacer (Grid mit Height="Auto")
  - Einstellungen-Button
  - **FEHLEND:** Sektion "Aktive Aufgaben" mit ItemsControl für `AktiveAufgaben` ObservableCollection

### Inhaltsbereich
- **ContentControl:** Zeigt `CurrentView` gemäß DataTemplate-Mappings
- **DataTemplates:** DashboardViewModel → DashboardView, ProjectListViewModel → ProjectListView, etc.

### Ressourcen
- **Converter:** BoolToWidthConverter, BoolToVisibilityConverter (global registriert)
- **Themes:** DarkTheme.xaml, LightTheme.xaml (für Brush-Ressourcen)

**FEHLEND (gemäß Anforderung):**
- Neue Sektion in der Seitenleiste nach den Navigations-Buttons
- `Border` als Separator
- `TextBlock` mit "Aktive Aufgaben"-Label
- `ScrollViewer` mit `MaxHeight="300"`
- `ItemsControl` mit `ItemsSource="{Binding AktiveAufgaben}"`
- `Visibility="{Binding IsDashboardVisible, Converter={StaticResource InvertedBoolToVisibilityConverter}}"`
- DataTemplate für Aufgabenkacheln (inline oder via UserControl)

---

## `DashboardView.xaml`
Datei: `src/Softwareschmiede.App/Views/DashboardView.xaml`

### Struktur
- **Layout:** 4-zeilige Grid (Titel + Statistik-Kacheln + Recovery-Banner + Fehler)

### Bestandteile
1. **Titel** (Grid.Row="0")
   - TextBlock: "Dashboard", FontSize 24, SemiBold

2. **Statistik-Kacheln** (Grid.Row="1", UniformGrid mit 3 Spalten)
   - Projekte-Kachel: Zeigt `ProjektAnzahl`
   - Aktive Aufgaben-Kachel: Zeigt `AktiveAufgaben` (int, nicht ObservableCollection)
   - Wartend-Kachel: Zeigt `WartendAufgaben`
   - Visuelle Gestaltung: Border mit Rahmen, abgerundete Ecken, SuccessBrush / WarningBrush

3. **Recovery-Banner** (Grid.Row="2")
   - Custom Control: `RecoveryBannerControl`
   - Zeigt `RecoveryKandidaten.Count`

4. **Fehler-Banner** (Grid.Row="3")
   - Sichtbar wenn `FehlerMeldung` nicht null/leer
   - Converter: `NullOrEmptyToVisibilityConverter`
   - Hintergrund: ErrorBrush

### Data-Binding
- `IsLoading` → blendet vermutlich Loading-Spinner aus (nicht im XAML sichtbar)
- `LetzteProjects` → nicht im aktuellen XAML enthalten

**FEHLEND (gemäß Anforderung):**
- Neue Sektion für aktive Aufgaben als `ObservableCollection<Aufgabe>`
- Platzierung: z.B. unterhalb der Statistik-Kacheln
- Ähnliches Layout wie in der Seitenleiste, aber ohne Höhen-Limit oder mit größerem Limit
- Optional mit ScrollViewer für viele Aufgaben

---

## `TaskDetailView.xaml`
Datei: `src/Softwareschmiede.App/Views/TaskDetailView.xaml`

### Hinweise
- Zeigt Details einer einzelnen Aufgabe
- Enthält Ribbon-Menü mit Buttons (Zurück, Speichern, Löschen, Starten, Beenden)
- Enthält Editing-Panels, CLI-Status, Protokoll, Diff-Viewer
- Wird durch Navigation zu `TaskDetailViewModel` angezeigt

**Verwendung für die Anforderung:**
- Ziel der Navigation via `NavigateZuAufgabeCommand` in `MainWindowViewModel`
- Der Command soll eine neue `TaskDetailViewModel`-Instanz mit der Aufgaben-ID erstellen
- `CurrentView` wird auf diese neue Instanz gesetzt

---

## Andere relevante Views

### `ProjectListView.xaml`
- Zeigt Liste von Projekten
- Relevanz: Navigation ist möglich von hier aus (könnte sich ändern, wenn Seitenleiste immer sichtbar ist)

### `ProjectDetailView.xaml`
- Zeigt Details eines Projekts mit zugehörigen Aufgaben
- Möglicherweise bereits Navigation zu `TaskDetailView`

### App.xaml
- Enthält Application-Resources
- Converter-Registrierung
- **ERFORDERLICH:** Registrierung des neuen `KiAusfuehrungsStatusConverter`

---

## Zusammenfassung: Erforderliche XAML-Änderungen

| Datei | Änderungstyp | Details |
|-------|-------------|---------|
| MainWindow.xaml | Erweiterung | Neue Sektion "Aktive Aufgaben" in Seitenleiste mit ItemsControl, Border-Separator, Sichtbarkeit basierend auf `IsDashboardVisible` |
| DashboardView.xaml | Erweiterung | Neue Sektion mit aktiven Aufgaben als ObservableCollection unter Statistik-Kacheln |
| App.xaml | Registrierung | `KiAusfuehrungsStatusConverter` registrieren |
| Optional: AktiveAufgabeKachel.xaml | Neu | Wiederverwendbarer UserControl für Aufgabenkachel-Darstellung |
