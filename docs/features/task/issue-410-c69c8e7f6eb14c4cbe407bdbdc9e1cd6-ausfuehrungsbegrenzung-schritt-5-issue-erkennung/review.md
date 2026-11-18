# Review: „Issue erstellt"-Erkennung und trigger-freier Aufbereitungsauftrag (Schritt 5)

**Status: Umsetzung vollständig** — Plan gegen implementiertes Ergebnis geprüft (selbst gegengeprüft,
kein Subagenten-Start möglich; dokumentierte Abweichung).

## Planabdeckung

| Planelement | Umsetzung | Nachweis |
|-------------|-----------|----------|
| §1 Persistenz: `GitRepository.IssueAufbereitungAktiv`, `Aufgabe.Zweck`, `ScmIncomingEvent.Zweck` (je `HasMaxLength`/`HasDefaultValue`) + Migration `AddIssueAufbereitung` | Umgesetzt; Migration `20261117231445_AddIssueAufbereitung` via `dotnet ef` inkl. Designer + ModelSnapshot | Build + Integration-Tests |
| §2 Einstellungsschlüssel `scm.issueaufbereitung.enabled` (opt-in `false`), `.plugin` (leer→Fallback), `.comment.enabled` (Default `true`, Annahme A1) + typisierte Getter | Umgesetzt in `AppEinstellungService` Z. 45-52, 211-241 | Unit-Tests `AppEinstellungServiceTests_IssueAufbereitung` (7 Tests) |
| §3 `ScmIssueAufbereitungsService` mit `Zweck`-Konstante, `BaselineKey` (`issueaufbereitung/…`), `AufbereitungsSourceKey` (`…/IssueAufbereitung/{n}`), `AufbereitungsEventKey` (ContentKey+Zweck), `ErkennenUndEinreihenAsync` | Umgesetzt als neue Datei; Baseline einmalig (nie fortgeschrieben), Erstbeobachtung via fehlendem `ScmContentSnapshot`, EventKey-Unique-Index dedupliziert, `Protokolleintrag` SystemMeldung, `DbUpdateException`-Toleranz | Integration-Tests 1-3, 6-7 |
| §4 Andockpunkt `RunOnceAsync`: globaler Schalter pro Zyklus, `linked`-Filter `x.Zweck == null`, Aufruf nur `!inboxMode` + Issue + `Description` + Beobachtungen > 0, vor `AcceptBatchAsync` | Umgesetzt Z. 46, 48-51, 198-207 | Alle Monitoring-Tests unverändert grün (346/346) |
| §5 Resolver-Isolation: `a.Zweck == null` in `ResolveIssueAsync` und `FindActiveContextTaskIdsAsync` | Umgesetzt Z. 65, 135 | Integration-Test 8 (Trigger → kanonische Aufgabe, kein Ambiguous) |
| §6 `CreateFromIssueEventAsync` um optionale `zweck`/`kiPluginPrefix` | Umgesetzt Z. 273-284, 312-313 | Happy-Path-Test (`KiPluginPrefix == "custom.auf"`) |
| §7 Orchestrator-Guard `item.Zweck` → `NextAttemptUtc = long.MaxValue` + `WaitAsync`, vor Plugin-Auflösung | Umgesetzt `DispatchCoreAsync` Z. 131-138 | Integration-Test 10 (Wartend, kein Prompt/CLI/Hostaktion) |
| §8 Übersichts-Label „Issue-Aufbereitung" in `ScmEventQueueItem.From` | Umgesetzt Z. 402-403 | — (manuell geprüft; Projektion unverändert kompatibel) |

## Akzeptanzszenarien (aus plan.md) → Testlage

1. Happy Path → `Monitor_NewIssueAfterBaseline_CreatesPreparationTaskAndInternalEvent` ✔
2. Baseline ohne Retro-Scan → `Monitor_FirstObservation_CreatesBaselineOnly`, `Monitor_IssueCreatedBeforeBaseline_CreatesNothing` ✔
3. Dedup → `Monitor_RepeatedPolls_CreateNoSecondPreparation` ✔
4. Negative Schalter → `Monitor_GlobalSwitchDisabled_*`, `Monitor_RepositoryFlagDisabled_*`, `Monitor_RepositoryWithoutEventCapablePlugin_*` ✔
5. Schleifenschutz → `Monitor_DescriptionChangeAfterPreparation_CreatesNoLoop` (inkl. eigener Marker-Body) ✔
6. Isolation → `Monitor_TriggerOnPreparedIssue_ResolvesToCanonicalTask` ✔
7. Geparkter interner Auftrag → `Monitor_InternalEvent_ExplicitDispatch_WaitsWithoutDelivery` ✔
8. Externe Trigger unverändert → 346/346 Integrationstests bestehen (kein Bestandstest angepasst) ✔

## Planabweichungen

- `BaselineKey`/`AufbereitungsSourceKey`/`AufbereitungsEventKey` sind `public` statt `internal`
  (Test-Assert-Zugriff nicht nötig, aber unproblematisch; CS1591-konform dokumentiert) — kosmetisch,
  bewusst vereinfacht.
- `Reason` des internen Ereignisses trägt den Kommentar-Schalter sichtbar („Die Analyse soll als
  Issue-Kommentar dokumentiert werden.") — dokumentiert die gelesene Konfiguration; fachliche
  Auswertung folgt in Schritt 6 wie geplant.
- Keine weiteren Abweichungen; Annahmen A1-A3 wie geplant umgesetzt.

## Befunde

Keine. Die Umsetzung entspricht dem Plan; sämtliche Akzeptanzkriterien sind durch grüne Tests belegt.
