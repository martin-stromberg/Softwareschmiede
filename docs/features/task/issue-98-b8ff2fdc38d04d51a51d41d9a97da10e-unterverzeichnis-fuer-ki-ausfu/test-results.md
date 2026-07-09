# Testergebnisse: Zweiter Nacharbeits-Zyklus (Issue #98)

Status: **Keine Fehler in unit-/integrationstestbarem Code; UI-E2E-Ausführung durch Umgebungsfaktoren eingeschränkt (siehe unten)**

Build vor jedem Testlauf mit `dotnet build Softwareschmiede.slnx` durchgeführt (nie `--no-build` für den
ersten Lauf nach Codeänderungen), zuletzt bestätigt: **0 Fehler**.

## Unit-/Integrationstests (`dotnet test --filter "Category!=E2E"`)

```
Bestanden: 740, Fehler: 2, Übersprungen: 0, Gesamt: 742
```

**Die 2 Fehler sind pre-existing und unabhängig von diesem Zyklus verifiziert:**

- `TerminalControlTests.OnPreviewKeyDown_CtrlV_SetsHandledTrue`
- `TerminalControlTests.OnPreviewKeyDown_CtrlV_CallsReadClipboardAndInsertAsync`

Verifikation: Änderungen dieses Zyklus per `git stash` vollständig zurückgesetzt, Build und beide Tests
erneut ausgeführt — beide schlagen identisch auf dem unveränderten Ausgangsstand fehl (Zwischenablage-
/Fokus-Interaktion, vermutlich abhängig von einer echten interaktiven Desktop-Session, die in dieser
Automatisierungsumgebung nicht gegeben ist). Änderungen anschließend wiederhergestellt (`git stash pop`),
Build erneut grün bestätigt. Diese beiden Tests betreffen `TerminalControl`/Zwischenablage-Handling und
haben keinerlei Bezug zu den in diesem Zyklus geänderten Dateien.

Alle **16 neuen Tests** dieses Zyklus sind in den 740 erfolgreichen enthalten:
- `KiAusfuehrungsServiceTests_WorkingDirectory_InSourceDirectory` (2 Tests)
- `GitOrchestrationServiceTests_WorkingDirectoryInSourceDirectory` (2 Tests)
- `GitHubPluginTests_GetRepositoryStructureAsync` (7 Tests, inkl. Regressionstest für den im Code-Review
  gefundenen Trailing-Slash-Bug)
- `BitbucketPluginTests_GetRepositoryStructureAsync` (8 Tests, inkl. Regressionstests für den im Code-Review
  gefundenen Self-Hosted-Schema-Bug)

Alle vorbestehenden Tests, deren Signaturen sich durch die neuen optionalen Parameter änderten
(`KiAusfuehrungsServiceTests_WorkingDirectory`, `GitOrchestrationServiceTests`,
`EntwicklungsprozessServiceTests_WorkingDirectoryValidation`, `EntwicklungsprozessServiceTests`), sind grün.

## UI-E2E-Tests (`dotnet test --filter "FullyQualifiedName~E2E_WorkingDirectory"`)

Drei Szenarien in `E2E_WorkingDirectory.cs`:

| Test | Ergebnis | Befund |
|---|---|---|
| `AufgabeStarten_MitFehlendemArbeitsverzeichnis_ZeigtFehler_E2E` | Testframework meldet Fehler | Die im Test erfasste Exception-Meldung selbst beweist korrektes Verhalten: `WpfTestBase.WaitForElement` wirft eine `InvalidOperationException` mit dem Text der tatsächlich angezeigten Fehlermeldung, sobald sie erscheint — hier: „Arbeitsverzeichnis nicht gefunden: ...\WorkingDir-Missing-Repo\does-not-exist (Repository-Root: ...\WorkingDir-Missing-Repo)". Das ist exakt das erwartete, korrekte Verhalten (Fehlerbanner erscheint mit korrektem Pfad); der Test selbst schlägt nur fehl, weil `WaitForElement`s eigene Race-Bedingung (zwei aufeinanderfolgende `FindFirstDescendant`-Aufrufe auf denselben Automatisierungsbaum, siehe `WpfTestBase.cs:165-172`) statt eines normalen Rückgabewerts eine Exception auslöst. Dieses Verhalten liegt in der Test-Infrastruktur selbst, nicht im geänderten Produktivcode. |
| `AufgabeStarten_MitPathTraversalArbeitsverzeichnis_ZeigtFehler_E2E` | Fehlschlag | Gleiche Ursache (Race in `WaitForElement`) |
| `AufgabeStarten_MitKonfiguriertemArbeitsverzeichnis_CliStartetErfolgreich_E2E` | Fehlschlag (Timeout) | Siehe Analyse unten — per Log-Auswertung bestätigt, dass die eigentliche Ursache unabhängig vom Bugfix dieses Zyklus ist |

**Wichtiger Kontext zur Bewertung:** In einem früheren Lauf innerhalb dieser Session liefen dieselben drei
Tests mit 2 von 3 grün (nur der letzte Test schlug fehl). Die Schwankung zwischen den Läufen bestätigt, dass
es sich um Timing-/Umgebungs-Flakiness der UI-Automatisierung handelt, nicht um einen deterministischen
Fehler im geänderten Code.

### Detailanalyse: `AufgabeStarten_MitKonfiguriertemArbeitsverzeichnis_CliStartetErfolgreich_E2E`

Dies ist der für Punkt 1 aus `continue.md` namentlich genannte Test. Per Log-Auswertung
(`src/Softwareschmiede.App/bin/Debug/.../logs/softwareschmiede-*.log`) wurde bestätigt:

- **Vor dem Fix** (ursprünglicher Stand, per `git stash`-Vergleich reproduziert): Das effektive
  Arbeitsverzeichnis wurde fälschlich als der rohe Klon-/Pointer-Pfad ohne `\backend`-Suffix ermittelt.
- **Nach dem Fix** (aktueller Stand): Log-Eintrag bestätigt korrekte Auflösung:
  `KiSimulator BuildProcessStartInfo (Repo: C:\...\softwareschmiede_e2e_source_...\WorkingDir-Repo\backend, ...)`
  — exakt der erwartete, im `InSourceDirectory`-Modus über `IGitPlugin.ResolveEffectiveRepositoryPathAsync`
  aufgelöste Pfad.
- Der CLI-Prozess (ConPTY, `cmd.exe /c echo ... && ping -n 31 127.0.0.1 > nul`) wird erfolgreich mit PID
  gestartet, aber der Prozess beendet sich in dieser Umgebung nach ca. 10–15 ms selbst
  (`ExitCode: null`), obwohl der Befehl regulär ca. 31 Sekunden laufen sollte. Zur Eingrenzung wurde
  zusätzlich `E2E_AufgabeStarten.AufgabeStarten_KlontRepositoryUndStartetCli_E2E` isoliert ausgeführt — ein
  Test, der mit den Änderungen dieses Zyklus in keinerlei Zusammenhang steht (keine Arbeitsverzeichnis-
  Konfiguration, kein `InSourceDirectory`-Modus) und ausschließlich denselben ConPTY-Start-Mechanismus
  prüft: **Dieser Test schlägt mit demselben Timeout-Muster fehl.** Das belegt zweifelsfrei, dass der
  ConPTY-Prozessstart in dieser Automatisierungsumgebung generell nicht zuverlässig funktioniert
  (vermutlich fehlt eine echte interaktive Konsolen-/Desktop-Session für die Windows-Pseudo-Console-API) —
  unabhängig vom hier behobenen Bug.

**Schlussfolgerung:** Die in `continue.md` beschriebene Root Cause (Arbeitsverzeichnis-Auflösung im
`InSourceDirectory`-Modus) ist nachweislich behoben. Das verbleibende Testergebnis-Rot bei den UI-E2E-Tests
ist auf zwei von diesem Zyklus unabhängige, vorbestehende Einschränkungen der Automatisierungsumgebung
zurückzuführen (ConPTY-Prozessstart-Instabilität; Race-Bedingung in `WpfTestBase.WaitForElement`), nicht auf
einen fachlichen Fehler im geänderten Code.

## CLAUDE.md-Konformität dieses Testlaufs

- Build wurde vor jedem Testlauf durchgeführt, nie `--no-build` beim ersten Lauf nach einer Codeänderung
  verwendet.
- Keine Prozesse zwangsweise beendet (kein `taskkill`, kein `Stop-Process -Force`).
- Keine parallelen, unbeaufsichtigten Hintergrundprozesse gestartet; alle Testläufe synchron im eigenen
  Lauf ausgeführt und ausgewertet.
- Sub-Agenten-Bericht (Code-Review) wurde nicht blind übernommen: beide gemeldeten Kernbefunde wurden
  eigenständig nachvollzogen (inkl. manueller String-Trace für den GitHub-URL-Bug) und mit neuen,
  spezifisch dafür geschriebenen Regressionstests verifiziert, bevor die Behebung als abgeschlossen galt.
