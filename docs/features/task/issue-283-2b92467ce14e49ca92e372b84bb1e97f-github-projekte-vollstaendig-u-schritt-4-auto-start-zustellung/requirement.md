# Übersetzte Anforderung: Durchgängiger automatischer Start und Zustellung automatisch angelegter Aufgaben inklusive Wartend-Verhalten ohne Standard-CLI

## Fachliche Zusammenfassung

Die in Schritt 3 eingeführte Aufgabenanlage/-wiederverwendung (`ScmEventTaskResolver` → `AufgabeService.CreateFromIssueEventAsync` / `CreateFromPullRequestAsync`) soll denselben Start- und Zustellungspfad durchlaufen wie bisherige aufgabenverknüpfte Ereignisse: Nach den bestehenden Sicherheits- und Owner-Prüfungen pro Zustellversuch wird die Aufgabe automatisch gestartet (`AufgabeStatus.Neu` → `AufgabeStatus.Gestartet`; Klon, Branch und CLI-Start über `EntwicklungsprozessService`) und der Auftrag über den Antwortkanal an die aufgelöste Standard-CLI zugestellt (Auflösungskette Aufgabe → Projekt-Default → globaler Default). Fehlt eine Standard-CLI, bleibt das Ereignis mit der bestehenden, bedienbaren Aufforderung wartend; nach CLI-Auswahl oder -Konfiguration läuft die Zustellung an. Der Schritt umfasst außerdem die Bestandsschutz-Verifikation des Gesamtfeatures (Trigger-/Owner-/Token-Regeln, deduplizierte Zustellung, SCM-Ereignisübersicht) und die dokumentierte fachliche Entscheidung zum Anlagezeitpunkt bei fehlender Standard-CLI.

## Betroffene Klassen und Komponenten

- `ScmEventOrchestrator` (`src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs`) — `DispatchCoreAsync` enthält bereits den kompletten Pfad: erneute Repository-Identitäts-/`CanWrite`-Prüfung über `IScmEventSource.GetEventRepositoryAsync`, Trigger-/Bearbeiter-Revalidierung via `ScmEventValidationService.GetRejection`, `BlockReason` (beendet/archiviert/pausiert), `AufgabeService.SetStatusAsync(Neu → Gestartet)`, CLI-Auflösung `PluginSelectionService.ResolveDevelopmentAutomationPluginWithProjectScopeAsync`, Start über `EntwicklungsprozessService.ProzessStartenUndCliStartenAsync` / `CliNeustartenAsync`, Zustellung über `ScmEventExchangeProtocol` + `IScmPromptSessionAdapter.WritePromptAsync`. Wartend-Text „Keine Standard-CLI konfiguriert. Ereignis öffnen und CLI auswählen." bereits vorhanden.
- `ScmEventMonitoringService` — `RunOnceAsync` dispatchet neu aufgenommene Inbox-Einträge (inkl. der für automatisch angelegte Aufgaben erzeugten) noch im selben Lauf über `ScmEventOrchestrator.DispatchAsync`.
- `ScmEventTaskResolver` / `AufgabeService.CreateFromIssueEventAsync` / `CreateFromPullRequestAsync` — Anlage erfolgt bereits vor der Inbox-Aufnahme, unabhängig von einer konfigurierten CLI (Grundlage der zu dokumentierenden Entscheidung).
- `ScmEventQueueView` / `ScmEventQueueViewModel` — Aktionen „Öffnen / CLI auswählen" (`ScmEventOpen` → `OpenFromQueueAsync`) und „Erneut prüfen" (`ScmEventRetry` → `RetryAsync`) sind bereits vorhanden und greifen für die neuen Ereignisse unverändert.
- Tests: `src/Softwareschmiede.IntegrationTests/Services/` (neue Themenklasse auf Basis von `ScmDispatchFixture`), konsolidierte FlaUI-Methode `ScmEvents_QueueReceptionDispatchAndReplies_UseRealPipeline` in `src/Softwareschmiede.Tests/E2E/E2E_ScmEvents.cs` (Erweiterung des vorhandenen E7-Anlageschritts, keine neue Testmethode).

## Implementierungsansatz

Der Produktionspfad ist strukturell bereits vollständig vorhanden: `RunOnceAsync` legt die Aufgabe an bzw. verwendet sie wieder, nimmt das Ereignis mit `AufgabeId` in die Inbox auf und ruft anschließend `DispatchAsync` auf; `DispatchCoreAsync` führt alle geforderten Prüfungen, den Statusübergang, Klon/Branch/CLI-Start und die Zustellung durch — ohne neuen Parallelmechanismus. Der Schritt ist daher primär eine **Nachweis- und Absicherungsaufgabe** mit kleinen Korrekturen nur dort, wo die Verifikation tatsächliche Lücken zeigt:

- Integrationstests (real SQLite + `ScmDispatchFixture`): „Ereignis ohne Aufgabe → Anlage → Auto-Start (Klon/Branch/Sitzung) → Zugestellt" mit konfiguriertem CLI-Default; „keine Standard-CLI → Wartend mit bedienbarer Aufforderung → CLI-Konfiguration + `RetryAsync` bzw. `OpenFromQueueAsync` → Zugestellt"; blockierte wiederverwendete Aufgaben (beendet/archiviert/pausiert) bleiben mit Grund wartend und werden nicht gestartet; Anlage erfolgt nachweislich auch ohne CLI (Entscheidungsnachweis, keine Duplikate, keine verlorenen Ereignisse).
- E2E: Im konsolidierten SCM-E2E-Lauf den Anlageschritt (Issue 20) um den Nachweis „wartend ohne CLI → Auswahl über `ScmEventOpen` → Klon/Start/Zustellung bis `Verarbeitet`" erweitern.
- Dokumentation der Entscheidung: **Aufgabenanlage erfolgt auch bei fehlender Standard-CLI** (empfohlene Variante — Ereignis bleibt wartend und nach CLI-Konfiguration zustellbar, Aufgabe ist sofort sichtbar, keine Duplikatanlage über `SourceKey`-Dedup).

## Konfiguration

Keine neuen Konfigurationseinträge. Wiederverwendung der bestehenden CLI-Auflösung: `Aufgabe.KiPluginPrefix` → `PluginDefaultSettingsService` Projekt-Default → globaler Default (`AppEinstellungen`-Schlüssel `plugins.default.*`).

## Offene Fragen

- Keine fachlich offenen Fragen; die einzige Entscheidung (Anlagezeitpunkt bei fehlender CLI) wird wie empfohlen getroffen und im Plan sowie in `docs/help/plugins/github-ereignisse/business-rules.md` dokumentiert.
