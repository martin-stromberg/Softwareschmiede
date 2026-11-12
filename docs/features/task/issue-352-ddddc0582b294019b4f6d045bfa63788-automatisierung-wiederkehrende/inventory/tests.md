# Tests und Test-Ausgangszustand

## Test-Ausgangszustand vor der Umsetzung

- **Zeitpunkt (mit Zeitzone):** 2026-11-12, ca. 12:54–12:59 Uhr (UTC+01:00)
- **Branch und Commit-ID:** `task/issue-352-ddddc0582b294019b4f6d045bfa63788-automatisierung-wiederkehrende` @ `2378cdfeea9717fb8cba28deb50386102c6fcba8` („Mehrinstanzfähigkeit: SCM-Kommentare mit eigener Kontoreaktion ignorieren (#353)", 2026-11-12 09:55:29 +0100)
- **Uncommittete Änderungen im getesteten Stand:** ausschließlich das untracked Feature-Verzeichnis `docs/features/task/issue-352-ddddc0582b294019b4f6d045bfa63788-automatisierung-wiederkehrende/` (Dokumentationsartefakte, kein Produktiv-/Testcode). Getesteter Stand = HEAD + dieses Verzeichnis.
- **Testumgebung und Runtime-/SDK-Versionen:** Windows-Entwickler-Sandbox (Git Bash), .NET SDK `10.0.403`, Laufzeit `.NET 10.0.15`, xUnit.net VSTest Adapter `v3.1.5`; `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` gesetzt (Sandbox-Auflage aus `CLAUDE.md` — wirkt nur auf ConPTY-Tests der `OsInterface`-Kategorie, die in den ausgeführten Lanes ohnehin nicht enthalten sind). `gh version 2.92.0`.
- **Ermittelte Testsuiten und Quellen der Testbefehle:** Zwei Testprojekte — `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj` und `src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj`. Lanes aus `.github/workflows/pr-staging-ci.yml`/`staging-ci.yml` (Zeilen ~120–132 bzw. ~115–124): blockierend `Category!=OsInterface`, best-effort `Category=OsInterface`. Stabile Lane laut `CLAUDE.md`: `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"`. Vorab-Voll-Build gemäß `CLAUDE.md`/`CONTRIBUTING.md` („immer zuerst `dotnet build`").

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Build | `dotnet build Softwareschmiede.slnx -c Debug` | Repo-Root | 0 | 0 Warnungen, 0 Fehler (~15 s) | — | — | Konsolenausgabe (Kopf dieser Datei) |
| Regular Unit | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface" --logger "trx;LogFileName=inventory-baseline-regular-tests.trx"` | Repo-Root | 0 | 2288 | 0 | 1 | [TRX](test-results/inventory-baseline-regular-tests.trx), [Log](test-results/baseline-regular-tests.log) |
| Regular Integration | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj --filter "Category!=OsInterface" --logger "trx;LogFileName=inventory-baseline-regular-integration.trx"` | Repo-Root | 0 | 280 | 0 | 0 | [TRX](test-results/inventory-baseline-regular-integration.trx), [Log](test-results/baseline-regular-integration.log) |

Hinweis zum Regular-Unit-Lauf: Das Ausführungswerkzeug legte den `dotnet test`-Prozess nach ~27 s ohne Output automatisch in den Hintergrund (kein `run_in_background`-Parameter); der Lauf lief unverändert zu Ende (3,12 min) und das vollständige Konsolenlog liegt vor. `Category=OsInterface` enthält die umgebungssensitiven FlaUI-E2E-/ConPTY-Tests — siehe Testlücken.

### Nachgewiesene bestehende Testfehler

Keine — beide ausgeführten Lanes endeten mit 0 Fehlschlägen.

### Testlücken und Ausführungsprobleme

- **`Category=OsInterface`-Lanes nicht ausgeführt** (beide Projekte, Umfang: E2E/FlaUI, ConPTY, Clipboard, Prozessstart etc.): Diese Tests benötigen eine interaktive Windows-Desktop-Sitzung und sind in dieser Agent-Sandbox instabil bzw. explizit nur über `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS` skip-bar (`CLAUDE.md`). Die CI führt sie best-effort mit `continue-on-error: true` aus (`pr-staging-ci.yml`/`staging-ci.yml`, Zeilen ~213–226). Aussage über deren Zustand: **unbekannt/nicht ermittelt**.
- **Übersprungener Test im Regular-Unit-Lauf:** `Softwareschmiede.Tests.Application.Services.ArbeitsverzeichnisOeffnenServiceTests.Oeffne_AufNichtWindows_WirftPlatformNotSupportedException` — dokumentierter, plattformbedingter Skip (`Skip.If(OperatingSystem.IsWindows(), …)`, `src/Softwareschmiede.Tests/Application/Services/ArbeitsverzeichnisOeffnenServiceTests.cs:34-36`). Kein Defekt.
- **Workflow-Verifikation ohne lokale Testinfrastruktur:** Es existiert kein YAML-Lint-/Workflow-Test-Tooling im Repo (`package.json` enthält nur Semantic-Release-Skripte). Für Workflow-Änderungen gibt es folglich keine automatisierte lokale Verifikation — üblich ist `workflow_dispatch`-Probelauf + Actions-Log (in `security-scan.yml` vorgelebte Konvention).
- Keine Build-, Setup- oder Infrastrukturfehler während der Läufe.

## Testklassen (anforderungsrelevant)

### `ScmEventValidationServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/ScmEventValidationServiceTests.cs` (202 Zeilen)

- `SeparatesKonto_MentionReplacesKeywordTrigger` — `@ViewerLogin` als frei stehender Trigger im SeparatesKonto (Theory, Z. 105)
- `SeparatesKonto_AuthorOrEditorMustBeOwner_NotViewer` — **genau die für das Feature kritische Regel**: Autor bzw. Bearbeiter muss Owner sein (Theory, Z. 114)
- `SeparatesKonto_ViewerOwnerRuleDoesNotApply` — Viewer≠Owner ist erlaubt (Z. 127)
- `SeparatesKonto_OwnResponseMarkerStillRejected` — `<!-- softwareschmiede:`-Marker (Z. 132)
- `SeparatesKonto_ViewerReaction_Rejects` — Kommentar mit Kontoreaktion (Z. 158)
- `ViewerReaction_DescriptionNotRejected` — Beschreibungen von der Reaktions-Dedup ausgenommen (Z. 177)
- `ActualAuthorOrEditorMustBeOwner`, `TriggerMustBeFreeStanding`, `ConfiguredTriggerKeyword_*`, `LoginCannotOverrideDifferentIds` u. a. — Benutzerkonto-/Grundregeln

### `ScmEventMonitoringServiceTests_Notifications` (Integration)
Datei: `src/Softwareschmiede.IntegrationTests/Services/ScmEventMonitoringServiceTests_Notifications.cs` (296 Zeilen)

- `Monitor_SeparatesKonto_MentionNotification_EntersInboxViaResolver` — Mention-Notification → Inbox-Eintrag über Task-Resolver
- `Monitor_SeparatesKonto_ForeignNotification_DiscardedWithDiagnostic` — `Inbox:`-Zählerdiagnose
- `Monitor_SeparatesKonto_NonMentionReasons_AreIgnored` — nur `reason = mention`
- `Monitor_SeparatesKonto_InboxFailure_KeepsWatermarkForRetry` / `Monitor_SeparatesKonto_InboxCommit_WithPreexistingCheckpoint_UpdatesWatermark` — Watermark-/Checkpoint-Verhalten
- `Monitor_SeparatesKonto_NotificationFromForeignHost_DiscardedDespiteMatchingNodeId` — Host-Bindung der node_id
- `Monitor_ModeSwitch_BackToEnumeration` — Moduswechsel

### `GitHubEventSourceTests`
Datei: `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubEventSourceTests.cs`

- `Description_VersionsDistinguishTextAndEdits_NotMetadata`, `MissingEditor_RemainsMissing_*`, `Watermark_ExcludesNewerVersion` — Editor-/Versionsbehandlung
- `ViewerReactions_MapFromReactionGroups`, `MissingReactionGroups_ThrowsPermanent`, `ReactionGroups_RequestedInQuery` — Reaktions-Mapping
- `Comments_ReadAllPages_AndBodyEditorFromSameVersion`, `OwnMarkedComment_IsExcluded_*`, `EventKey_SeparatesEveryOriginDimension` — Lesepfad

## Hilfsmethoden

### `ScmDispatchFixture`
Datei: `src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs`

- Baut `ScmEventObservation`-Testinstanzen inkl. `ScmEventRepository` (u. a. Zeile ~184: Konstruktor-Aufruf mit allen Identitätsfeldern) — Referenz für Owner-/Viewer-/Autor-Fixtures.

### Weitere relevante Hilfs-/Fixture-Dateien

- `src/Softwareschmiede.Tests/Application/Services/AppEinstellungServiceTests_KontoModus.cs` — Kontomodus-Persistenz (`scm.monitoring.accountmode`)
- `src/Softwareschmiede.IntegrationTests/Services/ScmEventMonitoringServiceTests_RepositoryResolution.cs` — Alias→`ScmEventRepository`-Auflösung (Allowlist-Aufbau für die Inbox-Zuordnung)
- `src/Softwareschmiede.IntegrationTests/Services/ScmEventDispatchTests*.cs` — End-to-end Dispatch des qualifizierten Ereignisses an die CLI
- `scripts/Run-AllTestsIndividually.ps1` — vorhandenes Skript für Einzeltestläufe (nicht Teil der CI-Lanes)
