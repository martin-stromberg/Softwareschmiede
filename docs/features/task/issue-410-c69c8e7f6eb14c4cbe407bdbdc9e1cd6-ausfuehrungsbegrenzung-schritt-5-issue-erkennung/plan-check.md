# Plan-Check: „Issue erstellt"-Erkennung und trigger-freier Aufbereitungsauftrag (Schritt 5)

**Status: Plan vollständig**

Geprüft: `requirement.md`, `inventory.md`, `plan.md` gegen Projektplan-Schritt 5 + Vorgehensentscheidungen 3/4 sowie die Akzeptanzkriterien des Auftrags. Hinweis: Dieser Check wurde vom ausführenden Agenten selbst erstellt (kein Subagenten-Start möglich — dokumentierte Abweichung); Befundlage bewusst konservativ.

## Abdeckung der Akzeptanzkriterien

| Kriterium | Planelement | Nachweis |
|-----------|-------------|----------|
| Neues Issue → genau ein Aufbereitungsvorgang (Aufgabe + internes Ereignis) | §3 (ErkennenUndEinreihenAsync), §4 (Andockpunkt vor AcceptBatch) | Integrationstest 2 |
| Baseline ohne Auslösung; Altbestand; deaktivierte Funktion; nicht freigeschaltete Repos; kein ereignisfähiges Plugin; Dedup | §3 (Baseline-Checkpoint, Schalter-Short-Circuits, EventKey-/Snapshot-Dedup) | Integrationstests 1, 3–6 |
| Internes Ereignis ohne Trigger-Validierung, reguläres Status-/Wartemodell, `AufgabeId` gesetzt; externe Trigger unverändert | §3 (direkte Entity-Anlage, kein `GetRejection`-Aufruf; `AufgabeId`, `Status`, `NextAttemptUtc`) | Integrationstest 2 + unveränderte Bestandstests |
| Selbst verursachte Beschreibungsersetzung löst nichts aus | §3 Schritt 3 (Snapshot-Erstbeobachtung) + vorhandene `<!-- softwareschmiede:`-Ablehnung | Integrationstest 7 |
| Isolation: keine Zuordnung/Ambiguous-Sperre; eigener SourceKey-Namensraum; Unique-Index intakt | §5 (Zweck-Filter in beiden Resolver-Abfragen) + §4 (`linked`-Filter) + §3 (`IssueAufbereitung`-Namensraum) | Integrationstest 8 |
| Persistenz (GitRepository-Flag, Aufbereitungs-Kennzeichnung) + EF-Migration | §1 | Migration `AddIssueAufbereitung` |
| Integrationstests über Mock-SCM-Quelle (`ScmDispatchFixture`), Baseline/Dedup/Schleife | Testbedarf | neue Testdatei |

## Geprüfte Einzelpunkte

- **Baseline-Mechanik:** Per-SourceKey-Baseline würde jede neue Issue-Quelle unterdrücken — der Plan legt die Baseline korrekt auf Repository-Ebene (dedizierter Checkpoint-Namensraum in `ScmObservationCheckpoints`); der Enumerations-Watermark ist als „Beobachtungsbeginn" ungeeignet (wird fortgeschrieben). ✔
- **Reihenfolge Erkennung vor `AcceptBatchAsync`:** notwendig (Snapshot-Erstbeobachtung) und im Plan explizit verankert; Fehler laufen in den bestehenden Checkpoint-Backoff, Wiederholung ist über SourceKey/EventKey idempotent. ✔
- **Dritter Isolationspunkt:** Zusätzlich zu den zwei planmäßigen Resolver-Abfragen filtert der Plan den `linked`-Aufbau im Monitoring (Z. 53-60) — ohne ihn würde die Aufbereitungs-Aufgabe Trigger-Ereignisse auf sich ziehen. Ergänzung zur benannten Vorgabe, deckt die Akzeptanz ab. ✔
- **Parken des internen Ereignisses:** `NextAttemptUtc = long.MaxValue` + Orchestrator-Guard verhindert die rohe Body-Zustellung, die der generische Pfad sonst ausführen würde (Dispatch-Revalidierung lässt „Kein Trigger" passieren). Konsistent zu „reguläres Status-/Wartemodell" + Schritt-6-Fortsetzung; als Annahme A2 dokumentiert. ✔
- **Kommentar-Schalter:** Schlüssel + Getter existieren, werden bei Einreihung gelesen und sichtbar (Reason); fachliche Auswertung bewusst Schritt 6. Default `true` als Annahme A1 markiert. ✔
- **Konventionen:** CS1591 (deutsche XML-Docs auf allen neuen öffentlichen Membern), `dotnet format`-Sauberkeit, Migration via `dotnet ef` (Konvention eingehalten), Scoped-DI in App + Fixture, kein UI in diesem Schritt (kein E2E-Test nötig). ✔
- **Abhängigkeit Schritt 1:** `ResolveEnabledDevelopmentAutomationPluginWithProjectScopeAsync` übernimmt später den Fallback; der Plan setzt nur `Aufgabe.KiPluginPrefix` — korrekt. ✔

## Festgestellte Lücken

Keine. Alle Akzeptanzkriterien sind einem Planelement und einem Testnachweis zugeordnet.
