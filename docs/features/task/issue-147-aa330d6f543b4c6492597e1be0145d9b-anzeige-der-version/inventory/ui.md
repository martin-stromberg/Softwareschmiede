# UI-Komponenten

## `MainWindow.xaml`
Datei: `src/Softwareschmiede.App/Views/MainWindow.xaml`

Das Hauptfenster der Anwendung. Besteht aus einer vertikalen Zweiteilung:
- **Spalte 0:** Seitenleiste (Border mit Grid intern)
- **Spalte 1:** Inhaltsbereich (ContentControl mit Binding an `CurrentView`)

**Seitenleisten-Struktur (Border, Grid mit 3 Zeilen):**

| Grid.Row | Inhalt | Beschreibung |
|----------|--------|-------------|
| 0 | StackPanel | Navigation-Schalter: Hamburger-Menü, Dashboard, Projekte, Einstellungen, und Separator |
| 1 | ScrollViewer | Aktive Aufgaben (nur sichtbar wenn nicht im Dashboard) |
| 2 | StackPanel | **Fußzeile mit Update-Buttons:** Border (Separator), "Update" Button, "Prüfen" Button |

**Fußzeile (Grid.Row="2"):**
- Margin: 4,8,4,8
- Enthält einen Border mit Separator und zwei Buttons:
  - `UpdateStartenCommand` Button – nur sichtbar wenn `UpdateVerfuegbar == true`
  - `UpdatePruefenCommand` Button – immer sichtbar
  - Beide Buttons haben Visibility-Binding an `IsNavigationExpanded`

**Binding-Kontext:** MainWindowViewModel (implizit über DataContext)

**Aktuelle Befunde:**
- Die Fußzeile existiert und enthält die Update-Buttons
- **Keine TextBlock für die Versionsnummer vorhanden**
- **Keine Binding an `CurrentVersion` vorhanden** (die Property selbst existiert auch nicht im ViewModel)

**Zu ändern:**
- TextBlock für Versionsnummer zu Grid.Row="2" hinzufügen
- Binding an die zukünftige `CurrentVersion` Property im ViewModel
