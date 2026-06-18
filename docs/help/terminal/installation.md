← [Zurück zur Übersicht](index.md)

# CLI-Fenster-Einbettung — Installation und Konfiguration

## Voraussetzungen

| Anforderung | Details |
|-------------|---------|
| Betriebssystem | Windows 10 / 11 (Win32 `SetParent`-API erforderlich) |
| .NET | .NET 9+ |
| KI-Plugin | Plugin muss `IKiPlugin.StartCliAsync` implementieren und ein startfähiges CLI liefern |

Die Fenster-Einbettung benötigt kein separates Backend. Das CLI-Tool selbst (z.B. `claude.exe`) muss installiert und über die `PATH`-Umgebungsvariable erreichbar sein.

## Konfiguration

| Parameter | Schlüssel (AppEinstellung) | Standardwert | Beschreibung |
|-----------|---------------------------|--------------|--------------|
| Arbeitsverzeichnis | `WorkDir` | (leer) | Lokales Verzeichnis für Repository-Klons; muss beschreibbar sein |
| Standard-KI-Plugin | `DefaultKiPlugin` | `"Claude"` | Plugin-Prefix des standardmäßig gewählten KI-Plugins |

Die Einstellungen werden auf der **Einstellungsseite** der Anwendung gesetzt.

## Plugin-Voraussetzungen

Jedes KI-Plugin muss für die Fenster-Einbettung folgende Methode implementieren:

```csharp
Task<ProcessStartInfo> StartCliAsync(string localRepoPath, string? optionalParameters, CancellationToken ct);
```

Die `ProcessStartInfo` muss einen GUI-Prozess beschreiben (kein reines Konsolen-Programm ohne Fenster), damit `Process.MainWindowHandle` einen gültigen Handle liefert.

## Überprüfung

1. Einstellungsseite öffnen und Arbeitsverzeichnis konfigurieren.
2. Eine Aufgabe anlegen, „Gestartet setzen" klicken (Repository klonen).
3. KI-Plugin auswählen, „CLI starten" klicken.
4. Das CLI-Fenster sollte nach wenigen Sekunden in der Aufgabenansicht erscheinen.

Erscheint das Fenster nicht:
- Prüfen ob das CLI-Tool über `PATH` erreichbar ist (z.B. `claude --version` in PowerShell ausführen).
- Log-Dateien im `logs/`-Verzeichnis der Anwendung auf Fehler beim Prozessstart prüfen.
- Sicherstellen, dass das Plugin `IKiPlugin.StartCliAsync` korrekt implementiert ist.
