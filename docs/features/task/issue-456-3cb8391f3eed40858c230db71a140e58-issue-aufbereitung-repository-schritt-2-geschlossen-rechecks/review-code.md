# Code Review: Re-Checks bei geschlossenem Issue (Schritt 2)

## Geprüfte Dateien

- `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs`
- `src/Softwareschmiede.IntegrationTests/Services/ScmEventDispatchTests_IssuePreparation.cs`
- `docs/help/plugins/github-ereignisse/business-rules.md`
- `docs/RELEASE_NOTES.md`

## Befund

- **Pre-Zustell-Check** (`DispatchPreparationAsync`, vor `exchange.PrepareAsync`):
  Zweiter `ReadIssueAsync` nach Kanalprobe — deckt das volle Fenster zwischen
  Vor-Dispatch-Check und Zustellung ab (Plugin-Auflösung, CLI-Start,
  `WaitForInputReadyAsync`, Access-Probe). `null` → `Wartend` (identischer Grund
  wie der Vor-Check); `IsClosed` → `Verarbeitet` + Protokolleintrag, keine
  Zustellung. `issue` wird wiederverwendet — `issue.IssueUrl` fließt danach
  frisch in die Exchange-Request.
- **Abgleich-Check** (`ReconcilePreparationAttemptAsync`): Signatur um
  `IScmEventSource` erweitert; einmaliger Live-Read nur bei
  `messages.Actions.Count > 0` (kein Remote-Bedarf bei `NoRemoteAction`).
  `null` → persistierter `InBearbeitung`-Status, nächster Abgleich wiederholt
  (die Session-Tot/Timeout-Prüfung am Methodenende läuft dann weiter).
  `IsClosed` → je Aktion ehrliches `success=false`-Result mit Begründung
  (gleiches Muster wie der Kommentar-Schalter-Verwurf), `Verarbeitet`,
  Protokolleintrag — die Abschlusskriterien-Auswertung wird korrekt übersprungen,
  weil der Status bereits `Verarbeitet` ist.
- **Status-Semantik** konsistent mit dem vorhandenen Vor-Dispatch-Check:
  `Verarbeitet` + Protokoll statt `Abgebrochen` (kein Benutzerabbruch) oder
  neuem Status. Kein Fehlerkommentar am Issue — unverändert eingehalten.
- **Keine Scope-Ausweitung:** Checks sitzen ausschließlich im
  Aufbereitungspfad; generische Ereignisse und die Host-Outbox
  (`ScmOutgoingActions`, nicht-`ai:`-Aktionen) bleiben unberührt.
- **TOCTOU:** Die Restlücke zwischen letztem Lesen und Remote-POST ist
  dokumentiert (`business-rules.md`) — inhärent und bewusst akzeptiert.
- Die `ReadStableAsync`-Stabilitätsregel des Antwortkanals (zwei identische
  Reads) erforderte in den neuen Tests zwei `DispatchAsync`-Runden — gleiches
  Muster wie die Bestandstests.

## Offene Punkte

- Keine.
