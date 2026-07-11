# Logikklassen / Services

## `PromptVorlagenService`
Datei: `src/Softwareschmiede/Application/Services/PromptVorlagenService.cs`

Verwaltet Promptvorlagen inklusive initialer Standardvorlagen. Wird genutzt von `TaskDetailViewModel` zum Laden verfügbarer Vorlagen.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetAllAsync(CancellationToken)` | public | Lädt alle Promptvorlagen sortiert für die Anzeige |
| `CreateAsync(name, prompttext, CancellationToken)` | public | Legt eine neue Promptvorlage an |
| `UpdateAsync(id, name, prompttext, CancellationToken)` | public | Aktualisiert eine bestehende Promptvorlage |
| `DeleteAsync(id, CancellationToken)` | public | Löscht eine Promptvorlage |
| `EnsureInitialPromptVorlagenAsync(CancellationToken)` | public | Legt initiale Standardvorlagen einmalig an |

### Abhängigkeiten
- `SoftwareschmiededDbContext` (_db)
- `ILogger<PromptVorlagenService>` (_logger)

---

## `KiAusfuehrungsService`
Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`

Singleton-Service, der laufende CLI-Prozesse für KI-Ausführungen verwaltet. Startet, stoppt und überwacht CLI-Prozesse pro Aufgabe. Wird von `TaskDetailViewModel` verwendet, um die `PseudoConsoleSession` abzurufen (auf die der Prompt geschrieben wird).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `IsRunning(aufgabeId)` | public | Prüft, ob ein CLI-Prozess für eine Aufgabe läuft |
| `GetRunningProcess(aufgabeId)` | public | Gibt den laufenden Prozess zurück, oder null |
| `GetRunningCount()` | public | Gibt die Anzahl der aktuell laufenden CLI-Prozesse zurück |
| `StartCliAsync(aufgabeId, kiPlugin, localRepoPath, optionalParameters, ct, startConfig, gitPlugin)` | public | Startet einen CLI-Prozess und gibt das Handle zurück |
| `GetPseudoConsoleSession(aufgabeId)` | public | Gibt die aktive `PseudoConsoleSession` für eine Aufgabe zurück, oder null |
| `StopCliAsync(aufgabeId, ct)` | public | Stoppt einen laufenden CLI-Prozess |

### Events
| Event | Parameter | Zweck |
|-------|-----------|-------|
| `CliProcessStatusChanged` | `Guid aufgabeId, CliProcessStatus status` | Wird ausgelöst, wenn ein CLI-Prozess gestartet, gestoppt oder Fehler aufgetreten ist |

### Abhängigkeiten
- `ILogger<KiAusfuehrungsService>` (_logger)
- `ILoggerFactory` (_loggerFactory)
- `IServiceScopeFactory` (_scopeFactory)

---

## `PseudoConsoleSession`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs`

Koordiniert eine laufende Pseudo-Console-Sitzung aus `PseudoConsole`, `Process`, Eingabe- und Ausgabe-Stream. Betreibt die Leseschleife ab Konstruktion bis `Dispose` unabhängig vom Lebenszyklus eines anzeigenden Controls.

| Eigenschaft / Methode | Sichtbarkeit | Kurzbeschreibung |
|--------|-------------|------------------|
| `InputStream` | public | Schreibbarer Stream für Tastatureingaben an den Prozess – **hierauf schreiben die Prompts** |
| `OutputStream` | public | Lesbarer Stream für die Prozessausgabe |
| `Process` | public (get) | Der verwaltete Prozess der Sitzung |
| `RuntimeStatus` | public (get) | Laufzeitstatus der aktiven CLI (Inaktiv, Läuft, WartetAufEingabe) |
| `Buffer` | public (get) | Terminal-Buffer, gefüllt von der Leseschleife |
| `MarkOutputActivity()` | public | Meldet gelesene Ausgabe der CLI an die Status-Erkennung |
| `MarkInputActivity()` | public | Meldet eine Benutzereingabe an die Status-Erkennung |
| `Resize(cols, rows)` | public | Ändert die Größe der Pseudo Console |
| `ReadLoopAsync(CancellationToken)` | private | Kontinuierliche Leseschleife der Ausgabe |

### Events
| Event | Parameter | Zweck |
|-------|-----------|-------|
| `RuntimeStatusChanged` | `object? sender, CliRuntimeStatusChangedEventArgs e` | Wird ausgelöst, wenn sich der Laufzeitstatus der CLI ändert |
| `BufferChanged` | `object? sender, EventArgs e` | Wird nach jeder erfolgreichen Verarbeitung eines Ausgabe-Chunks ausgelöst |

### Abhängigkeiten
- `PseudoConsole` (_pseudoConsole)
- `Process` (_process)
- `TimeProvider` (_timeProvider)
- `ILogger` (_logger, optional)

### Hinweis: Prompt-Versand
`TaskDetailViewModel.PromptVorlageAuswaehlenAsync` schreibt den Prompt direkt auf `session.InputStream`:
```csharp
var bytes = Encoding.UTF8.GetBytes(prompt + Environment.NewLine);
await session.InputStream.WriteAsync(bytes, ct);
await session.InputStream.FlushAsync(ct);
session.MarkInputActivity();
```

---

## `CliRuntimeStatus` (Enum)
Datei: `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs`

Laufzeitstatus einer aktiven CLI-Sitzung.

| Wert | Bedeutung |
|------|-----------|
| `Inaktiv` | Kein laufender CLI-Prozess ist aktiv |
| `Laeuft` | Die CLI läuft und hat kürzlich Ausgabe oder Eingabe verarbeitet |
| `WartetAufEingabe` | Die CLI läuft, erzeugt aber seit längerer Zeit keine Ausgabe und wartet vermutlich auf Benutzereingabe |

