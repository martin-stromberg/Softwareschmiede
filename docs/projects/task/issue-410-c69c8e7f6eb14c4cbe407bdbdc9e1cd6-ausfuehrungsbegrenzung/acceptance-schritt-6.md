# Abnahmeprüfung – Entwicklungsschritt 6

**Geprüft:** `git diff task/issue-410-c69c8e7f6eb14c4cbe407bdbdc9e1cd6-ausfuehrungsbegrenzung...HEAD`
(Commits `0c92fc33` Planung + `0753f989` Umsetzung, „Issue-Aufbereitung — KI-Analyse,
Beschreibungsersetzung, optionaler Analyse-Kommentar, Einstellungen-UI (#412 T2)").

## Ergebnis

**Status:** Anforderung vollständig erfüllt

## Abweichungen

Keine.

## Hinweise

### Verifikationsnachweise (selbst ausgeführt)

- `dotnet build Softwareschmiede.slnx` → **0 Warnungen, 0 Fehler** (inkl. `Softwareschmiede.App`).
- `dotnet format Softwareschmiede.slnx --verify-no-changes` → **sauber** (Exit 0).
- `dotnet test src/Softwareschmiede.IntegrationTests --filter "FullyQualifiedName~IssueAufbereitung"` → **21/21 grün** (11 `ScmEventDispatchTests_IssueAufbereitung` + 10 `ScmEventMonitoringServiceTests_IssueAufbereitung`).
- `dotnet test src/Softwareschmiede.Tests --filter "FullyQualifiedName~IssueAufbereitung&Category!=OsInterface"` → **20/20 grün** (5 Prompt- + 7 SettingsViewModel- + 8 AppEinstellung-Tests).

### Akzeptanzkriterien im Einzelnen

1. **Schalter/Plugin/Repos/Kommentar persistiert + bedienbar** — `SettingsView.xaml` Z. 516–596 (Expander `IssueAufbereitungExpander`, Funktions-, Plugin-, Repository- und Kommentar-Controls), `SettingsViewModel.cs` Z. 387–427, 620–635 (Laden) und 763–776 (Speichern über `scm.issueaufbereitung.*` plus `ProjektService.SetIssueAufbereitungAktivAsync` → `GitRepository.IssueAufbereitungAktiv`, `ProjektService.cs` Z. 214–221). `IssueAufbereitungKiPluginOption.Standard` = `null`-Prefix = Fallback-Eintrag. ViewModel-Tests decken Laden/Speichern/Gating ab.
2. **Neue Issues → automatische Analyse** — Erkennung in `ScmIssueAufbereitungsService.ErkennenUndEinreihenAsync` legt den Auftrag jetzt **sofort fällig** an (`NextAttemptUtc` entfällt, Diff ScmIssueAufbereitungsService.cs); `DispatchDueEventsAsync` nimmt ihn noch im selben Zyklus auf. Fester Prompt (`ScmIssueAufbereitungPrompt.Build`) enthält Titel (`IssueReferenz.Titel`) + Beschreibung (`observation.Body`) und alle 10 Qualitätskriterien (Verständlichkeit, sprachliche Qualität, Vollständigkeit, fehlende Informationen, offene Fragen, technische + fachliche Risiken, Zielkonflikte, alternative Lösungsansätze, mögliche Fehlinterpretationen) sowie den Ausgabevertrag `issueBodyUpdate` + `comment` mit explizitem „Titel wird nicht geändert".
3. **Beschreibung remote ersetzt, Titel unverändert; Kommentar nur bei Schalter** — `ReconcileAufbereitungAttemptAsync` (ScmEventOrchestrator.cs Z. 817–963) mappt `issueBodyUpdate` → `ScmResponseKind.IssueBodyUpdate` über die idempotente Outbox (PATCH-Machinerie samt Marker-Abgleich aus Vorarbeit `389b0702`); `comment` wird bei deaktiviertem Schalter **hostseitig verworfen** (Z. 853–866: ehrliches `WriteResultAsync success=false`, kein Outbox-Eintrag, kein Remote-Call möglich) und protokolliert (Z. 899–901). Schalter wird live beim Abgleich gelesen (Z. 837).
4. **#410-Limit-Einrechnung** — dieselbe atomare `PruefeUndReserviereStartPlatzAsync` (Z. 698) wie der generische Pfad, Reservierung per `using` über den Start (Z. 706–716); `GetBelegungAsync` zählt die Aufbereitungs-CLI über Session-Prefix/Task wie jede andere Ausführung. Integrationstest `Dispatch_Aufbereitung_GlobalesLimitErreicht_WartetMitGrund` belegt das Warten ohne Start.
5. **Fehler → Log + Protokolleintrag, Issue unverändert, kein Fehler-Kommentar** — `ProtokolliereAufbereitungsFehlerAsync` (Z. 803–808, dedupliziert nach Reason) + `ILogger` in allen Fehlerzweigen (Z. 784, 793, 894); im gesamten Aufbereitungspfad nur `WaitAsync` (nie `WaitWithCommentAsync`), keine Attachments-/Warte-/Fehlerkommentare. Test `..._RückschreibungFehler_UngeklärtOhneFehlerkommentar` belegt Protokolleintrag + fehlenden Remote-Kommentar.
6. **Tests** — 11 neue Dispatch-Integrationstests (Happy Path inkl. Prompt-Assertions gegen `request.body`, Kommentar an/aus, fehlender Kommentar → unklar, kein/deaktiviertes Plugin → Wartegrund, Limit → Wartegrund, Funktion/Repository aus → Wartegrund, geschlossenes Issue → Verarbeitet, Rückschreibfehler → unklar + Protokoll, `NoRemoteAction` → Verarbeitet), Schritt-5-Monitoring-Tests korrekt umgebaut (Park-Assertions → sofort fällig/Wartend), 5 Prompt-Unit-, 7 SettingsViewModel-Tests, konsolidierter FlaUI-E2E `Einstellungen_IssueAufbereitungKonfigurieren_E2E` innerhalb `General_BasisUndEinstellungen_E2E` (kein zusätzlicher App-Start; Reset im `finally`). Keine Remote-Calls: SCM-Quelle/Responder gemockt, Exchange-Kanal echt über Dateien, SQLite echt — ehrliche Nachweise.

### Kritikpunkte der Prüfung

- **(a) Entparken/Isolation:** Guard `item.Zweck == Zweck` (Z. 135–139) vor der generischen Trigger-Validierung ersetzt den Schritt-5-Park-Block (`NextAttemptUtc = long.MaxValue` + Wartetext) vollständig. Isolation intakt: `a.Zweck == null`-Filter in `ScmEventTaskResolver` (Z. 65, 135) und `ScmEventMonitoringService` (Z. 51) unverändert; Test `Monitor_TriggerOnPreparedIssue_ResolvesToCanonicalTask` belegt weiterhin die kanonische Auflösung. *Einziger Randfall:* bereits mit dem Schritt-5-Stand **persistierte** geparkte Ereignisse bleiben `long.MaxValue`-parked und werden nur über „Erneut prüfen" (`RetryAsync` setzt `NextAttemptUtc = null`) entparkt — für produktive Daten irrelevant, da Schritt 5 nie eigenständig ausgeliefert wurde (Stufen-Branch).
- **(b) Ehrliche Endzustände:** `Verarbeitet` nur bei ≥1 erfolgreichem `IssueBodyUpdate` (+ ≥1 erfolgreichem `Comment` bei aktivem Schalter) oder deklariertem `NoRemoteAction`; sonst `VerarbeitungUngeklärt`/`Wartend` mit Grund (Z. 902–943). Geschlossenes Issue → `Verarbeitet` + Protokolleintrag ohne Remote-Änderung (Z. 655–665).
- **(c) Kommentar-Schalter hostseitig erzwungen** — gelieferte Kommentaraktionen erreichen bei „aus" niemals die Outbox; aus dem `complete`-Satz abgezogene IDs verfälschen die Abschlussprüfung nicht (`skippedActionIds`, Z. 844, 914–916).
- **(d) Idempotenz:** deterministische Zwecke `ai:{actionId}` + `EnsureAsync`-Kollisionsprüfung + `ExecuteAsync`-Frühreturn bei `Succeeded` + markerbasierter Remote-Abgleich → kein doppeltes PATCH/Kommentar bei Re-Dispatch oder Reconcile (Happy-Path-Test dispatcht doppelt und erwartet `Single`).
- **(g) Fehlerfälle:** Analyse-Timeout/Diagnose → `VerarbeitungUngeklärt` mit persistiertem Grund (Status + Reason am Vorgang; spiegelbildlich zum generischen Pfad — ohne zusätzlichen Protokolleintrag, konsistent zur bestehenden Semantik); Provider-/Start-/Rückschreibefehler → Log + `SystemMeldung` im Aufgabenprotokoll.
- **(i) Testnachweise ehrlich** — alle Remote-Grenzen gemockt, alle Assertions über echte DB-Zustände, Outbox-Requests und tatsächlich geschriebene Exchange-Dateien.

### Kleinigkeiten (keine Abweichungen)

- `test-results.md` spricht von „`ScmEventDispatchTests_IssueAufbereitung` (21 Tests)“ — die Klasse enthält 11; die 21 stimmt für die Gesamtmenge der Aufbereitungs-Integrationstests (11 Dispatch + 10 Monitoring). Nur Dokumentations-Ungenauigkeit.
- Der feste Analyse-Prompt erhält bewusst **keinen** konfigurierten `ScmPromptAppendix` (`PrepareAsync` ohne `promptSuffix`, Z. 747–749) — deckt sich mit „fest hinterlegtem Analyse-Prompt".
- Kein „früheres-Ereignis"-Reihenfolge-Check im Aufbereitungspfad — folgerichtig: pro dedizierter Aufgabe existiert genau ein Ereignis (EventKey-Dedup `{ContentKey}/IssueAufbereitung`).
- Wird der Kommentar-Schalter **zwischen** Zustellung und Abgleich ausgeschaltet, nachdem eine Kommentar-Outbox-Aktion bereits persistiert war, endet der Vorgang ehrlich `VerarbeitungUngeklärt`; die verwaiste `ai:`-Aktion hält das Ereignis im Due-Filter (harmloser Poll-Overhead, kein Remote-Versand).
- Bei geleerter Plugin-Einstellung greift für bereits angelegte Aufgaben das hinterlegte `task.KiPluginPrefix` als erste Stufe (Z. 680–682) — entspricht der Aufgaben-Zuordnungssemantik; die Live-Einstellung hat stets Vorrang, nicht ausführbare Prefixes fallen ehrlich in die #411-Kette.
