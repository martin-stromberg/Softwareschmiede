# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### TaskDetailViewModel.cs (TaskDetailViewModel)

- **Doppelter Code** — Der Block zum Ermitteln der Startkonfiguration und Auflösen des effektiven Arbeitsverzeichnisses über `WorkingDirectoryResolver.DetermineEffectiveWorkingDirectoryAsync(lokalerKlonPfad, startConfig, gitPlugin: null, ct: ...)` ist identisch in drei Methoden dupliziert: `OeffneArbeitsverzeichnis()` (Zeile 1782–1788), `OeffneIdeAsync()` (Zeile 1809–1815) und `OeffneVisualStudioCodeFallback()` (Zeile 1867–1873). Jede der drei Stellen wiederholt exakt dieselben vier Zeilen (`var startConfig = _aufgabe.GitRepository?.StartKonfiguration;` gefolgt vom Aufruf des Resolvers mit `gitPlugin: null`).

  Empfehlung: Die drei Vorkommen in eine private Hilfsmethode extrahieren, z. B. `private Task<string> ErmittleEffektivesArbeitsverzeichnisAsync(string lokalerKlonPfad, CancellationToken ct)`, und diese aus allen drei Aufrufstellen nutzen.

- **Fehlende Kapselung / redundante Bedingung** — `ErmittleSolutionPfade(Aufgabe? aufgabe)` (Zeile 1742–1761) prüft in Zeile 1750 selbst, ob `startConfig?.WorkingDirectoryRelativePath is null`, um zwischen `aufgabe.LokalerKlonPfad!` und `WorkingDirectoryResolver.ResolveEffectiveWorkingDirectory(...)` zu unterscheiden. Diese Fallunterscheidung führt `WorkingDirectoryResolver.ResolveEffectiveWorkingDirectory` selbst bereits durch (siehe `WorkingDirectoryResolver.cs` Zeile 68–71: `if (string.IsNullOrWhiteSpace(relativePath)) return normalizedRoot;`). Die Logik ist damit doppelt implementiert.

  Empfehlung: Den Ternary-Ausdruck entfernen und direkt `WorkingDirectoryResolver.ResolveEffectiveWorkingDirectory(aufgabe.LokalerKlonPfad!, aufgabe.GitRepository?.StartKonfiguration?.WorkingDirectoryRelativePath)` aufrufen; die Methode behandelt `null`/leeren `relativePath` bereits korrekt.

- **Fehlerbehandlung / Kopplung** — `OeffneArbeitsverzeichnis()` wurde von einer synchronen Methode zu `async void` geändert (Zeile 1773), ist aber weiterhin über das synchrone `RelayCommand` gebunden (`OeffneArbeitsverzeichnisCommand = new RelayCommand(OeffneArbeitsverzeichnis, () => ShowFileExplorerPanel);`, Zeile 631). Anders als praktisch jede andere asynchrone Aktion in dieser Klasse (`CliStoppenCommand`, `SpeichernCommand`, `OeffneIdeCommand` usw.), die über `AsyncRelayCommand` laufen und dadurch einen Re-Entrancy-Schutz (`_isExecuting`-Flag in `AsyncRelayCommand`, siehe `ViewModelBase.cs` Zeile 106–121) sowie Cancellation-Handling erhalten, hat `RelayCommand` keinen solchen Schutz. Mehrfaches schnelles Klicken auf „Arbeitsverzeichnis öffnen" kann dadurch mehrere überlappende Auflösungs-/Öffnen-Vorgänge gleichzeitig auslösen.

  Empfehlung: `OeffneArbeitsverzeichnisCommand` auf `AsyncRelayCommand` umstellen und `OeffneArbeitsverzeichnis()` zu `private async Task OeffneArbeitsverzeichnisAsync(CancellationToken ct)` machen, analog zu `OeffneIdeAsync`.

- **Fehlerbehandlung** — `OeffneIdeAsync()` ruft in Zeile 1828 `OeffneVisualStudioCodeFallback();` auf, ohne das Ergebnis zu awaiten. Vor dieser Änderung war `OeffneVisualStudioCodeFallback` rein synchron, sodass das fehlende Await unproblematisch war. Jetzt führt die Methode selbst asynchrone I/O aus (`await WorkingDirectoryResolver.DetermineEffectiveWorkingDirectoryAsync(...)`, Zeile 1869–1873) und ist `async void`. Dadurch kann `OeffneIdeAsync` (und damit das umschließende `AsyncRelayCommand` von `OeffneIdeCommand`) bereits als abgeschlossen gelten, während `OeffneVisualStudioCodeFallback` im Hintergrund noch läuft und ggf. `FehlerMeldung` erst danach setzt — außerhalb des Re-Entrancy-Schutzes und der Fehlerbehandlung von `OeffneIdeCommand`.

  Empfehlung: `OeffneVisualStudioCodeFallback` zu `private async Task OeffneVisualStudioCodeFallbackAsync(...)` machen und in `OeffneIdeAsync` mit `await` aufrufen.

## Geprüfte Dateien

- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_VerzeichnisAktionen.cs`
- `src/Softwareschmiede.Tests/E2E/MainTest.cs`
