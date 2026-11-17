# Testergebnisse

Branch: `task/issue-410-c69c8e7f6eb14c4cbe407bdbdc9e1cd6-ausfuehrungsbegrenzung-schritt-3-startpruefung`
Umgebung: Windows, .NET 10, `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` auf jedem
`dotnet test`-Aufruf gesetzt; alle Läufe synchron im Vordergrund, jeweils nach
vollständigem Build (kein `--no-build`).

## Builds

- `dotnet build src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj`: **0 Warnungen, 0 Fehler**
- `dotnet build src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj`: **0 Warnungen, 0 Fehler**
- `dotnet build src/Softwareschmiede.App/Softwareschmiede.App.csproj`: **0 Warnungen, 0 Fehler**
- `dotnet format Softwareschmiede.slnx --verify-no-changes`: **sauber** (Exit 0)

## Reguläre Lane

`dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"`

- **2646 erfolgreich, 2 Fehler, 3 übersprungen, 2651 gesamt** (3 m 34 s)
- Fehlschläge: `TerminalControlTests.Selection_NewOutputOutsideSelection_KeepsSelectionAndCopyText`
  und `TerminalControlTests.OnPreviewKeyDown_MausankerDannShiftRechts_ZeichnetAuswahlUndKopiertText` —
  beide `COMException 0x800401D0 (CLIPBRD_E_CANT_OPEN)` beim echten
  Windows-Zwischenablagen-Zugriff. Ursache: Clipboard-Konflikt in der
  Sandbox-Session (ein anderer Prozess hält die Zwischenablage), kein Bezug zum
  Branch-Diff — die Tests berühren ausschließlich die WPF-Terminal-Selektion.
  Gezielter Nachlauf: 1 bestanden, 1 weiterhin `CLIPBRD_E_CANT_OPEN` →
  Umgebungsproblem, **vorab als bekannte, nicht-regressive Einschränkung
  dokumentiert** (kein Codebezug zu den geänderten SCM-Services möglich).

## Integrationstests

`dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj`

- **331 erfolgreich, 0 Fehler, 331 gesamt** (49 s)
- Davon 7 neue Fälle in `Services/ScmEventDispatchTests_Startpruefung.cs`:
  - `StartPruefung_CliBeendet_WartendesEreignisStartetOhnePoll` —
    `StopCliAsync` + `session.Exited` → `RunningCountChanged` 1→0 → wartendes
    Ereignis startet ohne Poll.
  - `StartPruefung_PauseAufgehoben_EreignisStartetOhnePoll` — `SetPauseAsync`-Pfad
    (Setzen → Wartegrund; Aufheben → sofortiger Start).
  - `StartPruefung_VerzoegerteAufnahme_LoestSignalAus` — `#verzögert`-Direktive
    setzt Pause über `ScmEventStore` und löst aus.
  - `StartPruefung_RateLimitPause_LoestSignalAus` — `VerarbeiteRateLimitAsync`
    pausiert und löst aus.
  - `StartPruefung_UeberlappendeAusloeser_KeinDoppelstart` — Burst-Signale +
    parallele `RunOnceAsync`/Poll bei belegtem Platz: genau eine Session,
    zweites Ereignis wartet mit Limit-Grund.
  - `StartPruefung_MonitoringDeaktiviert_KeinDispatch` — `scm.monitoring.enabled=false`
    blockt auch die ereignisgesteuerte Prüfung.
  - `StartPruefung_NichtScmLauf_ZaehlungKorrektUndLoestAus` — Nicht-SCM-Lauf
    belegt den Platz korrekt (Ereignis wartet), sein Ende löst die Prüfung aus
    und das Ereignis startet.

## Unit-Tests (neu)

`Services/ScmStartPruefungsServiceTests` — **4/4 bestanden** (62 ms):
Auslöser-Koaleszierung, nur sinkende Laufzahl löst aus, Rückgang auf 0,
Dispose trennt Abonnements.

## Echtes E2E (OsInterface)

`dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "FullyQualifiedName~General_BasisUndEinstellungen_E2E"`

- **1 erfolgreich, 0 Fehler** (1 m 8 s) — startet die echte WPF-App mit der
  neuen Hosted-Service-Verdrahtung (`ScmStartPruefungsService` +
  `ScmStartPruefungsAusloeser` in `App.xaml.cs`) und durchläuft u. a. das
  konsolidierte Ausführungslimits-Szenario unverändert: ein DI-/Startfehler
  des neuen Dienstes würde den App-Start brechen — kein Befund.

Ein neues dediziertes FlaUI-Szenario ist nicht anwendbar: Der Schritt ändert
keine UI — das veränderte Verhalten (sofortiges Nachfüllen freier Plätze)
wird durch die Integrationstests mit skriptbaren Sessions/`TimeProvider`
nachgewiesen.

## Fehlerhistorie während der Umsetzung

1. Erstlauf der Trigger-Tests: `Mock<ITerminalSession>` feuert kein
   automatisches `Exited` beim Prozessende (produktiv liefert das
   `PseudoConsoleSession`) → `RunningCountChanged` lief nie, Wartezeit-Timeout.
   Behoben durch explizites `session.Raise(x => x.Exited += null, …)` nach
   `StopCliAsync` — exakt das produktive Signal.
2. `ScmStartPruefungsServiceTests.Ausloeser_SignalWirdAngenommen` — Erwartung
   „max. 1 ausstehend" wurde erst nach der Koaleszierungs-Schärfung des
   Signals auf `SemaphoreSlim(0,1)` erfüllt (Designkorrektur, kein Test-Hack).
