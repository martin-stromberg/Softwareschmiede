## Fachliche Zusammenfassung

Die Anwendung soll die aktuell installierte Versionsnummer dauerhaft im Fußbereich der Navigations-Seitenleiste anzeigen. Dies ermöglicht Benutzern, die aktuelle Programmversion auf einen Blick zu erkennen, ohne auf Einstellungen oder Dialoge zugreifen zu müssen. Die Version wird über den existierenden `ApplicationVersionProvider` aus der lokalen `version.json` ausgelesen und im UI mit semantischer Versionierung (X.Y.Z) dargestellt.

## Betroffene Klassen und Komponenten

### UI-Komponenten
- `MainWindow.xaml` – Erweitern der Fußzeile (Grid.Row="2") zur Anzeige der Versionsnummer zusätzlich zu den bestehenden Update-Buttons
- `MainWindowViewModel.cs` – Neue öffentliche Eigenschaft `CurrentVersion` zur Bindung der Versionsnummer im View

### Service-Komponenten (existierend, Wiederverwendung)
- `IApplicationVersionProvider` – Existierendes Interface, das `GetInstalledVersionAsync()` bereitstellt
- `ApplicationVersionProvider` – Existierende Implementierung, liest Version aus `version.json`
- `InstalledVersionInfo` – Datenklasse mit Versionsinformation (bereits vorhanden)

### Tests
- `MainWindowViewModelTests` – Unit-Tests für die neue `CurrentVersion`-Eigenschaft
- E2E-Test (optional) – UI-Validierung, dass die Versionsnummer korrekt angezeigt wird

## Implementierungsansatz

### 1. ViewModel-Änderung
- Neue Eigenschaft `CurrentVersion` (string, nullable) im `MainWindowViewModel` hinzufügen
- Diese Property wird beim ViewModel-Konstruktor asynchron über `IApplicationVersionProvider.GetInstalledVersionAsync()` mit der aktuellen Version gefüllt
- Bei Fehler oder fehlender Version → leerer String oder Standard-Fallback (z. B. "Version unbekannt")

### 2. XAML-Änderung
- Im `MainWindow.xaml` in der Fußzeile (Grid.Row="2" der Seitenleiste) ein neues `TextBlock`-Element hinzufügen
- Binding an `CurrentVersion` in `MainWindowViewModel`
- Styling: In dunkler/heller Schrift, ggf. kleinere Schriftgröße (z. B. FontSize="11") zur Differenzierung von Navigations-Schaltflächen
- Platzierung: Unter den Update-Buttons oder oben über der Trennlinie – abhängig vom gewünschten Layout

### 3. Abhängigkeitsinjizierung
- `IApplicationVersionProvider` ist bereits im DI-Container registriert (siehe `App.xaml.cs`)
- `MainWindowViewModel` erhält den Service über Konstruktor-Injection

### 4. Fehlerbehandlung
- Wenn `GetInstalledVersionAsync()` `null` zurückgibt (z. B. `version.json` fehlt), soll ein sicherer Fallback verwendet werden
- Keine Exception sollte das Anzeigen des Fensters blockieren

## Konfiguration

Kein zusätzlicher Konfigurationsbedarf. Die Versionsnummer wird automatisch aus der Anwendungs-`version.json` bezogen, die bereits beim Build erzeugt wird.

## Offene Fragen

1. **Platzierung in der Fußzeile:** Sollte die Versionsnummer über oder unter den Update-Buttons erscheinen? Oder an einer anderen Position?
2. **Fallback-Text:** Was soll angezeigt werden, wenn `version.json` nicht vorhanden ist? (z. B. "Unbekannt", "Dev", oder nur leer?)
3. **Formatierung:** Soll nur "X.Y.Z" angezeigt werden, oder auch zusätzliche Metadaten (z. B. "v1.2.3" mit Präfix, oder "Version 1.2.3" mit Label)?
4. **Sichtbarkeit bei eingeklappter Navigation:** Sollte die Version auch sichtbar sein, wenn die Navigation eingeklappt ist (nur die Symbole-Leiste zeigt), oder nur bei aufgeklappter Navigation?
5. **Interaktivität:** Soll ein Klick auf die Versionsnummer etwas auslösen (z. B. Link zu Release-Notes oder Changelog), oder nur reine Anzeige?
