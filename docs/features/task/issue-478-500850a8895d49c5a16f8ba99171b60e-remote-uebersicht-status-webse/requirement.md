# Übersetzte Anforderung: Remote-Übersicht über externe Status-Webseite

**Quelle:** Issue #478 / Aufgaben-ID `500850a8-895d-49c5-a16f-8ba99171b60e`
**Branch:** `task/issue-478-500850a8895d49c5a16f8ba99171b60e-remote-uebersicht-status-webse`

## Fachliche Zusammenfassung

Es wird eine externe, zugriffsgeschützte Status-Webseite eingeführt, die den Gesamtzustand der Softwareschmiede-Anwendung auch ohne laufende App-Sicht von unterwegs (Mobilgerät/Zweitrechner) einsehbar macht. Die Anwendung hält dazu eine persistente, ausschließlich ausgehende `wss://`-Relay-Verbindung (bevorzugt SignalR) zu einem neu zu erstellenden Dienst auf dem vorhandenen Linux-Server (nginx-Reverse-Proxy) und liefert dort einen periodisch projizierten Status-Snapshot ab; der Server cached diesen Snapshot mit `lastSeenUtc`, ergänzt GitHub-Daten (PRs + Checks, Releases/Tags, Issue-Zustände) eigenständig per GitHub-API und rendert die read-only Seite mit pro Sektion erkennbarer Quellenzugehörigkeit und Veraltungs-Markierung. Steuerung erfolgt nicht über den Relay, sondern ausschließlich über vordefinierte, serverseitig geschriebene GitHub-Kommentare, die über den bestehenden SCM-Ereigniseingang (`ScmEventMonitoringService` → `ScmIncomingEvent` → `ScmEventOrchestrator`) auditierbar in die App zurückfließen.

## Fachlicher Scope

- **Verarbeitungseinheit:** Der *App-Status-Snapshot* je App-Instanz — eine zeitpunktbezogene Projektion des Gesamtzustands aus vorhandenen Datenquellen (`WorkItem`s mit `Status`/`RunStatus`/`PausedUntilUtc`/`LastHeartbeatUtc`/`ActiveRunId`, `ScmIncomingEvent`-Warteschlange und -Diagnosen, `PullRequestReference`-Bestand, CLI-Session-Laufzeitstatus, App-Version) plus vom Server live ergänzten GitHub-Sektionen. Serverseitige Beobachtungseinheit ist die *Relay-Verbindung je Instanz-ID* (verbunden = online, getrennt = „offline seit `lastSeenUtc`").
- **Auslösende Ereignisse:**
  - App-Start bei aktivierter Remote-Übersicht → Verbindungsaufbau und initialer Snapshot (Systemereignis).
  - Snapshot-/Heartbeat-Intervall sowie relevante Zustandsänderungen in der App → Snapshot-Push (Systemereignis; vorhandene Change-Hooks wie `WorkItemRunDataChangedNotifier` und `ScmEventChangeNotifier`, DI `App.xaml.cs` Zeilen ~232/~277, sind als Triggersignale abwägbar — Annahme).
  - Verbindungsabbruch/Reconnect (Netzabbruch, PC-Suspend, Server-Neustart) → Server markiert App-Sektionen als veraltet bzw. wieder live.
  - Browser-Aufruf der Status-URL → Server rendert aus Snapshot-Cache plus direkter GitHub-API-Abfrage.
  - Nutzeraktion auf der Seite (vordefinierter Aktionstyp, kein Freitext) → Server schreibt einen GitHub-Kommentar → die App nimmt ihn über den bestehenden SCM-Ereigniskanal entgegen und verarbeitet ihn.
- **Erwartete Wirkung:** Die Seite ist unter der bestehenden Domain (Subdomain oder Pfad) zugriffsgeschützt erreichbar und zeigt die Sektionen „Laufende Aufgaben", „Wartet auf Anwender", „Offene Pull Requests", „Letzte Releases/Versionen", „Letzte SCM-Ereignisse/Diagnosen" und einen Heartbeat-/Offline-Badge. Bei verbundener App: Live-Zustand; bei getrennter Relay-Verbindung: App-Sektionen eindeutig veraltet markiert mit `lastSeenUtc`/„offline seit", GitHub-Sektionen bleiben live; bei serverseitig nicht erreichbarer GitHub-API werden auch GitHub-Sektionen mit Zeitstempel/Fehler gekennzeichnet statt still zu veralten.
- **Umgang mit fehlenden Objekten:**
  - *Instanz-ID der App-Installation:* **erzeugen und persistent wiederverwenden.** Es existiert heute kein stabiles Instanzkonzept — `ScmTaskLease.Owner`/`ScmIncomingEvent.LeaseOwner` sind flüchtige, pro Dispatch neu vergebene GUIDs (`ScmEventOrchestrator`, Zeile ~94/133). Eine persistente Instanz-ID (z. B. generierte GUID als `AppSetting`) ist neu einzuführen.
  - *Relay-Token fehlt/ungültig:* Verbindung wird nicht aufgebaut bzw. abgelehnt; der Fehlerzustand ist ehrlich zu melden (App-Log/Einstellungsanzeige), keine stille Dauerschleife.
  - *Kein Snapshot im Server-Cache* (Server-Neustart, App noch nie verbunden): App-Sektionen werden als „keine Daten/offline" dargestellt, GitHub-Sektionen bleiben live — kein falscher „alles läuft"-Zustand.
  - *Serverseitiges GitHub-Token/-Zugriff fehlt oder Rate-Limit erschöpft:* GitHub-Sektionen werden mit Zeitstempel/Fehler markiert.
- **Scope-Ausschlüsse:**
  - Kein Kommando-Rückkanal über den Relay (keine allowlisteten Remote-Befehle) — Steuerung ausschließlich über GitHub-Kommentare auf dem bestehenden SCM-Eingangskanal.
  - Keine neue Schema-/Persistenz-Ebene in der Anwendung: Der Snapshot wird aus `SoftwareschmiedeDbContext`-Bestandsdaten und Laufzeit-Singletons projiziert.
  - Kein eingehender Netzwerkzugriff auf den Windows-PC.
  - Kein Multi-User-Portal; die Seite ist reine externe Übersicht und ersetzt nicht die Aufgabenliste in der Anwendung.
  - Kein freier Kommentartext auf der Seite — nur vordefinierte Aktionstypen in enger Allowlist auf bekannte Issues/PRs.
  - MQTT-Variante (Broker mit retained messages) wurde als Alternative abgewogen und ist nicht primärer Ansatz.
  - Echter Relay-Rückkanal langfristig — bewusst nicht Teil dieser Anforderung.

## Betroffene Klassen und Komponenten

Das Feature umfasst zwei Auslieferungseinheiten: den App-seitigen Relay-Client in diesem Repository und einen neuen, separat zu betreibenden Server-Dienst (Technologie/Ablageort offen, siehe Offene Fragen).

### Neu zu erstellende Artefakte appseitig (Namen sind Vorschläge)

- `RemoteStatusRelayService` (`Application/Services/`) — `BackgroundService` bzw. `PeriodicBackgroundService`-Ableitung, die die persistente `wss://`-Verbindung zum Relay-Endpunkt hält (SignalR-Client via `Microsoft.AspNetCore.SignalR.Client` als neue NuGet-Abhängigkeit, oder `ClientWebSocket` — abhängig von der Server-Technologie), inkl. Auto-Reconnect/Backoff und periodischem Snapshot-/Heartbeat-Versand. DI-Registrierung in `src/Softwareschmiede.App/App.xaml.cs` nach dem Muster `services.AddSingleton<T>(); services.AddHostedService(sp => sp.GetRequiredService<T>());` (vgl. `ScmEventMonitoringService`, Zeilen ~243/244).
- Snapshot-Projektion (z. B. `StatusSnapshotProjector`/`RemoteStatusSnapshotService`) — read-only-Aggregation per `SoftwareschmiedeDbContext` (`AsNoTracking`, Muster `ScmEventMonitoringService.RunOnceAsync`, Zeilen ~59–67) plus Laufzeit-Singletons.
- Snapshot-/Protokoll-DTOs (z. B. `StatusSnapshot`, `StatusSnapshotWorkItem`, `StatusSnapshotScmEvent`, `StatusSnapshotHeartbeat`) inkl. expliziter Protokollversion — Ablage (Core vs. geteiltes Contract-Projekt) ist von der Server-Technologie-Entscheidung abhängig.
- `AppSettingService`-Schlüssel für die Konfiguration (Konvention: `Key`/`GetXxxAsync`-Paare, vgl. `ScmMonitoringEnabledKey = "scm.monitoring.enabled"`, `AppSettingService.cs` Zeilen ~43ff) — z. B. `status.remote.enabled`, `status.remote.url`, `status.remote.instanceid`.
- Relay-Token-Ablage über `ICredentialStore` (`WindowsCredentialStore`, `src/Softwareschmiede/Infrastructure/Services/`) — Geheimnis, nicht als `AppSetting`.
- Einstellungs-UI: Abschnitt im `SettingsViewModel`/Einstellungen-View (Ein-/Ausschalter, Relay-URL, Verbindungsstatus/Instanz-ID) — Annahme; Umfang der Oberfläche offen.

### Bestehende, als Datenquelle betroffene Artefakte (read-only, keine fachliche Änderung erwartet)

- `WorkItem` (`Domain/Entities/WorkItem.cs`) mit `WorkItemStatus`, `WorkItemExecutionStatus`, `WorkItemRunStatus` (`Running`/`WaitingForInput` — fachliche Basis für „wartet auf Benutzereingabe"), `ActiveRunId`, `LastHeartbeatUtc`, `PausedUntilUtc`, `Purpose`, `Kind`, `IssueReference`, `PullRequests`, `ScmCompletionReference`.
- `WorkItemRunActivity`/`WorkItemRecoveryService.HeartbeatTimeoutMinutes` — bestehende „läuft aktiv"-Regel als Referenz für die serverseitige Veraltungs-Logik.
- `IActiveWorkItemsService`/`WorkItemService`, `RunningAutomationsCount`, `CliProcessManager`/`AiExecutionService`/`TerminalSessionService` — laufende Aufgaben, Queue-Zählung und CLI-Session-Status.
- `ScmIncomingEvent`/`ScmIncomingEventStatus`/`ScmEventStore`/`ScmEventOrchestrator` — SCM-Ereignis-Warteschlange und Diagnosen; Wartegründe stehen in `ScmIncomingEvent.Reason` (Texte aus `ScmCliWaitReason`, `ScmCliWorkspaceTrust`, „CLI beschäftigt" etc. — `ScmEventOrchestrator` Zeilen ~245–307).
- `PullRequestReference`/`PullRequestMonitoringService`/`PullRequestWorkflowRun` — appseitig bekannter PR-Stand (ergänzend; GitHub-Sektionen sind primär Server-Datenquelle).
- `ApplicationVersionProvider` (`IApplicationVersionProvider`), `IUpdateService`/`UpdateService`/`GitHubReleaseClient` — App-Version und Release-Bezug.
- `LogService`/`LogEntry` — Fehler-/Diagnoseausgabe des Relay-Clients.

### Server-seitig neu zu erstellende Artefakte (voraussichtlich)

- Relay-Endpunkt (SignalR-Hub unter `status.<domain>/relay` bei ASP.NET Core) mit Token-Validierung, Instanz-ID-Zuordnung und Protokollversion.
- Snapshot-Cache mit `lastSeenUtc` je Instanz.
- GitHub-API-Client mit eigenem serverseitigem Read-Token (bevorzugt GitHub App statt PAT), ETag-/Cache-schonend wegen Rate-Limits.
- Auslieferung der Status-Seite (HTML; Aktualisierung via Push oder Polling — offen) plus Kommentar-Aktions-Endpunkt mit enger Allowlist.
- Betriebseinheit: systemd-Service o. ä., nginx-Location-Config, Deployment-/Update-Weg — neuer Betriebskanal außerhalb des App-Deployments.

### Tests

- `src/Softwareschmiede.Tests/`: Unit-Tests der Snapshot-Projektion (gegebene `WorkItem`-/`ScmIncomingEvent`-Bestände → DTO), des Aktivierungs-Schalters, der Instanz-ID-Erzeugung/-Persistenz und des Reconnect-/Heartbeat-Verhaltens (testbar über `TimeProvider`, Konvention `PeriodicBackgroundService`).
- Serverseitige Tests nur, falls der Server-Code in diesem Repo abgelegt wird (offene Frage); die Verifikation des Gesamtpfades (echter Server, echtes GitHub) ist über die übliche Unit-/E2E-Testspur hinaus manuell/probehalber zu prüfen.

## Implementierungsansatz

- **App-Seite:** Der Relay-Client ist ein weiterer Hintergrunddienst im bestehenden `PeriodicBackgroundService`-/`AddHostedService`-Muster. Beim Start (sofern aktiviert) baut er die `wss://`-Verbindung mit Token-Authentifizierung auf, sendet einen initialen Snapshot und danach periodisch Heartbeat + Snapshot (Intervalle offen). Für push-nahe Aktualisierung können die vorhandenen Change-Signale `WorkItemRunDataChangedNotifier` und `ScmEventChangeNotifier` als zusätzliche Trigger dienen (Annahme). Der Snapshot ist eine reine Projektion: keine neuen Entities, keine Migration.
- **Server-Seite:** Cachet den Snapshot je Instanz-ID mit `lastSeenUtc`; markiert App-Sektionen als veraltet, sobald Intervall + Toleranz überschritten ist (Schwelle zu definieren). Holt GitHub-Daten direkt per GitHub-API (Caching/ETags für Rate-Limits) und rendert die Seite mit pro Sektion sichtbarer Quellenzugehörigkeit und Zeitstempel, damit live-GitHub- und veraltete App-Daten keine widersprüchliche Gesamtanzeige ergeben.
- **Steuerpfad:** Seitenaktionen erzeugen serverseitig GitHub-Kommentare. Kritische Abhängigkeit: `ScmEventValidationService` (Zeilen ~71–80) verlangt, dass der Akteur (Autor/Editor) der Repository-Owner bzw. ein Eintrag des erlaubten Kreises ist (`ScmAccountMode`: `UserAccount`/`SeparateAccount`). Ein vom Server unter einer Bot-/GitHub-App-Identität geschriebener Kommentar würde die Validierung vermutlich nicht bestehen — die Identität, unter der Kommentare geschrieben werden, ist daher Teil der Architekturentscheidung (offene Frage).
- **Protokollversionierung:** Version im Handshake/DTO mitführen, damit Server- und App-Version unabhängig aktualisierbar bleiben.
- **Robustheit:** Reconnect/Backoff für Netzabbrüche, PC-Suspend und Server-Neustarts; der Offline-Zustand darf nie still „frisch" aussehen.

## Konfiguration

- **App-Ebene (`AppSetting`-Schlüssel via `AppSettingService`):** Aktivierung der Remote-Übersicht (Default: aus — Annahme), Relay-URL, persistierte Instanz-ID; ggf. Snapshot-/Heartbeat-Intervall. Aufnahme in die Einstellungen-Oberfläche (Konvention `docs/help/einstellungen/`).
- **Geheimnisse App-seitig:** Relay-Token in `ICredentialStore`/`WindowsCredentialStore`; Rotation ohne App-Update muss möglich sein (Weg offen).
- **Server-Ebene (Datei/Environment, außerhalb des App-Settings-Systems):** GitHub-Token/GitHub-App-Credential (minimaler Read-Scope), Relay-Token(s) je Instanz, Repository-Liste für die GitHub-Aggregation, Kommentar-Aktions-Allowlist, Zugriffsschutz der Seite (Basic Auth / IP-Allowlist / Tailscale — Pflicht vor Livegang), nginx-Location.

## Offene Fragen

1. **Server-Technologie und Ablageort:** ASP.NET Core SignalR-Dienst (naturgemäß für .NET, ggf. als weiteres Projekt in diesem Repo/`Softwareschmiede.slnx` mit eigener Deploy-Lane) vs. schlankerer Node/Go-Dienst vs. MQTT-Variante — und gehört der Server-Code in dieses Repository oder in ein separates Repo?
2. **Zu aggregierende Repositories:** Nur `martin-stromberg/Softwareschmiede` oder alle als `Active` markierten `GitRepository`-Einträge (Mehr-Repo-Support beeinflusst Snapshot-Schema, GitHub-Token-Scopes und Rate-Limit-Budget)?
3. **Umfang der ersten Version:** Nur Übersicht (read-only) oder bereits Kommentar-Aktionen über die Seite? Welche konkreten Aktionstypen stehen auf der Allowlist (z. B. „passt so"-Bestätigung, PR-Merge-Anweisung)?
4. **Kommentar-Autor vs. Ereignis-Validierung (kritisch):** Unter welcher Identität schreibt der Server Kommentare, damit sie `ScmEventValidationService` (Akteur = Repository-Owner/erlaubter Kreis) akzeptiert — Owner-Token, erlaubtes Bot-Konto, oder Erweiterung des Validierungsvertrags?
5. **Konkrete Adresse:** Eigene Subdomain `status.<domain>` vs. Pfad unter einer bestehenden Site?
6. **Zugriffsschutz-Methode der Seite:** Basic Auth, IP-Allowlist oder Tailscale — Pflichtentscheidung vor Livegang.
7. **Aktualisierungsmodell und Intervalle:** Push zum Browser (SignalR/SSE) vs. Polling; Snapshot-/Heartbeat-Intervall der App und Veraltungs-Schwelle des Servers (Orientierung an `WorkItemRecoveryService.HeartbeatTimeoutMinutes` möglich — Annahme).
8. **Instanz-ID:** Format und Sichtbarkeit (generierte GUID vs. benennbarer Instanzname); Darstellung bei mehreren sendenden Installationen (eine Instanz wählen? Sektionen je Instanz?).
9. **Relay-Token-Rotation:** Mechanismus, wie der Token ohne App-Update ausgetauscht wird (serverseitige Token-Vergabe, manueller Eintrag in `WindowsCredentialStore`, UI-Eingabefeld?).
10. **Deployment und Betrieb des Server-Dienstes:** Paketierung, Update-Weg, Monitoring, Neustart-Verhalten — komplett neuer Ops-Kanal außerhalb des bisherigen App-Deployments.
11. **Sektion „Wartet auf Anwender":** Genügen `WorkItemRunStatus.WaitingForInput` plus `ScmIncomingEvent.Reason`-Wartegründe (`ScmCliWaitReason`, Trust-Bestätigung via `ScmCliWorkspaceTrust`, „CLI beschäftigt"), oder sollen weitere Wartezustände (Scope-Bestätigungen, geplante Prompts via `PromptScheduleDispatchService`, Pausen via `PausedUntilUtc`) explizit projiziert werden?
12. **Relay-Protokoll:** SignalR-Hub-Methoden in beide Richtungen vs. schlichtes WebSocket+JSON — abhängig von Frage 1; Versionierungsstrategie des Protokolls.
