# Code Review — Feature 72: WPF Aufgabenworkflow

**Status: Befunde vorhanden**

---

## Befunde

### 1. Kompilierfehler — `KiAusfuehrungsService`-Konstruktor in Tests mit fehlendem Parameter (3 Stellen)

**Schwere: Blocker (Kompilierfehler)**

Der Produktiv-Konstruktor von `KiAusfuehrungsService` erwartet seit dem Refactoring zwei Parameter `(ILogger, IServiceScopeFactory)`. Drei Teststellen übergeben nur den Logger.

- `src/Softwareschmiede.Tests/Components/Pages/Aufgaben/AufgabeDetailFolgePromptTests.cs:1308`
- `src/Softwareschmiede.Tests/Components/Pages/Aufgaben/AufgabeDetailGitActionsBunitTests.cs:835`
- `src/Softwareschmiede.Tests/Components/Pages/Aufgaben/AufgabeDetailGitActionsBunitTests.cs:1001`
- `src/Softwareschmiede.Tests/Components/Pages/Aufgaben/AufgabeDetailWorkspacePreviewBunitTests.cs:417`

```csharp
// Falsch (alle vier Stellen):
new KiAusfuehrungsService(NullLogger<KiAusfuehrungsService>.Instance)

// Korrekt:
new KiAusfuehrungsService(NullLogger<KiAusfuehrungsService>.Instance, scopeFactoryMock.Object)
```

---

### 2. Race Condition in `StopCliAsync` — `HasExited`-Prüfung außerhalb des Try-Blocks

**Schwere: Hoch**

`src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`, Zeile 167

```csharp
// Zeile 167 — außerhalb des try-catch:
if (process.HasExited)
    return;

handle.AbsichtlichGestoppt = true;  // Zeile 172

try                                  // Zeile 176
{
    process.CloseMainWindow();
    ...
}
```

Zwei Probleme:
1. `process.HasExited` auf Zeile 167 liegt **vor** dem `try`-Block. Wenn `Dispose()` concurrent läuft, wirft `HasExited` eine `ObjectDisposedException`, die ungefangen zu `TaskDetailViewModel.CliStoppenAsync` propagiert und dort als Fehlermeldung im UI erscheint.
2. Race zwischen Zeile 167 (`HasExited` gelesen) und Zeile 172 (`AbsichtlichGestoppt = true`): Beendet sich der Prozess in diesem Fenster, feuert der `Exited`-Handler mit `AbsichtlichGestoppt == false`. Wenn der ExitCode ungleich 0 ist, ruft der Handler `PersistFehlgeschlagenAsync` auf und setzt den Status auf `Beendet` — obwohl der Nutzer gerade absichtlich gestoppt hat.

**Fix:** `AbsichtlichGestoppt` vor dem `HasExited`-Check setzen und die `HasExited`-Prüfung in den `try`-Block nehmen.

---

### 3. Race Condition — `IsCliRunning = true` nach potenziell bereits gefeuertem `Exited`-Event

**Schwere: Mittel**

`src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`, Zeilen 606–610

```csharp
var handle = await _kiService.StartCliAsync(...);  // Zeile 606
// Der Exited-Handler kann hier bereits gefeuert haben (sehr kurz lebender Prozess)
SelectedKiPluginPrefix = pluginPrefix;             // Zeile 608
IsCliRunning = true;                               // Zeile 609 — zu spät gesetzt
CliProzessGestartet?.Invoke(handle.Process);       // Zeile 610
```

Wenn der gestartete CLI-Prozess sofort abstürzt (z. B. fehlende Abhängigkeit), feuert der `Exited`-Handler in `KiAusfuehrungsService` über den Dispatcher und setzt `IsCliRunning = false`. Danach überschreibt Zeile 609 diesen Wert mit `true`. Das ViewModel zeigt den CLI dauerhaft als laufend an, obwohl kein Prozess mehr existiert — `KannCliStoppen` bleibt `true`, der Stop-Button ist aktiv.

---

### 4. Rollback-Pfad in `ProzessStartenUndCliStartenAsync` bei ProzessStartenAsync-Fehler nach `StartenAsync`-Aufruf

**Schwere: Mittel**

`src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`, Zeile 209

```csharp
await ProzessStartenAsync(aufgabeId, repositoryUrl, basisBranchName, null, ct); // Zeile 209

try                                                                               // Zeile 211
{
    ...
    await _kiAusfuehrungsService.StartCliAsync(...);
}
catch (Exception ex)
{
    await RollbackStartAsync(aufgabeId, ct);                                     // Zeile 232
    throw;
}
```

Der `try`-Block beginnt erst nach `ProzessStartenAsync`. Wenn `ProzessStartenAsync` selbst nach dem internen Aufruf von `_aufgabeService.StartenAsync` fehlschlägt (z. B. das Startskript wirft nach dem Klon), verlässt die Methode Zeile 209 mit einer Exception. Der Rollback-catch auf Zeile 232 wird **nicht** erreicht. Die Aufgabe bleibt im Status `Gestartet` mit einem geklonten Verzeichnis auf der Festplatte, ohne dass je ein Cleanup stattfindet.

---

### 5. `PluginSelectionDialogService` — `CancellationToken` wird nicht geprüft

**Schwere: Niedrig**

`src/Softwareschmiede.App/Services/PluginSelectionDialogService.cs`, Zeilen 16–29

Der `ct`-Parameter wird akzeptiert, aber weder via `ct.ThrowIfCancellationRequested()` noch anderweitig geprüft. Wenn der `CancellationToken` zum Zeitpunkt des Aufrufs bereits abgebrochen ist (z. B. durch Navigation weg vom TaskDetailView), öffnet sich der Dialog trotzdem. Der Nutzer sieht einen Dialog für eine Operation, die längst abgebrochen wurde.

```csharp
public Task<PluginSelectionResult> ShowPluginSelectionDialogAsync(
    IEnumerable<string> availablePlugins,
    string? currentSelection,
    Guid projektId,
    CancellationToken ct = default)   // ct wird nie geprüft
{
    var result = Dispatcher.Invoke(() => { ... });  // Dialog öffnet sich immer
    return Task.FromResult(result);
}
```

---

### 6. `TryParseRateLimitSuggestion` ist toter Code

**Schwere: Niedrig (technische Schuld)**

`src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`

Die Methode `TryParseRateLimitSuggestion` ist `public static` und verbleibt im Service, aber kein Code-Pfad im Branch speist ihr mehr zeilenweise CLI-Ausgabe zu. `KiAusfuehrungsService` puffert keine Ausgabezeilen mehr (die ehemalige `KiSession`-Klasse wurde entfernt). Rate-Limit-Marker (`[[SOFTWARESCHMIEDE_RATE_LIMIT:...]]`) werden im laufenden Prozess nie erkannt und nie in `PromptVorschlag` gespeichert.

---

## Zusammenfassung

| Nr. | Datei | Zeile | Schwere |
|-----|-------|-------|---------|
| 1 | `*FolgePromptTests.cs` / `*GitActionsBunitTests.cs` (2×) / `*WorkspacePreviewBunitTests.cs` | 1308 / 835, 1001 / 417 | Blocker |
| 2 | `KiAusfuehrungsService.cs` | 167, 172 | Hoch |
| 3 | `TaskDetailViewModel.cs` | 606–610 | Mittel |
| 4 | `EntwicklungsprozessService.cs` | 209 | Mittel |
| 5 | `PluginSelectionDialogService.cs` | 16 | Niedrig |
| 6 | `EntwicklungsprozessService.cs` | `TryParseRateLimitSuggestion` | Niedrig |
