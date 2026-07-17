# Bestandsaufnahme - Tests strukturieren

## Ergebnis

Die Testsuite hat bereits eine grobe Trennung ueber xUnit-Traits, aber noch keine einheitliche Kategorie `OsInterface`.

- E2E-Tests sind ueber `Trait("Category", "E2E")` markiert.
- Ein ConPTY-Integrationstest ist ueber `Trait("Category", "ConPTY")` markiert.
- Die CI schliesst aktuell `Category=E2E` und `Category=ConPTY` aus, nicht `Category=OsInterface`.
- Mehrere regulaer wirkende Unit-/Control-Tests greifen dennoch auf echte OS-Ressourcen zu, insbesondere Windows-Clipboard und echte PseudoConsole-Erzeugung.
- `TaskDetailViewModelTestFactory` erzeugt aktuell einen echten `KiAusfuehrungsService`; der Service hat zwar bereits einen injizierbaren `IPseudoConsoleProcessLauncher`, die Factory nutzt diesen Austauschpunkt aber nicht.
- Ein Hilfsskript fuer isolierte Einzeltest-Laeufe existiert und enthaelt bereits Retry-Logik fuer Infrastrukturfehler. Diese Retry-Logik ist nicht an Testkategorien gekoppelt.

## Detaildokumente

- [Teststruktur und Kategorien](inventory/teststruktur.md)
- [OS-Schnittstellen und flaky Testfelder](inventory/os-schnittstellen.md)
- [Mocking und Test-Doubles](inventory/mocking.md)
- [Testauswertung, Skripte und CI](inventory/testauswertung-ci.md)
- [Dokumentation und bekannte Sonderfaelle](inventory/dokumentation-sonderfaelle.md)

## Relevante Einstiegspunkte

| Bereich | Dateien |
|---|---|
| xUnit-Testprojekt | `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj` |
| Integrationstestprojekt | `src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj` |
| E2E-Tests | `src/Softwareschmiede.Tests/E2E/` |
| Terminal-/Clipboard-Control-Tests | `src/Softwareschmiede.Tests/App/Controls/TerminalControlTests*.cs` |
| ViewModel-Testfactory | `src/Softwareschmiede.Tests/Helpers/TaskDetailViewModelTestFactory.cs` |
| ConPTY-Produktivcode | `src/Softwareschmiede/Infrastructure/Terminal/`, `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs` |
| Einzeltest-Skript | `scripts/Run-AllTestsIndividually.ps1` |
| CI-Testworkflow | `.github/workflows/test.yml` |

## Umsetzungsrelevante Beobachtungen

1. Ein zentrales `OsInterface`-Attribut existiert noch nicht. Die vorhandenen Kategorien `E2E` und `ConPTY` koennen entweder auf `OsInterface` migriert oder um `OsInterface` ergaenzt werden.
2. Der gewuenschte Befehl `dotnet test --filter Category!=OsInterface` waere heute nicht ausreichend, weil OS-nahe Tests keine `OsInterface`-Kategorie tragen.
3. Die Clipboard-Tests sind inhaltlich wertvoll, aber nicht regulaer deterministisch, da sie die systemweite Windows-Zwischenablage verwenden.
4. Einige TerminalControl-Tests erstellen per `PseudoConsole.Create(1, 1)` echte ConPTY-Ressourcen, obwohl sie teilweise nur Control-Bindung und Stream-Verhalten pruefen.
5. `KiAusfuehrungsService` ist bereits fuer einen testbaren Launcher vorbereitet. Das reduziert den Umbauaufwand fuer ViewModel-Tests und OS-freie Tests.
6. CI und lokale Hilfsskripte muessen auf die neue Kategorie-Struktur abgestimmt werden, sonst bleibt die Testauswertung fachlich uneinheitlich.

## Risiken fuer die Planung

- xUnit-Trait-Attribute auf Methoden- und Klassenebene sind vorhanden; ein eigenes Attribut muss sicherstellen, dass `Category=OsInterface` fuer `dotnet test --filter` sichtbar ist.
- Eine reine Umbenennung von `ConPTY`/`E2E` nach `OsInterface` kann bestehende lokale Aufrufe brechen. Kompatibilitaet oder Dokumentation ist einzuplanen.
- Clipboard-Tests brauchen entweder Markierung als OS-Schnittstelle oder eine entkoppelte Clipboard-Abstraktion im Produktivcode.
- Fuer `/run-tests` und `/lifecycle` sind im Repository keine direkt versionierten Kommandoimplementierungen sichtbar; vermutlich liegen sie ausserhalb dieses Codebases als Codex-/Skill-Logik. Die Umsetzung muss dort erfolgen oder in den erzeugten Testartefakten sauber abgebildet werden.
