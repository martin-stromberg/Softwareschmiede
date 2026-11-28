# Ansicht 1: Status-Übersicht `/` — `StatusPageComposer`

Analysierte Datei: `server/Softwareschmiede.StatusServer/Status/StatusPageComposer.cs`
(Datenquellen: `Status/InstanceSnapshotCache.cs` → `InstanceSnapshotView`,
`GitHub/GitHubStatusService.cs` → `GitHubRepositoryStatus`, Contracts in
`src/Softwareschmiede.StatusContracts/`).

Die Status-Seite ist die einzige Seite mit eigenem Dokument-Aufbau (nutzt nicht
`BeginPage`): sie bettet `PageShell.ThemeCss` + eigenes `Css` + `PageShell.NavCss`
ein und fügt `<meta http-equiv="refresh" content="{refreshSeconds}">` hinzu
(`Page:RefreshSeconds`, Standard 15 s, `Options/StatusServerOptions.cs`).
Footer: `softwareschmiede-status · server-gerendert · read-only` (anders als `EndPage`).

## `Render(instances, gitHub, refreshSeconds, user)`

| Parameter | Typ | Inhalt |
|-----------|-----|--------|
| `instances` | `IReadOnlyList<InstanceSnapshotView>` | App-Instanzen aus dem Relay-Snapshot-Cache |
| `gitHub` | `IReadOnlyList<GitHubRepositoryStatus>` | Konfigurierte GitHub-Repositories |
| `refreshSeconds` | `int` | Meta-Refresh-Intervall |
| `user` | `PageUserContext` | Nav-Kontext (Pflicht — Seite ist autorisiert) |

## Dokumentstruktur (Reihenfolge)

1. `<header class="top">` — `RenderHeader`
2. `nav.topnav` — `PageShell.AppendNav`
3. `<div class="meta-line">` — „Stand {HH:mm:ss} UTC · Aktualisierung alle {n} s · /status.json" (Link)
4. Je Instanz: optional `.stale-note`, dann bis zu 3 `<section>` (ggf. `class="dim"`)
5. Je GitHub-Repo: optional `.stale-note`, dann `<section>`
6. `<footer>`

### Kopfzeile (`RenderHeader`)

- `<h1>Softwareschmiede-Status</h1>`
- Gesamt-Badge `<span class="badge online|offline|stale"><span class="dot"></span>{Text}</span>`:
  - `instances.Count == 0` → `offline` „keine Instanz"
  - mind. eine verbunden, keine stale → `online` „online"
  - mind. eine verbunden + mind. eine stale → `stale` „teilweise veraltet"
  - keine verbunden → `offline` „offline"
- Je Instanz `<span class="inst on-dark">Instanz „{Name|"(ohne Namen)"} · {ShortId(InstanceId)}</span>`
  (`ShortId`: erste 8 Zeichen + `…`)

### Meta-Zeile

`Stand {FmtTime(now)} UTC · Aktualisierung alle {refreshSeconds} s · ` + Link auf
`{BasePath}/status.json`. `FmtTime` zeigt nur `HH:mm:ss`, bei anderem Datum oder
>24 h Abstand zusätzlich `dd.MM.`.

### Leer-Zustand (`instances.Count == 0`)

- `.stale-note` mit Anleitung: „Es hat sich noch keine App-Instanz am Relay
  registriert. Sobald in den App-Einstellungen die Remote-Übersicht aktiviert ist
  (Einstellungen → Automatisierung → Remote-Übersicht), erscheinen hier die
  App-Sektionen."
- `<section><h2>App-Instanzen</h2><div class="empty">Keine App-Instanz hat bisher
  Daten geliefert.</div></section>`

### Pro Instanz (`RenderInstance`)

Warnhinweis vor den Sektionen (`.stale-note`):
- `!Connected` → „Instanz „{Name}" offline seit {Zeit} UTC – die App-Sektionen sind veraltet."
- `Connected && IsStale` → „Instanz „{Name}" liefert seit {Zeit} UTC keinen Snapshot – Daten ggf. veraltet."

Bei `IsStale` tragen alle drei Sektionen `class="dim"` (Tabellen-Text wird `--muted`).
Bei `Snapshot == null`: einzelne Sektion „Instanz {Name} `<span class="src app">App</span>`"
mit `.empty` „Noch kein Snapshot empfangen." — sonst drei Sektionen:

| Sektion | Zähler | Inhalt |
|---------|--------|--------|
| `Laufende Aufgaben (n)` | `running.Count` | WorkItems, die nicht „wartend" sind; `.empty` wenn leer |
| `Wartet auf Anwender (n)` | `waiting.Count + waitingEvents.Count` | wartende WorkItems + SCM-Events mit Status `Wartend`/`Verzögert`; `.empty` wenn leer |
| `Letzte SCM-Ereignisse (n)` | `events.Count` | alle `ScmEvents`; `.empty` wenn leer |

„Wartend" heißt (`IsWaiting`): `RunStatus == "WaitingForInput"` **oder**
`PausedUntilUtc > now`.

Jeder `h2` trägt: Titel, `<span class="count">(n)</span>`,
`<span class="src app">App · {LastSeen} UTC</span>`,
`<span class="inst">Instanz {Name}</span>`.

#### WorkItem-Zeile (`RenderWorkItemRow`) — `<table>` ohne `<th>`-Kopfzeile

| Spalte | Klasse | Inhalt |
|--------|--------|--------|
| 1 | `td.num` | `#{IssueNumber}` als Link auf `IssueUrl` (bzw. `–` / unverlinkt) |
| 2 | — | `Title` + optional `<span class="inst">{RepositoryName}</span>` |
| 3 | — | Run-Pill (siehe unten) |
| 4 | `td.muted` | Details: `Kind` · „pausiert bis {t} UTC" **oder** „Heartbeat {t} UTC" |
| 5 | `td.right` | `PR #{n} ↗`-Links je `PullRequests`-Eintrag + `Issue`-Link |

Run-Pill-Logik (`RunPill`):
- `PausedUntilUtc > now` → `pill pause` „pausiert"
- `RunStatus == "WaitingForInput"` → `pill wait` „Wartet auf Eingabe"
- `HasActiveRun || RunStatus == "Running"` → `pill run` „läuft"
- sonst → `pill wait` mit `RunStatus ?? Status` als Text

#### SCM-Event-Zeile (`RenderScmEventRow`)

`td.muted` Zeit (`FmtTime`) | Referenz `ReferenceDisplay` verlinkt auf `Url` |
Status-Pill | `td.muted` `Reason`. Pill-Mapping (`ScmStatusPill`):
`Verarbeitet`/`Aufgenommen` → `done`, `Abgelehnt`/`Fehlgeschlagen` → `err`,
`Wartend`/`Verzögert` → `wait`, sonst `pause`.

### Pro GitHub-Repo (`RenderGitHubRepository`)

- Bei `Error`: `.stale-note` „GitHub-Abruf für {DisplayName} fehlgeschlagen
  ({ErrorUtc} UTC): {Error}" — die Sektion wird trotzdem mit Alt-/Leerdaten gerendert.
- `<section><h2>`: Repo-Name als externer Link (`IsSafeHttpUrl`-Whitelist http/https),
  `<span class="src gh">GitHub · {FetchedUtc} UTC</span>` (ohne FetchedUtc nur „GitHub"),
  optional `<span class="count">· {n} offene Issues</span>`.
- `<h3 class="count">Offene Pull Requests (n)</h3>` + Tabelle `#{Number}` + `Title`
  (beide verlinkt) oder `.empty` „Keine offenen Pull Requests."
- `<h3 class="count gap-top">Letzte Releases (n)</h3>` + Tabelle `TagName`-Link |
  `Name` | `td.muted` Datum `dd.MM.yyyy` — oder `.empty` „Keine Releases."

Externe Links tragen das Suffix ` ↗` (Text) und werden nur für http/https-URLs
ausgegeben (`IsSafeHttpUrl`, `AppendLinked`) — `javascript:`/`data:`/etc. werden als
reiner Text gerendert.

## Eigenes CSS (`StatusPageComposer.Css`)

Zusätzlich zu den `PageShell`-Tokens:

| Selektor/Klasse | Zweck |
|-----------------|-------|
| `.badge` + `.online`/`.offline`/`.stale` + `.dot` | Gesamtstatus-Pill im Header (9px-Punkt) |
| `.meta-line` | `margin: 10px 4px 22px`, `--muted`, 12.5px |
| `section h2` | Flex-Zeile (Titel + count + src + inst umbrechen gemeinsam) |
| `.src` + `.app`/`.gh` | Quellen-Badges (11px, `--src-fg` auf `--src-app`/`--src-gh`) |
| `.count` (+ `.gap-top`) | Zähler in Sektions-/Unterüberschriften |
| `td.num` | 70px, Monospace, `--accent`; mit `td.right` `nowrap` |
| `td.right`/`th.right` | rechtsbündig (PR/Issue-Links) |
| `td.muted` | Detailspalte, 12.5px |
| `.pill.run` | Pill „läuft" (`--pill-run-*`) |
| `td a` | `overflow-wrap: normal` — Links brechen nur an Wortgrenzen |
| `.inst` / `.inst.on-dark` | Instanz-Tags (muted / helles Grau auf dunklem Header) |
| `.stale-note` | Warnbox (warn-Tokens) — für Offline/Stale/GitHub-Fehler/Leer-Hinweis |
| `.dim td` | abgedimmte Tabelle bei veralteter Instanz |
| `@media (max-width: 600px)` | Mobil: kleinere Paddings, `td.num` Breite auto |

## Datenmodelle der Ansicht (Referenz)

- `InstanceSnapshotView` (Record, `InstanceSnapshotCache.cs:164`): `InstanceId`,
  `InstanceName?`, `Snapshot?` (`StatusSnapshot`), `Connected`, `RegisteredUtc?`,
  `LastSeenUtc?`, `IsStale`.
- `StatusSnapshot` (`src/Softwareschmiede.StatusContracts/StatusSnapshot.cs`):
  `InstanceId`, `InstanceName?`, `CreatedUtc`, `ProtocolVersion`, `AppVersion?`,
  `RunningAutomationsCount`, `WorkItems`, `ScmEvents`. **Nicht alle Felder werden
  gerendert** — `AppVersion`, `RunningAutomationsCount`, `CreatedUtc`,
  `ProtocolVersion`, `RegisteredUtc` erscheinen aktuell nicht auf der Seite.
- `StatusSnapshotWorkItem`: `Title`, `Status`, `RunStatus?` (`Running`/`WaitingForInput`),
  `Kind?`, `Purpose?`, `PausedUntilUtc?`, `LastHeartbeatUtc?`, `HasActiveRun`,
  `RepositoryName?`, `IssueNumber?`, `IssueUrl?`, `PullRequests` (`Number`, `Title`,
  `Url`, `Status?`, `RepositoryId?`). **`Purpose` und PR-`Title`/`Status` werden
  aktuell nicht angezeigt** — PRs nur als „PR #n".
- `StatusSnapshotScmEvent`: `Status`, `Reason?`, `ReceivedUtc`, `ReferenceDisplay?`, `Url?`.
- `GitHubRepositoryStatus` (`GitHubStatusService.cs:32`): `DisplayName`, `Url`,
  `OpenPullRequests` (`GitHubPullRequestView`: `Number`, `Title`, `Url`),
  `Releases` (`GitHubReleaseView`: `Name`, `TagName`, `Url`, `PublishedUtc?`),
  `OpenIssuesCount?`, `FetchedUtc?`, `Error?`, `ErrorUtc?`.
