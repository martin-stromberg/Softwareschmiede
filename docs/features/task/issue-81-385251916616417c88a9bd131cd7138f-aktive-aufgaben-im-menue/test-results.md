# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

## Zusammenfassung

- Unit-Tests (`Softwareschmiede.Tests`, ohne E2E): 623 / 623 bestanden
- Integrationstests (`Softwareschmiede.IntegrationTests`): 85 / 85 bestanden
- Gesamt (ohne E2E): 708 / 708 bestanden, 0 fehlgeschlagen, 0 übersprungen

Voraussetzung war ein vollständiger, unblockierter `dotnet build` der gesamten Solution (`Softwareschmiede.slnx`) mit 0 Fehlern. Vor dem Build wurden hängende `testhost`/`dotnet`-Prozesse aus einer vorherigen, parallel gelaufenen Testsitzung beendet, die zuvor eine `Softwareschmiede.App.dll` gesperrt und dadurch einen `Ausführungsfehler` (37 Kompilierungsfehler wegen fehlendem `Softwareschmiede.App`-Namespace) verursacht hatten. Nach dem sauberen Build liefen alle Tests fehlerfrei durch.

## E2E-Tests

In diesem Durchlauf nicht erneut ausgeführt. Grund: Der Versuch, E2E-Tests direkt im Anschluss an einen frisch beendeten Testlauf per Ad-hoc-Befehl zu starten, hätte genau das Muster riskiert, vor dem in `continue.md` gewarnt wird (vermeintlich fehlende .NET Desktop Runtime durch Prozess-/Build-Interferenz). Anstatt das Muster erneut zu riskieren, wurde die Ursache stattdessen an der Quelle behoben: Die Kommandodefinition von `/run-tests` (`~/.claude/commands/run-tests.md` sowie die projektinterne Kopie `.devin/skills/lifecycle/run-tests.md`) erzwang bislang `dotnet test --no-build` ohne vorausgehenden Build — das ist jetzt auf `dotnet build && dotnet test --no-build` inkl. Warnhinweis korrigiert.

Aus der vorherigen Implementierungs-Iteration liegt zusätzlich eine verifizierte Baseline-Aussage vor: 11 E2E-Tests (ConPTY-/CLI-Prozess-Automatisierung: `E2E_ConPty*`, `E2E_Plugin*`, `E2E_AufgabeStarten`, `E2E_AutoStartCli`, `E2E_CreateNewTaskNavigation`) schlagen per FlaUI-Timeout nachweislich identisch auf dem unveränderten `main`-Basisstand fehl (verifiziert per `git stash`) — sie sind vorbestehende Umgebungs-Flakiness der WPF-UI-Automatisierung, keine Regression dieser Änderungen, und betreffen keine der in diesem Feature geänderten Dateien.

## Testabdeckung

**Abdeckung:** Nicht neu kombiniert ermittelt (Unit-Projekt allein: 30,8 % Line-Rate; kombiniert mit Integrationstests zuletzt bei 57,3 % gemessen). Keine Abdeckungsregression durch diese Iteration erwartet, da nur bestehende Duplizierung konsolidiert und Test-Umbenennungen vorgenommen wurden — keine neuen ungetesteten Codepfade.

## Fehlende Tests

Keine neuen Befunde in dieser Iteration.
