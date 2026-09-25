← [Zurück zur Übersicht](index.md)

# Terminal-Integration — Installation und Konfiguration

## Voraussetzungen

| Anforderung | Details |
|-------------|---------|
| Betriebssystem | Windows 10 Build 17763+ / Windows 11 (Pseudo Console API; ältere Builds → diagnostizierter Pipe-Fallback) |
| .NET | .NET 9+ mit TFM `net10.0-windows10.0.17763.0` |
| KI-Plugin | Plugin muss `IKiPlugin.GetTerminalStartSpecAsync` liefern (bei `CliKiPluginBase`-Plugins automatisch aus `BuildProcessStartInfo` gemappt) und ein startfähiges CLI bereitstellen |

Das Terminal-System benötigt die Pseudo Console API (`CreatePseudoConsole`), die erst ab Windows 10 Build 17763 verfügbar ist — auf älteren Builds greift der Pipe-Fallback (sichtbar als „eingeschränkter Modus"), CLIs mit `RequiresPty`-Anforderung schlagen dort fehl. Das CLI-Tool selbst (z. B. `claude.exe`, `codex.cmd`) muss installiert und über die `PATH`-Umgebungsvariable erreichbar sein; nackte Befehlsnamen werden mit PATHEXT-Semantik aufgelöst, `.cmd`/`.bat`-Shims (z. B. npm-Installationen) werden intern zu `cmd.exe /d /s /c` normalisiert.

## Konfiguration

| Parameter | Schlüssel (AppEinstellung) | Standardwert | Beschreibung |
|-----------|---------------------------|--------------|--------------|
| Arbeitsverzeichnis | `WorkDir` | (leer) | Lokales Verzeichnis für Repository-Klons; muss beschreibbar sein |
| Standard-KI-Plugin | `DefaultKiPlugin` | `"Claude"` | Plugin-Prefix des standardmäßig gewählten KI-Plugins |

### Plugin-spezifische Einstellungen

Einige Plugins erlauben zusätzliche Konfiguration:

| Plugin | Einstellung | Schlüssel | Beschreibung |
|--------|-------------|----------|--------------|
| Codex CLI | Executable-Pfad | `Softwareschmiede.Codex.ExecutablePath` | Optionaler absoluter Pfad zur `codex`-Executable. Falls nicht gesetzt, wird `codex` über `PATH` gesucht. |
| Devin CLI | Executable-Pfad | `Softwareschmiede.Devin.ExecutablePath` | Optionaler absoluter Pfad zur `devin`-Executable. Falls nicht gesetzt, wird `devin` über `PATH` gesucht. |
| Claude CLI | API-Key | `Softwareschmiede.Claude.ApiKey` | Falls leer, wird `ANTHROPIC_API_KEY`-Umgebungsvariable genutzt. |

Die Einstellungen werden auf der **Einstellungsseite** der Anwendung gesetzt.

### Terminal-Laufzeitparameter (`appsettings.json`)

Die Sektion `Terminal` in `src/Softwareschmiede/appsettings.json` steuert die Laufzeitparameter der Session-Erzeugung:

| Parameter | Typ | Standardwert | Beschreibung |
|-----------|-----|--------------|--------------|
| `Terminal:ReplayBufferByteBudget` | `int` (Bytes) | `524288` (512 KiB) | Größe des Replay-Puffers pro Session — begrenzt, wie viel Rohoutput für den Neuaufbau der Anzeige beim erneuten Öffnen einer Aufgabenseite vorgehalten wird |
| `Terminal:DefaultCols` | `int` | `220` | Initiale Spaltenanzahl beim Session-Start (muss zwischen 1 und 32767 liegen) |
| `Terminal:DefaultRows` | `int` | `50` | Initiale Zeilenanzahl beim Session-Start (muss zwischen 1 und 32767 liegen) |

### Test-/Debug-Hook (nicht für den Produktivbetrieb)

| Schlüssel | Ort | Beschreibung |
|-----------|-----|--------------|
| `Terminal.ForcePtyUnavailable` | `AppEinstellungen`-Tabelle (Wert `"true"`) | Erzwingt den Fehlschlag des PTY-Verfügbarkeits-Checks im Preflight — treibt den Pipe-Fallback inkl. `[Terminal-Diagnose]`-Marker bzw. den `RequiresPty`-Fehler. Wird pro Session-Start gelesen; dient ausschließlich Tests/Diagnose und wird nie implizit gesetzt. |

## Plugin-Voraussetzungen

Für den interaktiven Terminal-Pfad muss jedes KI-Plugin `IKiPlugin.GetTerminalStartSpecAsync` liefern — die `TerminalSessionStartSpec` enthält `FileName`, `Arguments`, `WorkingDirectory`, `EnvironmentVariables`, `Capabilities` und `PluginName`. Bei Plugins auf `CliKiPluginBase`-Basis geschieht das automatisch: `BuildTerminalStartSpec` mappt die `ProcessStartInfo` aus `BuildProcessStartInfo` auf die Spec; das Plugin implementiert weiterhin nur `BuildProcessStartInfo` und kann optional `TerminalCapabilities` überschreiben (`SupportsPty` ist der Default; `RequiresPty` für CLIs, die ein echtes TTY benötigen). `IKiPlugin.StartCliAsync` bleibt davon unberührt für den klassischen, nicht-interaktiven Pipe-Start bestehen.

```csharp
// CliKiPluginBase-Ableitung: nur die ProcessStartInfo bauen — Spec und Capabilities kommen aus der Basis.
protected override ProcessStartInfo BuildProcessStartInfo(string localRepoPath, string? parameters)
    => new()
    {
        FileName = ResolveExecutablePath(...), // nackter Name, .cmd/.bat-Shim oder absoluter Pfad
        Arguments = parameters ?? "",
        WorkingDirectory = localRepoPath,
        UseShellExecute = false,
        RedirectStandardInput = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true,
    };

// Optional: TUI-CLI, die ein echtes Pseudo-Terminal benötigt
public override TerminalProviderCapabilities TerminalCapabilities =>
    TerminalProviderCapabilities.RequiresPty | TerminalProviderCapabilities.SupportsPty;
```

`FileName` darf ein nackter Befehlsname, ein `.cmd`/`.bat`-Pfad oder ein absoluter Pfad sein — die Auflösung auf ein `CreateProcess`-fähiges Ziel übernimmt der Host (`TerminalExecutableResolver`). `UseShellExecute` sollte `false` sein, damit Standard Input/Output/Error umgeleitet werden können.

## Überprüfung

1. **Einstellungen:** Einstellungsseite öffnen und **Arbeitsverzeichnis** konfigurieren.
2. **Plugin-Health:** Im Ribbon auf das KI-Plugin klicken; die Anwendung prüft Verfügbarkeit.
3. **Terminal-Start:** Eine Aufgabe anlegen, Status auf **Gestartet** setzen (Repository klonen), **Starten** klicken.
4. Das Terminal sollte unmittelbar mit Farbe und Text-Attributen gerendert werden.

### Fehlerbehebung

| Problem | Ursache | Lösung |
|---------|---------|--------|
| Terminal bleibt leer | Prozess hat Fehler beim Start | Logs im `logs/`-Verzeichnis prüfen; `CodexPlugin.CheckHealthAsync` aufrufen |
| Start schlägt mit „CLI-Executable '…' kann nicht gestartet werden" fehl | `TerminalExecutableResolver` meldet `NotFound` (Executable nicht im Arbeitsverzeichnis/`PATH`) oder `NotExecutable` (z. B. `.ps1`-Skript) | CLI installieren bzw. `PATH` prüfen oder den `ExecutablePath` des Plugins in den Einstellungen auf einen absoluten `.exe`-Pfad setzen; Details liefert die `[Terminal-Diagnose]`-Zeile im Aufgabenprotokoll |
| Start schlägt mit „… erfordert ein Pseudo-Terminal (PTY)" fehl | Plugin deklariert `RequiresPty`, aber ConPTY ist nicht verfügbar (OS-Build < 17763 oder `Terminal.ForcePtyUnavailable`-Test-Hook aktiv) | Windows ≥ 10 Build 17763 verwenden; prüfen, ob der Test-Override `Terminal.ForcePtyUnavailable` in den `AppEinstellungen` gesetzt ist |
| Statuszeile zeigt „ (eingeschränkter Modus – kein Pseudo-Terminal)" | Session läuft auf dem Pipe-Fallback (PTY nicht verfügbar oder Plugin ohne `SupportsPty`) | Gewollter Fallback — die `[Terminal-Diagnose]`-Zeile im Aufgabenprotokoll nennt Grund und Einzelcheck-Ergebnisse; für PTY-Betrieb Windows-Build und Plugin-Capabilities prüfen |
| Nur ASCII-Text, keine Farben | ANSI-Sequenzen nicht erkannt | Plugin-Output prüfen (z.B. `codex --version`); TERM-Umgebungsvariable auf `xterm-256color` setzen |
| `Ctrl+C` löst „Batchdatei abbrechen (J/N)?" aus | CLI wurde über einen `.cmd`/`.bat`-Shim gestartet (z. B. npm-Installation) | Bekanntes Verhalten von Batch-Shims — die Rückfrage mit `J` bestätigen oder die CLI normal beenden |
| Größenänderung verursacht Fehler | `ResizePseudoConsole` schlägt fehl | Seltener Windows-Fehler; Prozess beenden und neu starten |
| Tastatureingaben funktionieren nicht | Focus nicht im Terminal | Terminal-Bereich klicken um Focus zu setzen |
