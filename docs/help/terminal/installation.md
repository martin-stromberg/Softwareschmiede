← [Zurück zur Übersicht](index.md)

# Terminal-Integration — Installation und Konfiguration

## Voraussetzungen

| Anforderung | Details |
|-------------|---------|
| Betriebssystem | Windows 10 Build 17763+ / Windows 11 (Pseudo Console API erforderlich) |
| .NET | .NET 9+ mit TFM `net10.0-windows10.0.17763.0` |
| KI-Plugin | Plugin muss `IKiPlugin.StartCliAsync` implementieren und ein startfähiges CLI liefern |

Das Terminal-System benötigt die Pseudo Console API (`CreatePseudoConsole`), die erst ab Windows 10 Build 17763 verfügbar ist. Das CLI-Tool selbst (z.B. `claude.exe`) muss installiert und über die `PATH`-Umgebungsvariable erreichbar sein.

## Konfiguration

| Parameter | Schlüssel (AppEinstellung) | Standardwert | Beschreibung |
|-----------|---------------------------|--------------|--------------|
| Arbeitsverzeichnis | `WorkDir` | (leer) | Lokales Verzeichnis für Repository-Klons; muss beschreibbar sein |
| Standard-KI-Plugin | `DefaultKiPlugin` | `"Claude"` | Plugin-Prefix des standardmäßig gewählten KI-Plugins |

### Plugin-spezifische Einstellungen

Einige Plugins erlauben zusätzliche Konfiguration:

| Plugin | Einstellung | Schlüssel | Beschreibung |
|--------|-------------|----------|--------------|
| Codex CLI | Executable-Pfad | `Softwareschmiede.Codex.ExecutablePath` | Optionaler absoluter Pfad zur `codex.exe`. Falls nicht gesetzt, wird `codex` über `PATH` gesucht. |
| Claude CLI | API-Key | `Softwareschmiede.Claude.ApiKey` | Falls leer, wird `ANTHROPIC_API_KEY`-Umgebungsvariable genutzt. |

Die Einstellungen werden auf der **Einstellungsseite** der Anwendung gesetzt.

## Plugin-Voraussetzungen

Jedes KI-Plugin muss für das Terminal-System folgende Methode implementieren:

```csharp
Task<ProcessStartInfo> StartCliAsync(string localRepoPath, string? optionalParameters, CancellationToken ct);
```

Die `ProcessStartInfo` beschreibt einen Konsolenprozess (für ConPTY geeignet). Das Feld `UseShellExecute` sollte `false` sein, damit Standard Input/Output/Error umgeleitet werden können.

### Beispiel: CodexPlugin

```csharp
public override async Task<ProcessStartInfo> StartCliAsync(
    string localRepoPath, string? optionalParameters, CancellationToken ct = default)
{
    var codexCommand = GetCodexCommand(); // findet codex.exe oder Pfad aus Einstellung
    return new ProcessStartInfo
    {
        FileName = codexCommand,
        Arguments = optionalParameters ?? "",
        WorkingDirectory = localRepoPath,
        UseShellExecute = false,
        RedirectStandardInput = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true,
    };
}
```

## Überprüfung

1. **Einstellungen:** Einstellungsseite öffnen und **Arbeitsverzeichnis** konfigurieren.
2. **Plugin-Health:** Im Ribbon auf das KI-Plugin klicken; die Anwendung prüft Verfügbarkeit.
3. **Terminal-Start:** Eine Aufgabe anlegen, Status auf **Gestartet** setzen (Repository klonen), **Starten** klicken.
4. Das Terminal sollte unmittelbar mit Farbe und Text-Attributen gerendert werden.

### Fehlerbehebung

| Problem | Ursache | Lösung |
|---------|---------|--------|
| Terminal bleibt leer | Prozess hat Fehler beim Start | Logs im `logs/`-Verzeichnis prüfen; `CodexPlugin.CheckHealthAsync` aufrufen |
| Nur ASCII-Text, keine Farben | ANSI-Sequenzen nicht erkannt | Plugin-Output prüfen (z.B. `codex --version`); TERM-Umgebungsvariable auf `xterm-256color` setzen |
| Größenänderung verursacht Fehler | `ResizePseudoConsole` schlägt fehl | Seltener Windows-Fehler; Prozess beenden und neu starten |
| Tastatureingaben funktionieren nicht | Focus nicht im Terminal | Terminal-Bereich klicken um Focus zu setzen |
