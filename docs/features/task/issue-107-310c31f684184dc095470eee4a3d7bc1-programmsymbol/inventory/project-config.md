# Projekt-Konfiguration

## `Softwareschmiede.App.csproj`
Datei: `src/Softwareschmiede.App/Softwareschmiede.App.csproj`

### Aktuelle Konfiguration in der PropertyGroup

| Property | Aktueller Wert | Beschreibung |
|----------|----------------|-------------|
| `TargetFramework` | `net10.0-windows10.0.17763.0` | WPF-Anwendung für .NET 10 |
| `UseWPF` | `true` | Windows Presentation Foundation aktiviert |
| `OutputType` | `WinExe` | Ausführbare Windows-Anwendung |
| `ApplicationManifest` | `app.manifest` | Manifest-Datei ist gesetzt |
| `StartupObject` | `Softwareschmiede.App.App` | Einstiegspunkt der App |
| `ApplicationIcon` | **nicht vorhanden** | Muss hinzugefügt werden |

### Abgeleitete Erkenntnisse

- Das Projekt ist bereits als WinExe mit Manifest konfiguriert
- Die `<ApplicationIcon>`-Property wird noch nicht verwendet
- Nach Hinzufügen der Property kann MSBuild das Icon automatisch in die `.exe` einbetten
- Die App.manifest (`app.manifest`) enthält derzeit keine Icon-Referenzen

## `app.manifest`
Datei: `src/Softwareschmiede.App/app.manifest`

Das Manifest ist rein für Laufzeit-Einstellungen (Sicherheitsanforderungen, Windows-Kompatibilität, DPI-Awareness) konfiguriert. Icon-Referenzen sind hier nicht erforderlich — das ApplicationIcon wird über MSBuild in die Binärdatei eingebettet.
