# Offene Aufgaben

Erstellt am: 2026-08-13
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine.

## Code-Review-Befunde

- [ ] **Fehlerbehandlung (TaskDetailViewModel.cs):** In `OeffneArbeitsverzeichnisAsync` (Zeile 1784–1802), `OeffneIdeAsync` (innerer try/catch um die Arbeitsverzeichnis-Auflösung, Zeile 1811–1823) und `OeffneVisualStudioCodeFallbackAsync` (Zeile 1864–1880) fehlt vor dem allgemeinen `catch (Exception ex)` das in dieser Klasse durchgängig etablierte `catch (OperationCanceledException) { throw; }`. Ohne diesen Vorab-Catch fängt der breite `catch (Exception ex)` in den drei genannten Methoden eine `OperationCanceledException` lokal ab (z. B. bei Aufgabenwechsel/Seitenwechsel während des Awaits) und setzt fälschlich eine sichtbare `FehlerMeldung`, statt den Abbruch wie bei allen anderen Befehlen der Klasse lautlos zu propagieren. Empfehlung: In allen drei Methoden vor dem bestehenden `catch (Exception ex)` einen `catch (OperationCanceledException) { throw; }`-Block ergänzen (in `OeffneVisualStudioCodeFallbackAsync` vor dem `catch (InvalidOperationException ex) when (...)`-Block einfügen, da die Reihenfolge der Catch-Klauseln relevant ist).

- [ ] **Doppelter Code (TaskDetailViewModelTests_Arbeitsverzeichnis.cs / TaskDetailViewModelTests_VisualStudioCode.cs):** Die beiden neuen Testklassen sind bis auf die eigentlichen `[Fact]`-Testmethoden nahezu vollständig identisch (Feld-Deklarationen, Konstruktor, `Dispose()`, `CreateSut(...)`, `ErstelleAufgabeMitRepositoryAsync(string?)` — ca. 100 Zeilen verdoppelte Testinfrastruktur). Empfehlung: Gemeinsame Setup-Logik in eine gemeinsame abstrakte Basisklasse extrahieren, von der beide Klassen erben.

- [ ] **Struktur/Konsistenz (TaskDetailViewModelTests.cs):** Die beiden neuen Tests `OeffneIdeAsync_FindetSolutionImAufgeloestenArbeitsverzeichnis` und `OeffneIdeAsync_OhneLoesungenImArbeitsverzeichnis_FaelltZuVsCodeZurueck` (Zeile 2325–2404) wurden an die bereits sehr große, generische `TaskDetailViewModelTests.cs` (2478 Zeilen) angehängt, obwohl im selben Branch mit `TaskDetailViewModelTests_Arbeitsverzeichnis.cs` und `TaskDetailViewModelTests_VisualStudioCode.cs` bereits zwei nach Thema geschnittene Testklassen für exakt diesen Funktionsbereich eingeführt wurden. Empfehlung: Beide Tests in die thematisch passenden neuen Testklassen verschieben (bzw. `OeffneIdeAsync_OhneLoesungenImArbeitsverzeichnis_FaelltZuVsCodeZurueck` nach `TaskDetailViewModelTests_VisualStudioCode.cs`, `OeffneIdeAsync_FindetSolutionImAufgeloestenArbeitsverzeichnis` nach `TaskDetailViewModelTests_Arbeitsverzeichnis.cs`).

## Fehlgeschlagene Tests

Keine.
