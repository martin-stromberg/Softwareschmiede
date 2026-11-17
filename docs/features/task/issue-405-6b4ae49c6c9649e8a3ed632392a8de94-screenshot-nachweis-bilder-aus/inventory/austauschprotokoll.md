# Detailinventar: SCM-Event-Austauschprotokoll und `complete.json`

Bestandteil von [inventory.md](../inventory.md). Alles in
`src/Softwareschmiede/Application/Services/ScmEventExchangeProtocol.cs` (479 Zeilen).

## Protokoll-Typen (Records)

| Typ | Zeilen | Inhalt |
|---|---|---|
| `ScmExchangeRequest` | 13–19 | `EventId, AttemptId, CliRunId, AufgabeId, Body, EventKind, Repository, OriginUrl`, `ProtocolVersion = 1`. Unveränderlicher Auftrag; wird als `request.json` persistiert und bei jedem Lesen auf Bitgleichheit geprüft. |
| `ScmExchangeAttempt` | 22–37 | `Request` + `DirectoryPath`; `RestrictedWriteSids` (Z. 25), `PromptSuffix` (Z. 32 — bewusst nur im Prompt, nicht im Request), `RequestPath` (Z. 34), `Prompt` (Z. 36 via `BuildPrompt`). |
| `ScmExchangeAccepted` | 40 | Korrelations-Annahme. |
| `ScmExchangeAction` | 43–45 | `Kind` ∈ {comment, reaction, pullRequest} + `Body/Reaction/Head/Base/Title`; keine Ziel-URLs. |
| `ScmExchangeComplete` | 52–54 | `ActionIds[]`, `NoRemoteAction`, `Reason`, `Report`, `PullRequestNumber`. **Kein `attachments`-Feld.** |
| `ScmExchangeActionResult` | 57–58 | Hostbestätigung je Aktion (`results/{ActionId}.json`). |
| `ScmExchangeMessages` | 61–62 | Rückgabe von `ReadAsync`: `Accepted`, `Actions`, `Complete`, `Diagnostic`. |
| `ScmExchangeAccessProbe` | 65–74 | Dateitool-Probe (`challenge.json`, `challenge-read.json`, `proof.json`). |

## Verzeichnis- und Dateipfade

```
{WorkingDirectory}/.softwareschmiede-scm-exchange/     DirectoryName, Z. 83
    .channel-v1                                       Ownership-Marker, Z. 86/210–219
    .access-probe-{guid:N}/                           Probe-Verzeichnis, Z. 106
        challenge.json / challenge-read.json / proof.json / proof.tmp
    {cliRunId:D}/                                     Run-Verzeichnis, Z. 221
        {attemptId:D}/                                Versuch-Verzeichnis, Z. 181
            request.json                              atomar via .request.tmp + Move, Z. 192–194
            accepted.json                             Agent
            actions/{actionId:D}.json                 Agent (max. 128, Z. 239)
            results/{actionId:D}.json                 Host (atomar, Z. 279–292)
            complete.json                             Agent
```

- Ein `screenshots/`- oder sonstiges Nicht-JSON-Unterverzeichnis existiert **nicht**; `ReadAsync` liest ausschließlich `accepted.json`, `actions/*.json`, `complete.json` — fremde Dateien im Versuchsverzeichnis werden ignoriert (die Strenge gilt nur für `actions/`: dort muss jeder `*.json`-Dateiname eine Guid im Format `D` sein und mit `action.ActionId` übereinstimmen, Z. 238–249).
- `PrepareAsync` (Z. 171–198) verweigert ein existierendes Versuchsverzeichnis (Z. 183–184) und legt `actions/` + `results/` beim Erzeugen an (Z. 186–187). Ein `screenshots/`-Unterverzeichnis würde an dieser Stelle erzeugt.
- `EnsureGitExcludedAsync` (Z. 440–458): `git rev-parse --show-toplevel`, Verbot versionierter Inhalte (`ls-files`), `info/exclude`-Regel `/{relpath}/`, Verifikation via `check-ignore --no-index`.
- Rechteprofil: `CreatePrivateDirectory` (Z. 398–419) — Windows: Owner = aktueller Benutzer, vererbungsgeschützt, `FullControl` für Owner plus optional `Modify|Synchronize` je `RestrictedWriteSids`-SID; Unix: `0700`. `VerifyPrivateDirectory` (Z. 421–438) erzwingt dies bei jedem Zugriff.
- `EnsureSafePath` (Z. 391–396, `public static`): Reparse-Point-/Symlink-Verbot auf **jedem existierenden** Pfadbestandteil — maßgebliche Konvention auch für neue Datei-Einsammelpfade.
- `PreparationLock` (statisches `SemaphoreSlim`, Z. 88) serialisiert `PrepareAccessProbeAsync`/`PrepareAsync` prozessweit.

## `complete.json`-Parsing und -Validierung

- JSON-Optionen (Z. 89–93): `JsonSerializerDefaults.Web`, **`UnmappedMemberHandling.Disallow`**, `MaxDepth = 16`. Ein derzeit unbekanntes Feld (z. B. `attachments`) führt zu `JsonException` → in `ReadAsync` abgefangen → `ScmExchangeMessages.Diagnostic` (Z. 267–270); das Ereignis geht in `VerarbeitungUngeklärt` (Orchestrator Z. 393–397).
- Stabilität: `ReadStableAsync` (Z. 356–369) liefert eine Meldung erst, wenn der SHA-256 zweier aufeinanderfolgender Reads identisch ist (`_lastReads`, Z. 87). Erste Beobachtung wird nie ausgeführt; eine stabile, aber ungültige Meldung wird als Protokollfehler sichtbar.
- `ReadAsync` (Z. 229–271) führt zuerst `ValidateAttemptAsync` (Z. 343–354) aus: Pfadstruktur muss `{DirectoryName}/{cliRunId}/{attemptId}` entsprechen, `VerifyPrivateDirectory` auf dem Versuchsverzeichnis, `request.json`-Deserialize muss dem in-memory `ScmExchangeRequest` entsprechen.
- `complete`-Validierung (Z. 257–263): `ActionIds` nicht null, ≤ 128, keine leeren/doppelten Guids; `PullRequestNumber` muss > 0 sein; bei `NoRemoteAction` Pflicht: `ActionIds` leer **und** `Reason` nicht leer; sonst Pflicht: `ActionIds` nicht leer **oder** `Report` nicht leer.
- `ReadBoundedAsync` (Z. 371–380): `MaximumMessageBytes = 1 MiB` (Z. 85) pro Kanaldatei plus Lese-während-Schreiben-Erkennung (`Position != Length`). Das Limit gilt bislang für alle gelesenen Dateien; für Binärdateien existiert kein separater Pfad.
- `ValidateAction` (Z. 321–331): `comment` → `Body` non-empty; `reaction` → feste Emoji-Liste; `pullRequest` → `Title` + `SafeBranch(Head/Base)` (Z. 333–335).
- `Correlate` (Z. 337–341): `protocolVersion == 1` und IDs = Event/Attempt/CliRun des Versuchs — gilt für accepted, jede Aktion und complete.
- `WriteResultAsync` (Z. 274–293): atomar via `.tmp` + `Move(overwrite: true)`; einmal `Success = true` ist unveränderlich, `false` darf überschrieben werden (Retry).
- `BuildPrompt` (Z. 296–312): einzeiliger JSON-Envelope mit `instruction` (Z. 300, beschreibt requestPath/accepted/actions/results/complete/report/pullRequestNumber/promptSuffix), `request`, `requestPath`, `acceptedSchema`, `actionSchema` (Z. 304), `completeSchema` (Z. 305 — müsste ein neues `attachments`-Feld nennen), optional `promptSuffix` (Z. 309–310).

## Konstruktion und Wiederanlauf des Versuchs

- Erzeugung im Orchestrator: `DispatchCoreAsync` Z. 198–218 — `PrepareAsync` mit `access.RestrictedWriteSids` und `promptSuffix`; Persistenz in `ScmIncomingEvent`: `DeliveryAttemptId`, `CliRunId`, `ExchangePath`, `ExchangeRestrictedWriteSidsJson` (`ScmIncomingEvent.cs` Z. 45–55).
- Rekonstruktion beim Wiederanlauf: `ReconcileAttemptAsync` (Orchestrator Z. 383–390) baut `ScmExchangeRequest` + `ScmExchangeAttempt` aus den persistierten Feldern — der `PromptSuffix` wird dabei **nicht** rekonstruiert (nur Prompt-Bestandteil).
- `ResendAsync` (Z. 305–319) setzt `ExchangePath`, `DeliveryAttemptId`, `AcceptedUtc` u. a. zurück → neuer Versuch = neue `attemptId` = neues Verzeichnis.
