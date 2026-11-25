# Plan-Review (Iteration 2): Remote-Übersicht über externe Status-Webseite (Issue #478)

Geprüft am 2026-11-26 gegen `plan.md`,
`docs/features/task-issue-478-…-tasks.md` (Statusspalte nun befüllt: Tasks 1–16,
19–46 „Erledigt", 17/18 „Entfällt") sowie die sechs offenen Punkte aus
`review.1.md`, jeweils mit Code-Ankern verifiziert. Builds, Format-Check und die
betroffenen Test-Suiten wurden ausgeführt (siehe Verifikation unten).

Status: Vollständig umgesetzt

## Offene Aufgaben

- [x] ~~Betriebs-/Nutzungsdoku fehlt~~ — `server/README.md` (143 Zeilen) deckt
  alle geforderten Inhalte ab: lokaler Start (`localhost:5055`, Dev-Token),
  `dotnet publish` inkl. self-contained linux-x64, systemd-Unit-Beispiel,
  nginx-Location mit WebSocket-Upgrade-Headern für `/relay`,
  Token-Einrichtung **und** -Rotation (ergänzen → umstellen → entfernen),
  Zugriffsschutz-Pflicht vor Livegang sowie die bekannte Einschränkung
  „authentifizierter Unternehmensproxy" (Z. 134-138). Die Angaben stimmen mit
  der Konfiguration überein (`appsettings.Development.json` enthält
  `Relay:DevToken = "dev-token"`, produktiv `null`). Hilfeseite
  `docs/help/einstellungen/remote-uebersicht.md` beschreibt Schalter, Felder,
  Token-Verhalten (nie wieder angezeigt) und lokale Prüfung; verlinkt in
  `docs/help/einstellungen/index.md:16`.
- [x] ~~Kein erneutes `RegisterInstance` nach Auto-Reconnect~~ — umgesetzt:
  `OnClientReconnected` setzt `_registrationPending` + Dirty-Flag und weckt die
  Pump-Schleife (`RemoteStatusRelayService.cs:384-394`);
  `PumpWhileConnectedAsync` holt die Registrierung vor dem nächsten Push via
  `TryReregisterAsync` nach (`:222-229`, `:254-287`). SignalR-seitig ist das
  `Reconnected`-Event im Client verdrahtet
  (`RemoteStatusRelayClient.cs:80,106`).
- [x] ~~Ablehnungs-Pfad erzeugt Dauer-Push-Fehlschläge~~ — umgesetzt: Bei
  `Rejected` trennt der Dienst die Verbindung (`DisconnectQuietlyAsync`), setzt
  `Error` mit Ablehnungsgrund und kehrt in die äußere Backoff-Schleife zurück
  (`:185-194` initiale Registrierung, `:270-275` Re-Registrierung) — keine
  unregistrierten Pushes mehr.
- [x] ~~Service-Tests ohne Backoff-/Drossel-Nachweis~~ — umgesetzt in
  `src/Softwareschmiede.Tests/Application/Services/RemoteStatusRelayServiceTests.cs`:
  `ExecuteAsync_ConnectFailure_RetriesWithExponentialBackoff` (Z. 312-348)
  nutzt `FakeTimeProvider` und prüft deterministisch 5 s → 10 s Verdopplung;
  `ExecuteAsync_ChangeSignal_PushesAfterMinPushInterval` (Z. 353-380) prüft
  Drossel (kein Sofort-Push) **und** nachgeholten Push nach `MinPushInterval`.
  Zusätzlich: `ExecuteAsync_AutoReconnect_ReregistersInstanceAndPushes` und
  `ExecuteAsync_RejectedReregister_DisconnectsAndSetsErrorState`.
- [x] ~~Mobil-Nachweis nur teilweise assertiert~~ — umgesetzt im Sinne der in
  `review.1.md` erlaubten Alternative: `white-space: nowrap` ist aus dem CSS
  vollständig entfernt (`.pill`, `StatusPageComposer.cs:336-386`); die
  `td.num`-Fixbreite (70 px) gilt nur Desktop und wird in
  `@media (max-width:600px)` auf `auto` gesetzt (`:361,384`). Test
  `Render_Css_AllowsWrappingForMobile` (`StatusPageComposerTests.cs:254-263`)
  assertiert `NotContain("white-space: nowrap"/"nowrap")` + viewport.
- [x] ~~`RegisteredUtc` im `InstanceSnapshotCache` fehlt~~ — umgesetzt:
  `InstanceRecord.RegisteredUtc` (`InstanceSnapshotCache.cs:151`), gesetzt in
  `MarkConnected` (`:56`) und `UpdateSnapshot` (`:78`, `??=`-Semantik =
  erster Registrierungszeitpunkt), exponiert über `InstanceSnapshotView`
  (`:104,161-169`) und damit in `/status.json`.

## Umgesetzte Elemente

- **Dokumentation (Tasks 45+46):** `server/README.md` vollständig und
  inhaltlich korrekt; `docs/help/einstellungen/remote-uebersicht.md` vorhanden
  und in `index.md` verlinkt.
- **Reconnect-/Reject-Logik:** `_registrationPending`-Mechanismus +
  `TryReregisterAsync` mit Disconnect-auf-Reject; deckt Plan-Programmablauf
  „Reconnect → erneutes RegisterInstance + sofortiger Snapshot-Push" ab.
- **Testabdeckung App-Seite:** 13 Methoden in `RemoteStatusRelayServiceTests`
  inkl. FakeTimeProvider-Backoff, Change-Drossel, Auto-Reconnect-Re-Register
  und Reject-Disconnect.
- **Mobile CSS + Assertion:** kein `nowrap` mehr im Stylesheet; mobile
  Übersteuerung der einzigen Fixbreite; dedizierter Test.
- **Server-Cache:** `RegisteredUtc` ergänzt; 41/41 Server-Tests grün.
- **`tasks.md`-Buchhaltung:** Statusspalte nachgezogen (Erledigt/Entfällt).

## Verifikation (selbst ausgeführt, 2026-11-26)

- `dotnet build` — `Softwareschmiede.csproj`, `Softwareschmiede.Tests`,
  `Softwareschmiede.IntegrationTests`, `StatusServer.Tests`: je 0 Warnungen,
  0 Fehler.
- `dotnet format Softwareschmiede.slnx --verify-no-changes` — sauber (Exit 0).
- `dotnet test server/Softwareschmiede.StatusServer.Tests` — **41/41 bestanden**.
- `dotnet test src/Softwareschmiede.Tests --filter "FullyQualifiedName~RemoteStatus|
  FullyQualifiedName~StatusSnapshot"` — **44/44 bestanden**
  (mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`).
- `dotnet test src/Softwareschmiede.IntegrationTests --filter
  "FullyQualifiedName~RemoteStatusRelay"` — **2/2 bestanden**.

## Hinweise (kein eigenständiger Aufgabenpunkt)

- Mobiler/Desktop-Screenshot-Beleg für `test-results.md` (Lifecycle-Schritt 10)
  steht weiterhin aus — wie in `review.1.md` bereits als Hinweis, nicht als
  offene Aufgabe klassifiziert.
- Alle Änderungen liegen uncommitted auf dem Task-Branch vor (u. a. `server/`
  und `src/Softwareschmiede.StatusContracts/` als neue, ungetrackte Pfade).
