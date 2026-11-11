# Bestandsaufnahme: Funktionstrennung der Automatisierungs-Einstellungen (Issue #348)

Analysiert wurden die Einstellungsseite (`SettingsView.xaml`/`SettingsViewModel`), die drei zyklischen SCM-Automatisierungsdienste (`ScmEventMonitoringService`, `ScmTaskCompletionMonitoringService`, `ScmBackmergeService`), die Persistenzschicht (`AppEinstellungService`) sowie die zugehörigen Unit-, Integrations- und E2E-Tests — bezogen auf die Anforderung in `requirement.md`.

## Zusammenfassung

**Vorhanden:**
- Alle drei Automatisierungsfunktionen existieren als eigene Dienste mit eigenen Laufzeit-Schaltern; zwei davon laufen als `PeriodicBackgroundService`-Hosted-Services (`ScmEventMonitoringService`, `ScmTaskCompletionMonitoringService`), die Backmerge-Bewertung (`ScmBackmergeService`, `AddScoped`) hat **keinen eigenen Zyklus** — sie wird nur innerhalb der Repository-Enumeration des `ScmEventMonitoringService` aufgerufen.
- Alle von der Anforderung weiterverwendeten Einstellungsschlüssel existieren bereits (`scm.monitoring.enabled`, `scm.monitoring.accountmode`, `scm.trigger.keyword`, `scm.prompt.append.*`, `scm.taskcompletion.enabled`, `scm.taskcompletion.wait.minutes`, `scm.backmerge.mode`) samt typisierten Gettern/Settern mit `Enum.IsDefined`-Validierung.
- UI-Optionsmuster (`ScmBackmergeModusOption`/`ScmKontoModusOption` mit `Alle`/`FuerModus`), `Expander`-Panelmuster (ProjectDetailView, ScmEventQueueView) und Bool/Null-Visibility-Converter sind etabliert.
- Die Kandidatenauswahl der Abschlussüberwachung ist heute fest auf „nur Aufgaben mit `ScmIncomingEvent`" verdrahtet (Zeile 46–50 des Dienstes); der PR-Pfad hat Vorrang vor dem Issue-Pfad, und Aufgaben ohne Issue **können** heute (über PR-Verknüpfung) automatisch beendet werden.
- Die Pflichtfeld-Validierung `AgentToken` für `SeparatesKonto` (`ValidiereScmKontoModus`) existiert und berücksichtigt sogar ungespeicherte Formularwerte.

**Fehlt noch:**
- Neuer Einstellungsschlüssel + Enum + Optionsklasse für den Abschluss-Prüfmodus (z. B. `scm.taskcompletion.scope`, `ScmTaskCompletionPruefmodus`) — nichts davon existiert.
- Eigenständiger Backmerge-Prüfzyklus (gehosteter Dienst) und kanonische `Benutzerkonto`-Auflösung unabhängig von `scm.monitoring.enabled` und vom konfigurierten `ScmKontoModus`.
- Neues `TabItem` „Automatisierung" mit drei `Expander`-Panels; Sichtbarkeits-/`IsEnabled`-Regeln gibt es im SCM-Abschnitt heute überhaupt nicht.
- ViewModel-Properties für Prüfmodus, Sichtbarkeits-/Enabled-Flags und Panel-Aktivierungsstatus.
- Im `ScmEventQueueViewModel` zwei Textverweise auf den alten Menüpfad „Einstellungen → Allgemein → SCM-Ereignisse".
- Der E2E-Test `Einstellungen_AllgemeinEnthaeltNurVorhandeneAbschnitte_E2E` assertiert explizit das **Fehlen** von „Automatisierung" — er wird mit der Umsetzung zwangsläufig angepasst werden müssen.

**Test-Ausgangszustand:** Alle vier Test-Lanes wurden vor jeder Änderung ausgeführt — **0 Fehlschläge** (2245 + 264 + 9 + 65 erfolgreiche Tests; 1 plattformbedingter Skip + 5 ConPTY-Sandbox-Skips, beide dokumentiert und umgebungsbedingt). Vollständiger Nachweis mit Befehlen, TRX-Reports und Skip-Begründungen: [inventory/tests.md](inventory/tests.md).

## Details

- [Datenmodell](inventory/models.md) — `Aufgabe`, `IssueReferenz`, `PullRequestReferenz`, `GitRepository`, `ScmIncomingEvent`, `ScmObservationCheckpoint`, `AppEinstellung`, `ScmRepositoryContext`, `ScmEventRepository`/`ScmEventReference`/`ScmNotification`
- [Logik](inventory/logic.md) — `ScmEventMonitoringService`, `ScmTaskCompletionMonitoringService`, `ScmBackmergeService`, `AppEinstellungService`, `PeriodicBackgroundService`, `ScmEventOptions`/`ScmEventChangeNotifier`/`ScmEventStartupGate`, `ScmEventValidationService`, `ScmTaskCoordinator`, `ScmPromptAppendix`, `SettingsViewModel`, `ScmEventQueueViewModel`, DI-Registrierung in `App.xaml.cs`, `PullRequestMonitoringPolicy`
- [Enums](inventory/enums.md) — `ScmBackmergeModus`, `ScmKontoModus`, `AufgabeStatus`, `PullRequestStatus`, `PullRequestMergeStatus`, `ScmReferenceKind`, `ScmContentKind`, `ScmProviderFailureKind`, `PullRequestReferenzRolle`/`PullRequestMonitoringPhase`/`PullRequestProvider`, UI-Optionsklassen `ScmBackmergeModusOption(en)`/`ScmKontoModusOption(en)`
- [Interfaces](inventory/interfaces.md) — `IScmEventSource`, `IScmNotificationSource`, relevante `IGitPlugin`-/`IPluginManager`-Members
- [UI](inventory/ui.md) — `SettingsView.xaml`-Registerstruktur, SCM-Ereignisse-Abschnitt (Zeilen 279–429) mit allen Controls/Bindings/Automation-Namen, `Expander`-Muster, E2E-Page-Object
- [Tests](inventory/tests.md) — Test-Ausgangszustand (4 Lanes, TRX-Reports unter `inventory/test-results/`), betroffene Testklassen und Hilfsmethoden
