# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

- [x] `AufgabeLaufAktivitaet` (statische Hilfsklasse, `Softwareschmiede.Application.Services`) — angelegt unter `src/Softwareschmiede/Application/Services/AufgabeLaufAktivitaet.cs`
- [x] Methode `IstAktiv(string? aktiveRunId, DateTimeOffset? lastHeartbeatUtc, DateTimeOffset nowUtc)` (public static) — vorhanden; korrekte Bedingung `aktiveRunId != null && lastHeartbeatUtc != null && nowUtc - lastHeartbeatUtc.Value < TimeSpan.FromMinutes(AufgabeRecoveryService.HeartbeatTimeoutMinutes)` (striktes `<`)
- [x] `KiAusfuehrungsStatusConverter.Convert()` (`AppConverters.cs`) — Inline-Heartbeat-Bedingung durch `AufgabeLaufAktivitaet.IstAktiv(status.AktiveRunId, status.LastHeartbeatUtc, DateTimeOffset.UtcNow)` ersetzt; Substatus-Zweig (`WartetAufEingabe` → „⏸ Wartet" / sonst „▶ Läuft") unverändert
- [x] `CliUpdateSafetyService.CheckAsync()` — Filterprädikat auf `AufgabeLaufAktivitaet.IstAktiv(a.AktiveRunId, a.LastHeartbeatUtc, DateTimeOffset.UtcNow)` umgestellt; `using Softwareschmiede.Domain.Enums;` entfernt; Signatur/Rückgabetyp unverändert
- [x] Testklasse `AufgabeLaufAktivitaetTests` (neu) — angelegt mit allen fünf geplanten Fällen:
  - [x] `IstAktiv_ShouldReturnTrue_WhenRunIdSetAndHeartbeatFresh` — vorhanden
  - [x] `IstAktiv_ShouldReturnFalse_WhenHeartbeatOlderThanTimeout` — vorhanden
  - [x] `IstAktiv_ShouldReturnFalse_WhenHeartbeatExactlyAtTimeout` (Grenzfall striktes `<`) — vorhanden
  - [x] `IstAktiv_ShouldReturnFalse_WhenHeartbeatNull` — vorhanden
  - [x] `IstAktiv_ShouldReturnFalse_WhenRunIdNull` — vorhanden
- [x] Hilfsmethode `CreateActiveTaskAsync` in `CliUpdateSafetyServiceTests` — um optionalen Parameter `DateTimeOffset? lastHeartbeatUtc = null` erweitert (Default: frischer Heartbeat aus `AktivenLaufSetzenAsync`, Override setzt `tracked.LastHeartbeatUtc`)
- [x] `CliUpdateSafetyServiceTests.CheckAsync_ShouldTreatTaskWithFreshHeartbeatAsRisky` — vorhanden (frischer Heartbeat → riskant, `RequiresConfirmation == true`)
- [x] `CliUpdateSafetyServiceTests.CheckAsync_ShouldNotTreatTaskWithStaleHeartbeatAsRisky` — vorhanden (veralteter Heartbeat trotz `LaufStatus == Laeuft` → nicht riskant, `RequiresConfirmation == false`)
- [x] Bestehender Test `CheckAsync_ShouldTreatOnlyRunningStatusAsRisky` — entfernt (in Fresh-Heartbeat-Test überführt); repo-weit nicht mehr auffindbar
- [x] Einzige Quelle der Aktiv-Bedingung — repo-weiter Suchlauf bestätigt keine verbleibende Inline-Kopie der Heartbeat-Timeout-Bedingung außerhalb von `AufgabeLaufAktivitaet`

## Offene Aufgaben

Keine offenen Code-Elemente. (Prozess-Task 13 „Build + Tests grün verifizieren" wurde im Rahmen dieses reinen Plan-Reviews bewusst nicht ausgeführt — kein Code-Artefakt, sondern ein Verifikationsschritt.)

## Hinweise

- Keine Datenbankmigrationen, Validierungs- oder Konfigurationsänderungen im Plan — konsistent mit dem Code (keine solchen Änderungen vorhanden).
- Der Plan sieht keine E2E-Tests vor; Abdeckung erfolgt vollständig über die genannten Unit-Tests und die bestehenden `KiAusfuehrungsStatusConverterTests` als Regressionsnachweis der verhaltensneutralen Extraktion.
- Task 13 (voller Build + Testlauf) sollte vor Abschluss noch ausgeführt werden — projektregelkonform: `dotnet test` niemals im Hintergrund, `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` setzen, keine `Softwareschmiede.App.exe` beenden.
