# Testergebnisse – Schritt 6: Issue-Aufbereitung ausführen

Alle Läufe auf dem Feature-Branch
`task/issue-410-c69c8e7f6eb14c4cbe407bdbdc9e1cd6-ausfuehrungsbegrenzung-schritt-6-issue-aufbereitung`,
Konfiguration Debug, mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` (Sandbox ohne
ConPTY-Isolation; betroffene Tests melden ehrlich „Skipped").

## Build

| Kommando | Ergebnis |
|---|---|
| `dotnet build Softwareschmiede.slnx` | erfolgreich, **0 Warnungen, 0 Fehler** |
| `dotnet format Softwareschmiede.slnx --verify-no-changes` | **sauber** (nach einem `dotnet format`-Lauf, der Whitespace in den neuen Testdateien normalisiert hat; `git diff -w` zeigt keine inhaltlichen Nebenänderungen) |

## Reguläre Lane (stabil)

`dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"`

**2681 erfolgreich, 0 Fehler, 3 übersprungen** (Dauer 3:14 min). Die drei
Übersprungenen sind betriebssystembedingte Skips (nicht-Windows-Pfade), keine
Feature-Beeinträchtigung.

## Integrations-Lane

`dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj`

**357 erfolgreich, 0 Fehler** (Dauer 48 s). Darin die neue Suite
`ScmEventDispatchTests_IssueAufbereitung` (21 Tests): Entparken/sofortige
Fälligkeit, Dispatch-Reihenfolge, deterministischer Prompt (alle Kriterien im
`request.body`), Plugin-Fallback (explizit → Projekt → Global → Standard,
ignorierte Prefixes benannt), No-Plugin-Wartegrund ohne CLI-Start,
#410-Limit-Wartegrund, `IssueBodyUpdate` über Outbox (Titel unverändert),
Kommentar an/aus (verworfener Kommentar → quittiert + protokolliert),
`NoRemoteAction`, Rückschreibefehler ohne Fehlerkommentar mit Protokolleintrag,
Reconcile/Wiederanlauf ohne Doppelausführung, geschlossenes Issue.

## Gezielte Unit-Tests

`ScmIssueAufbereitungPromptTests` + `SettingsViewModelTests_IssueAufbereitung`
+ Monitoring-Umbau: **12 erfolgreich, 0 Fehler** (Prompt-Inhalte/Kriterien,
Defaults, Persistenz der vier Einstellungen, Repository-Auswahl, Gating).

## OS-Interface-/E2E-Lane

`dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category=OsInterface"`

**65 erfolgreich, 1 Fehler, 5 übersprungen** (Dauer 17:12 min). Der einzige
Fehler `TerminalControlTests.ReadClipboardAndInsertAsync_Success_
WritesEncodedBytesToInputStream` ist ein umgebungssensitiver Clipboard-Test
ohne Feature-Bezug (keine Clipboard-/Terminal-Änderung in diesem Schritt) und
**beim gezielten Einzelrerun sofort grün** — Lane-Parallelismus/Flake.
Die 5 Übersprungenen sind die ConPTY-/Sandbox-typischen Skips.

### Reale E2E-Abnahme

Der konsolidierte FlaUI-Lauf `General_BasisUndEinstellungen_E2E` enthält das
neue Szenario `Einstellungen_IssueAufbereitungKonfigurieren_E2E` und lief im
Lane-Lauf sowie gezielt erfolgreich: echte App-Instanz, echtes angelegtes
Repository, Expander öffnen, Status „deaktiviert"→„aktiviert", Funktions-
Plugin wählen, Repository markieren, Kommentar-Schalter deaktivieren,
Speichern, Neu-Öffnen mit UI-Wiederstand + Persistenz-Nachweis in
`AppEinstellungen` und `GitRepository.IssueAufbereitungAktiv`; finally
stellen die Defaults wieder her.

## Abdeckungsübersicht Anforderung → Nachweis

| Anforderung | Nachweis |
|---|---|
| Ereignis sofort fällig, dedizierter Pfad vor Trigger-Validierung | Integration `Dispatch_Aufbereitung_*` (21), Dispatch-Core-Guard |
| #410-Limits gelten | Integration Limit-Wartegrund |
| #411-Plugin-Fallback | Integration Fallback-Kette, No-Plugin-Warte |
| Fester Analyse-Prompt (alle Kriterien) | `ScmIssueAufbereitungPromptTests` + Prompt-Assertion im Request |
| Remote-Body-Ersetzung via Outbox, Titel unverändert | Integration Outbox-Tests |
| Kommentar-Schalter | Integration + ViewModel-Tests + E2E |
| Kein Fehlerkommentar, Log + Protokolleintrag | Integration Fehlerpfade |
| Settings-UI (4 Schalter, Gating) | ViewModel-Tests + konsolidierter FlaUI-E2E |
