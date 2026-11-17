# Datenmodell und Einstellungsschlüssel

## `ScmIncomingEvent`
Datei: `src/Softwareschmiede/Domain/Entities/ScmIncomingEvent.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Primärschlüssel |
| `AufgabeId` | `Guid` | Pflicht-Verknüpfung zur Aufgabe |
| `Status` | `string` | Status-Strings (kein Enum): `Aufgenommen`, `Wartend`, `Verzögert`, `ZustellungVorbereitet`, `Zugestellt`, `InBearbeitung`, `Verarbeitet`, `ZustellungUngewiss`, `VerarbeitungUngeklärt`, `Abgebrochen`; Default `Aufgenommen` (Z. 22) |
| `Reason` | `string?` | Anzuzeigender Warte-/Fehlergrund (Z. 25) |
| `NextAttemptUtc` | `long?` | Unix-ms-Sperre; `> now` blockiert den Poll-Dispatch (`ScmEventOrchestrator` Z. 126); `long.MaxValue` = heutige harte Sperre |
| `ErrorCode` | `string?` | Fehlercode (z. B. `ScmProviderFailureKind`) |
| `Attempts` | `int` | Versuchszähler (Backoff) |
| `DeliveryAttemptId`, `CliRunId`, `ExchangePath`, `AcceptedUtc`, `DeliveredUtc`, `ReadyConfirmedRunId` | divers | Zustellungs-/Versuchszustand — für diese Anforderung unverändert |
| `EventKey`, `ObservationJson`, `ReceivedUtc`, `Sequence`, `LeaseOwner`, `LeaseUntilUtc`, `ExchangeRestrictedWriteSidsJson` | divers | Eingang/Dedup/Lease — unverändert |

Keine Entity-Änderung nötig: „wartend mit Grund" ist über `Status`/`Reason` abbildbar; die Wiederaufnehmbarkeit ergibt sich daraus, `NextAttemptUtc` ungesetzt zu lassen.

## `Aufgabe` (relevante Felder)
Datei: `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `KiPluginPrefix` | `string?` | Gespeicherte Aufgaben-Plugin-Zuordnung (~Z. 62); wird vom `EntwicklungsprozessService` nach erfolgreichem Start auf den tatsächlich verwendeten Prefix aktualisiert |
| `ProjektId` | `Guid` | Scope für `plugins.default.project.<id>.<type>` |
| `LokalerKlonPfad` | `string?` | Entscheidet `ProzessStartenUndCliStartenAsync` vs. `CliNeustartenAsync` (Orchestrator Z. 170-174) |

## Einstellungsschlüssel (`AppEinstellung`)

| Schlüssel | Verwalter | Bedeutung |
|-----------|-----------|-----------|
| `plugins.enabled.<prefix>` | `PluginActivationService` | Aktivierung; fehlend/ungültig = aktiviert |
| `plugins.default.<PluginType>` | `PluginDefaultSettingsService` | Globaler Standard-Plugin-Prefix |
| `plugins.default.project.<projektId>.<PluginType>` | `PluginDefaultSettingsService` | Projekt-Standard-Plugin-Prefix |
| `ki.plugin.default` (`AppEinstellungService.DefaultKiPluginKey`) | `AppEinstellungService` | Einstellungen-Standard-CLI (PluginName oder -Prefix); letzte Stufe der Auflösungskette |
