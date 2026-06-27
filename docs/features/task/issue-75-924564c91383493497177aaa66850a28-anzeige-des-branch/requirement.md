# Anforderungsübersetzung: Anzeige des Branch-Names in der Statusleiste

## Fachliche Zusammenfassung

Die Statusleiste (Fußzeile) der Aufgabendetailansicht soll erweitert werden, um den Namen des aktuellen Git-Branches anzuzeigen. Diese Information wird zusammen mit dem Aufgabenstatus dargestellt, damit der Benutzer jederzeit sehen kann, auf welchem Branch die KI-gestützte Aufgabenbearbeitung läuft. Die Anzeige ist relevant für Aufgaben mit Status `Gestartet` oder höher (wenn ein Branch vorhanden ist).

## Betroffene Klassen und Komponenten

### UI-Komponenten
- **`StatusIndicatorControl.xaml`** (Erweiterung)
  - Anzeige-Template: zusätzliche TextBlock-Elemente für Branch-Name
  - Conditional-Rendering basierend auf Status und Branch-Verfügbarkeit

- **`StatusIndicatorControl.xaml.cs`** (Erweiterung)
  - `BranchName` Dependency Property hinzufügen
  - Logic zur Formatierung und Sichtbarkeit des Branch-Names

- **`TaskDetailView.xaml`** (Anpassung)
  - Binding für Branch-Name zum `StatusIndicatorControl` ergänzen

### ViewModel
- **`TaskDetailViewModel.cs`** (Anpassung)
  - Ggf. Property `AufgabeBranchName` hinzufügen (als Alias/Convenience für `Aufgabe.BranchName`)
  - OnPropertyChanged für `Aufgabe` um Benachrichtigung für Branch-Name-Änderungen prüfen

### Datenmodell
- **`Aufgabe` Entität** (bestehend)
  - Property `BranchName : string?` existiert bereits
  - Keine Änderungen erforderlich

## Implementierungsansatz

1. **`StatusIndicatorControl` erweitern:**
   - Neue Dependency Property `BranchName` hinzufügen (Type: `string?`)
   - XAML-Template anpassen: Nach dem Status-Text zusätzlich Branch-Name anzeigen
   - Formatierung: z.B. "Gestartet • feature/login-fix" oder nur "Gestartet" wenn Branch leer
   - Sichtbarkeit: Branch-Name nur anzeigen wenn `BranchName` nicht null/leer

2. **`TaskDetailViewModel` anpassen:**
   - Property `AufgabeBranchName` hinzufügen, die `Aufgabe?.BranchName` zurückgibt
   - In `Aufgabe` Property-Setter sicherstellen, dass `OnPropertyChanged(nameof(AufgabeBranchName))` aufgerufen wird

3. **`TaskDetailView.xaml` anpassen:**
   - Binding `BranchName="{Binding AufgabeBranchName, Mode=OneWay}"` zum `StatusIndicatorControl` hinzufügen

## Konfiguration

Keine explizite Konfiguration erforderlich. Die Anzeige des Branch-Names ist Teil der Standard-UI und wird für alle Aufgaben mit Status `Gestartet` oder höher angezeigt (wenn ein Branch-Name vorhanden ist).

## Offene Fragen

1. **Formatierung:** Soll der Branch-Name direkt nach dem Status angezeigt werden (z.B. "Gestartet • feature/xyz") oder auf einer separaten Zeile?
2. **Sichtbarkeitskriterium:** Soll der Branch-Name nur bei Status `Gestartet` angezeigt werden, oder auch bei `Wartend`, `Beendet`, `Archiviert`?
3. **Trennzeichen:** Welches Trennzeichen soll zwischen Status und Branch-Name verwendet werden? (Punkt, Bindestrich, Klammer, etc.)
4. **Styling:** Soll der Branch-Name in einer anderen Farbe oder Schriftgröße angezeigt werden?
5. **Kürzen:** Soll ein langer Branch-Name gekürzt werden (z.B. auf 50 Zeichen), und wenn ja, mit Ellipsis oder nur Abschneiden?
