# Code-Review

## Ergebnis

**Status:** Keine Befunde

## Befunde

Keine offenen Befunde. Der einzige gemeldete Befund (Namenskonvention, minor) wurde nachträglich vom
Orchestrator direkt behoben:

- ~~**Namenskonventionen und Einheitlichkeit** — In `GetNewEntries_LiestNurInhaltNachOffset` (Zeile 55)
  hält die Variable `var offset = AppStartupLogInspector.Snapshot(_logDirectory);` seit dem Refactoring
  einen `LogSnapshot` (Dateipfad + Offset), nicht mehr einen `long`-Offset.~~ Behoben: Variable in
  `snapshot` umbenannt (`src/Softwareschmiede.Tests/E2E/AppStartupLogInspectorTests.cs`, Zeilen 55/59).
  Build und die betroffenen 31 Tests (`KiAusfuehrungsServiceTests` + `AppStartupLogInspectorTests`)
  anschließend erneut grün.

## Bewertung der Schwerpunkt-Prüfpunkte (keine Befunde)

Die folgenden gezielt geprüften Punkte wurden als sauber umgesetzt bewertet und ergaben keine Befunde:

- **`SendCts`-Kopplung race-frei (`KiAusfuehrungsService.cs`)** — `sendToken` wird in `StartWithPseudoConsoleAsync` unmittelbar nach dem Erzeugen des `CancellationTokenSource` (und vor Handler-Registrierung/Veröffentlichung des Handles in `_handles`) abgegriffen; ein `Exited`-Event kann den CTS somit nicht disposen, bevor der Token gültig kopiert ist. `CancelAndDisposeConPtyResources` ruft `Cancel()` stets vor `SendCts.Dispose()` und `PseudoConsoleSession.Dispose()` auf — der Token ist damit garantiert bereits storniert, wenn der Input-Stream geschlossen wird, sodass `SendCommandDelayedAsync` in die `OperationCanceledException`-Behandlung statt in die `ObjectDisposedException`-Behandlung läuft. Das verbleibende schmale Mid-Write-Fenster ist durch den neuen `catch (ObjectDisposedException)` abgedeckt. Nebenläufige Aufrufe (`Dispose()` gegen `Exited`-Handler) sind sicher: `Cancel()` ist gegen `ObjectDisposedException` abgesichert, `CancellationTokenSource.Dispose()` ist idempotent, und `PseudoConsoleSession.Dispose()` ist `Interlocked`-geschützt. Beide Aufrufstellen liegen zudem in `try/catch(Exception)`. Keine neuen Leaks: Der `SendCts` wird in allen Pfaden (Normal-Exit, Früh-Exit, Service-`Dispose`) disposed.

- **Regressionstest deterministisch (`KiAusfuehrungsServiceTests.cs`)** — `StartWithPseudoConsoleAsync_ProzessEndetVorVerzoegertemSenden_...` erzwingt das Prozessende deterministisch per `Kill` bei t≈0, weit vor der 300-ms-Verzögerung; der (ohne Fix) auftretende `ObjectDisposedException`-Logeintrag würde um ~300 ms erfolgen und vom abschließenden 1-s-`WhenAny` sicher erfasst. Kein Timing-Flakiness-Risiko in der Fehlererkennungsrichtung; ohne den Fix schlägt der Test zuverlässig fehl (Mock matcht `e is ObjectDisposedException` auf jedem Log-Level).

- **Vorheriger Befund `WpfTestBase.LaunchApp` behoben** — Bei fehlendem Hauptfenster wird jetzt in beiden Teilfällen eine aussagekräftige `InvalidOperationException` geworfen (mit Log-Auszug bzw. mit Hinweis „keine [ERR]/[FTL]-Zeile gefunden"), statt stillschweigend einen bereits beendeten Prozess an den Aufrufer zurückzugeben. Kein neuer Randfall: Der Erfolgspfad (Fenster vorhanden) bleibt unverändert.

- **Vorheriger Befund `AppStartupLogInspector`-Log-Rotation behoben** — `Snapshot` liefert nun `LogSnapshot(FilePath, Offset)`; `GetNewEntries` wendet den Offset nur bei identischem Dateipfad an (`OrdinalIgnoreCase`) und liest bei Rollover ab Dateianfang. Der Truncation-Guard (`offset <= stream.Length`) und der Null-Pfad-Fall (Snapshot ohne Datei) sind korrekt behandelt und durch Tests (`GetNewEntries_BeiLogRollover_LiestNeueDateiAbAnfang`, `GetNewEntries_OhneRollover_WendetOffsetWeiterhinAn`) abgedeckt.

## Geprüfte Dateien

- `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`
- `src/Softwareschmiede.Tests/Application/Services/KiAusfuehrungsServiceTests.cs`
- `src/Softwareschmiede.Tests/E2E/WpfTestBase.cs`
- `src/Softwareschmiede.Tests/E2E/AppStartupLogInspector.cs`
- `src/Softwareschmiede.Tests/E2E/AppStartupLogInspectorTests.cs`
- `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs` (mitgeprüft zur Verifikation der Dispose-Idempotenz)

## Hinweis

Die weiteren im Branch geänderten Dateien (`.claude/hooks/*`, `.claude/settings.json`, E2E-Testdateien außerhalb des Fokus) wurden in diesem Lauf nicht erneut geprüft — sie wurden in `review-code.3.md` bereits behandelt und sind nicht Teil der in diesem Lauf geänderten Dateien.
