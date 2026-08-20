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

## Nachverfolgung (Iteration 3 — Fortsetzung über `continue.md`)

Auf ausdrücklichen Wunsch wurde der in Iteration 2 identifizierte vorbestehende Bug dennoch risikoarm korrigiert, da er der einzige Weg war, den `continue.md`-Punkt sauber abzuschließen:

- **Fix:** `WaitForSavedWorkingDirectoryAsync` (`src/Softwareschmiede.Tests/E2E/E2E_WorkingDirectory.cs:351-367`) prüft jetzt vor dem Zugriff auf `repo.Id`, ob `repo` `null` ist, und liefert in diesem Fall `null` als (noch nicht gespeicherten) Zwischenstand statt eine `NullReferenceException` auszulösen. Das Polling läuft danach unverändert bis zum Timeout weiter.
- **Build:** `dotnet build src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj` — 0 Fehler (nur vorbestehende Warnungen).
- **Testverifikation:** `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "FullyQualifiedName~End2EndTest.RunGeneralTests"`, zweimal ausgeführt.
  - Der ursprünglich gemeldete Fehler (`NullReferenceException`/`InvalidOperationException` in `RepositoryZuweisen_MitFehlgeschlagenemStrukturabruf_...`) trat in **keinem** der beiden Läufe mehr auf — der Test durchlief `RepositoryZuweisung` (inkl. der Fallback-Phase) vollständig und kam beide Male signifikant weiter (`Todo_ErstellenAbhakenLoeschenUndAbschlussValidierung_E2E`). Der Fix ist damit als wirksam verifiziert.
  - **Neu aufgetretener, davon unabhängiger Fehler:** Beide Läufe scheitern stattdessen konsistent (2/2) an einem `System.TimeoutException` in `WpfTestBase.WaitForElement`, aufgerufen aus `WpfTestBase.AufgabeDetailSpeichern` (Zeile 896), aufgerufen aus `E2E_TodoManagement.cs:33` (`Todo_ErstellenAbhakenLoeschenUndAbschlussValidierung_E2E`) — ein UI-Element (Speichern-Button im Aufgabe-Detail-Dialog) wurde nicht innerhalb von 15s gefunden.
  - **App-Log-Prüfung (gemäß CLAUDE.md-Regel):** Für beide Läufe wurde `src/Softwareschmiede.App/bin/Debug/net10.0-windows10.0.17763.0/logs/softwareschmiede-20260820.log` auf Startup-Exceptions geprüft. Keine `[ERR]`-Zeile im relevanten Zeitfenster deutet auf einen App-Absturz hin; alle `[ERR]`-Einträge sind erwartete, von anderen (vorangehenden) Testphasen bewusst provozierte Fehler (z. B. Path-Traversal-/fehlendes-Arbeitsverzeichnis-Fehlerbanner). Die App lief also sauber bis zum eigentlichen Timeout — kein Hinweis auf einen Programmierfehler in `App.xaml.cs`/Startup.
  - **Bezug zu dieser Anforderung/diesem Fix:** Keiner. `Todo_ErstellenAbhakenLoeschenUndAbschlussValidierung_E2E` und `WpfTestBase.AufgabeDetailSpeichern` stammen aus Commits vom 2026-08-06 bzw. 2026-08-13 (`git log`), Wochen vor diesem Branch, und werden vom hier vorgenommenen Fix (reiner Null-Guard in `WaitForSavedWorkingDirectoryAsync`) nicht berührt.
  - **Charakter:** Da der Fehler exakt reproduzierbar an derselben Stelle auftritt (nicht zufällig verteilt) und kein App-Absturz vorliegt, handelt es sich vermutlich um eine sandboxspezifische Timing-/UI-Automation-Eigenheit (z. B. Fokus-/Redraw-Verhalten des Dialogs unter FlaUI in dieser Umgebung) oder um einen weiteren, unabhängigen vorbestehenden Test-Defekt — beides außerhalb des Scopes dieser Anforderung und dieses Nacharbeits-Punkts. Eine tiefere Ursachenanalyse erfordert menschliche Entscheidung/Prüfung in einer interaktiven Session.
- **Ergebnis:** Der in `continue.md` dokumentierte Punkt (Fehler in `RepositoryZuweisen_MitFehlgeschlagenemStrukturabruf_...`) gilt als behoben und verifiziert. Der neu sichtbar gewordene, unabhängige Folgefehler in `Todo_ErstellenAbhakenLoeschenUndAbschlussValidierung_E2E` wird als neuer, separater Punkt in `continue.md` dokumentiert.
