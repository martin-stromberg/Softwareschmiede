# Umsetzungsplan: Funktionstrennung der Automatisierungs-Einstellungen (Issue #348)

## Übersicht

Die Einstellungsseite erhält ein neues Register „Automatisierung" (direkt nach „Allgemein"), das den bisherigen Abschnitt „SCM-Ereignisse" vollständig übernimmt und die drei Automatisierungsfunktionen in drei auf-/zuklappbare `Expander`-Panels gliedert: „Aufgaben ausführen" (`ScmEventMonitoringService`), „Aufgaben beenden" (`ScmTaskCompletionMonitoringService`) und „Prozessabläufe" (`ScmBackmergeService`). Fachlich werden die Funktionen entkoppelt: die Backmerge-Prüfung erhält einen eigenen gehosteten Prüfzyklus (`ScmBackmergeMonitoringService`), der stets unter `ScmKontoModus.Benutzerkonto` und unabhängig von `scm.monitoring.enabled` läuft; die Abschlussüberwachung erhält einen neuen Prüfmodus (`scm.taskcompletion.scope`), prüft das verknüpfte Issue kanonisch unter `Benutzerkonto` und beendet nur noch Aufgaben mit SCM-Referenz (Issue **oder** Pull Request — der PR-Pfad bleibt als Zusatzkriterium erhalten). Neu hinzu kommen zwei opt-in Merge-Robustheits-Optionen für Backmerge-PRs: automatische Draft-Aktivierung („ready for review") und ein Admin-Merge-Fallback bei blockiertem regulärem Merge. Im UI werden wirksamkeitslose Felder ausgeblendet bzw. deaktiviert und Panels zeigen ihren Aktivierungsstatus in der Titelleiste.

## Scope-Klärung

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Die drei zyklischen Automatisierungsfunktionen der SCM-Verarbeitung: (a) „Aufgaben ausführen" = `ScmEventMonitoringService`, (b) „Aufgaben beenden" = `ScmTaskCompletionMonitoringService`, (c) „Prozessabläufe" = `ScmBackmergeService` — jeweils mit Aktivierungsschalter, Modus-/Parameter-Einstellungen und den Sichtbarkeits-/Aktivierungsregeln ihrer Eingabefelder. |
| Auslösende Ereignisse | Nutzer öffnet/speichert die Einstellungen; zyklischer Poll der drei Hintergrunddienste (`PeriodicBackgroundService`, Intervall `ScmEventOptions.PollInterval`); SCM-Ereignis mit Trigger-Keyword/`@`-Erwähnung; verknüpftes Issue geschlossen **oder** alle verknüpften Pull Requests geschlossen/gemergt; neu enumerierter mergebarer Backmerge-PR (inkl. Draft-PR bei aktivierter Aktivierungsoption); manuelle Aktion „Quellen erneut prüfen" in der SCM-Ereignisübersicht. |
| Erwartete Wirkung | „Allgemein" enthält nur noch Design, Arbeitsverzeichnis, Updates/Update-Pfad, Diagnose. „Automatisierung" zeigt drei `Expander`-Panels mit Statusanzeige in der Titelleiste. Die drei Funktionen sind unabhängig schaltbar (Laufzeit-Schalter ohne Neustart). Backmerge läuft immer unter dem Benutzerkonto — auch bei deaktivierter Ereignisüberwachung und im Modus `SeparatesKonto`; die Issue-Abschlussprüfung läuft ebenfalls kanonisch unter `Benutzerkonto`. Die Abschlussprüfung umfasst je nach Prüfmodus nur ereignisgesteuerte oder alle nicht beendeten/archivierten Aufgaben mit SCM-Referenz; Aufgaben ohne Issue **und** ohne Pull-Request-Verknüpfung werden nie beendet (Aufgaben ohne Issue, aber mit verknüpftem PR, können weiterhin über den bestehenden PR-Pfad beendet werden — Anwenderentscheidung 2026-11-11). Der Backmerge-Merge aktiviert Draft-PRs und nutzt den Admin-Merge nur, wenn die jeweilige Checkbox im Panel „Prozessabläufe" aktiviert ist (Standard: deaktiviert). |
| Umgang mit fehlenden Objekten | Neuer Einstellungsschlüssel `scm.taskcompletion.scope`: fehlende/ungültige Werte → Fallback `Ereignisgesteuert` (bisheriges Verhalten). Neue Schlüssel `scm.backmerge.draft_activation` / `scm.backmerge.admin_merge`: fehlende Werte → `false` (deaktiviert). Aufgabe ohne `IssueReferenz` und ohne `PullRequests`-Verknüpfung: nie Abschlusskandidat (verwerfen). Draft-Backmerge-PR ohne aktivierte Option: verbleibt im `WaitingForMergeable`-Retry (Checkpoint wiederverwenden). `Aufgabe.ScmAbschlussErkanntUtc`: wiederverwenden — laufender Countdown überlebt die Umstellung. `SeparatesKonto` ohne `AgentToken`: bestehende `ValidiereScmKontoModus`-Pflichtfeldprüfung bleibt unverändert wirksam. Plugin ohne `MarkPullRequestReadyAsync`-Implementierung: Default-Implementierung liefert `Failed("nicht unterstützt")` → normale Retry-Behandlung. |

### Scope-Ausschlüsse

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | Bei aktiver Ereignisüberwachung im `Benutzerkonto` werden dieselben Repositories ggf. zweimal pro Intervall enumeriert (Ereignis-Enumeration plus eigenständige Backmerge-Enumeration). | Vollständige Entkopplung des Backmerge-Prüfpfads ist der Kern der Anforderung; geteilte Watermark-Checkpoints würden die Zyklen wieder aneinander koppeln. | bestätigt (2026-11-11) |
| 2 | Ohne aktivierte Draft-Aktivierungs-Option werden Draft-Backmerge-PRs nie automatisch gemergt; sie verbleiben dauerhaft im `WaitingForMergeable`-Retry (`mergeStateStatus` `DRAFT` → `PullRequestMergeStatus.Blocked`). | Bewusste Opt-in-Option: Der Anwender aktiviert die Draft-Aktivierung selbst; ohne Option gilt das bisherige Warte-Verhalten. | bestätigt (2026-11-11) |
| 3 | Ohne aktivierte Admin-Merge-Option scheitert der Backmerge-Merge an Branch-Protection-/Check-Anforderungen dauerhaft im Fehler-/Retry-Pfad — die bisher implizite Admin-Umgehung (`AllowProtectedBranchBypass: true` → `gh pr merge --admin`) entfällt und wird zum Opt-in-Fallback. | Bewusste Opt-in-Option (Anwenderentscheidung 2026-11-11): der Admin-Bypass soll nur nach regulärem Mergeversuch und nur bei expliziter Freigabe greifen. | bestätigt (2026-11-11) |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Unabhängiger Backmerge-Prüfzyklus | Neuer gehosteter Dienst `ScmBackmergeMonitoringService : PeriodicBackgroundService` (Singleton + `AddHostedService`), der Repository-Auflösung und Enumeration selbst durchführt und `ScmBackmergeService.EvaluateDiscoveredReferencesAsync` aufruft. | Spiegelt exakt das etablierte Muster von `ScmEventMonitoringService`/`ScmTaskCompletionMonitoringService` (Hosted Service als Scheduler, Scoped Service als fachliche Bewertung). Die Anforderung verlangt explizit „einen eigenen Prüfpfad ... unabhängig vom Monitoring-Schalter". |
| Checkpoints des Backmerge-Zyklus | Eigene Namensräume `backmerge-repository/{prefix}/{alias}` (Auflösung) und `backmerge-enumeration/{provider}/{host}/{repoId}` (Enumeration); die bestehenden `backmerge/...`-PR-Checkpoints bleiben unverändert. | Geteilte `repository/`-/`enumeration/`-Checkpoints würden Fehler-Backoffs und Watermarks der beiden Dienste vermischen und die Entkopplung unterlaufen. |
| Kontomodus der Nicht-Ereignis-Prüfungen | Sowohl die Backmerge-Auflösung (`GetEventRepositoryAsync` im neuen `ScmBackmergeMonitoringService`) als auch die Issue-Abschlussprüfung in `ScmTaskCompletionMonitoringService.DetermineClosureAsync` werden kanonisch mit `new ScmRepositoryContext(..., ScmKontoModus.Benutzerkonto)` aufgerufen — unabhängig vom gespeicherten `scm.monitoring.accountmode`. | Anwenderentscheidung 2026-11-11: Nicht-Ereignis-APIs laufen immer unter dem Benutzer-Token (Token-Grenze lt. `ScmKontoModus`-Enum-Hinweis); der PR-Statuspfad verwendet ohnehin den Plugin-Aufruf direkt (Benutzer-Token) und ist davon nicht betroffen. Der Test `CompletionService_PassesActiveKonto_ToRepositoryContext` wird entsprechend umgebaut. |
| Abschlusskriterium | `DetermineClosureAsync` bleibt fachlich erhalten: **PR-Vorrang-Pfad** (alle verknüpften PRs müssen `Closed`/`Merged` sein) und **Issue-Pfad** (nur ohne PR-Verknüpfung). Neu ist nur der Kandidatenfilter `IssueReferenz != null \|\| PullRequests.Any()` in `RunOnceAsync` sowie der `Benutzerkonto`-Kontext im Issue-Pfad. | Anwenderentscheidung 2026-11-11: Die PR-basierte Beendigung bleibt als Zusatzkriterium erhalten — eine Aufgabe ohne Issue, aber mit geschlossenem/gemergtem PR kann weiterhin beendet werden. Die Regel „Aufgaben ohne Issue werden nicht beendet" gilt präzisiert als „ohne Issue **und** ohne PR". |
| Draft-Erkennung und -Aktivierung | `PullRequestStatusInfo` erhält ein angehängtes Member `bool IsDraft = false` (positionskompatibel); `GitHubPlugin` ergänzt `isDraft` in der GraphQL-Statusabfrage. Neue optionale Plugin-Methode `MarkPullRequestReadyAsync(repositoryId, nummer, ct)` → `PullRequestCompletionResult` (Default in `IGitPlugin`/`GitPluginBase`: `Failed("nicht unterstützt")`); `GitHubPlugin` implementiert sie via `gh pr ready <nummer> --repo <repo>` über den bestehenden `_cliRunner`. | `mergeStateStatus` `DRAFT` wird heute auf `Blocked` abgebildet und ist von regulären Blocks nicht unterscheidbar; das Flag macht den Draft-Zustand explizit. `gh pr ready` ist das CLI-Äquivalent zu „ready for review" und folgt dem bestehenden `gh`-Pfad des Plugins (kein Octokit im Merge-Pfad). |
| Merge-Robustheit (Admin-Fallback) | Zwei-Versuche-Modell in `BewertePullRequestAsync`: erster Versuch `CompletePullRequestAsync` mit `AllowProtectedBranchBypass: false` (regulärer `gh pr merge --merge`); nur bei Misserfolg **und** aktivierter Option `scm.backmerge.admin_merge` ein zweiter Versuch mit `AllowProtectedBranchBypass: true` (`--admin`). Das bisherige unbedingte `true` entfällt. | Anwenderentscheidung 2026-11-11: Admin-Merge nur als Fallback nach dem regulären Versuch und nur bei Opt-in — so greift der Bypass ausschließlich, wenn Branch-Protection den normalen Merge verhindert, und die bisherige implizite Privilegumgehung wird abgeschafft. |
| Merge-Optionen als eigene Schalter | Zwei bool-Checkboxen im `ProzessablaeufeExpander` unterhalb der „Backmerge-Pullrequests"-ComboBox, beide Standard `false`, `IsEnabled` an `SelectedBackmergeModus?.Modus != Deaktiviert` gebunden. | Anforderung verlangt zwei eigenständige Aktivierungsoptionen mit Default „deaktiviert"; die `IsEnabled`-Bindung wendet dieselbe Wirksamkeitsregel an wie bei den übrigen Panels (Felder ohne wirksame Funktion werden deaktiviert). |
| Panel-Titelleiste | `Expander.Header` enthält einen horizontalen `StackPanel` aus Titel-`TextBlock` und gebundenem Status-`TextBlock` (`AufgabenAusfuehrenStatusText` u. a.); der Status ist immer sichtbar, nicht nur im zugeklappten Zustand. | Kein bestehender Expander bindet Header-Inhalte; immer sichtbarer Status ist die einfachste Erfüllung der Anforderung und deckt den zugeklappten Fall ab. |
| Sichtbarkeit vs. Deaktivierung | `Visibility` (`BoolToVisibilityConverter`): „Trigger-Keyword"-Bereich (nur sichtbar bei aktivem „Aufgaben automatisch starten" **und** `Überwachungsmodus == Benutzerkonto`) und „Prompt-Anhänge"-Bereich (nur bei aktivem Schalter). `IsEnabled`: „Überwachungsmodus"-ComboBox, „Prüfmodus"-ComboBox und „Wartezeit für Beendigung"-TextBox (an den jeweiligen Funktionsschalter) sowie die beiden Backmerge-Merge-Optionen (an `Modus != Deaktiviert`). | Anforderung nennt `Visibility` für Trigger-Keyword/Prompt-Anhänge und `IsEnabled` für Überwachungsmodus/Wartezeit; für „Prüfmodus" (fehlende Angabe) wird analog „Wartezeit" deaktiviert statt ausgeblendet; für „Trigger-Keyword" bei inaktiver Funktion wird analog „Prompt-Anhänge" ausgeblendet (beides Annahmen aus der Anforderung). |
| Automation-Namen | Umbenannte Labels erhalten neue `AutomationProperties.Name` (ComboBox „Konto-Modus" → `Ueberwachungsmodus`; neue „Prüfmodus"-ComboBox → `Pruefmodus`; drei Expander + Status-TextBlocks erhalten eigene Namen); technisch benannte Controls (`IsScmMonitoringEnabled`, `IsScmTaskCompletionEnabled`, `ScmTriggerKeyword`, `ScmTaskCompletionWaitMinutes`, `ScmPromptAppendix*`, `Backmerge-Modus`) bleiben unverändert; die zwei neuen Checkboxen erhalten `IsScmBackmergeDraftAktivierung` bzw. `IsScmBackmergeAdminMerge`. | Anforderung erlaubt beides; Namen, die sichtbare Labels spiegeln, folgen der bestehenden ComboBox-Konvention („Update-Modus" u. a.). Umlaut-freie Schreibweise wie bisher. |
| Manueller Prüfpfad | `ScmEventQueueViewModel.RetrySourcesCommand` ruft nach `ScmEventMonitoringService.RunOnceAsync` zusätzlich `ScmBackmergeMonitoringService.RunOnceAsync` (innerhalb der bestehenden Monitoring-aktiviert-Prüfung; der Backmerge-Dienst prüft seinen Modus selbst). | Erhält das heutige Nutzerverhalten: „Quellen erneut prüfen" löste bisher implizit auch die Backmerge-Bewertung aus. Die Checkpoint-Rücksetzung (`NextAttemptUtc = null` für alle Schlüssel) bleibt unverändert und gilt jetzt auch für die `backmerge-*`-Checkpoints. |
| UI-Validierung | `ValidiereScmTriggerKeyword`, `ValidiereScmTaskCompletionWait` und `ValidiereScmKontoModus` bleiben unverändert und unbedingt wirksam; neu: Null-Prüfung der „Prüfmodus"-ComboBox beim Speichern. Die beiden Backmerge-Checkboxen benötigen keine Validierung (bool). | Ausgeblendete Felder können über die UI keine ungültigen Werte annehmen (das Speichern wurde schon bisher bei ungültigem Wert verweigert) — die bestehende Validierung kann daher nicht „ins Leere" laufen. |

## Programmabläufe

### Einstellungen laden und speichern (Register „Automatisierung")

1. `SettingsViewModel.LadenAsync` liest zusätzlich `GetScmTaskCompletionPruefmodusAsync`, `GetScmBackmergeDraftAktivierungAsync` und `GetScmBackmergeAdminMergeAsync` und setzt `SelectedScmTaskCompletionPruefmodus` (via `ScmTaskCompletionPruefmodusOption.FuerModus`), `IsScmBackmergeDraftAktivierung` und `IsScmBackmergeAdminMerge`.
2. Die Schalter-/Modus-Properties (`IsScmMonitoringEnabled`, `IsScmTaskCompletionEnabled`, `SelectedKontoModus`, `SelectedBackmergeModus`) aktualisieren über Property-Changed-Kaskaden die abgeleiteten Sichtbarkeits-/Enabled-/Status-Properties.
3. `SettingsView.xaml` blendet „Trigger-Keyword" (`IsTriggerKeywordSichtbar`) und „Prompt-Anhänge" (`IsPromptAnhaengeSichtbar`) über `BoolToVisibilityConverter` ein/aus und deaktiviert „Überwachungsmodus", „Prüfmodus", „Wartezeit für Beendigung" und die beiden Merge-Optionen-Checkboxen über `IsEnabled`-Bindungen.
4. `SpeichernAsync` prüft `SelectedScmTaskCompletionPruefmodus` auf `null` (Fehlermeldung wie bei den übrigen ComboBoxen) und persistiert via `SetScmTaskCompletionPruefmodusAsync` sowie `SetBoolSettingAsync` für beide Merge-Optionen.

Beteiligte Klassen/Komponenten: `SettingsViewModel`, `SettingsView.xaml`, `AppEinstellungService`, `ScmTaskCompletionPruefmodusOption`, `BoolToVisibilityConverter`.

### Zyklische Backmerge-Prüfung (unabhängig)

1. `ScmBackmergeMonitoringService.RunOnceAsync`: `ScmEventStartupGate.WaitAsync` → `GetScmBackmergeModusAsync`; bei `Deaktiviert` kompletter Abbruch (keine Provider-Aufrufe).
2. Aktive `GitRepository`-Datensätze und SCM-Plugins laden; je `IScmEventSource`-Plugin je aktivem Repository mit `RepositoryUrl`: kanonische Auflösung via `GetEventRepositoryAsync` mit `ScmRepositoryContext(..., ScmKontoModus.Benutzerkonto)` — Auflösungsfehler werden je Alias im Checkpoint `backmerge-repository/...` mit Backoff (`ScmEventStore.RetryAt`) persistiert und isolieren nur dieses Repository.
3. Aufgelöste `ScmEventRepository`-Objekte deduplizieren (mehrere lokale Repositories können auf dasselbe Remote zeigen); zugeordnete lokale `GitRepository`-Listen je Remote sammeln (`lokaleRepositories` für die Kandidatenprüfung).
4. Je Remote: Enumeration via `EnumerateReferencesAsync` mit eigenem Watermark/Backoff-Checkpoint `backmerge-enumeration/...` (Dedup über `enumerated`-HashSet wie bisher).
5. `ScmBackmergeService.EvaluateDiscoveredReferencesAsync(plugin, repository, discovered, lokaleRepositories, ct)` aufrufen — in eigenem `try/catch`, damit ein Bewertungsfehler den erfolgreichen Enumerations-Checkpoint nicht zurücksetzt (Muster wie bisher in `ScmEventMonitoringService`, Zeilen 268–278).
6. Checkpoint schreiben/aktualisieren, `ScmEventChangeNotifier.Notify()`.

Beteiligte Klassen/Komponenten: `ScmBackmergeMonitoringService` (neu), `ScmBackmergeService`, `AppEinstellungService`, `IPluginManager`, `IScmEventSource`, `ScmObservationCheckpoint`, `ScmEventStore.RetryAt`, `ScmEventChangeNotifier`, `ScmEventStartupGate`.

### Backmerge-Bewertung mit Merge-Robustheit

1. `EvaluateDiscoveredReferencesAsync` liest einmalig `GetScmBackmergeModusAsync`, `GetScmBackmergeDraftAktivierungAsync` und `GetScmBackmergeAdminMergeAsync` und reicht beide Options-Flags an `BewertePullRequestAsync` weiter (keine Einstellungslesezugriffe je PR).
2. `BewertePullRequestAsync`: `GetPullRequestStatusAsync` → nicht `Open`: `Entscheide` (unverändert). Hauptbranch-Match je Kandidat (unverändert).
3. **Neu — Draft-Behandlung:** Ist `status.IsDraft`, gilt: Option `scm.backmerge.draft_activation` deaktiviert → `WaitingForMergeable`-Retry mit Diagnostic-Hinweis, dass der PR ein Draft ist (bisheriges Verhalten). Option aktiviert → `plugin.MarkPullRequestReadyAsync(repositoryId, nummer, ct)`; bei Erfolg Status via `GetPullRequestStatusAsync` erneut laden und mit der Mergebarkeitsprüfung fortfahren; bei Misserfolg (`!Success`) → `Failures++` + `Temporary`-Backoff mit Diagnostic „Draft-Aktivierung fehlgeschlagen: …".
4. `status.MergeStatus != Mergeable` → `WaitingForMergeable` + Retry (unverändert).
5. **Neu — Merge mit Admin-Fallback:** Erster Aufruf `plugin.CompletePullRequestAsync` mit `PullRequestCompletionOptions(Merge, Merge, AllowProtectedBranchBypass: false)`. Bei `result.Success && result.PullRequestMerged` → `Merged`/`Decided` (unverändert). Andernfalls: Option `scm.backmerge.admin_merge` aktiviert → zweiter Aufruf mit `AllowProtectedBranchBypass: true` (`gh pr merge --admin`); das Ergebnis des zweiten Aufrufs bestimmt den Checkpoint. Option deaktiviert → bisheriger Fehlerpfad des ersten Ergebnisses (`Blocked`/`Temporary`-Backoff).

Beteiligte Klassen/Komponenten: `ScmBackmergeService`, `IGitPlugin`/`GitPluginBase` (`CompletePullRequestAsync`, neu `MarkPullRequestReadyAsync`), `GitHubPlugin`, `PullRequestStatusInfo.IsDraft` (neu), `PullRequestCompletionOptions`, `AppEinstellungService`.

### Zyklische Ereignisüberwachung (verschlankt)

1. `ScmEventMonitoringService.RunOnceAsync` bleibt unverändert (Schalter `scm.monitoring.enabled`, Trigger-Keyword, `kontoModus`, Inbox-/Enumerations-Pfad, Inbox-Commits, Dispatch-Runde).
2. `EnumerateRepositoryAsync` verliert die Parameter `lokaleRepositories` und `backmerge` sowie den `ScmBackmergeService.EvaluateDiscoveredReferencesAsync`-Aufruf; `localRepositories` in `RunOnceAsync` bleibt für die Aufgabenzuordnung (`ScmEventTaskResolver.ResolveAsync`) bestehen.

Beteiligte Klassen/Komponenten: `ScmEventMonitoringService`, `ScmBackmergeService` (Aufruf entfernt).

### Zyklische Abschlussprüfung mit Prüfmodus

1. `ScmTaskCompletionMonitoringService.RunOnceAsync`: `startup.WaitAsync` → `GetScmTaskCompletionEnabledAsync` → `GetScmTaskCompletionWaitMinutesAsync` → neu `GetScmTaskCompletionPruefmodusAsync`.
2. Kandidatenabfrage modusabhängig (Status `!= Beendet && != Archiviert` bleibt unverändert):
   - `Ereignisgesteuert`: **und** mindestens ein `ScmIncomingEvent` (bisheriges Kriterium) **und** `(IssueReferenz != null || PullRequests.Any())`.
   - `AlleAufgaben`: **und** `(IssueReferenz != null || PullRequests.Any())`.
3. `CheckTaskAsync` unverändert (laufende Ereignisverarbeitung blockiert, Countdown über `ScmAbschlussErkanntUtc`, Abschluss unter `ScmTaskCoordinator.Shared` via `EntwicklungsprozessService.AbschliessenAsync`).
4. `DetermineClosureAsync` bleibt fachlich erhalten: **PR-Vorrang-Pfad** (`task.PullRequests.Count > 0`: alle PRs müssen `Closed`/`Merged` sein, inkl. `PullRequestReferenz`-Statusaktualisierung und `ResolvePlugin`/`PullRequestMonitoringPolicy`-Prüfung) hat Vorrang; **Issue-Pfad** (nur ohne PRs): `IssueReferenz.IssueNummer > 0 && GitRepository != null` → `GetEventRepositoryAsync` mit **`ScmKontoModus.Benutzerkonto` (neu — kanonisch statt `GetScmKontoModusAsync`)** → `ReadIssueAsync` → `IstGeschlossen` → Label `Issue #N` oder `null`.

Beteiligte Klassen/Komponenten: `ScmTaskCompletionMonitoringService`, `AppEinstellungService`, `ScmTaskCompletionPruefmodus`, `IScmEventSource`, `EntwicklungsprozessService`, `ScmTaskCoordinator`.

### Manuelles „Quellen erneut prüfen"

1. `ScmEventQueueViewModel.RetrySourcesCommand`: Monitoring-aktiviert-Prüfung (Meldungstext jetzt „… Einstellungen → Automatisierung → Aufgaben ausführen") → `NextAttemptUtc`-Rücksetzung aller Checkpoints → `ScmEventMonitoringService.RunOnceAsync` → neu `ScmBackmergeMonitoringService.RunOnceAsync` (prüft selbst `scm.backmerge.mode`).
2. `MonitoringNotice`-Text in `RefreshAsync` auf den neuen Pfad angepasst.

Beteiligte Klassen/Komponenten: `ScmEventQueueViewModel`, `ScmEventMonitoringService`, `ScmBackmergeMonitoringService`, `AppEinstellungService`.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `ScmTaskCompletionPruefmodus` (`src/Softwareschmiede/Domain/Enums/ScmTaskCompletionPruefmodus.cs`) | Enum | Prüfmodus der Abschlussüberwachung: `Ereignisgesteuert = 0` (nur Aufgaben mit `ScmIncomingEvent`, bisheriges Verhalten/Fallback), `AlleAufgaben = 1` (alle nicht beendeten/archivierten Aufgaben mit SCM-Referenz). |
| `ScmTaskCompletionPruefmodusTexte` / `ScmTaskCompletionPruefmodusOption` (`src/Softwareschmiede.App/ViewModels/ScmTaskCompletionPruefmodusOptionen.cs`) | `static class` + `record` | Anzeige-Texte („Nur ereignisgesteuerte Aufgaben", „Alle Aufgaben") und Options-Record mit `Alle`/`FuerModus` — exaktes Muster von `ScmBackmergeModusOptionen.cs`. |
| `ScmBackmergeMonitoringService` (`src/Softwareschmiede/Application/Services/ScmBackmergeMonitoringService.cs`) | Klasse (`sealed`, `: PeriodicBackgroundService`) | Eigenständiger zyklischer Prüfpfad für Backmerge-PRs: Benutzerkonto-Auflösung, eigene Checkpoints (`backmerge-repository/`, `backmerge-enumeration/`), Aufruf von `ScmBackmergeService.EvaluateDiscoveredReferencesAsync`; Fehlermeldung „SCM-Backmerge-Prüfung fehlgeschlagen." |

## Änderungen an bestehenden Klassen

### `AppEinstellungService` (Service)

- **Neue Konstanten:**
  - `ScmTaskCompletionPruefmodusKey = "scm.taskcompletion.scope"` (`public const string`) — Schlüssel des Prüfmodus der Abschlussüberwachung.
  - `ScmBackmergeDraftAktivierungKey = "scm.backmerge.draft_activation"` (`public const string`) — Opt-in: Draft-Backmerge-PRs vor dem Merge automatisch aktivieren.
  - `ScmBackmergeAdminMergeKey = "scm.backmerge.admin_merge"` (`public const string`) — Opt-in: Admin-Merge-Fallback bei blockiertem regulärem Merge.
- **Neue Methoden:**
  - `GetScmTaskCompletionPruefmodusAsync(ct)` → `Task<ScmTaskCompletionPruefmodus>` — `int.TryParse` + `Enum.IsDefined`; Fallback `Ereignisgesteuert` bei fehlendem/ungültigem Wert (Muster `GetScmBackmergeModusAsync`, Zeilen 190–196).
  - `SetScmTaskCompletionPruefmodusAsync(modus, ct)` → `Task` — `Enum.IsDefined`-Prüfung, wirft `ArgumentOutOfRangeException` (Muster `SetScmBackmergeModusAsync`, Zeilen 202–208).
  - `GetScmBackmergeDraftAktivierungAsync(ct)` / `GetScmBackmergeAdminMergeAsync(ct)` → `Task<bool>` — `GetBoolSettingAsync ?? false` (Muster `GetBackmergeProjektAktivAsync`).
  - `SetScmBackmergeDraftAktivierungAsync(wert, ct)` / `SetScmBackmergeAdminMergeAsync(wert, ct)` → `Task` — `SetBoolSettingAsync`-Persistierung.

### `ScmEventMonitoringService` (Service)

- **Geänderte Methoden:**
  - `EnumerateRepositoryAsync` — Parameter `lokaleRepositories` und `backmerge` entfallen; der `EvaluateDiscoveredReferencesAsync`-Aufruf samt Fehlerbehandlung (Zeilen 268–278) entfällt; Aufrufstelle in `RunOnceAsync` (Zeilen 146–151) übergibt `ScmBackmergeService` nicht mehr.
- **Kommentare:** Laufzeit-Schalter-Kommentar (Zeile 35) auf neuen Pfad „Einstellungen → Automatisierung → Aufgaben ausführen" aktualisieren.

### `ScmTaskCompletionMonitoringService` (Service)

- **Geänderte Methoden:**
  - `RunOnceAsync` — liest zusätzlich `GetScmTaskCompletionPruefmodusAsync`; Kandidatenabfrage (Zeilen 46–50) modusabhängig (`Ereignisgesteuert`: `ScmIncomingEvent`-Kriterium + `(IssueReferenz != null || PullRequests.Any())`; `AlleAufgaben`: nur `(IssueReferenz != null || PullRequests.Any())`) plus unverändertem Statusfilter.
  - `DetermineClosureAsync` — **PR-Vorrang-Pfad (Zeilen 149–184) bleibt vollständig erhalten** (inkl. `PullRequestReferenz`-Statusaktualisierung, `ResolvePlugin`, `PullRequestMonitoringPolicy.CanMonitor`); im Issue-Pfad (Zeilen 185–199) ersetzt `ScmKontoModus.Benutzerkonto` den Aufruf `GetScmKontoModusAsync` (kanonisches Benutzerkonto).
- **Kommentare:** Klassen-Summary (ereignisgesteuert → beide Modi, SCM-Referenz-Regel) und Schalter-Kommentar (Zeile 34, neuer Einstellungspfad) aktualisieren.

### `ScmBackmergeService` (Service)

- **Geänderte Methoden:**
  - `EvaluateDiscoveredReferencesAsync` — liest zusätzlich `GetScmBackmergeDraftAktivierungAsync` und `GetScmBackmergeAdminMergeAsync` einmalig neben `GetScmBackmergeModusAsync` und reicht beide Flags an `BewertePullRequestAsync` weiter.
  - `BewertePullRequestAsync` — zwei neue Schritte (siehe Programmablauf „Backmerge-Bewertung mit Merge-Robustheit"): (a) Draft-Behandlung über `status.IsDraft` + `MarkPullRequestReadyAsync` bei aktivierter Option; (b) Mergeversuch mit `AllowProtectedBranchBypass: false`, Admin-Zweitversuch (`true`) nur bei Misserfolg + aktivierter Option. Klassen-/Methoden-Summary anpassen („Admin-Merge" → „Merge mit optionalem Admin-Fallback und optionaler Draft-Aktivierung").

### `PullRequestStatusInfo` (Contracts-Record, `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/`)

- **Neues Member:** `IsDraft` (`bool`, Default `false`) — als letztes positionales Record-Member angehängt, damit alle bestehenden Konstruktor-Aufrufe quellkompatibel bleiben. Zweck: explizite Unterscheidung von Draft-PRs (`mergeStateStatus` `DRAFT` wird weiterhin auf `PullRequestMergeStatus.Blocked` abgebildet).

### `IGitPlugin` / `GitPluginBase` (Contracts-Interface/-Basisklasse)

- **Neue Methode:** `MarkPullRequestReadyAsync(repositoryId, pullRequestNumber, ct)` → `Task<PullRequestCompletionResult>` — markiert einen Draft-PR als bereit („ready for review"). Default in `IGitPlugin` und `GitPluginBase`: `PullRequestCompletionResult.Failed("…nicht unterstützt")` (Muster `CompletePullRequestAsync`, Zeilen 85–90 bzw. 57–62).

### `GitHubPlugin` (Plugin)

- **Geänderte Methoden:**
  - `GetPullRequestStatusAsync` — GraphQL-Query um das Feld `isDraft` ergänzen; `ParsePullRequestStatus` liest `isDraft` und befüllt `PullRequestStatusInfo.IsDraft`.
- **Neue Methoden:**
  - `MarkPullRequestReadyAsync` — `gh pr ready <nummer> --repo <repo>` über `_cliRunner.RunAsync("gh", ...)`; Fehlerbehandlung analog `CompletePullRequestAsync` (`Blocked` bei Auth-/Protection-Fehlern, sonst `Failed`); bei Erfolg `PullRequestCompletionResult.Approved(...)` bzw. äquivalentes Erfolgs-Result.

### `SettingsViewModel` (ViewModel)

- **Neue Eigenschaften:**
  - `ScmTaskCompletionPruefmodusOptionen` (`IReadOnlyList<ScmTaskCompletionPruefmodusOption>`) — statische Optionsliste.
  - `SelectedScmTaskCompletionPruefmodus` (`ScmTaskCompletionPruefmodusOption?`) — ComboBox-Bindung, Standard `FuerModus(Ereignisgesteuert)`.
  - `IsScmBackmergeDraftAktivierung` (`bool`) — Checkbox „Draft-Pull-Requests vor dem Merge automatisch aktivieren".
  - `IsScmBackmergeAdminMerge` (`bool`) — Checkbox „Bei blockiertem Merge Admin-Merge erlauben".
  - `IsUeberwachungsmodusEnabled` (`bool`, nur Getter) — `IsScmMonitoringEnabled`.
  - `IsTriggerKeywordSichtbar` (`bool`, nur Getter) — `IsScmMonitoringEnabled && SelectedKontoModus?.Modus == ScmKontoModus.Benutzerkonto`.
  - `IsPromptAnhaengeSichtbar` (`bool`, nur Getter) — `IsScmMonitoringEnabled`.
  - `IsTaskCompletionFelderEnabled` (`bool`, nur Getter) — `IsScmTaskCompletionEnabled`.
  - `IsProzessablaeufeFelderEnabled` (`bool`, nur Getter) — `SelectedBackmergeModus?.Modus != ScmBackmergeModus.Deaktiviert`.
  - `AufgabenAusfuehrenStatusText`, `AufgabenBeendenStatusText`, `ProzessablaeufeStatusText` (`string`, nur Getter) — „aktiviert"/„deaktiviert" je nach Schalter bzw. `SelectedBackmergeModus?.Modus != Deaktiviert`.
- **Geänderte Methoden:**
  - Setter von `IsScmMonitoringEnabled` — löst `OnPropertyChanged` für `IsUeberwachungsmodusEnabled`, `IsTriggerKeywordSichtbar`, `IsPromptAnhaengeSichtbar`, `AufgabenAusfuehrenStatusText` aus.
  - Setter von `IsScmTaskCompletionEnabled` — löst `OnPropertyChanged` für `IsTaskCompletionFelderEnabled`, `AufgabenBeendenStatusText` aus.
  - Setter von `SelectedKontoModus` — löst `OnPropertyChanged` für `IsTriggerKeywordSichtbar` aus.
  - Setter von `SelectedBackmergeModus` — löst `OnPropertyChanged` für `ProzessablaeufeStatusText`, `IsProzessablaeufeFelderEnabled` aus.
  - `LadenAsync` — lädt `GetScmTaskCompletionPruefmodusAsync` → `SelectedScmTaskCompletionPruefmodus` sowie beide Merge-Optionen → `IsScmBackmergeDraftAktivierung`/`IsScmBackmergeAdminMerge` (nach `SelectedKontoModus`/`SelectedBackmergeModus`, ~Zeile 468).
  - `SpeichernAsync` — neue Null-Prüfung für `SelectedScmTaskCompletionPruefmodus` (Fehlermeldung analog „Der ausgewählte Prüfmodus ist ungültig.") und Persistierung via `SetScmTaskCompletionPruefmodusAsync` sowie `SetBoolSettingAsync` für beide Merge-Optionsschlüssel (nach den übrigen SCM-Schlüsseln, ~Zeile 555).
  - Nutzerseitige „Konto-Modus"-Texte an das neue Label „Überwachungsmodus" angleichen: `FehlerMeldung` „Der ausgewählte Konto-Modus ist ungültig." (~Zeile 520) und die `ValidiereScmKontoModus`-Meldung (~Zeile 798). Property-Namen (`SelectedKontoModus`, `KontoModusOptionen`) bleiben unverändert.

### `SettingsView.xaml` (View)

- Neues `TabItem` „Automatisierung" direkt nach dem „Allgemein"-`TabItem` (vor „Plugins"), Header im etablierten `StackPanel`/`Ellipse`/`TextBlock`-Muster.
- Der bisherige Abschnitt „SCM-Ereignisse" (Zeilen 279–429) wird aus „Allgemein" entfernt und auf drei `Expander`-Panels im neuen Register aufgeteilt (Muster: `ProjectDetailView.xaml` „BeendeteAufgabenExpander" mit `AutomationProperties.Name` und `Foreground`-Binding):
  - `AufgabenAusfuehrenExpander` („Aufgaben ausführen"): `CheckBox IsScmMonitoringEnabled` (Content „Aufgaben automatisch starten"), Beschreibung, „Überwachungsmodus"-ComboBox (`AutomationProperties.Name="Ueberwachungsmodus"`, `IsEnabled={Binding IsUeberwachungsmodusEnabled}`), „Trigger-Keyword"-Block als Container `TriggerKeywordBereich` (`Visibility={Binding IsTriggerKeywordSichtbar, Converter=BoolToVisibilityConverter}`), „Prompt-Anhänge"-Block als Container `PromptAnhaengeBereich` (`Visibility={Binding IsPromptAnhaengeSichtbar, Converter=BoolToVisibilityConverter}`).
  - `AufgabenBeendenExpander` („Aufgaben beenden"): `CheckBox IsScmTaskCompletionEnabled` (Content „Aufgaben automatisch beenden"), Beschreibung (angepasst auf beide Modi + SCM-Referenz-Regel), „Prüfmodus"-ComboBox (`AutomationProperties.Name="Pruefmodus"`, `ItemsSource={Binding ScmTaskCompletionPruefmodusOptionen}`, `SelectedItem={Binding SelectedScmTaskCompletionPruefmodus}`, `IsEnabled={Binding IsTaskCompletionFelderEnabled}`), „Wartezeit für Beendigung"-Label + TextBox `ScmTaskCompletionWaitMinutes` (`IsEnabled={Binding IsTaskCompletionFelderEnabled}`).
  - `ProzessablaeufeExpander` („Prozessabläufe"): „Backmerge-Pull-Requests"-ComboBox `Backmerge-Modus` (unverändert), die beiden neuen Checkboxen `IsScmBackmergeDraftAktivierung` und `IsScmBackmergeAdminMerge` (jeweils `IsEnabled={Binding IsProzessablaeufeFelderEnabled}`) sowie angepasster Beschreibungstext (läuft unabhängig von Überwachungsschalter und -modus, immer unter dem Benutzerkonto; Hinweis auf die beiden Opt-in-Optionen).
- Jeder `Expander.Header` ist ein horizontaler `StackPanel` aus Titel-`TextBlock` und Status-`TextBlock` (z. B. `AutomationProperties.Name="AufgabenAusfuehrenStatus"`, `Text={Binding AufgabenAusfuehrenStatusText}`).
- Beschreibungstext unter „Überwachungsmodus" (Zeile 362): der Satz „… Backmerge-Pull-Requests werden dann nicht automatisch entdeckt" entfällt ersatzlos (Entkopplung).
- Beschreibungstexte der beiden Schalter-Checkboxen werden an die neuen Funktionsbezeichnungen angepasst.

### `ScmEventQueueViewModel` (ViewModel)

- **Geänderte Methoden:**
  - `RetrySourcesCommand` — Meldungstext auf „Einstellungen → Automatisierung → Aufgaben ausführen"; nach `ScmEventMonitoringService.RunOnceAsync` zusätzlich `ScmBackmergeMonitoringService.RunOnceAsync` aufrufen (Zeile 57).
  - `RefreshAsync` — `MonitoringNotice`-Text (Zeile 195) auf den neuen Einstellungspfad anpassen.

### `App` (`App.xaml.cs`, DI)

- **Neue Registrierung** (im SCM-Block, nach Zeile 298):
  `services.AddSingleton<ScmBackmergeMonitoringService>();`
  `services.AddHostedService(sp => sp.GetRequiredService<ScmBackmergeMonitoringService>());`
  `ScmBackmergeService` bleibt `AddScoped` (wird innerhalb der Scopes des neuen Dienstes aufgelöst).

### `ScmDispatchFixture` (Testinfrastruktur, `src/Softwareschmiede.Tests/Helpers/`)

- **Neue Registrierung:** `services.AddSingleton<ScmBackmergeMonitoringService>();` — Grundlage aller SCM-Integrationstests (in `Softwareschmiede.Tests` und via `<Compile Include>` in `Softwareschmiede.IntegrationTests`).

## Datenbankmigrationen

Keine — der neue Prüfmodus und beide Merge-Optionen werden wie alle übrigen Schlüssel in der bestehenden `AppEinstellung`-Key-Value-Tabelle gespeichert.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `SelectedScmTaskCompletionPruefmodus` | Darf beim Speichern nicht `null` sein (analog `SelectedBackmergeModus`/`SelectedKontoModus`). | `FehlerMeldung = "Der ausgewählte Prüfmodus ist ungültig."`, Abbruch ohne Persistierung. |
| `SetScmTaskCompletionPruefmodusAsync(modus)` | `Enum.IsDefined(modus)` muss gelten. | `ArgumentOutOfRangeException` (Muster `SetScmBackmergeModusAsync`). |
| `scm.taskcompletion.scope` (gelesen) | Fehlende oder nicht parse-/enum-definierte Werte fallen auf `Ereignisgesteuert` zurück. | Kein Fehler — Fallback (Muster `GetScmKontoModusAsync`). |
| `scm.backmerge.draft_activation` / `scm.backmerge.admin_merge` (gelesen) | Fehlende/nicht parsebare Werte fallen auf `false` zurück; bool-Checkboxen benötigen keine Eingabevalidierung. | Kein Fehler — Fallback `false`. |
| `MarkPullRequestReadyAsync` (Plugin ohne Implementierung) | Default liefert `Failed("nicht unterstützt")`. | `ScmBackmergeService` behandelt es wie einen temporären Fehler (`Failures++` + Backoff), kein Crash. |
| Bestehende Regeln | `ValidiereScmTriggerKeyword`, `ValidiereScmTaskCompletionWait`, `ValidiereScmKontoModus` bleiben unverändert (siehe Designentscheidungen). | — |

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `scm.taskcompletion.scope` (`AppEinstellungService.ScmTaskCompletionPruefmodusKey`) | int-Enum (`ScmTaskCompletionPruefmodus`) | `Ereignisgesteuert` (0) | Prüfmodus der Abschlussüberwachung: nur ereignisgesteuerte vs. alle nicht beendeten/archivierten Aufgaben mit SCM-Referenz. |
| `scm.backmerge.draft_activation` (`AppEinstellungService.ScmBackmergeDraftAktivierungKey`) | bool | `false` | Opt-in: Draft-Backmerge-PRs vor dem Merge automatisch als bereit markieren (`gh pr ready`). |
| `scm.backmerge.admin_merge` (`AppEinstellungService.ScmBackmergeAdminMergeKey`) | bool | `false` | Opt-in: bei blockiertem regulärem Merge einen Admin-Merge (`gh pr merge --admin`) als Fallback versuchen. |

## Seiteneffekte und Risiken

- **Admin-Merge wird Opt-in (Verhaltensänderung):** Bisher wurde der Backmerge-Merge unbedingt mit `AllowProtectedBranchBypass: true` (`gh pr merge --admin`) ausgeführt; künftig ist der erste Versuch ein regulärer Merge. Auf Repositories mit Branch-Protection-/Check-Anforderungen scheitern Backmerges ohne aktivierte Option `scm.backmerge.admin_merge` dauerhaft im `Blocked`/Retry-Pfad — gewollt (Scope-Ausschluss 3); betroffene Anwender müssen die Option einmalig aktivieren.
- **Draft-Backmerge-PRs:** Ohne `scm.backmerge.draft_activation` bleiben Draft-PRs im `WaitingForMergeable`-Retry (bisheriges Verhalten, jetzt mit klarer Diagnostic). Mit Option wird der PR via `gh pr ready` aktiviert — danach greifen ggf. Review-/Check-Anforderungen des Providers; der Merge erfolgt erst, wenn der Provider `Mergeable` meldet (sichtbarer Zwischenschritt, kein Fehler).
- **Doppelte Enumeration:** Bei aktiver Ereignisüberwachung im `Benutzerkonto` läuft pro Intervall zusätzlich die Backmerge-Enumeration derselben Repositories (eigener Checkpoint-Namensraum, eigener Watermark) — bewusster Kompromiss zugunsten der Entkopplung (Scope-Ausschluss 1).
- **Issue-Prüfung unter `Benutzerkonto`:** Im Modus `SeparatesKonto` wird das Issue künftig mit dem Benutzer- statt dem Agent-Token gelesen. Liegt das Agent-Token vor, das Benutzer-Token aber nicht (oder ohne Issue-Leserecht), schlägt die Issue-Prüfung fehl, wo sie bisher gelang — mit `PullRequestMonitoringPolicy.CanMonitor == false` oder nicht monitorbaren Repositories ohnehin nur Issue-getriebene Abschlüsse betroffen; `CanMonitor`-PR-Pfad unverändert (Benutzer-Token).
- **Diagnoseoberfläche:** Fehler des neuen Dienstes landen als `backmerge-repository/`-/`backmerge-enumeration/`-Checkpoints in derselben `ScmObservationCheckpoints`-Tabelle und erscheinen damit in den „Integrationshinweisen" bzw. können den `MonitoringNotice`-Hinweis in der SCM-Ereignisübersicht auslösen — gewünschter Sichtbarkeitseffekt, gleiche Oberfläche wie bisher. Draft-/Merge-Fehler erscheinen weiterhin in den `backmerge/`-PR-Checkpoints.
- **Manuelles Prüfen:** `RetrySourcesCommand` löst künftig auch die Backmerge-Prüfung aus; die Sperre bei deaktivierter Ereignisüberwachung bleibt bestehen (die periodische Backmerge-Prüfung heilt sich ohnehin im nächsten Intervall selbst).
- **E2E-Fixture:** Die unter FlaUI gestartete App hostet den neuen Dienst ebenfalls (E2E-Pollintervall 300 ms); mit Standard `scm.backmerge.mode = Deaktiviert` verlässt `RunOnceAsync` sofort — kein Einfluss auf bestehende E2E-Szenarien.
- **`PullRequestStatusInfo`-Erweiterung:** Das neue Member `IsDraft` wird mit Default `false` angehängt — quellkompatibel für alle anderen Erzeuger/Leser (`PullRequestMonitoringService`, `PullRequestReferenzService`, Tests). Plugins, die `isDraft` nicht liefern, melden `false` → Drafts werden dort wie bisher als `Blocked` behandelt.
- **Dokumentation (Folgeschritt):** `docs/help/einstellungen/beschreibung.md`, `docs/help/einstellungen/datenmodell.md`, `docs/help/plugins/github-ereignisse/*` (Backmerge-Einschränkung im separaten Modus entfernen, Merge-Robustheits-Optionen beschreiben), `README.md`, `docs/RELEASE_NOTES.md` werden im Dokumentationsschritt angepasst.

## Umsetzungsreihenfolge

1. **`ScmTaskCompletionPruefmodus`-Enum anlegen** (`src/Softwareschmiede/Domain/Enums/`)
   - Voraussetzungen: Keine.
   - Beschreibung: Enum mit `Ereignisgesteuert = 0`, `AlleAufgaben = 1` samt XML-Dokumentation (Konvention `ScmBackmergeModus`).

2. **`AppEinstellungService` um Prüfmodus- und Merge-Options-Schlüssel erweitern**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: Konstanten `ScmTaskCompletionPruefmodusKey = "scm.taskcompletion.scope"`, `ScmBackmergeDraftAktivierungKey = "scm.backmerge.draft_activation"`, `ScmBackmergeAdminMergeKey = "scm.backmerge.admin_merge"`; Getter/Setter nach den Mustern `Get/SetScmBackmergeModusAsync` (Enum) bzw. `GetBoolSettingAsync ?? false` / `SetBoolSettingAsync` (bool).

3. **Contracts erweitern: `PullRequestStatusInfo.IsDraft` + `MarkPullRequestReadyAsync`**
   - Voraussetzungen: Keine.
   - Beschreibung: `PullRequestStatusInfo` um angehängtes `bool IsDraft = false` ergänzen; `IGitPlugin.MarkPullRequestReadyAsync` + `GitPluginBase`-Default (`Failed("nicht unterstützt")`) deklarieren.

4. **`GitHubPlugin`: `isDraft` lesen und `gh pr ready` implementieren**
   - Voraussetzungen: Schritt 3.
   - Beschreibung: GraphQL-Statusabfrage um `isDraft` ergänzen, `ParsePullRequestStatus` befüllt `IsDraft`; `MarkPullRequestReadyAsync` via `_cliRunner.RunAsync("gh", ["pr","ready",nummer,"--repo",repo], ...)` mit `Blocked`/`Failed`-Fehlerbehandlung analog `CompletePullRequestAsync`.

5. **`ScmBackmergeMonitoringService` anlegen und registrieren**
   - Voraussetzungen: `PeriodicBackgroundService`, `ScmBackmergeService`, `ScmObservationCheckpoint`, `ScmEventStore.RetryAt`, `ScmEventStartupGate`, `ScmEventChangeNotifier` (alle vorhanden).
   - Beschreibung: Neuer Dienst nach dem Muster `ScmEventMonitoringService` (Scope-Erzeugung via `IServiceScopeFactory`, `MatchesPlugin`, per-Repo-Fehlerisolation mit `backmerge-repository/`- bzw. `backmerge-enumeration/`-Checkpoints, `enumerated`-Dedup); DI-Registrierung `AddSingleton` + `AddHostedService` in `App.xaml.cs` sowie `AddSingleton` in `ScmDispatchFixture`.

6. **`ScmEventMonitoringService` vom Backmerge-Aufruf entkoppeln**
   - Voraussetzungen: Schritt 5 (der neue Dienst muss die Bewertung übernehmen, bevor die alte Einbettung entfällt).
   - Beschreibung: `backmerge`-/`lokaleRepositories`-Parameter und `EvaluateDiscoveredReferencesAsync`-Aufruf aus `EnumerateRepositoryAsync` entfernen; Kommentar-Pfad aktualisieren.

7. **`ScmBackmergeService` um Draft-Aktivierung und Admin-Fallback erweitern**
   - Voraussetzungen: Schritte 2–4.
   - Beschreibung: Options-Flags in `EvaluateDiscoveredReferencesAsync` lesen und weiterreichen; in `BewertePullRequestAsync` Draft-Behandlung und Zwei-Versuche-Merge ergänzen; Summary-Kommentare anpassen.

8. **`ScmTaskCompletionMonitoringService` umbauen**
   - Voraussetzungen: Schritt 2.
   - Beschreibung: Prüfmodus lesen; Kandidatenabfrage modusabhängig mit `(IssueReferenz != null || PullRequests.Any())`; Issue-Pfad in `DetermineClosureAsync` auf `ScmKontoModus.Benutzerkonto` umstellen (PR-Pfad unverändert); Kommentare aktualisieren.

9. **`ScmEventQueueViewModel` anpassen**
   - Voraussetzungen: Schritt 5.
   - Beschreibung: `ScmBackmergeMonitoringService.RunOnceAsync` im `RetrySourcesCommand` ergänzen; beide Textverweise auf „Einstellungen → Allgemein → SCM-Ereignisse" durch „Einstellungen → Automatisierung → Aufgaben ausführen" ersetzen.

10. **`ScmTaskCompletionPruefmodusOptionen` anlegen + `SettingsViewModel` erweitern**
    - Voraussetzungen: Schritte 1 + 2.
    - Beschreibung: Optionsklasse nach `ScmBackmergeModusOptionen`-Muster; neue Properties (Prüfmodus, beide Merge-Optionen, abgeleitete Sichtbarkeits-/Enabled-/Status-Flags), Property-Changed-Kaskaden, Laden/Speichern/Null-Prüfung wie oben beschrieben.

11. **`SettingsView.xaml` umbauen**
    - Voraussetzungen: Schritt 10 (Bindungsziele müssen existieren).
    - Beschreibung: Neues `TabItem` „Automatisierung" nach „Allgemein"; SCM-Ereignisse-Abschnitt entfernen und auf drei `Expander` verteilen; neue Checkboxen im Prozessabläufe-Panel; Sichtbarkeits-/`IsEnabled`-Bindungen; neue Labels; Beschreibungstexte aktualisieren.

12. **Unit-/Integrationstests anpassen und ergänzen**
    - Voraussetzungen: Schritte 2–10.
    - Beschreibung: siehe Abschnitt „Tests" — neue Testdateien/-methoden, Migration der Backmerge-Integrationstests auf den neuen Dienst, Umbau des Kontomodus-Tests.

13. **E2E-Tests anpassen**
    - Voraussetzungen: Schritt 11.
    - Beschreibung: Page Object `E2E/Views/SettingsView.cs` (Selektoren + neue Hilfsmethoden), `E2E_SettingsFeatureFlags.cs` (Szenario umschreiben), `E2E_ScmEvents.cs` (Tab-Wechsel).

14. **Dokumentation aktualisieren (Folgeschritt)**
    - Voraussetzungen: Schritte 1–13.
    - Beschreibung: `docs/help/einstellungen/beschreibung.md` + `datenmodell.md` (neues Register, Panel-Struktur, Prüfmodus- und Merge-Options-Schlüssel), `docs/help/plugins/github-ereignisse/` (Backmerge-Einschränkung im separaten Modus entfernen, Draft-/Admin-Optionen und Entkopplung beschreiben), `README.md`, `docs/RELEASE_NOTES.md`.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `GetScmTaskCompletionPruefmodusAsync_WithoutEntry_DefaultsToEreignisgesteuert` | `AppEinstellungServiceTests_ScmTaskCompletionPruefmodus` (neu, `src/Softwareschmiede.Tests/Application/Services/`) | Fallback ohne Eintrag. |
| `GetScmTaskCompletionPruefmodusAsync_InvalidStoredValue_FallsBackToEreignisgesteuert` (Theory) | dto. | Ungültige Werte (`"kein-wert"`, `"99"`) → `Ereignisgesteuert`. |
| `SetScmTaskCompletionPruefmodusAsync_PersistedValue_IsHonored` (Theory: beide Modi) | dto. | Gespeicherter Modus wird gelesen. |
| `SetScmTaskCompletionPruefmodusAsync_UndefinedValue_Throws` | dto. | Undefinierter Enum-Wert → `ArgumentOutOfRangeException`. |
| `GetScmBackmergeMergeOptionen_DefaultToFalse_AndPersist` (Theory: beide Schlüssel) | `AppEinstellungServiceTests_ScmMonitoring` (bestehende Datei) bzw. dto. | `scm.backmerge.draft_activation`/`scm.backmerge.admin_merge` liefern `false` ohne Eintrag und geben gespeicherte Werte zurück. |
| `LadenCommand_LoadsScmTaskCompletionPruefmodusDefaults` | `SettingsViewModelTests` | Standard = „Nur ereignisgesteuerte Aufgaben". |
| `SpeichernCommand_PersistsScmTaskCompletionPruefmodus` | `SettingsViewModelTests` | Auswahl „Alle Aufgaben" wird persistiert. |
| `SpeichernCommand_RejectsMissingScmTaskCompletionPruefmodus` | `SettingsViewModelTests` | `null`-Auswahl → `FehlerMeldung`, keine Persistierung. |
| `SpeichernCommand_PersistsBackmergeMergeOptionen` | `SettingsViewModelTests` | Beide Checkboxen werden geladen (Default `false`) und persistiert. |
| `Sichtbarkeitsregeln_TriggerKeywordUndPromptAnhaenge` | `SettingsViewModelTests` | `IsTriggerKeywordSichtbar` nur bei aktivem Schalter + `Benutzerkonto`; `IsPromptAnhaengeSichtbar` nur bei aktivem Schalter; Kaskade bei Modus-/Schalterwechsel. |
| `Aktivierungsregeln_UeberwachungsmodusPruefmodusWartezeitUndMergeOptionen` | `SettingsViewModelTests` | `IsUeberwachungsmodusEnabled`/`IsTaskCompletionFelderEnabled` folgen den Schaltern; `IsProzessablaeufeFelderEnabled` folgt `SelectedBackmergeModus != Deaktiviert`. |
| `PanelStatusTexte_SpiegelnFunktionszustand` | `SettingsViewModelTests` | Drei Status-Properties liefern „aktiviert"/„deaktiviert" passend zu Schaltern und `SelectedBackmergeModus`. |
| `Monitor_PruefmodusAlleAufgaben_ManuellAngelegteAufgabeMitGeschlossenemIssue_WirdBeendet` | `ScmTaskCompletionMonitoringServiceTests` (Integration) | Ohne `ScmIncomingEvent`: geschlossenes Issue → Countdown → `Beendet` nach Wartezeit. |
| `Monitor_AufgabeOhneIssueUndOhnePullRequest_WirdNiemalsBeendet` (Theory: beide Prüfmodi) | dto. | Aufgabe mit `ScmIncomingEvent`, aber weder `IssueReferenz` noch `PullRequests` → kein Kandidat, weder Countdown noch Beendigung. |
| `Monitor_Backmerge_BeiDeaktiviertemMonitoring_WirdTrotzdemGemergt` | `ScmBackmergeMonitoringServiceTests` (neu; migriert aus `ScmEventMonitoringServiceTests_Backmerge`) | `scm.monitoring.enabled = false`, Modus `Global` → `ScmBackmergeMonitoringService.RunOnceAsync` mergt trotzdem. |
| `Monitor_Backmerge_ImSeparatesKonto_LaeuftUnterBenutzerkonto` | dto. | `ScmKontoModus.SeparatesKonto` gespeichert → `GetEventRepositoryAsync` wird mit `context.Konto == Benutzerkonto` aufgerufen, Merge läuft. |
| `BewertePullRequest_DraftOhneOption_WartetAufMergebarkeit` | `ScmBackmergeServiceTests` (bestehend, `src/Softwareschmiede.Tests/Application/Services/`) | `IsDraft = true`, Option aus → `WaitingForMergeable`-Retry; `MarkPullRequestReadyAsync` + `CompletePullRequestAsync` → `Times.Never`. |
| `BewertePullRequest_DraftMitOption_AktiviertUndMerged` | dto. | `IsDraft = true`, Option an → `MarkPullRequestReadyAsync` aufgerufen, Status-Re-Fetch, dann regulärer Merge. |
| `BewertePullRequest_DraftAktivierungFehlgeschlagen_TemporaryBackoff` | dto. | `MarkPullRequestReadyAsync` → `Failed` → `Failures++`, `Temporary`-Backoff, kein Mergeversuch. |
| `BewertePullRequest_MergeFehlgeschlagenMitAdminOption_AdminFallback` | dto. | Erster Merge (`AllowProtectedBranchBypass: false`) → `Failed`/`Blocked`; zweiter Aufruf mit `true` → `Merged` → Checkpoint `Merged`. |
| `BewertePullRequest_MergeFehlgeschlagenOhneAdminOption_NurRegulaererVersuch` | dto. | Fehlschlag ohne Option → `CompletePullRequestAsync` nie mit `AllowProtectedBranchBypass: true` aufgerufen; `Blocked`/`Temporary`-Backoff. |
| `GetPullRequestStatusAsync_ParsedIsDraft` | `GitHubPluginTests` | GraphQL-Antwort mit `isDraft: true` → `PullRequestStatusInfo.IsDraft == true` (und `MergeStatus == Blocked` bei `DRAFT`). |
| `MarkPullRequestReadyAsync_RuftGhPrReady` | `GitHubPluginTests` | `gh`-Aufruf mit `pr ready <nummer> --repo <repo>`; Fehlerpfad → `Failed`/`Blocked`. |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `ScmEventMonitoringServiceTests_Backmerge` (alle 5 Methoden) | `Monitor(fixture)` muss `ScmBackmergeMonitoringService` auflösen (Registrierung über `ScmDispatchFixture`); Testklasse in `ScmBackmergeMonitoringServiceTests` umbenennen/verschieben, da der Evaluationspfad aus dem Ereignisdienst entfernt wird. `Monitor_BackmergeDisabled_NoStatusQueryNoMerge` kann zusätzlich `GetEventRepositoryAsync` → `Times.Never` assertieren. Merge-Verifies auf `AllowProtectedBranchBypass` an das neue Zwei-Versuche-Modell anpassen (`false` im Erstversuch; `true` nur bei gesetzter Option). |
| `CompletionService_PassesActiveKonto_ToRepositoryContext` (`ScmTaskCompletionMonitoringServiceTests`, Zeile 162) | Issue-Pfad läuft künftig kanonisch unter `Benutzerkonto`: Assertion `context.Konto == SeparatesKonto` → `Benutzerkonto` (bei gespeichertem `SeparatesKonto`); Test sinnvoll umbenennen (z. B. `CompletionService_PassesBenutzerkonto_ToRepositoryContext`), Summary-Kommentar anpassen. |
| `Monitor_MergedPullRequest_StopsCliAndCompletesTaskAfterWait`, `Monitor_MergedPullRequest_CancelsScheduledPromptOnComplete`, `Monitor_ReopenedPullRequest_ResetsCountdown`, `Monitor_OpenPullRequestOrPendingEvent_KeepTaskRunning` | **Keine Anpassung nötig** — der PR-basierte Abschlusspfad bleibt erhalten; diese Tests dokumentieren weiterhin das gewollte Verhalten (belegen zugleich den Zusatzkriterium-Pfad „Aufgabe ohne Issue, mit PR → Beendigung"). |
| `Monitor_DisabledAndManualTask_SkipsObservation` | Bleibt gültig; die manuell angelegte Aufgabe mit PR-Verknüpfung wird im Modus `Ereignisgesteuert` weiterhin nicht beobachtet (kein `ScmIncomingEvent`). Ergänzend kann der `AlleAufgaben`-Kontrast über die neuen Tests abgedeckt werden. |
| `ScmBackmergeServiceTests` (bestehende Merge-Assertions, u. a. Zeilen 71–73) | Bisher ein Aufruf mit `AllowProtectedBranchBypass: true`; künftig Erstversuch `false`. Assertions auf die Options-`PullRequestCompletionOptions` anpassen; neue Draft-/Admin-Fälle siehe „Neue Tests". |
| `GitHubPluginTests` (Status-Parsing-Tests) | `PullRequestStatusInfo`-Konstruktionen bleiben quellkompatibel (`IsDraft`-Default); Tests, die GraphQL-Fixtures spiegeln, können `isDraft` ergänzen. |
| `SettingsViewModelTests` (bestehende SCM-Tests) | Unverändert lauffähig; neue Tests oben ergänzen dieselbe Klasse. |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Register „Automatisierung" existiert nach „Allgemein"; drei `Expander`-Panels mit Titel + Statusanzeige; „Allgemein" enthält keinen SCM-Block mehr | `E2E_SettingsFeatureFlags.cs` (erweitertes Szenario, weiterhin in `General_BasisUndEinstellungen_E2E`) | Sichtbare Registerstruktur und Panel-Titelleisten | Nur via UI-Automation prüfbar (TabControl, Expander-Header, gebundene Texte). |
| Pflicht | Schalter aus → Überwachungsmodus deaktiviert, Trigger-Keyword + Prompt-Anhänge unsichtbar; Modus `Separates Konto` → Trigger-Keyword unsichtbar; Schalter „Aufgaben beenden" aus → Prüfmodus + Wartezeit deaktiviert; Backmerge-Modus `Deaktiviert` → beide Merge-Optionen-Checkboxen deaktiviert, Modus `Global` → aktiviert | dto. | Sichtbarkeits-/Aktivierungsregeln der Eingabefelder inkl. der neuen Checkboxen | `Visibility`/`IsEnabled`-Bindungen sind reines UI-Verhalten. |
| Pflicht | Prüfmodus „Alle Aufgaben" + beide Merge-Optionen aktivieren → Speichern → Auswahl bleibt nach erneutem Laden erhalten | dto. | Persistierung der neuen Einstellungsschlüssel über die UI | Happy Path des geänderten Benutzerflusses (Einstellen → Speichern → Wirksamkeit). |
| Pflicht | Expander auf-/zuklappbar; Titelleiste zeigt im zugeklappten Zustand „aktiviert"/„deaktiviert" | dto. | Panel-Verhalten inkl. Statusanzeige | UI-Interaktion (ExpandCollapse-Pattern). |

Keine neuen E2E-Testmethoden: alle vier Szenarien werden in das bestehende, konsolidierte `General_BasisUndEinstellungen_E2E`-Szenario eingearbeitet (Projektvorgabe: minimale FlaUI-Methodenzahl bei voller Abdeckung). Die Draft-/Admin-Merge-Logik selbst ist nicht über die UI erreichbar (Hintergrunddienst + externer Provider) und wird daher über Unit-/Integrationstests nachgewiesen.

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `Einstellungen_AllgemeinEnthaeltNurVorhandeneAbschnitte_E2E` (`E2E_SettingsFeatureFlags.cs`, aufgerufen aus `MainTest.General_BasisUndEinstellungen_E2E`) | Assertiert heute explizit das **Fehlen** von „Automatisierung" und das **Vorhandensein** von `IsScmMonitoringEnabled` auf „Allgemein" — beides ändert sich; wird zum konsolidierten Szenario „Allgemein + Automatisierung" umgebaut (keine neue Testmethode — Konsolidierungsvorgabe). |
| `BotModeAsync` in `E2E_ScmEvents.cs` (`ScmEventsE2ETests.ScmEvents_QueueReceptionDispatchAndReplies_UseRealPipeline`) | `SwitchTab("Allgemein")` vor `SetKontoModus`/`WaitForKontoModus` (Zeilen 356, 366) muss auf `SwitchTab("Automatisierung")` wechseln; Selektor „Konto-Modus" → „Ueberwachungsmodus". |
| Page Object `src/Softwareschmiede.Tests/E2E/Views/SettingsView.cs` | `GetKontoModus`/`SetKontoModus`/`WaitForKontoModus` auf neuen Selektor `Ueberwachungsmodus` umstellen; neue Hilfsmethoden für Prüfmodus-ComboBox, Schalter-Checkboxen, Merge-Optionen-Checkboxen, Expander-Status. |

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | Einstellungen: neues Register mit drei Panels (Happy Path) | Nutzer öffnet die Einstellungen | Frische Installation ohne gespeicherte SCM-Schlüssel (alle Defaults, beide Merge-Optionen `false`) | Register „Automatisierung" (zwischen „Allgemein" und „Plugins") wählen → drei Panels mit Status „aktiviert"/„deaktiviert" sichtbar → Panel „Aufgaben beenden": Prüfmodus „Alle Aufgaben" wählen → Panel „Prozessabläufe": Modus `Global` wählen (Checkboxen werden aktiviert) → beide Merge-Optionen ankreuzen → „Speichern" | Speicherbestätigung; beim erneuten Öffnen stehen „Alle Aufgaben", `Global` und beide aktivierten Checkboxen; Titelleiste des Panels zeigt den Schalterzustand | E2E-Szenario (konsolidiert in `General_BasisUndEinstellungen_E2E`) + `SettingsViewModelTests` |
| 2 | Backmerge unabhängig vom Monitoring-Schalter | Zyklischer Poll von `ScmBackmergeMonitoringService` | `scm.monitoring.enabled = false`, `scm.backmerge.mode = Global`, Repository mit Basisbranch `staging`, mergebarer PR `main → staging` beim Provider | Nächster Poll: Auflösung unter `Benutzerkonto` → Enumeration → `EvaluateDiscoveredReferencesAsync` → regulärer Merge | PR wird automatisch gemergt; `backmerge/...`-Checkpoint `Merged`/`long.MaxValue`; Ereignisüberwachung bleibt inaktiv | `ScmBackmergeMonitoringServiceTests.Monitor_Backmerge_BeiDeaktiviertemMonitoring_WirdTrotzdemGemergt` |
| 3 | Backmerge im Überwachungsmodus „Separates Konto" | Zyklischer Poll | `ScmKontoModus.SeparatesKonto` + `AgentToken` hinterlegt, Modus `Global`, mergebarer Backmerge-PR | Wie #2; `GetEventRepositoryAsync` erhält `ScmRepositoryContext` mit `Konto = Benutzerkonto` | Merge erfolgt trotz Inbox-Modus der Ereignisüberwachung (dokumentierte bisherige Einschränkung entfällt) | `ScmBackmergeMonitoringServiceTests.Monitor_Backmerge_ImSeparatesKonto_LaeuftUnterBenutzerkonto` |
| 4 | Draft-Backmerge mit und ohne Aktivierungsoption | Zyklischer Poll | Mergebarer Backmerge-PR mit `isDraft = true` (`mergeStateStatus` `DRAFT`) | Option aus: PR bleibt im `WaitingForMergeable`-Retry (Diagnostic nennt Draft). Option an: `MarkPullRequestReadyAsync` (`gh pr ready`) → Status-Re-Fetch → `Mergeable` → Merge | Ohne Option kein Mergeversuch; mit Option wird der PR aktiviert und gemergt | `ScmBackmergeServiceTests.BewertePullRequest_DraftOhneOption_...` / `..._DraftMitOption_AktiviertUndMerged` |
| 5 | Admin-Merge-Fallback bei Branch-Protection | Zyklischer Poll | Mergebarer Backmerge-PR; regulärer `gh pr merge` scheitert an Branch-Protection | Option aus: `Blocked`/`Temporary`-Backoff, kein `--admin`-Aufruf. Option an: zweiter Versuch mit `AllowProtectedBranchBypass: true` → `Merged` | Merge gelingt nur bei aktivierter Option | `ScmBackmergeServiceTests.BewertePullRequest_MergeFehlgeschlagenMitAdminOption_...` / `..._OhneAdminOption_...` |
| 6 | Abschluss im Modus „Alle Aufgaben" | Verknüpftes Issue geschlossen | Manuell angelegte Aufgabe (kein `ScmIncomingEvent`) mit `IssueReferenz` (ohne PRs), `scm.taskcompletion.scope = AlleAufgaben`, Schalter aktiv, evtl. laufende KI-Ausführung | Poll: Kandidat (Status ≠ Beendet/Archiviert + SCM-Referenz) → `DetermineClosureAsync` → Issue unter `Benutzerkonto` gelesen, geschlossen → `ScmAbschlussErkanntUtc` gesetzt → nach Wartezeit `AbschliessenAsync` unter `ScmTaskCoordinator`-Sperre | Protokolleintrag „wird nach Ablauf … automatisch beendet", danach `AufgabeStatus.Beendet`, laufende CLI beendet | `ScmTaskCompletionMonitoringServiceTests.Monitor_PruefmodusAlleAufgaben_..._WirdBeendet` |
| 7 | Aufgabe ohne Issue und ohne PR wird nie beendet | Zyklischer Poll | Aufgabe mit `ScmIncomingEvent`, aber weder `IssueReferenz` noch `PullRequests`; getestet in beiden Prüfmodi | Poll: Aufgabe ist kein Kandidat | Kein Countdown (`ScmAbschlussErkanntUtc` bleibt `null`), Status bleibt `Gestartet` | `Monitor_AufgabeOhneIssueUndOhnePullRequest_WirdNiemalsBeendet` (Theory) |
| 8 | Aufgabe ohne Issue, aber mit PR (Zusatzkriterium bleibt) | Zyklischer Poll | Ereignisgesteuerte Aufgabe ohne `IssueReferenz`, mit verknüpftem PR, der gemergt wird | Poll: PR-Pfad in `DetermineClosureAsync` → alle PRs `Merged` → Countdown → nach Wartezeit `AbschliessenAsync` | Aufgabe wird trotz fehlendem Issue über den PR-Pfad beendet | Bestehender Test `Monitor_MergedPullRequest_StopsCliAndCompletesTaskAfterWait` (unverändert) |
| 9 | Laufzeitschalter ohne Neustart | Nutzer deaktiviert „Aufgaben automatisch beenden" und speichert | Aktive Abschlussüberwachung, ggf. gesetzter `ScmAbschlussErkanntUtc` | Nächste Polls überspringen komplett; Countdown bleibt persistiert; Reaktivierung setzt die Beobachtung fort | Keine Beobachtung/Beendigung bei deaktiviertem Schalter; persistierter Countdown läuft nach Reaktivierung weiter | Bestehender Test `Monitor_DisabledAndManualTask_SkipsObservation` |
| 10 | Sichtbarkeitsregeln im Register | Nutzer ändert Schalter/Modus im geöffneten Register | Register „Automatisierung" geöffnet | „Aufgaben automatisch starten" aus → Überwachungsmodus deaktiviert, Trigger-Keyword + Prompt-Anhänge verschwinden; Modus „Separates Konto" → Trigger-Keyword verschwindet; „Aufgaben automatisch beenden" aus → Prüfmodus + Wartezeit deaktiviert; Backmerge-Modus `Deaktiviert` → Merge-Optionen deaktiviert | Felder erscheinen/verschwinden bzw. werden deaktiviert ohne Speichern, rein gebunden | E2E-Szenario + `SettingsViewModelTests` |

## Offene Punkte

Keine.
