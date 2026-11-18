# Bestandsaufnahme – Schritt 6 (Issue-Aufbereitung ausführen)

Detail-Ist-Stand des Gesamtprojekts: `docs/projects/task/issue-410-…/inventory.md`.
Hier nur die für Schritt 6 verifizierten Code-Fakten (Stand auf diesem Branch).

## Vorhandene Bausteine (Schritte 1–5)

- **`ScmIssueAufbereitungsService`** (`src/Softwareschmiede/Application/Services/`):
  `Zweck = "IssueAufbereitung"`; `ErkennenUndEinreihenAsync` legt Zweck-Aufgabe
  (`CreateFromIssueEventAsync(…, Zweck, pluginPrefix)` — `task.KiPluginPrefix` =
  konfiguriertes Aufbereitungs-Plugin) + internes `ScmIncomingEvent`
  (`Zweck`, `EventKey = {ContentKey}/IssueAufbereitung`,
  `NextAttemptUtc = long.MaxValue`, Reason-Text, SystemMeldung-Protokolleintrag) an.
- **`ScmEventOrchestrator.DispatchCoreAsync`** (Z. 133-138): Guard parkt
  Zweck-Ereignisse erneut (`long.MaxValue`, Wartegrund). Der generische Pfad
  darunter: Quellen-/Responder-Auflösung per ProviderPrefix, Hostaktionen
  (nicht-`ai:`-Outbox), `ReconcileAttemptAsync` bei `DeliveryAttemptId`,
  ältere offene Ereignisse, `BlockReason`, `GetEventRepositoryAsync`+`CanWrite`,
  Trigger-Revalidierung, `ResolveEnabledDevelopmentAutomationPluginWithProjectScopeAsync`
  (Kette: Aufgabe → Projekt-Default → Global-Default → `ki.plugin.default`,
  `IgnoredPluginPrefixes`), `ScmEventExecutionLimitService.
  PruefeUndReserviereStartPlatzAsync` (atomare Reservierung), strenger
  Plugin-Resolve + `CheckHealthAsync`, `--scm-exchange`-Parameter bei
  KiSimulator, `ProzessStartenUndCliStartenAsync`/`CliNeustartenAsync`,
  `GetScmPromptSession`/`WaitForInputReadyAsync`/`CheckExchangeAccessAsync`,
  `exchange.PrepareAsync` + `adapter.WritePromptAsync`, Statusmaschine
  `ZustellungVorbereitet → Zugestellt → InBearbeitung → Verarbeitet` +
  `VerarbeitungUngeklärt`/`ZustellungUngewiss`, Abbruch über `IsCancelledAsync`.
- **`ReconcileAttemptAsync`** (Z. 477-592): `exchange.ReadAsync(attempt)` liest
  `accepted.json`/`actions/*.json`/`complete.json`; Aktionsmapping
  `comment|reaction|pullRequest → ScmResponseKind`; Outbox-Zweck `ai:{actionId}`;
  `WriteResultAsync`; Abschlussregel `NoRemoteAction` bzw. alle deklarierten
  Aktionen `Succeeded`; Attachments-Kommentar; PR-Verknüpfung; Zeitüberschreitung
  via `ScmEventOptions.AcceptanceTimeout/CompletionTimeout`.
- **`ScmEventExchangeProtocol`**: `ScmExchangeRequest(EventId, AttemptId,
  CliRunId, AufgabeId, Body, EventKind, Repository, OriginUrl)` wird als
  `request.json` persistiert und beim Wiederanlauf bitgleich gegen den
  rekonstruierten Request geprüft (`ValidateAttemptAsync` Z. 613) —
  → Der Aufbereitungs-Body muss aus **persistierten** Daten deterministisch
  rekonstruierbar sein (`IssueReferenz.Titel` + `observation.Body`, beide
  unveränderlich). `ValidateAction` (Z. 582) erlaubt nur
  `comment|reaction|pullRequest`; `BuildPrompt` beschreibt das actionSchema im
  Prompt. `ScmExchangeAttempt.Prompt` = JSON-Envelope.
- **Schritt 4:** `ScmResponseKind.IssueBodyUpdate` (Execute + Reconcile in
  `IScmEventResponder`, GitHub `PATCH issues/{n}`, Marker
  `<!-- softwareschmiede:action:{id} -->`). Hostaktionen laufen über
  `outbox.EnsureAsync(item, "issuebody", request)` + `ExecuteAsync` — belegt
  durch `ScmEventDispatchTests_IssueBodyUpdate`.
- **Schritt 2:** `ScmEventExecutionLimitService` zählt über
  `KiAusfuehrungsService`-Handles (`Aufgabe.KiPluginPrefix`-Attribution);
  `GlobalLimitKey`, `PluginLimitKey(prefix)`.
- **Einstellungen:** `AppEinstellungService`-Getter
  `GetScmIssueAufbereitungEnabledAsync` (Standard false),
  `GetScmIssueAufbereitungPluginPrefixAsync` (null=Fallback),
  `GetScmIssueAufbereitungKommentarEnabledAsync` (Standard true) existieren;
  Setter = generisches `SetSettingAsync`/`SetBoolSettingAsync`.
  `GitRepository.IssueAufbereitungAktiv` (Migration angewendet, kein UI).
- **`ScmEventMonitoringService.RunOnceAsync`**: Erkennung nur im
  Enumerationskanal (`!inboxMode`, `issueAufbereitungAktiv`, Issue+Description);
  endet mit `DispatchDueEventsAsync` — ein entparkter Auftrag wird im selben
  Zyklus dispatcht.
- **`ScmEventQueueViewModel`**: Zweck-Ereignisse erscheinen als „Issue-Aufbereitung";
  Öffnen/Retry/Abbrechen sind generisch (`OpenFromQueueAsync` mit
  Plugin-Auswahl → `explicitPluginPrefix`).
- **Einstellungen-UI:** `SettingsView.xaml` Tab „Automatisierung" mit Expandern
  („Aufgaben ausführen" `AufgabenAusfuehrenExpander` mit Status-Text im Header,
  „Aufgaben beenden", „Prozessabläufe"). `SettingsViewModel`: Lade-/Speicher-
  Muster `LadenAsync`/`SpeichernAsync`, Options-Records wie
  `ScmKontoModusOption`, `NullWennLeer`. `ProjektService.
  GetAllRepositoriesAsync` liefert alle `GitRepository` (ohne Projekt-Include).
- **Testinfrastruktur:** `ScmDispatchFixture` (echte SQLite-DB, gemocktes
  SCM-Plugin `test-scm` mit `IScmEventSource`/`IScmEventResponder`,
  KiSimulator-Profil `--scm-exchange`, `Prompts`/`Responses`/`StartSpecs`,
  `DispatchAsync`, `ConfigureIssue`, `SetSettingAsync`,
  `CompleteProbeAsync`); Exchange-Simulation per `accepted.json`/
  `actions/{id}.json`/`complete.json` ins `item.ExchangePath`.
  E2E: `End2EndTest` partial, `SettingsView`-Wrapper (Expander-Helper),
  Szenario-Aufruf in `General_BasisUndEinstellungen_E2E`.
- **Sonstiges:** `Issue(Nummer, Titel, Body, Labels, Milestone, IssueUrl,
  IstGeschlossen)`; `ProtokollTyp.SystemMeldung`/`GitAktion`;
  `protokoll.AddEintragAsync(aufgabeId, typ, inhalt, ct: ct)`;
  Orchestrator hat **keinen** `ILogger` (wird ergänzt);
  `ScmEventStore.Observation(item)`, `ActionId(eventId, purpose)`, `RetryAt`.

## Verifizierte Architektur-Entscheidungen (aus Planung bestätigt)

- Ergebnisrücklauf über `actions/*.json` mit neuem Aktionskind
  `issueBodyUpdate` → `ScmResponseKind.IssueBodyUpdate`; Kommentar über
  vorhandenes `comment`-Kind — konsistentester Mechanismus (idempotent über
  `ai:`-Outbox, Reconcile-sicher).
- Der `request.Body` des Aufbereitungs-Versuchs trägt den festen Analyse-Prompt
  (aus `IssueReferenz.Titel` + `observation.Body` gebaut — bitgleich
  rekonstruierbar); der Kommentar-Schalter wird **hostseitig** beim Abgleich
  erzwungen (Prompt verlangt immer beide Aktionen, deaktivierte Kommentare
  werden verworfen+protokolliert) — sonst wäre die Request-Rekonstruktion vom
  später änderbaren Schalter abhängig.
