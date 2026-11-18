# Übersetzte Anforderung: „Issue erstellt"-Erkennung und trigger-freier Aufbereitungsauftrag (Entwicklungsschritt 5, Issue #412 Teil 1)

**Quelle:** Projektplan `docs/projects/task/issue-410-c69c8e7f6eb14c4cbe407bdbdc9e1cd6-ausfuehrungsbegrenzung/project-plan.md`, „Schritt 5" + Vorgehensentscheidungen 3 (Erkennung am Beobachtungskanal) und 4 (dedizierte Aufbereitungs-Aufgabe inkl. Absatz „Isolation von der Issue-Ereignisauflösung"); GitHub-Issue #412.
**Basisbranch (Diff-Basis):** `task/issue-410-c69c8e7f6eb14c4cbe407bdbdc9e1cd6-ausfuehrungsbegrenzung`
**Vorarbeiten (bereits gemergt):** Schritt 1 (aktivierungsbewusste Plugin-Auflösung), Schritt 2 (Ausführungslimits + Priorisierung + atomare Slot-Reservierung + Settings-UI, `ScmEventExecutionLimitService`), Schritt 3 (ereignisgesteuerte Startprüfung, `ScmStartPruefungsService`/`DispatchDueEventsAsync`), Schritt 4 (`ScmResponseKind.IssueBodyUpdate` + GitHub-PATCH + Outbox-Pfad, Zweck `"issuebody"`).

## Fachliche Zusammenfassung

Für Repositories mit aktivierter Issue-Aufbereitung erkennt die Anwendung **neu angelegte Issues am bestehenden Beobachtungskanal** (`ScmEventMonitoringService`, repository-weite Enumeration): Der erstmalige Beobachtungsstand einer Quelle etabliert eine **Baseline ohne Auslösung** (kein Retro-Scan des Altbestands); danach gilt eine neu erscheinende `Description`-Beobachtung einer Issue-Referenz mit Erstellungszeitpunkt **nach Beobachtungsbeginn** als „Issue erstellt".

Pro Erkennung wird ein eigener, dem Issue über `IssueReferenz` zugeordneter **Aufbereitungsvorgang als Aufgabe** angelegt und ein **internes, trigger-freies `ScmIncomingEvent`** für diese Aufgabe eingereiht — sichtbar in der Ereignisübersicht, mit deduplizierendem `EventKey`, **ohne Keyword-Validierung** und ohne den Trigger-Pfad externer Ereignisse zu verändern. Die Aufbereitungsaufgabe ist als solche kennzeichbar (Zweck-Markierung). Von der Anwendung selbst ersetzte Beschreibungen lösen keine Folgeanalyse aus.

Erkennung und Einreihung lesen die **persistierte Konfiguration** (globaler Funktionsschalter, Repository-Flag, Funktions-Plugin-Schlüssel, Kommentar-Schalter) — die Einstellungen-Oberfläche folgt in Schritt 6, die Schlüssel/Persistenz gehören bereits jetzt dazu.

## Fachlicher Scope

- **Verarbeitungseinheit:** Eine erkannte „Issue erstellt"-Beobachtung → eine Aufbereitungs-Aufgabe + ein internes Ereignis (dedupliziert über `EventKey` und den einmaligen `IssueReferenz.SourceKey` im eigenen Namensraum).
- **Auslösende Ereignisse:** Neue `Description`-Beobachtung einer Issue-Referenz in einem freigeschalteten, aktiven Repository mit ereignisfähigem SCM-Plugin, mit `CreatedAt` nach etabliertem Beobachtungsbeginn; niemals die Erstbeobachtung (Baseline) und niemals eine spätere Beschreibungsänderung (Schleifenschutz — Auslöser ist strikt die Erstbeobachtung eines neuen Issues).
- **Erwartete Wirkung:**
  - Genau ein Aufbereitungsvorgang pro neuem Issue (Aufgabe + internes Ereignis), nachweisbar in Ereignis- und Aufgabenbestand.
  - Das interne Ereignis umgeht die Trigger-Validierung, bleibt aber im regulären Status-/Wartemodell (`AufgabeId` gesetzt); externe Trigger-Ereignisse unverändert.
  - Isolation: `ScmEventTaskResolver.ResolveIssueAsync` (Auflösung über `IssueNummer`, ~Z. 63-68) und `FindActiveContextTaskIdsAsync` (~Z. 132-144) schließen markierte Aufbereitungs-Aufgaben ausdrücklich aus; `IssueReferenz.SourceKey` liegt im eigenen Namensraum `…/IssueAufbereitung/{number}` statt `…/Issue/{number}` (`IssueSourceKey` ~Z. 172-176) — kein Unique-Index-Konflikt mit der späteren kanonischen Stamm-Aufgabe (`SoftwareschmiededDbContext` ~Z. 267), Einmaligkeit des Aufbereitungsvorgangs je Issue über denselben Index gesichert.
- **Umgang mit fehlenden Objekten:**
  - Repository ohne Freischaltung / Funktion global deaktiviert / Repository ohne ereignisfähiges SCM-Plugin / Repository inaktiv → keine Erkennung, kein Auftrag.
  - Bereits vorhandene Aufbereitungs-Aufgabe oder bereits eingereihtes internes Ereignis → kein zweiter Auftrag (Dedup über `SourceKey`-Unique-Index und `EventKey`).
- **Scope-Ausschlüsse:**
  - Keine Einstellungen-UI in diesem Schritt (folgt Schritt 6); Schlüssel, Defaults und persistierte Lesepfade werden jetzt angelegt. *(Status: bestätigt — verbindliche Vorgabe des Auftrags/Projektplans.)*
  - Kein eigentlicher Analyse-Lauf, kein Prompt, kein Body-Rückschrieb in diesem Schritt — das interne Ereignis wird eingereiht; die fachliche Verarbeitung folgt in Schritt 6. *(Status: bestätigt — Projektplan-Schritt 5 umfasst ausschließlich Erkennung + Einreihung.)*
  - Kein dedizierter Beobachtungsdienst/Poller — Erkennung dockt am bestehenden Kanal an (Entscheidung 3). *(Status: bestätigt.)*
  - Issue-Aufbereitung bleibt faktisch GitHub-only, solange nur das GitHub-Plugin `IScmEventSource` implementiert (Lücke L9). *(Status: bestätigt — Projektplan.)*

## Betroffene Klassen und Komponenten

- `ScmEventMonitoringService` (`src/Softwareschmiede/Application/Services/ScmEventMonitoringService.cs`): Erkennung im Beobachtungspfad der repository-weiten Enumeration (Baseline-Verhalten des Checkpoint-/Beobachtungsstands).
- `ScmEventStore`/Aufnahme-Pfad (`src/Softwareschmiede/Application/Services/ScmEventStore.cs`): interne, validierungsfreie Einreihung eines `ScmIncomingEvent` (eigener `EventKey`), ggf. eigener Aufnahme-Entry-Point neben `AcceptBatchAsync`.
- `ScmIncomingEvent` (`src/Softwareschmiede/Domain/Entities/ScmIncomingEvent.cs`): ggf. Zweck-Kennzeichnung des internen Aufbereitungs-Ereignisses.
- `ScmEventTaskResolver` (`src/Softwareschmiede/Application/Services/ScmEventTaskResolver.cs`): Ausschluss markierter Aufbereitungs-Aufgaben in `ResolveIssueAsync` (~Z. 63-68) und `FindActiveContextTaskIdsAsync` (~Z. 132-144).
- `AufgabeService`/`IssueReferenz` (`src/Softwareschmiede/Domain/Entities/IssueReferenz.cs`, `IssueSourceKey`): eigener SourceKey-Namensraum, Aufgabenanlage als markierter Aufbereitungsvorgang, Zweck-Markierung am `Aufgabe`-Entity.
- `GitRepository` (`src/Softwareschmiede/Domain/Entities/GitRepository.cs`) + EF-Migration: Freischalt-Flag; Zweck-Markierung der Aufgabe persistieren.
- `AppEinstellungService` (`src/Softwareschmiede/Application/Services/AppEinstellungService.cs`): neue Schlüssel — globaler Funktionsschalter, Funktions-Plugin-Schlüssel („Standard-KI-Plugin für die Issue-Aufbereitung"), Kommentar-Schalter (persistiert, ohne UI).
- Ereignisübersicht (`ScmEventQueueViewModel`): internes Ereignis sichtbar (reguläres Statusmodell; ggf. Kennzeichnung).
- Tests: `src/Softwareschmiede.Tests` (Unit) + `src/Softwareschmiede.IntegrationTests` (`ScmDispatchFixture`, Mock-SCM-Quelle) — Baseline-, Dedup-, Isolations- und Schleifenfälle.

## Implementierungsansatz (Grob)

- Erkennung im Beobachtungspfad: Beim repository-weiten Enumerieren werden Issue-`Description`-Beobachtungen ausgewertet; erstmaliger Quell-Beobachtungsstand = Baseline (kein Auslösen); danach neue Beobachtung + `CreatedAt` nach Beobachtungsbeginn → „Issue erstellt".
- Einreihung: Aufbereitungs-Aufgabe über bestehendes Anlagemuster (`CreateFromIssueEventAsync`-Muster) mit `IssueReferenz` (IssueNummer/Titel/URL, `SourceKey` im `IssueAufbereitung`-Namensraum) und Zweck-Markierung; internes `ScmIncomingEvent` mit deduplizierendem `EventKey` und gesetzter `AufgabeId`, ohne Trigger-Validierung.
- Konfiguration: `AppEinstellungService`-Schlüssel im Bestandsmuster (`scm.*`); `GitRepository`-Flag per Migration.

## Offene Fragen

Keine — Anforderung und Isolationsentscheidung sind verbindlich; Detailentscheidungen (Schlüsselnamen, Markierungs-Property, EventKey-Schema, Baseline-Mechanik) werden in `plan.md` als dokumentierte Annahmen festgelegt.
