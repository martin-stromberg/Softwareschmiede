# Bestandsaufnahme: Anzeige der Programmversion in der Seitenleiste

Diese Bestandsaufnahme analysiert den bestehenden Projektcode bezogen auf die Anforderung, die Versionsnummer in der Fußzeile der Navigations-Seitenleiste anzuzeigen.

## Zusammenfassung

**Was bereits vorhanden ist:**
- Das Service-Ökosystem für Versionsverwaltung ist **vollständig implementiert:**
  - `IApplicationVersionProvider` Interface (liest Programmversion aus `version.json`)
  - `ApplicationVersionProvider` Implementierung (funktionsfähig, getestet)
  - `InstalledVersionInfo` Datenklasse (Record mit Version, TagName, Commit, CreatedAtUtc)
  - Registrierung im DI-Container (bereits vorhanden)
- Das Hauptfenster (`MainWindow.xaml`) mit Seitenleiste und Fußzeile (Grid.Row="2") existiert
- `MainWindowViewModel` existiert mit vielen Properties (aber **KEINE** `CurrentVersion` Property)
- Umfangreiche Unit-Tests für beide Komponenten existieren

**Was fehlt:**
- `CurrentVersion` Property im `MainWindowViewModel`
- Asynchrone Initialisierungslogik im ViewModel-Konstruktor zum Laden der Version via `IApplicationVersionProvider`
- TextBlock in `MainWindow.xaml` zur Anzeige der Versionsnummer (mit Binding an `CurrentVersion`)
- Unit-Tests für die neue `CurrentVersion` Property

## Details

- [Datenmodelle](inventory/models.md) – `InstalledVersionInfo` Record
- [Interfaces](inventory/interfaces.md) – `IApplicationVersionProvider`
- [Logik](inventory/logic.md) – `ApplicationVersionProvider` und `MainWindowViewModel`
- [UI-Komponenten](inventory/ui.md) – `MainWindow.xaml` Seitenleisten-Struktur
- [Tests](inventory/tests.md) – Bestehende Testklassen und Hilfsmethoden

## Kritische Erkenntnisse

### Version-Service ist produktionsreif
- `ApplicationVersionProvider` ist vollständig implementiert mit:
  - Fehlerbehandlung (null-Rückgabe bei Datei-Fehler, ungültiger Version)
  - Logging aller Fehlerfälle (Warnungen)
  - Version-Normalisierung via `UpdateVersionComparer` (entfernt führendes "v")
  - Bereits getestet in `ApplicationVersionProviderTests`

### MainWindowViewModel-Integration erforderlich
- Das ViewModel empfängt `IApplicationVersionProvider` **nicht** als Konstruktor-Parameter
- Die `CurrentVersion` Property muss neu hinzugefügt werden
- Initialisierungslogik muss asynchron im Konstruktor laufen (wie bei `UpdatePruefenImHintergrund()`)
- Bei Fehler sollte ein sicherer Fallback (z. B. "Unbekannt" oder leerer String) verwendet werden

### UI-Platzierung vorgesehen
- Die Fußzeile (Grid.Row="2") im MainWindow enthält bereits Separator und Update-Buttons
- Platz für zusätzlichen TextBlock ist vorhanden
- Styling (Schriftgröße, Farbe) muss entsprechend MainWindow-Design gewählt werden
- Sichtbarkeit bei eingeklappter Navigation ist geklärt (StackPanel mit anderen UI-Elementen)

### Test-Abdeckung fehlt
- Keine Unit-Tests für die zukünftige `CurrentVersion` Property
- Optionaler E2E-Test zur UI-Validierung möglich, aber nicht zwingend
