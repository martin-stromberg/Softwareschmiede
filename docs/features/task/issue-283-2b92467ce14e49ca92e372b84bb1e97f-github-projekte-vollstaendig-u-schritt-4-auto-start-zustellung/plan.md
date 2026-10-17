# Umsetzungsplan: Durchgängiger automatischer Start und Zustellung automatisch angelegter Aufgaben (Schritt 4)

## Übersicht

Der geforderte Start- und Zustellungspfad existiert bereits vollständig in `ScmEventOrchestrator.DispatchCoreAsync` und wird von `ScmEventMonitoringService.RunOnceAsync` auch für automatisch angelegte/wiederverwendete Aufgaben erreicht. Dieser Schritt ist daher eine **Nachweis- und Absicherungsaufgabe**: Die Kette „Ereignis ohne Aufgabe → Anlage → Auto-Start → Zustellung" sowie „fehlende Standard-CLI → wartend → CLI-Auswahl/-Konfiguration → Zustellung" wird durch neue Integrationstests und einen erweiterten Abschnitt in der konsolidierten SCM-E2E-Methode abgesichert; die fachliche Entscheidung (Anlage erfolgt auch ohne Standard-CLI) wird dokumentiert. Produktionscode-Änderungen sind nur erforderlich, falls die Verifikation eine konkrete Lücke zeigt.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Anlagezeitpunkt bei fehlender Standard-CLI | **Aufgabe wird auch ohne Standard-CLI angelegt** (Ist-Zustand beibehalten) | Anlage erfolgt in der Aufnahmephase vor jeder CLI-Auflösung; das Ereignis bleibt mit bedienbarer Aufforderung wartend (`NextAttemptUtc = null` → automatischer Folgeversuch), die Aufgabe ist sofort sichtbar, `SourceKey`-Dedup verhindert Duplikate, kein Ereignisverlust. Die Alternative (Anlage zurückstellen) würde einen neuen Parallelzustand ohne fachlichen Gewinn erfordern. |
| Aufgabenstatus bei wartendem Ereignis ohne CLI | Bestehendes Verhalten beibehalten (`Neu → Gestartet` vor der CLI-Auflösung) | `NoDefault_ActivatesTaskBeforeWaitingForSelection` sichert dies bereits für aufgabenverknüpfte Ereignisse ab; gleiches Verhalten für angelegte Aufgaben vermeidet eine Abweichung im gemeinsamen Pfad. |
| Zustellpfad | Kein neuer Mechanismus — vorhandener `DispatchCoreAsync`-Pfad | Anforderung verlangt ausdrücklich denselben Pfad wie aufgabenverknüpfte Ereignisse. |

## Programmabläufe

### Auto-Start und Zustellung einer angelegten/wiederverwendeten Aufgabe

1. `RunOnceAsync` legt per `ScmEventTaskResolver.ResolveAsync` eine Aufgabe an (`AufgabeService.CreateFromIssueEventAsync`/`CreateFromPullRequestAsync`, `Status=Neu`) oder verwendet eine vorhandene; `AcceptBatchAsync` schreibt das Ereignis mit `AufgabeId` in die Inbox.
2. Dieselbe `RunOnceAsync`-Dispatchphase ruft `ScmEventOrchestrator.DispatchAsync` auf → `DispatchCoreAsync` unter `ScmTaskCoordinator` + DB-Lease.
3. Prüfungen: Plugin-Vertrag → Hostaktionen → `Verarbeitet`/Attempt-Reconcile → `NextAttemptUtc` → FIFO → Aufgabe gelöscht/`BlockReason` → erneute `GetEventRepositoryAsync`-Identitäts-/`CanWrite`-Prüfung → `GetRejection`-Revalidierung.
4. `task.Status == Neu → AufgabeService.SetStatusAsync(Gestartet)`.
5. CLI-Auflösung `ResolveDevelopmentAutomationPluginWithProjectScopeAsync` (Aufgabe → Projekt-Default → globaler Default). Ergebnis `null` → `WaitWithCommentAsync` „Keine Standard-CLI konfiguriert. Ereignis öffnen und CLI auswählen." (bedienbar, `NextAttemptUtc = null`).
6. `ResolveDevelopmentAutomationPluginStrictAsync` + `CheckHealthAsync`; kein `LokalerKlonPfad` → `ProzessStartenUndCliStartenAsync` (Klon, Branch, `StartenAsync`, `StartTerminalSessionAsync`), sonst `CliNeustartenAsync`.
7. Erneute Blocker-Prüfung → Bereitschaft (`WaitForInputReadyAsync`, inkl. `TrustPrompt`) → Kanalnachweis (`CheckExchangeAccessAsync`) → `PrepareAsync` → `WritePromptAsync` → `Zugestellt` → `ReconcileAttemptAsync` → `Verarbeitet`.

Beteiligte Klassen: `ScmEventMonitoringService`, `ScmEventTaskResolver`, `AufgabeService`, `ScmEventOrchestrator`, `PluginSelectionService`, `EntwicklungsprozessService`, `KiAusfuehrungsService`, `ScmPromptSessionAdapter`, `ScmEventExchangeProtocol`, `ScmTaskCoordinator`.

### Wartend → Zustellung nach CLI-Auswahl/-Konfiguration

1. Ereignis wartet mit „Keine Standard-CLI …" (`NextAttemptUtc = null`).
2a. **CLI-Auswahl:** `OpenFromQueueAsync` → Blocker-Prüfung, `Neu → Gestartet`, `select`-Callback liefert Prefix → `NextAttemptUtc = null` → `DispatchCoreAsync` mit explizitem Prefix → Zustellung.
2b. **CLI-Konfiguration + „Erneut prüfen":** Projekt-/Global-Default speichern → `RetryAsync` löscht Sperren → `DispatchCoreAsync` löst nun den Default auf → Zustellung.
2c. **Automatische Fortsetzung:** Da `NextAttemptUtc = null`, genügt bereits der nächste Poll nach gespeicherter Konfiguration.

Beteiligte Klassen: `ScmEventOrchestrator` (`OpenFromQueueAsync`, `RetryAsync`, `MutateAndDispatchAsync`), `ScmEventQueueViewModel` (`OpenAsync`, `ShowPluginSelectionDialogAsync`).

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `ScmEventMonitoringServiceTests_AutoStartDelivery` | Testklasse (IntegrationTests) | Nachweis Auto-Start + Zustellung angelegter Aufgaben, Wartend-Verhalten ohne CLI, blockierte Wiederverwendung |

Keine neuen Produktionsklassen.

## Änderungen an bestehenden Klassen

### `E2E_ScmEvents.cs` (`ScmEventsE2ETests`) — Testdatei

- **Neuer Abschnitt im E7-Anlageschritt** (Ende der konsolidierten Methode `ScmEvents_QueueReceptionDispatchAndReplies_UseRealPipeline`): Nach dem vorhandenen Anlagenachweis wird geprüft: Ereignis der neuen Aufgabe erreicht `Wartend` mit „Keine Standard-CLI" (kein globaler Default im Fixture); Zeile ist über den Aufgabentitel „SCM Referenz 20" wählbar; `ScmEventOpen` + Plugin-Auswahl → `Zugestellt` → `Verarbeitet`; Aufgabe zeigt `Gestartet`, `LokalerKlonPfad` unter `TestRoot` (echter Klon über `origin`), `BranchName` mit `task/issue-20-…`, `KiPluginPrefix = Softwareschmiede.KiSimulator`; genau ein zusätzlicher Prompt.
- **Neue Hilfsmethode** `SelectEventByTitle(Window, string title)`: wie `SelectEvent`, aber Zeilenvergleich über den Aufgabentitel statt `"SCM Aufgabe " + number` (für angelegte Aufgaben steht der Referenztitel in der Aufgaben-Spalte). `SelectEvent` wird intern darauf umgestellt (gemeinsamer Kern).
- **Neue Hilfsmethode** `WaitEventByTaskAsync(ScmE2EFixture fixture, Guid taskId, string status)`: DB-Poll auf `ScmIncomingEvents` gefiltert nach `AufgabeId` (analog `WaitEventAsync`, aber ohne `TaskIds`-Mapping); kann als Methode der Testklasse implementiert werden.

### Produktionscode

Keine Änderungen geplant. Falls die Verifikation eine Lücke zeigt, wird sie im Review festgehalten und in einer Folgeiteration behandelt.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine — alle Prüfungen existieren bereits in `DispatchCoreAsync`.

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **E2E-Laufzeit:** Der neue Abschnitt fügt der konsolidierten Methode einen echten Klon + Simulator-Start + Zustellzyklus hinzu (vergleichbar dem E2-Clone-Abschnitt). Keine neue Testmethode → kein zusätzlicher App-Start.
- **Blockierte Aufgaben:** Werden über `linked` auch bei `taskIds.Length == 1` zugeordnet; `BlockReason` verhindert den Start — Verhalten wird durch neuen Test abgesichert, keine Änderung.
- **Doppel-Dispatch:** Angelegte Aufgabe + Ereignis laufen durch `ScmTaskCoordinator` und Lease — bereits für den Bestandspfad bewährt.

## Umsetzungsreihenfolge

1. **Integrationstests `ScmEventMonitoringServiceTests_AutoStartDelivery` anlegen**
   - Voraussetzungen: `ScmDispatchFixture` (vorhanden), `PluginDefaultSettingsService` im Fixture registriert (vorhanden, scoped).
   - Beschreibung: Neue Datei `src/Softwareschmiede.IntegrationTests/Services/ScmEventMonitoringServiceTests_AutoStartDelivery.cs` mit den unter „Tests" gelisteten Methoden; Hilfsmethoden aus `ScmEventMonitoringServiceTests_TaskAssignment`-Muster übernehmen (`AddRepositoryAsync`, `ConfigureEnumeration`, `ConfigureObservations`, `ConfigureIssue`, `Monitor`).

2. **E2E-Abschnitt in konsolidierter Methode erweitern**
   - Voraussetzungen: Schritt 1 grün; `ScmE2EFixture` unverändert nutzbar (Szenario-Texte, Holds bereits freigegeben).
   - Beschreibung: E7-Block in `E2E_ScmEvents.cs` um Wartend-Nachweis, `SelectEventByTitle`, `ScmEventOpen` + `ChoosePlugin`, Zustellung bis `Verarbeitet`, Aufgaben-Assertions (Klon, Branch, Prefix, Status) erweitern.

3. **Fachliche Entscheidung dokumentieren (Testabsicherung liegt in Schritt 1)**
   - Voraussetzungen: Keine.
   - Beschreibung: Entscheidung „Anlage erfolgt auch ohne Standard-CLI" steht in diesem Plan (Designentscheidungen) und wird in Schritt 9 in `docs/help/plugins/github-ereignisse/business-rules.md` übernommen.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `Monitor_CreatedTaskWithDefaultCli_StartsAndDelivers` | `ScmEventMonitoringServiceTests_AutoStartDelivery` | Globaler CLI-Default gesetzt → qualifiziertes Issue-Ereignis → eine Aufgabe (`Gestartet`, `AusfuehrungsStatus`, `KiPluginPrefix == KiPrefix`, `LokalerKlonPfad` unter `Root`, `BranchName` `task/issue-9-…`), Ereignis `Zugestellt` mit `DeliveryAttemptId`/`CliRunId`, genau eine Sitzung + ein Prompt |
| `Monitor_CreatedTaskWithoutCli_WaitsThenConfiguresAndDelivers` | dto. | Kein CLI-Default → Aufgabe trotzdem angelegt (Entscheidung), Ereignis `Wartend` mit „Keine Standard-CLI konfiguriert. Ereignis öffnen und CLI auswählen.", `waiting`-Kommentar ausgegangen; Folgelauf ohne CLI → weiterhin genau eine Aufgabe/ein Ereignis (kein Duplikat/Verlust); dann Default speichern + `RetryAsync` → `Zugestellt` |
| `Monitor_CreatedTaskWithoutCli_QueueSelectionDelivers` | dto. | Wartend ohne CLI → `OpenFromQueueAsync` mit `select → KiPrefix` → `Zugestellt`, Aufgabe `Gestartet` mit Klonpfad, ein Prompt |
| `Monitor_ReusedBlockedTask_StaysWaitingWithoutStart` | dto. (Theory: `Beendet`, `Archiviert`, `Neu`+`PausiertBisUtc`) | Vorhandene Aufgabe mit Issue-Referenz + qualifiziertes Ereignis → Ereignis `Wartend` mit Blockgrund („beendet/archiviert" bzw. „pausiert"), Aufgabenstatus unverändert, keine Sitzung, kein Prompt |

### Betroffene bestehende Tests

Keine — keine Produktionsänderung; `Monitor_QualifiedCommentOnUnassignedIssue_CreatesTaskAndInboxEntry` bleibt unverändert gültig (assertiert bereits `Gestartet` ohne CLI).

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| „Ereignis ohne Aufgabe → Anlage → wartend ohne CLI → Öffnen/CLI-Auswahl → Auto-Start (Klon) → Zustellung → Verarbeitet" | `E2E_ScmEvents.cs` — Erweiterung des E7-Blocks in `ScmEvents_QueueReceptionDispatchAndReplies_UseRealPipeline` | E2E-Nachweis Auto-Start + Zustellung angelegter Aufgabe; Wartend-Aufforderung bedienbar; CLI-Auswahl führt zur Zustellung |

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `SelectEvent` (private Hilfsmethode) | Wird auf titelbasierte Variante `SelectEventByTitle` generalisiert; Aufrufe bleiben unverändert funktional |

## Offene Punkte

Keine.
