# Test-Ergebnisse — Remote-Übersicht (Issue #478)

## Build / Format

- `dotnet build Softwareschmiede.slnx -p:TreatWarningsAsErrors=true`: 0 Warnungen, 0 Fehler.
- `dotnet format Softwareschmiede.slnx --verify-no-changes`: sauber.

## Automatisierte Tests

- `dotnet test server/Softwareschmiede.StatusServer.Tests` — 44/44 bestanden
  (inkl. WebApplicationFactory-Endpunkttests, RelayTokenValidator, PageAccessMiddleware,
  InstanceSnapshotCache inkl. EvictDisconnected-/Cross-Instance-Cleanup-Tests,
  StatusPageComposer inkl. Mobile-Wrap-Assertion).
- `dotnet test src/Softwareschmiede.Tests --filter "Category!=OsInterface"` — 2869/2869 bestanden
  (mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`; darin die neuen RemoteStatus-Tests:
  RelayService-Backoff/Re-Register/Reject-Disconnect, UrlParser, Projector, SettingsViewModel, AppSettingService).
- `dotnet test src/Softwareschmiede.IntegrationTests` — 451/451 bestanden
  (inkl. `RemoteStatusRelayIntegrationTests`: Push gegen echten TestServer-Hub).
- **Ausstehend:** E2E-Lane (`Category=OsInterface`, FlaUI, u. a. `E2E_RemoteStatusSettings`)
  konnte in dieser Umgebung nicht ausgeführt werden (kein interaktiver Desktop) —
  in CI/interaktiver Session nachzuholen.

## Lokaler Funktionsnachweis

StatusServer lokal auf `http://localhost:5055` (DevToken), Snapshot-Push per
SignalR-Client mit echter `RegisterInstance`+`PushSnapshot`-Sequenz.

- `01-nachweis-online.png` — Instanz online, alle Sektionen, GitHub-Links aktiv.
- `02-nachweis-offline.png` — nach Trennen des Pushers: `connected:false`, `isStale:true`
  (via `/status.json` verifiziert), App-Sektionen gedimmt, Offline-Banner, GitHub live.
- `03-nachweis-keine-daten.png` — Leerzustand ohne je verbundene Instanz.
- `04-nachweis-mobil.png` — 430 px: kein Überragen über den rechten Rand,
  Status-Pills und Issue-Nummern bleiben ungeteilt.
