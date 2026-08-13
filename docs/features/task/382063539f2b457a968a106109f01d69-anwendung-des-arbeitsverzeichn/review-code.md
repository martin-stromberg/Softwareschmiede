# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### TaskDetailViewModel.cs (TaskDetailViewModel)

- **Fehlerbehandlung** — In `OeffneArbeitsverzeichnisAsync` (Zeile 1784–1802), `OeffneIdeAsync` (innerer try/catch um die Arbeitsverzeichnis-Auflösung, Zeile 1811–1823) und `OeffneVisualStudioCodeFallbackAsync` (Zeile 1864–1880) fehlt vor dem allgemeinen `catch (Exception ex)` das in dieser Klasse durchgängig etablierte `catch (OperationCanceledException) { throw; }`. Alle anderen asynchronen Befehlsmethoden der Klasse (`LadenAsync`, `CliStoppenAsync`, `CliNeustartenAsync`, `AufgabeAbschliessenAsync`, `SpeichernAsync`, `LoeschenAsync`, `PullRequestErstellenAsync`, `PullRequestsAktualisierenAsync`, `IssueZuweisenAsync`, `IssueAnlegenAsync`, `AktualisierePullRequestCapabilityAsync`, `AktualisiereIssueCreateCapabilityAsync`) fangen `OperationCanceledException` explizit vorab ab und werfen sie weiter, damit `AsyncRelayCommand.ExecuteAsync`/`ExecuteAsync<T>` (siehe `ViewModelBase.cs`, Zeile 153 bzw. 219) den Abbruch als „kein Fehler" stillschweigend behandelt. Ohne diesen Vorab-Catch fängt der breite `catch (Exception ex)` in den drei genannten Methoden eine `OperationCanceledException` lokal ab (z. B. wenn während des Awaits von `ErmittleEffektivesArbeitsverzeichnisAsync` die Aufgabe gewechselt oder die Seite verlassen wird) und setzt fälschlich eine sichtbare `FehlerMeldung` wie „Arbeitsverzeichnis konnte nicht geöffnet werden: The operation was canceled." bzw. „IDE konnte nicht geöffnet werden: …", statt den Abbruch wie bei allen anderen Befehlen der Klasse lautlos zu propagieren.

  Empfehlung: In allen drei Methoden vor dem bestehenden `catch (Exception ex)` einen `catch (OperationCanceledException) { throw; }`-Block ergänzen (in `OeffneVisualStudioCodeFallbackAsync` vor dem `catch (InvalidOperationException ex) when (...)`-Block einfügen, da die Reihenfolge der Catch-Klauseln für die Auswahl relevant ist).

### TaskDetailViewModelTests_Arbeitsverzeichnis.cs / TaskDetailViewModelTests_VisualStudioCode.cs

- **Doppelter Code** — Die beiden neuen Testklassen sind bis auf die eigentlichen `[Fact]`-Testmethoden nahezu vollständig identisch: Feld-Deklarationen (`_db`, `_aufgabeService`, `_protokollService`, `_todoService`, `_kiService`, `_promptVorlagenService`, `_promptVorlagenPlatzhalterService`, `_promptZeitVersandService`, `_einstellungService`, `_dialogServiceMock`, `_projektId`, `_tempDirectoryFixture`), Konstruktor, `Dispose()`, `CreateSut(...)` (bis auf den zusätzlichen `visualStudioCodeLocator`-Parameter in `TaskDetailViewModelTests_VisualStudioCode`) und `ErstelleAufgabeMitRepositoryAsync(string?)` sind zeichengleich in beiden Dateien vorhanden (`TaskDetailViewModelTests_Arbeitsverzeichnis.cs` Zeile 16–140 vs. `TaskDetailViewModelTests_VisualStudioCode.cs` Zeile 16–140). Das sind ca. 100 Zeilen verdoppelter Testinfrastruktur-Code.

  Empfehlung: Gemeinsame Setup-Logik (Felder, Konstruktor, `Dispose`, `CreateSut`, `ErstelleAufgabeMitRepositoryAsync`, `CreateTempDirectory`) in eine gemeinsame abstrakte Basisklasse (z. B. `TaskDetailViewModelArbeitsverzeichnisTestBase` in `Softwareschmiede.Tests.Helpers` oder direkt neben den beiden Testklassen) extrahieren, von der beide Klassen erben; die beiden abgeleiteten Klassen enthalten dann nur noch die themenspezifischen Testmethoden.

### TaskDetailViewModelTests.cs

- **Struktur/Konsistenz (Testorganisation)** — Die beiden neuen Tests `OeffneIdeAsync_FindetSolutionImAufgeloestenArbeitsverzeichnis` und `OeffneIdeAsync_OhneLoesungenImArbeitsverzeichnis_FaelltZuVsCodeZurueck` (Zeile 2325–2404) wurden an die bereits sehr große, generische `TaskDetailViewModelTests.cs` (2478 Zeilen) angehängt, obwohl im selben Branch mit `TaskDetailViewModelTests_Arbeitsverzeichnis.cs` und `TaskDetailViewModelTests_VisualStudioCode.cs` bereits zwei neue, nach Thema geschnittene Testklassen für exakt diesen Funktionsbereich (Arbeitsverzeichnis-Auflösung von `OeffneIdeCommand`/`OeffneArbeitsverzeichnisCommand`) eingeführt wurden. Insbesondere `OeffneIdeAsync_OhneLoesungenImArbeitsverzeichnis_FaelltZuVsCodeZurueck` prüft denselben Fallback-Pfad (`OeffneVisualStudioCodeFallbackAsync`), der bereits Thema von `TaskDetailViewModelTests_VisualStudioCode.cs` ist. Damit wächst die ohnehin schon sehr große Datei weiter, statt die im selben Branch etablierte Konvention der thematischen Aufteilung konsequent anzuwenden.

  Empfehlung: Beide Tests in eine der neuen, thematisch passenden Testklassen verschieben (z. B. `OeffneIdeAsync_OhneLoesungenImArbeitsverzeichnis_FaelltZuVsCodeZurueck` nach `TaskDetailViewModelTests_VisualStudioCode.cs`, `OeffneIdeAsync_FindetSolutionImAufgeloestenArbeitsverzeichnis` nach `TaskDetailViewModelTests_Arbeitsverzeichnis.cs` oder in eine dedizierte `TaskDetailViewModelTests_Ide.cs`), statt sie der monolithischen `TaskDetailViewModelTests.cs` hinzuzufügen.

## Geprüfte Dateien

- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_Arbeitsverzeichnis.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_VisualStudioCode.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_VerzeichnisAktionen.cs`
- `src/Softwareschmiede.Tests/E2E/MainTest.cs`
- `src/Softwareschmiede.Tests/E2E/WpfTestBase.cs`
