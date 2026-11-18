# Testergebnisse: „Issue erstellt"-Erkennung und trigger-freier Aufbereitungsauftrag (Schritt 5)

**Status: Alle Lanes erfolgreich** (selbst ausgeführt und befundet; dokumentierte Abweichung vom
Subagenten-Modell).

## Ausgeführte Lanes (nach vollständigem Build `dotnet build Softwareschmiede.slnx` — 0 Fehler)

| Lane | Befehl | Ergebnis |
|------|--------|----------|
| Integration (gesamt) | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj` | **346/346 bestanden**, 0 Fehler, Dauer 45 s |
| Regulär (Unit + App) | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"` | **2669/2672 bestanden**, 3 Umgebungs-Skips (SystemShutdown/Arbeitsverzeichnis — nicht Windows-bezogen, kein Bezug zur Änderung), Dauer 3 m 9 s |
| Format | `dotnet format Softwareschmiede.slnx --verify-no-changes` | Exit 0, keine Änderungen |

Die OS-Interface-Lane (`Category=OsInterface`: E2E/ConPTY/Clipboard) ist separat und wurde nicht
angezogen — die Änderung hat keinen neuen Benutzerfluss und keine OS-nahe Funktion; kein Bestandstest
dieser Lane referenziert die neuen Schlüssel/Entities.

## Neue Tests

### Integration: `ScmEventMonitoringServiceTests_IssueAufbereitung` (10 Tests, `ScmDispatchFixture`)

| Test | Szenario |
|------|----------|
| `Monitor_FirstObservation_CreatesBaselineOnly` | Erstbeobachtung legt nur `issueaufbereitung/…`-Checkpoint an; kein Vorgang, auch nicht im Folgezyklus |
| `Monitor_NewIssueAfterBaseline_CreatesPreparationTaskAndInternalEvent` | Happy Path: genau 1 Aufgabe (`Zweck`, `…/IssueAufbereitung/9`-SourceKey, `KiPluginPrefix` aus `scm.issueaufbereitung.plugin`) + 1 internes Ereignis (`Zweck`, `AufgabeId`, EventKey `…/IssueAufbereitung`, `NextAttemptUtc = long.MaxValue`, Status `Aufgenommen`, ohne Trigger im Body, mit Kommentar-Hinweis im Reason) |
| `Monitor_RepeatedPolls_CreateNoSecondPreparation` | Dedup über Snapshot/EventKey in Folgezyklen |
| `Monitor_GlobalSwitchDisabled_CreatesNothing` | Globaler Schalter aus: kein Vorgang, keine Baseline |
| `Monitor_RepositoryFlagDisabled_CreatesNothing` | Repository-Flag aus: kein Vorgang, Baseline unverbraucht |
| `Monitor_RepositoryWithoutEventCapablePlugin_CreatesNothing` | Plugin ohne `IScmEventSource`: nie enumeriert, nichts |
| `Monitor_IssueCreatedBeforeBaseline_CreatesNothing` | Altbestand (`CreatedAt` < Baseline) trotz später Entdeckung |
| `Monitor_DescriptionChangeAfterPreparation_CreatesNoLoop` | Beschreibungsänderung inkl. eigenem `<!-- softwareschmiede:`-Marker → Schleifenschutz |
| `Monitor_TriggerOnPreparedIssue_ResolvesToCanonicalTask` | Isolation: Trigger auf demselben Issue → kanonische Aufgabe (`…/Issue/9`), kein Ambiguous, Aufbereitungsaufgabe unberührt |
| `Monitor_InternalEvent_ExplicitDispatch_WaitsWithoutDelivery` | Geparkter interner Auftrag: expliziter Dispatch → `Wartend` + Reason, kein Prompt/CLI/Hostaktion |

### Unit: `AppEinstellungServiceTests_IssueAufbereitung` (7 Tests)

Defaults und Roundtrips der drei Schlüssel (`enabled` opt-in `false`, `plugin` leer→`null`/getrimmt,
`comment.enabled` Default `true`).

## Reale End-to-End-Abnahme

**Nicht durchführbar** (benutzerflussbezogen): Der Schritt hat bewusst keine Bedienoberfläche —
Funktionsschalter, Repository-Flag und Plugin-Schlüssel existieren nur als persistierte
Einstellungsschlüssel (UI folgt in Schritt 6), und ein echtes SCM-Backend ist in dieser Umgebung
nicht angebunden. Die Erkennung ist daher über die Oberfläche nicht einschaltbar und nicht
beobachtbar. Der Nachweis liegt auf der realen Persistenz-/Serviceebene der
`ScmDispatchFixture`-Integrationstests (echte SQLite-DB, echte Services, gemockte SCM-Quelle):
Beobachtung → Baseline → Erkennung → Aufgaben-/Ereignisanlage → Dedup → Isolation → Parkzustand
sind Ende-zu-Ende innerhalb des Prozesses belegt. Manuelle Abnahme durch den Anwender ist für den
UI-freien Zwischenschritt nicht sinnvoll möglich; dies wird wie in Schritt 4 dokumentiert
festgehalten und ist kein stillschweigend als erfolgreich markiertes UI-Szenario.
