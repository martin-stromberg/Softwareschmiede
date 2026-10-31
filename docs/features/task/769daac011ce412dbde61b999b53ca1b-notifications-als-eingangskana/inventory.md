# Bestandsaufnahme: GitHub-Notifications als Eingangskanal für SCM-Ereignisse (Kontomodus)

Analyse des bestehenden SCM-Ereignispfads (Akquisition → Inbox → Dispatch) sowie der Einstellungs-, Plugin- und Authentifizierungsinfrastruktur bezogen auf die Anforderung „GitHub-Notifications als Eingangskanal (Kontomodus)".

## Zusammenfassung

- Der komplette Ereignispfad existiert und ist auf einen einzigen Akquisitionsmodus ausgelegt: `ScmEventMonitoringService` pollt pro aktivem `GitRepository` über `IScmEventSource` (Repository-Auflösung via `GetEventRepositoryAsync`, Enumeration via `EnumerateReferencesAsync`, Textquellen via `ReadEventsAsync`), schreibt dedupliziert über `ScmEventStore.AcceptBatchAsync` in die Inbox (`ScmIncomingEvents` + `ScmContentSnapshots` + `ScmObservationCheckpoints`) und dispatcht über `ScmEventOrchestrator`. Nur `GitHubPlugin` implementiert `IScmEventSource`/`IScmEventResponder`/`IScmAgentAccess` (BitBucket und LocalDirectory nicht).
- **Nicht vorhanden:** ein Kontomodus-Enum/Einstellung (`scm.monitoring.accountmode`), eine Notification-Fähigkeit (`IScmNotificationSource` o. ä.), Notification-Value-Objects (Thread-ID, Subject, Reason, `updated_at`), ein `GET /notifications`-Zugriff im `GitHubPlugin`/`IGitHubEventApiClient` sowie ein zweites Credential (Bot-PAT). Grep nach „notifications" findet außer Dokumenten nichts.
- Die Checkpoint-Infrastruktur unterstützt beliebige `SourceKey`-Namensräume (`ScmObservationCheckpoint.SourceKey` ist freier String); heutige Namensräume sind `repository/…`, `enumeration/…` und `{provider}/{host}/{repoId}/{kind}/{number}/{contentKind}` — ein neuer `notifications/{provider}/{account}`-Namensraum ist unbelegt.
- Die modusabhängigen Reibungspunkte der Anforderung sind bestätigt: `ScmEventValidationService.GetRejection` codiert „Viewer = Repository-Owner" und Trigger-Keyword-Pflicht fest; `GitHubPlugin` nutzt überall denselben `GitHubTokenCredentialKey` für Host-Lesen (`GetGhEnvironment`), Antworten (`IScmEventResponder` via `GhEventApiClient`) und Agenten-Sitzung (`GetAgentEnvironmentAsync` → `GH_TOKEN` + `http.extraheader`).
- Vorlagen für die neuen Teile existieren: Enum-Einstellung nach `ScmBackmergeModus`-Muster (`ScmBackmergeModeKey`, `Get-/SetScmBackmergeModusAsync`, `ScmBackmergeModusOption` in `SettingsViewModel`/`SettingsView.xaml`), Secret-Pluginfeld nach `Token`-Muster (`PluginSettingFieldType.Secret` → `ICredentialStore` via `PluginSettingsService`).
- **Test-Ausgangszustand:** Reguläre Lanes fehlerfrei (2049/2049 + 243/243, je 0 Fehlschläge). OsInterface-Lane: 62/70 bestanden, **3 Fehlschläge** (zwei WPF-E2E-Timeouts — darunter der konsolidierte SCM-Pipeline-E2E `ScmEvents_QueueReceptionDispatchAndReplies_UseRealPipeline` — und ein STA-Clipboard-Test), 5 ConPTY-Skips (Sandbox-Limitation) — Details und Nachweise unter [Tests](inventory/tests.md).

## Details

- [Datenmodell](inventory/models.md)
- [Logik](inventory/logic.md)
- [Enums](inventory/enums.md)
- [Interfaces](inventory/interfaces.md)
- [Tests](inventory/tests.md)
