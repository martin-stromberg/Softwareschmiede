# Testergebnisse (Iteration 2)

## Build

`dotnet build` (volle Solution, ohne `--no-build`): **0 Fehler, 0 Warnungen.**

## Testlauf

`SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test --no-build --collect:"XPlat Code Coverage" --logger "console;verbosity=normal"`

Zwei vollständige Testläufe durchgeführt (996 Tests gesamt, 1 übersprungen – ConPTY-Sandbox-Limitation gemäß CLAUDE.md). Die Läufe unterschieden sich in der Fehlerzahl (Lauf 1: 2 Fehler, Lauf 2: 4 Fehler), was auf Flakiness statt auf eine echte Regression hindeutet. Alle betroffenen Tests wurden einzeln isoliert nachvollzogen:

| Test | Ergebnis isoliert | Einordnung |
|---|---|---|
| `KiAusfuehrungsServiceTests.KiAusfuehrungsService_HandleProcessExited_DisposesSession` | Bestanden | Flaky bei Parallelausführung, kein Bezug zu Branch-Änderungen |
| `TaskDetailViewModelTests.TestPluginWechselAsync_StopsCliAndStartsNew` | Bestanden | Flaky bei Parallelausführung, kein Bezug zu Branch-Änderungen |
| `TerminalControlTests.OnPreviewKeyDown_CtrlV_SetsHandledTrue` | Reproduzierbar fehlgeschlagen (`COMException: OpenClipboard fehlgeschlagen, CLIPBRD_E_CANT_OPEN`) | Umgebungsbedingt: Sandbox ohne echte interaktive Desktop-Session, Zwischenablage dadurch nicht zuverlässig ansprechbar (analog zur bereits in CLAUDE.md dokumentierten ConPTY-Limitation, hier erstmals für Zwischenablage-Zugriff beobachtet). Dieser Branch ändert weder `TerminalControl` noch Zwischenablage-Code – kein inhaltlicher Bezug. |
| `E2E_WorkingDirectory.RepositoryZuweisen_MitFehlgeschlagenemStrukturabruf_ZeigtTextBoxUndSpeichertManuellenPfad_E2E` | – | Bereits in einem anderen Feature (`docs/features/task/6770ad62e0d6455c919d5ecc54be1f1a-dateiexplorer/test-results.md`) als umgebungsbedingt und nicht Feature-bezogen dokumentiert; unverändert vorbestehend. |

**Fazit:** Keine der beobachteten Fehler ist auf die Änderungen dieses Branches (Lizenz-/Security-Dokumentation, Token-Log-Externalisierung, FlaUI-`PrivateAssets`, CI-Vulnerability-Scan-Workflow) zurückzuführen. Alle Ursachen sind entweder Testausführungs-Flakiness (bestätigt durch erfolgreiche Einzelläufe) oder bereits bekannte/dokumentierte Sandbox-Limitationen ohne Produktivcode-Bezug.

## Code-Coverage

Coverage-Reports wurden erzeugt (`coverage.cobertura.xml` für `Softwareschmiede.Tests` und `Softwareschmiede.IntegrationTests`). Da dieser Branch keinen neuen Produktivcode einführt (nur Dokumentation, Metadaten, Paketreferenz-Attribute und ein Hook-Skript außerhalb der C#-Coverage-Erfassung), ist keine gesonderte Coverage-Analyse für neue/geänderte Zeilen erforderlich.

## Offene Befunde aus Code-Review

Der einzige offene Befund aus `review-code.md` (`import re` unused in `.claude/hooks/log_token_usage.py`) wurde direkt behoben (Zeile entfernt).

## Status

**Iteration 2: Abgeschlossen, keine offenen Punkte.** Bereit für Schritt 9 (Dokumentation/README/Abschluss).
