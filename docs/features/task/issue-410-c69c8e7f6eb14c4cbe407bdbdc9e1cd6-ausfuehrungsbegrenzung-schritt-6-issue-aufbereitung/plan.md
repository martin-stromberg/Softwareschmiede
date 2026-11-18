# Umsetzungsplan – Schritt 6: Issue-Aufbereitung ausführen

## Architektur (aus Bestandsaufnahme abgeleitet)

Der Aufbereitungsauftrag ist ein geparkter interner `ScmIncomingEvent`
(Zweck `IssueAufbereitung`). Der reguläre Orchestrator-Pfad wird für diesen
Zweck dediziert erweitert — keine Parallelmechanik.

### 1. Entparken + Dispatch-Verzweigung

- `ScmIssueAufbereitungsService`: neuen Auftrag mit `NextAttemptUtc = null`
  (sofort fällig) statt `long.MaxValue` einreihen; Kommentar-/Plugin-Auflösung
  aus der Reason-Text-Erzeugung entfernen (entfällt zur Laufzeit).
- `ScmEventOrchestrator.DispatchCoreAsync`: Guard ersetzen durch
  `DispatchAufbereitungAsync(item, explicitPluginPrefix, ct)`.
- `ScmIssueAufbereitungPrompt` (neu, `Application/Services/`): statischer
  `Build(titel, beschreibung)` — fester deutschsprachiger Analyse-Prompt mit
  allen Qualitätskriterien und Ausgabevertrag („issueBodyUpdate"-Aktion +
  „comment"-Aktion). **Deterministisch** aus `task.IssueReferenz.Titel` +
  `observation.Body` — bitgleich für die `request.json`-Rekonstruktion beim
  Wiederanlauf. Der Kommentar-Schalter fließt **nicht** in den Prompt ein;
  er wird hostseitig beim Abgleich erzwungen (s. §3).

### 2. `DispatchAufbereitungAsync` (Spiegel des generischen Zustellpfads)

Reihenfolge identisch zum Referenzpfad:

1. Quellen-/Responder-Auflösung via `observation.Reference.Repository.
   ProviderPrefix` (fehlt → `WaitAsync` „SCM-Anbieter ist nicht verfügbar.").
2. Hostaktionen: nur nicht-`ai:`-Outbox-Aktionen ausführen; bei `Verarbeitet`
   abbrechen.
3. `DeliveryAttemptId` gesetzt → `ReconcileAufbereitungAttemptAsync` (s. §3),
   dann return.
4. `NextAttemptUtc`-Zeitplan, Aufgabe laden (`Include(GitRepository)`),
   `BlockReason` → `WaitAsync` (keine Issue-Kommentare!).
5. **Schalter-Wirksamkeit:** `GetScmIssueAufbereitungEnabledAsync` → false =
   „Issue-Aufbereitung ist deaktiviert."; `task.GitRepository.
   IssueAufbereitungAktiv` → false = „für dieses Repository deaktiviert." —
   ehrlicher Wartegrund, Ereignis bleibt eingereiht.
6. `GetEventRepositoryAsync` + `CanWrite` wie generischer Pfad.
7. `source.ReadIssueAsync(observation.Reference)`: `null` → Wartegrund
   „Issue konnte nicht gelesen werden."; `IstGeschlossen` → `Verarbeitet` +
   Protokolleintrag (kein Remote-Schreiben auf geschlossenes Issue).
8. CLI-Wiederverwendung wie generisch (Alive-Prüfung, Prefix-Mismatch,
   ungeprüfter-Vertrag-Wartegrund).
9. **Plugin-Auflösung** (nur wenn kein Adapter lebt):
   `explicitPluginPrefix ?? (GetScmIssueAufbereitungPluginPrefixAsync() ??
   task.KiPluginPrefix)` → `ResolveEnabledDevelopmentAutomationPluginWithProjectScopeAsync`
   (Kette: Aufbereitungs-Plugin → Projekt-Default → Global-Default →
   `ki.plugin.default`, activation-aware mit `IgnoredPluginPrefixes`).
   `null` + ignorierte Präfixe → deduplizierter Protokolleintrag + ehrlicher
   Wartegrund („konfiguriertes Plugin nicht installiert/aktiviert, kein
   Standard-Plugin"); `null` ohne ignorierte → „Keine ausführbare CLI für
   die Issue-Aufbereitung konfiguriert."
10. **Ausführungsbegrenzung:** `PruefeUndReserviereStartPlatzAsync(prefix)`;
    Wartegrund → dedupliziert protokollieren + `WaitAsync`. Reservierung hält
    bis nach `CliStart`/`Neustart` (`using`).
11. Strenger Resolve + `CheckHealthAsync`, `--scm-exchange` bei KiSimulator,
    `ProzessStartenUndCliStartenAsync` (kein KlonPfad → automatischer Klon
    über `RepositoryUrl`/`BasisBranchName`) bzw. `CliNeustartenAsync`.
12. `WaitForInputReadyAsync`/`IsBusy`/`CheckExchangeAccessAsync` wie generisch.
13. `exchange.PrepareAsync` mit `ScmExchangeRequest(item.Id, attemptId,
    runId, task.Id, ScmIssueAufbereitungPrompt.Build(...), "IssueAufbereitung",
    "owner/repo", issueUrl)` → Status `ZustellungVorbereitet` →
    `adapter.WritePromptAsync` → `Zugestellt`; Fehler → `ZustellungUngewiss`.
14. Fehlerfänge wie generisch: `ScmProviderException` (typisiert → Retry/
    `long.MaxValue`), `InvalidOperationException/IOException/
    UnauthorizedAccessException` → `NextAttemptUtc = long.MaxValue` +
    Wartegrund + `SystemMeldung`-Protokolleintrag + `ILogger.LogError`
    (Konstruktor-Injection `ILogger<ScmEventOrchestrator>` neu — Pflicht
    laut Anforderung „Anwendungs-Log").

### 3. `ReconcileAufbereitungAttemptAsync`

- `exchange.ReadAsync(attempt)` mit bitgleich rekonstruiertem Request
  (`Build(task.IssueReferenz.Titel, observation.Body)` — `IssueReferenz` via
  `Include` laden).
- `accepted` → `InBearbeitung`.
- Actions: `issueBodyUpdate` → `ScmResponseKind.IssueBodyUpdate` (Body=
  neue Beschreibung; Titel bleibt per Definition unverändert — `ScmResponseRequest`
  dokumentiert das bereits); `comment` → `ScmResponseKind.Comment`, aber nur
  wenn `GetScmIssueAufbereitungKommentarEnabledAsync` — sonst `Skipped`-Result
  + Protokollhinweis; andere Kinds → `VerarbeitungUngeklärt`.
- Outbox-Zweck `ai:{actionId}` via `EnsureAsync`+`WriteResultAsync`+`ExecuteAsync`
  — identisch zu generisch; `ScmActionIdCollisionException` →
  `VerarbeitungUngeklärt`.
- `complete`: `NoRemoteAction` → `Verarbeitet` + Protokolleintrag mit
  `ReportText`; sonst erfolgreich wenn ≥1 `issueBodyUpdate` `Succeeded` **und**
  (bei aktivem Kommentar-Schalter) ≥1 `comment` `Succeeded`; fehlende/
  fehlgeschlagene Aktionen → `VerarbeitungUngeklärt` mit ehrlichem Grund +
  Protokoll. Kommentar-Schalter aus + geskippte Kommentare zählen nicht als
  fehlend. Timeout-Regeln (`AcceptanceTimeout`/`CompletionTimeout`) identisch.
- **Kein** Attachments-Kommentar, keine PR-Verknüpfung, kein Wartehinweis-
  Kommentar am Issue (Vorschlag 15).
- `IssueBodyUpdate`-Mapping auch im generischen `ReconcileAttemptAsync`
  ergänzen (aktuelle Default→Unklar-Behandlung würde eine versehentlich
  gesendete `issueBodyUpdate`-Aktion verwerfen); `ValidateAction` +
  `BuildPrompt`-Schema um das neue Kind erweitern (`Body` Pflicht).

### 4. Einstellungen-Oberfläche (Tab „Automatisierung", neuer Expander)

- `SettingsViewModel` (+ `ProjektService` in den Konstruktor):
  `IsIssueAufbereitungEnabled`, `IsIssueAufbereitungKommentarEnabled`,
  `IssueAufbereitungKiPluginOptionen` + `SelectedIssueAufbereitungKiPlugin`
  (Eintrag „Globale Standard-CLI verwenden" = `null`), `IssueAufbereitungRepositories`
  (`IssueAufbereitungRepositoryEntry` mit `Repository`-Ref + `IsSelected`),
  `IsIssueAufbereitungFelderEnabled` (Schalter ∧ Plugin-Aktivierungen gültig),
  `IssueAufbereitungStatusText` im Expander-Header. Laden/Speichern über die
  bestehenden Schlüssel; Repository-Flag via neuem `ProjektService.
  SetIssueAufbereitungAktivAsync(id, aktiv, ct)` (lädt Entität, setzt Flag,
  `SaveChangesAsync` — kleines `UpdateAsync`-Muster analog
  `SetEventMonitoringAktivAsync` … Existenz prüfen; sonst generisch).
- `SettingsView.xaml`: Expander zwischen „Aufgaben ausführen" und „Aufgaben
  beenden": CheckBox Funktion (`IssueAufbereitungEnabledCheckBox`), ComboBox
  KI-Plugin (`IssueAufbereitungPluginComboBox`), ItemsControl Repository-
  Checkliste (`IssueAufbereitungRepositoriesList` mit einzelnen
  `IssueAufbereitungRepositoryCheckBox`-AutomationProperties), CheckBox
  Kommentar (`IssueAufbereitungCommentCheckBox`), Status-Text im Header
  (TextBlock-Bindung `IssueAufbereitungStatusText`). Alle Felder
  `IsEnabled="{Binding IsIssueAufbereitungFelderEnabled}"`.

### 5. Queue-Darstellung

`ScmEventQueueViewModel.ErzeugeEventViewItem` bereits zweck-bewusst; Status-
Texte bleiben generisch. Ggf. `AktionLabel`/`Plugin`-Kosmetik — nur falls im
Review nötig.

### 6. Tests

- **Integrationstests** (`ScmEventDispatchTests_IssueAufbereitung.cs`, neu):
  Happy Path (Task+Ereignis+Issue anlegen → `DispatchAsync` → Prompt enthält
  Titel/Body/alle Kriterien; `accepted`+`issueBodyUpdate`+`comment`+
  `complete` → `Verarbeitet`, `Responses` PATCH+POST, Titel unverändert);
  Kommentar-Schalter aus → nur PATCH; `NoRemoteAction` → Verarbeitet ohne
  Writes; fehlendes `issueBodyUpdate` trotz Deklaration → `VerarbeitungUngeklärt`;
  kein Plugin → `Wartend` mit ehrlichem Grund, kein Prompt; Plugin-Limit
  voll → Wartegrund Limit, kein Prompt; Funktion deaktiviert → Wartegrund;
  Provider-Fehler → keine Remote-Änderung + Protokolleintrag.
- **Schritt-5-Tests anpassen** (`ScmEventMonitoringServiceTests_IssueAufbereitung`):
  `NextAttemptUtc == null`, Reason-Aussage vereinfachen; den
  Explicit-Dispatch-Test in „Dispatch nutzt den Aufbereitungspfad" umbauen.
- **Unit-Tests:** `ScmIssueAufbereitungPromptTests` (alle 9 Kriterien + Titel/
  Body-Einbettung + Aktionsschema); `SettingsViewModelTests` neue Datei
  (Laden/Speichern aller 4 Einstellungen, Plugin-Auswahl inkl. Standard-Eintrag,
  FelderEnabled-Logik).
- **FlaUI E2E (konsolidiert, ein zusätzlicher Lauf im bestehenden
  Settings-Szenario):** `E2E_IssueAufbereitung.cs` — Settings öffnen → Expander
  → Funktion an → Plugin wählen → Repository-Haken setzen → Kommentar aus →
  Speichern → Neu öffnen → UI + DB-Persistenz prüfen (Schlüssel + Flag) →
  in `finally` zurücksetzen. Aufruf aus `General_BasisUndEinstellungen_E2E`
  (separater `EnsureRunningAndGetWindowAsync`-Block — eigener App-Lauf,
  minimal gehalten) + Wrapper-Methoden in `Views/SettingsView.cs`.
  Validierungsschritt (ungültige Eingabe) hier nicht nötig — nur Bool/Combos.

### 7. Verifikation & Artefakte

- Build: `dotnet build Softwareschmiede.slnx`; bei App-Lock Teil-Build.
- Tests: `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test
  src/Softwareschmiede.Tests/… --filter "Category!=OsInterface"`; gezielt
  Integrationstests; E2E einzeln (`Category=OsInterface`), synchron.
- `dotnet format Softwareschmiede.slnx --verify-no-changes`.
- `review.md`, `review-usability.md`, `review-code.md`, `test-results.md`.
- Docs: `docs/help/` Abschnitt Issue-Aufbereitung + `README.md` +
  `docs/RELEASE_NOTES.md`.
- Abschluss-Commit `feat: …` mit `Closes #412`. Kein Merge.

## Reihenfolge der Umsetzung

1. `ScmIssueAufbereitungsService` (Entparken, Reason vereinfachen)
2. `ScmIssueAufbereitungPrompt` (neu)
3. `ScmEventExchangeProtocol` (`issueBodyUpdate` validieren + Schema-Text)
4. `ScmEventOrchestrator` (Dispatch + Reconcile + Logger)
5. `ProjektService` + `SettingsViewModel` + `SettingsView.xaml`
6. Unit-Tests, Integrationstests (inkl. Schritt-5-Anpassungen), E2E
7. Format, Reviews, test-results, Docs, Commit

## Offene Punkte

Keine.
