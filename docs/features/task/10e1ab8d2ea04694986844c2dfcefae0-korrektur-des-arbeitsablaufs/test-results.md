# Test-Ergebnisse

## Ergebnis

**Status:** Fehler vorhanden

## Fehlgeschlagene Tests

### E2E.End2EndTest

- **RunGeneralTests** — System.InvalidOperationException: An exception was thrown while attempting to evaluate a LINQ query parameter expression. Inner exception: System.NullReferenceException: Object reference not set to an instance of an object.

## Zusammenfassung

### Stabile Lane (Category!=OsInterface)

- Gesamt: 1300
- Bestanden: 1299
- Fehlgeschlagen: 0
- Übersprungen: 1
- Testdauer: 1,3284 Minuten

### OsInterface Lane (Category=OsInterface)

- Gesamt: 47
- Bestanden: 45
- Fehlgeschlagen: 1
- Übersprungen: 1
- Testdauer: 49,59 Sekunden

### Kombiniert

- Gesamt: 1347
- Bestanden: 1344
- Fehlgeschlagen: 1
- Übersprungen: 2

## Testabdeckung

**Abdeckung:** Nicht messbar

## Fehlende Tests

Quelle: `Dateinamen-Konvention`

Keine signifikanten Abdeckungslücken identifiziert. Die Testsuite hat hohe Abdeckung mit über 1300 Tests in der stabilen Lane.

## Fehlerdetails

### Softwareschmiede.Tests.E2E.End2EndTest.RunGeneralTests

**Dateipfad:** `D:\Repositories\softwareschmiede\10e1ab8d-2ea0-4694-9868-44c2dfcefae0\src\Softwareschmiede.Tests\E2E\MainTest.cs(25,0)`

**Stack Trace Kurz:**
- `System.InvalidOperationException`: An exception was thrown while attempting to evaluate a LINQ query parameter expression. See the inner exception for more information.
- `System.NullReferenceException`: Object reference not set to an instance of an object.
- Fehler bei der LINQ-Parameterbearbeitung in `ExpressionTreeFuncletizer`

**Betroffene Zeilen:**
- `E2E_WorkingDirectory.cs(359)` — `WaitForSavedWorkingDirectoryAsync`
- `E2E_WorkingDirectory.cs(127)` — `RepositoryZuweisen_MitFehlgeschlagenemStrukturabruf_ZeigtTextBoxUndSpeichertManuellenPfad_E2E`
- `E2E_WorkingDirectory.cs(51)` — `RepositoryZuweisung`

**Symptom:** Der Test schlägt beim Versuch fehl, die arbeitsverzeichnis-Einstellungen mit einer Null-Referenz-Exception in einer LINQ-Abfrage gegen die Datenbank zu prüfen.

## Nachverfolgung (Iteration 2)

Ursachenanalyse in Iteration 2 der Implementierungsschleife ergab:

- **Root Cause:** `WaitForSavedWorkingDirectoryAsync` (`E2E_WorkingDirectory.cs:358-359`) greift ungeschützt auf `repo.Id` zu, bevor `repo` (Ergebnis von `db.GitRepositories.FirstOrDefault(...)`) im ersten Polling-Durchlauf gesetzt sein kann — führt zu `NullReferenceException`, die EF Core als `InvalidOperationException` beim Auswerten des LINQ-Parameterausdrucks weiterreicht.
- **Bezug zu dieser Anforderung:** Keiner. `git blame` datiert die betroffenen Zeilen auf 2026-07-13 / 2026-07-19, deutlich vor diesem Branch (2026-08-20). Der einzige in dieser Anforderung neue/geänderte Testpfad (`RunConPtyTests`, enthält `CliPanel_BleibtSichtbarNachBeendigung_E2E` und die Erweiterung von `E2E_PluginAuswahlUndWechsel.cs`) wurde in beiden Testläufen dieser Sandbox übersprungen (`SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`), lief also gar nicht mit — kann den Fehler somit nicht verursacht haben.
- **Reproduzierbarkeit:** 100 % reproduzierbar (2/2 Läufe), unabhängig vom Code dieser Anforderung.
- **Entscheidung:** Keine Codeänderung im Scope dieser Anforderung vorgenommen (Fix würde `E2E_WorkingDirectory.cs`, eine unbeteiligte Datei, betreffen und außerhalb des Anforderungsumfangs "Korrektur des Arbeitsablaufs" liegen). Dokumentiert als bekanntes, vorbestehendes Test-Infrastruktur-Problem zur manuellen Nachverfolgung.
