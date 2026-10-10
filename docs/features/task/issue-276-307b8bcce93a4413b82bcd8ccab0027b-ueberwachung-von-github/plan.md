# Umsetzungsplan: Überwachung von GitHub

Stand: 2026-10-10. Überarbeitet anhand PC-01 bis PC-08 sowie PC-06-R1 und PC-08-R1 aus `plan-check.2.md`.

## Ziel und verbindliche Produktentscheidungen

Ereignisse zu lokal verknüpften GitHub-Issues und Pull Requests werden dauerhaft aufgenommen und einer bestehenden Aufgabe zugeordnet. Gültige Ereignisse gelangen zur laufenden CLI, zu einer konfigurierten Standard-CLI oder in eine bedienbare Queue. Weitere SCM-Anbieter können dieselbe Application-Pipeline verwenden.

Verbindlich bestätigt: Polling lokal verknüpfter Repositories; Trigger `$Softwareschmiede` case-insensitiv; vollständiger neuer Beschreibungstext ohne Empfangsreaktion; authentifizierter GitHub-Nutzer als hinreichende Owner-Identität; kurzer Warte-Kommentar; keine automatische Reaktivierung beendeter/archivierter Aufgaben. Diese Entscheidungen präzisieren `requirement.md` und schließen dessen offene Punkte.

Der tatsächliche Ereignisautor/Bearbeiter muss dem authentifizierten Nutzer und Repository-Owner entsprechen. Keine zusätzliche menschliche Identität konfigurieren. Schreibberechtigung oder Organisationsmitgliedschaft allein ersetzen die Owner-Prüfung nicht. Ungültige Ereignisse verursachen weder Empfangsreaktion noch Warte-Kommentar, Queue-Auftrag oder CLI-Start.

## 1. Verträge und Zuordnung (PC-01)

Im Projekt `Softwareschmiede.Plugin.Contracts` optionale Fähigkeiten `IScmEventSource` und `IScmEventResponder` ergänzen; keine neue abstrakte Pflichtmethode in `IGitPlugin`. GitHub implementiert beide; Bitbucket und LocalDirectory ohne Fähigkeit werden vom Monitor übersprungen. Fehler werden typisiert: Authentifizierung, Berechtigung, Rate-Limit, temporär, endgültig.

DTOs enthalten ProviderPrefix, Host, stabile Repository-ID, Owner/Name, Referenztyp Issue/PR, Nummer, RemoteNodeId, Kommentarart, RemoteCommentId, CreatedAt, UpdatedAt, LastEditedAt, AuthorId/Login, EditorId/Login, Body, BodyHash, URL und Beobachtungszeit. Loginvergleiche ordinal case-insensitiv, Identitäten bevorzugt über Provider-IDs. Ziel-URLs entstehen aus validiertem Providerkontext; Ereignistext darf kein Ziel verändern.

`ScmEventValidationService` ordnet über GitRepository sowie `IssueReferenz`/`PullRequestReferenz` zu. Unbekannte Referenzen werden ignoriert; mehrdeutige Zuordnungen als Integrationsdiagnose sichtbar und nicht willkürlich einer Aufgabe zugeteilt. Alle gültigen Ereignisse einschließlich sofort zustellbarer werden vor externen Aktionen persistiert. Ungültige Beobachtungen bleiben ausschließlich Snapshot/Checkpoint und inhaltsarme Diagnose.

## 2. GitHub-Abfragen und Versionierung (PC-01)

`GitHubPlugin.cs` erhält einen injizierbaren `IGitHubEventApiClient`. Produktion nutzt vorhandenes `gh`, Credential-Store und `GetGhEnvironment`; Tests ersetzen nur diesen Transport. Argumentlisten und JSON-stdin bzw. `--input`-Dateien verwenden, keine interpolierten Shellbefehle.

Quellen:

- `GET /user` liefert Viewer-ID/Login; Repository-Metadaten liefern Repository-ID, Owner und Berechtigungen.
- `GET /repos/{owner}/{repo}/issues/{number}/comments?per_page=100&since=...` liefert Issue-Kommentare und allgemeine PR-Unterhaltung.
- Für PR zusätzlich `GET /repos/{owner}/{repo}/pulls/{number}/comments?per_page=100&since=...` für Review-Zeilenkommentare. Jede Link-Folgeseite lesen; Kommentararten besitzen getrennte ID-Namensräume.
- Beschreibungen über GraphQL `repository.issue(number:)` bzw. `pullRequest(number:)`: id, body, author, editor, createdAt, updatedAt, lastEditedAt, url. Kommentare mit Änderungshinweis über ihre Node-ID mit demselben Autor-/Editor-Satz ergänzen. Body und Editor müssen aus derselben GraphQL-Antwort stammen.
- Veröffentlichte PR-Review-Zusammenfassungen über paginierte `pullRequest.reviews`-Connection mit ID, Body, Autor/Editor und Veröffentlichungszeit; Pending-Reviews ignorieren. Damit sind PR-Unterhaltung, Zeilenkommentar und veröffentlichter Review-Text ausdrücklich abgedeckt.

Bei neuer lokaler Referenz den aktuellen Beschreibungstext und alle aktuell sichtbaren veröffentlichten Kommentare einmal prüfen; initial vorhandener gültiger Trigger ist ein Auftrag. Eigener Checkpoint pro Referenz und Quellenart, damit später hinzugefügte Referenzen keinen fortgeschrittenen Repository-Cursor erben. Entfernen/erneutes Verknüpfen derselben Remote-Referenz setzt Deduplizierung nicht zurück.

Pro Text letzten Body-Hash und Textversion, Editor und Editzeit speichern. Identischer Body mit neuer Metadatenzeit erzeugt keinen Auftrag. Echte beobachtete Textänderung wird mit Trigger und tatsächlichem Editor neu geprüft. Bei lastEditedAt=null gilt ursprünglicher Autor, andernfalls ausschließlich zuverlässiger Editor der aktuellen Version. Fehlender Editor: keine Owner-Ausführung, Diagnose „Bearbeiter nicht bestimmbar“; spätere Identitätsauflösung derselben Version bleibt möglich. Fremdbearbeitungen niemals dem ursprünglichen Owner-Autor zurechnen.

Ereignisschlüssel: `Provider/Host/RepositoryId/ReferenceKind/Number/ContentKind/RemoteNodeId/Revision/BodyHash`; Revision=LastEditedAt oder CreatedAt für uneditierten Inhalt. Für Beschreibungen Node statt Kommentar-ID. Hash unterscheidet verschiedene Texte bei gleicher Zeit, Snapshotvergleich verhindert Metadatenaufträge. A→B→A mit neuer Editzeit ist eine neue Version. Sortierung nach Remotezeit und stabilem Schlüssel.

Polling erfasst nur sichtbare Textstände; zwischen Abfragen überschriebene/gelöschte Zwischenversionen sind nicht rekonstruierbar. Keine Zusage sämtlicher GitHub-Zwischenereignisse. Intervall über Options konfigurierbar, Standard 60 Sekunden.

API-Grundlagen: [Issue-Kommentare](https://docs.github.com/en/rest/issues/comments), [PR-Kommentare](https://docs.github.com/en/rest/pulls/comments), [GraphQL Issues](https://docs.github.com/en/graphql/reference/issues), [GraphQL Pull Requests](https://docs.github.com/en/graphql/reference/pulls). Die GraphQL-Felder dienen Body-/Editor-Abgleich; updatedAt allein ist kein Inhaltsänderungsnachweis.

## 3. Dauerhaftigkeit und Zustandsmaschine (PC-02, PC-05)

EF-Migration einschließlich ModelSnapshot/Mapping für `ScmIncomingEvent`, `ScmObservationCheckpoint`, `ScmContentSnapshot` und `ScmOutgoingAction`. Incoming: Unique-Index auf Ereignisschlüssel, Aufgabe/Ursprung, Originaltext, Status/Grund, Aufnahmefolge, Versuche, NextAttemptUtc, Fehlercode, LeaseOwner/LeaseUntil, DeliveryAttemptId, CliRunId. Outgoing: Ereignis, Aktionsart, deterministische ActionId, Body/Marker, RemoteId, Zustand, Versuche. Keine Secrets persistieren.

Ein Poll sammelt alle Seiten einer Referenz/Quelle vollständig bis Abfragebeginn als Watermark; Inbox-Inserts, Snapshots und Checkpoint anschließend in EINER SQLite-Transaktion speichern. Fehler auf Folgeseite: kein Cursorfortschritt dieser Quelle. Wiederholungen inklusive Grenze mit Überlappung von mindestens zwei Pollintervallen; Unique-Index/Snapshots entfernen Duplikate und gleiche Zeitstempel. GraphQL-Connections vollständig paginieren. Andere Quellen dürfen unabhängig fortfahren. Absturz vor Commit: erneut lesen; nach Commit: Inbox fortsetzen.

| Zustand | Bedeutung |
|---|---|
| Aufgenommen | Validierter dauerhafter Auftrag, Bestätigung/Routing folgen |
| Wartend | Keine Standard-CLI, Pause, beschäftigt/nicht bereit, beendet/archiviert, Berechtigung oder Startfehler; Grund separat |
| ZustellungVorbereitet | Aufgabenlease und AttemptId/RunId vor externem Schreibversuch committed |
| Zugestellt | WritePromptAsync erfolgreich; keine Verarbeitungsbestätigung |
| InBearbeitung | Korrelierte KI-Annahme für Event/Attempt/Run |
| Verarbeitet | Korrelierter KI-Abschluss und alle angeforderten Rückantwortaktionen bestätigt |
| ZustellungUngewiss | Schreibversuch/externer Erfolg ohne sichere lokale Bestätigung; kein automatischer Neuversand |
| VerarbeitungUngeklärt | Zustellung bekannt, aber Exit/Fehler/Timeout/fehlender Abschluss; kein automatischer Neuversand |

Empfangsreaktion, Warte-Kommentar, Promptzustellung und KI-Antwort erhalten getrennte Zustände/Versuche. Reaktionsfehler blockieren die Verarbeitung nicht dauerhaft und lösen keinen zweiten Prompt aus; separat sichtbar wiederholbar. Beschreibungen erhalten niemals Empfangsreaktionen.

Atomarer Claim über bedingtes UPDATE auf Zustand/abgelaufene Lease. Persistierte Aufgabenlease erlaubt höchstens ein Ereignis in Zustellung/Bearbeitung je Aufgabe und schützt Worker, UI und weitere App-Instanzen. Gemeinsamer Prozesskoordinator serialisiert außerdem manuelle CLI-Starts und Eingaben. Lease 60 Sekunden, Erneuerung alle 15 Sekunden. Kein Netzwerkaufruf innerhalb DB-Transaktion. Verwaiste Claims ohne externen Versuch dürfen erneut übernommen werden; nach begonnenem Versuch zuerst rekonstruieren. Nachfolgende Ereignisse FIFO nach Aufnahmefolge bis zur Klärung zurückhalten.

Empfangsreaktion `eyes`: vor Wiederholung vollständige Reaktionsliste lesen und vorhandene gleiche Viewer-Reaktion übernehmen. Warte-Kommentar: kurzer Text ohne Trigger plus `<!-- softwareschmiede:event:{EventId}:waiting -->`; Marker vor POST persistieren, nach unbekanntem Ergebnis Kommentare paginiert nach Marker und Viewer abgleichen. Solange Ausgang nicht sicher rekonstruierbar, „Ausgang ungewiss“ statt erneut posten. KI-Antwortaktionen analog über ActionId-Marker/providerseitige Wiedererkennung; bei fehlender Wiedererkennung keine automatische Wiederholung nach Ungewissheit. Eigene Marker-Kommentare und gespeicherte eigene Antwort-IDs bei Aufnahme ausschließen.

Erreichbare Garantie: genau ein lokaler Auftrag pro beobachteter Version; keine automatische doppelte Promptzustellung nach ungewissem Versuch; idempotente/abgeglichene externe Bestätigungen. Keine verteilte Exactly-once-Zusage für KI/Remoteaktionen. Bei Ungewissheit „Status erneut prüfen“ sowie ausdrücklich bestätigter manueller Neuversand mit Hinweis auf mögliche Doppelbearbeitung; dieser erzeugt neue AttemptId.

Retry: Netzwerk/5xx 30 Sekunden, 2 Minuten, 10 Minuten, danach maximal 30 Minuten mit Jitter; 429 nach Retry-After/Reset. 401/ungültige Credentials sperrt Provideraufnahme bis Credentialänderung oder „Erneut prüfen“; 403 sperrt betroffene Aktion. Keine schnelle Endlosschleife. Oberfläche zeigt Fehler, nächste Prüfung und Wiederaufnahme; GUI-Start bleibt unabhängig.

## 4. Arbeitsbereich und CLI (PC-03, PC-04)

Gemeinsamer Application-Einstieg `ScmEventOrchestrator.DispatchAsync(eventId, explicitPluginPrefix?)` für Hintergrund/UI. Bestehender Promptpfad ist konkret `KiAusfuehrungsService.GetTerminalSession(aufgabeId)` plus `ITerminalSession.WritePromptAsync`; die gegenteilige Vermutung in inventory/tasks-cli.md ist hiermit präzisiert.

1. Unter Aufgabenlease Aufgabe/Pause/Session neu laden. Beendet/Archiviert: wartend/gesperrt, keine Aktivierung. Pause bleibt erhalten. Fachliches AufgabeStatus.Wartend ist weder Queue-Warten noch CLI-Bereitschaft. Erlaubte unpausierte Aufgaben über vorhandene Statusübergänge aktivieren.
2. Lebende Session hat Vorrang auch ohne Standard. Prozess/Sessionobjekt prüfen, AktiveRunId allein reicht nicht. Unmittelbar vor Write Identität/Run/Lebendigkeit erneut prüfen. Automatische und manuelle Starts/Eingaben verwenden denselben Koordinator.
3. Ohne Session exakt `ResolveDevelopmentAutomationPluginWithProjectScopeAsync`: Aufgabenwahl > Projektstandard > Globalstandard. Gesetzter, aber entfernter/deaktivierter/defekter höherer Standard fällt nicht auf anderen zurück. Prefix muss in `GetEnabledDevelopmentAutomationPluginsAsync` vorkommen, `CheckHealthAsync` bestehen und StartSpec über `TerminalExecutableResolver` ausführbar sein. Bestehende Startdienste erhalten strikten Auflösungsmodus: zwischenzeitlich entfernter Prefix bedeutet Fehler, niemals beliebiger Resolver-Fallback. Fehlender/unstartbarer Standard: Queue und Warte-Kommentar, kein Ersatzstart.
4. Neu ohne Arbeitsbereich über `EntwicklungsprozessService.ProzessStartenUndCliStartenAsync` (Repository/Basisbranch, Klon, Arbeitsverzeichnisvalidierung, Initialisierung, Start). Bestehender gültiger Klon über `CliNeustartenAsync`. Start-Rollback auf Ressourcen des konkreten neuen Starts begrenzen und vorherigen Status wiederherstellen; niemals bereits benutzten Klon löschen. Pause-Guards bleiben vor Seiteneffekten. Queue-Öffnen darf Neu aktivieren; Dialogabbruch lässt Aufgabe aktiv und Eintrag wartend.
5. Vor Prompt sichere Eingabebereitschaft herstellen. `CliRuntimeStatus.WartetAufEingabe` ist im Bestand nur I/O-Ruhe-Heuristik und kein sicherer Bereitschafts-/Abschlussnachweis. Ergänze `IScmPromptSessionAdapter` mit WaitForInputReadyAsync, IsBusy und sicherer Texteingabe. Adapter für lokale KI-Plugins verwenden konkrete Prompt-/Turn-Hooks bzw. verifizierte Eingabeprompt-Erkennung und Initialisierungsergebnis; KiSimulator deterministische Ready/Busy-Signale. Automatische Bereitschaft mindestens für Codex, ClaudeCli und Simulator mit aufgezeichneten Start-/Arbeits-/Eingabetranskripten implementieren und prüfen. Andere Plugins können denselben Vertrag implementieren. Unbekannter/nicht unterstützter Zustand wartet sichtbar; Queue erlaubt einmalige explizite Bestätigung der Eingabebereitschaft für genau den sichtbaren lebenden Run. Kein Sleep allein ersetzt Ready.
6. Beschäftigte CLI nicht durch Texteingabe unterbrechen. Nach Ready und erfolgreicher adaptergebundener Kanalprüfung aus Abschnitt 5 Attempt committen, dann genau eine Texteingabe. Exit beim Write mit nicht auszuschließender Teilzustellung: ungewiss. Sicherer Fehler vor Write: normal wiederaufnehmbar. Erfolgreiches Write bedeutet ausschließlich Zugestellt. Eine manuelle Bereitschaftsbestätigung ersetzt die Kanalprüfung nicht.

Prompt enthält vollständigen Inhalt, Ereignisart, validierten Ursprung, Repository, Aufgabe, Event-/Attempt-/Run-ID und Anleitung zum Antwortkanal. Originaltext als UTF-8-JSON im privaten Austauschverzeichnis ablegen. Terminal erhält einzeiligen JSON-escaped Prompt mit vollständigem escaped Inhalt und Dateiverweis: CR/LF, ESC, NUL und sämtliche C0/C1-Zeichen aus Remoteinhalt dürfen niemals als Steuerbytes wirken. Kein Remoteinhalt in Shellbefehle/CLI-Argumente interpolieren. Adapter schreibt nur in bestätigten KI-Prompt, nicht in Shellprompt.

## 5. Rückantwort und Ereignisabschluss (PC-05, PC-06)

Keine GitHub-Anmeldung innerhalb der KI voraussetzen. `ScmEventExchangeService` stellt pro Attempt ein lokales Austauschverzeichnis `<validiertes tatsächliches CLI-Arbeitsverzeichnis>/.softwareschmiede-scm-exchange/{RunId}/{AttemptId}/` bereit. Dieser bewusst innerhalb der CLI-Schreibwurzel liegende, nicht versionierte Ort ersetzt den bisher geplanten App-Daten-Pfad. Weder `.git`, `.codex`, `.agents` noch ein Symlink in App-Daten dienen als Austauschort. Nur aktueller OS-Nutzer und die von dessen CLI-Sandbox benötigten beschränkten Ausführungsidentitäten erhalten Dateizugriff; keine Freigabe für andere Benutzer. GitHub-Token verbleiben ausschließlich im Host-Credential-Store und Provideraufruf.

Vor dem ersten Schreiben lokales Git-Exclude über den vom Repository aufgelösten `info/exclude`-Pfad additiv um den relativ zum Worktree verankerten Austauschpfad ergänzen; vorhandene Regeln erhalten. `git check-ignore` und `git ls-files` müssen Ausschluss und fehlende Versionierung belegen. Bereits versionierte, fremd belegte oder nicht ausschließbare Pfade führen zu Wartend/Kanal nicht verfügbar; keine vorhandenen Dateien entfernen oder überschreiben. Der Host erstellt Verzeichnis und `request.json`, prüft kanonische Pfade, Dateirechte und Rename-Fähigkeit; diese Hostprüfung allein ist ausdrücklich kein Nachweis des CLI-Zugriffs.

`IScmPromptSessionAdapter` erhält zusätzlich `CheckExchangeAccessAsync(session, exchangePath)`. Das Ergebnis enthält Supported/Denied/Unknown, Grund, tatsächliche Schreibwurzel und eine an CliRunId gebundene Zugriffsbeschreibung. Der Startkoordinator erfasst dafür vor jedem Start die effektive CLI-Konfiguration einschließlich konfigurierter Parameter, Arbeitsverzeichnisüberschreibungen, Profil-/Projekt-/Benutzer- und Managed-Berechtigungen sowie deaktivierter Dateitools; StartSpec, gestarteter Run und Ready-Nachweis müssen dieselbe Beschreibung verwenden. Unterstützte Konfigurationsformen werden explizit ausgewertet und durch Fixtures belegt; unbekannte Versionen/Overrides oder nicht auflösbare Vorgaben ergeben Unknown, niemals eine angenommene Freigabe. Keine zusätzlichen Rechte, globalen Freigaben oder deaktivierten Sicherheitsprüfungen durch die SCM-Funktion.

| Adapter | Funktionsfähiger Normalpfad und Voraussetzung |
|---|---|
| Codex | Tatsächliches Startarbeitsverzeichnis im validierten Klon; vorhandene effektive `workspace-write`-Konfiguration beziehungsweise gleichwertiges aufgelöstes Profil erlaubt Lesen, Schreiben und Rename am Austauschpfad. Dateitools müssen verfügbar sein; explizite Read-only-/Deny-Regeln haben Vorrang. `CodexPlugin.BuildProcessStartInfo` und Sessionregistrierung liefern die effektiven Startdaten; es wird kein Full-access-/Bypass-Flag angehängt. |
| ClaudeCli | Tatsächliches Startarbeitsverzeichnis im validierten Klon; effektive Dateitool-Berechtigungen erlauben Read und Write/Edit dort. Normalpfad ist eine bereits vom Nutzer konfigurierte Freigabe (z. B. `acceptEdits` ohne entgegenstehende Deny-/Managed-Regel); Default-/Planmodus mit noch erforderlicher Freigabe gilt nicht als automatisch schreibfähig. `ClaudeCliPlugin.BuildProcessStartInfo` und Sessionregistrierung liefern die effektiven Startdaten. Der Adapter ändert weder Permission-Modus noch Allow-/Deny-Regeln. Die KI kann vollständige JSON-Dateien mit Write publizieren; falls Rename nicht als Tool erlaubt ist, verwendet sie die unten beschriebene stabile vollständige JSON-Veröffentlichung. |
| KiSimulator | Derselbe arbeitsbereichslokale Kanal und dieselbe Runbindung; positive und verweigerte Zugriffsbeschreibungen gezielt steuerbar. Der Simulator allein ersetzt die realen Adaptervertrags- und eingeschränkten Prozesstests nicht. |

Bestehende lebende Codex-/ClaudeCli-Sitzungen mit erfasstem passendem Zugriffsvertrag verwenden ihren bereits freigegebenen Arbeitsbereich ohne Neustart; fehlender App-Daten-Zugriff ist damit unschädlich. Vor jedem Ereignis Run, Pfad und aktuelle bekannte Einschränkungen erneut prüfen. Bei Alt-Sessions ohne verlässliche Zugriffsbeschreibung, fehlenden Dateitools oder verweigertem Zugriff: vor Ereignisprompt und ZustellungVorbereitet Wartend mit „Antwortkanal für diese CLI nicht verfügbar“ und konkreter Ursache. Wiederaufnahme: Nutzer behebt lokale Dateirechte und wählt „Erneut prüfen“; bei nicht zur Laufzeit änderbaren/unklaren CLI-Rechten konfiguriert er regulär einen unterstützten CLI-Modus, beendet/startet die Sitzung über die vorhandene Aufgabenbedienung und wählt anschließend „Erneut prüfen“. Neuer Run wird frisch geprüft, kein automatisches Beenden/Neustarten und kein stilles Übernehmen alter Rechte. Die bestehende Session blockiert bis dahin einen Ersatzstart. Eine Freigabe der Eingabebereitschaft kann dieses Hindernis nicht aufheben.

Die Wahl des arbeitsbereichslokalen Kanals ist eine Integrationsentscheidung auf Grundlage der dokumentierten [Codex-Workspace-Schreibgrenzen](https://github.com/openai/codex/blob/main/codex-rs/prompts/templates/permissions/sandbox_mode/workspace_write.md) und [Claude-Code-Berechtigungen](https://code.claude.com/docs/en/permissions); daraus folgt keine pauschale Zusage für beliebige nutzerdefinierte Konfigurationen.

Versioniertes JSON-Protokoll: unveränderliches request.json; von der KI accepted.json, actions/{ActionId}.json und complete.json; Host schreibt results/{ActionId}.json. Veröffentlichung vorzugsweise über temporäre Datei und Rename. Für Dateitools ohne Rename schreibt die KI jedes Dokument einmal vollständig unter seinem endgültigen Namen; der Host verarbeitet es erst nach zwei identischen vollständigen Reads mit bestandener Schema-/Korrelationsprüfung, unvollständige Dateien bleiben unbeachtet und werden erneut gelesen. Jede Meldung trägt EventId/AttemptId/CliRunId. Fremde/alte IDs ablehnen, Größenlimit, feste Namen/UUIDs, keine Reparse-Points/Symlinks oder Pfadsegmente aus Eingaben. Lokaler Nutzer ist Vertrauensgrenze; Inhalte bleiben untrusted.

Die KI wählt Kommentar, Reaktion auf konkreten Ursprungskommentar, PR im zugehörigen Repository oder begründete Bearbeitung ohne Remoteaktion. Host führt ausschließlich strukturierte KI-Aktionen über IScmEventResponder mit ActionId/Outbox-Regeln aus. Kommentarziel ist gespeicherte Issue-/PR-Nummer; PR-Aktion hat validierte Head/Base/Titel/Text und nutzt vorhandenes CreatePullRequestAsync. Dies ist Transport der KI-Entscheidung, keine weitere vom Programm gewählte Antwort/Reaktion. Programmreaktionen bleiben 👀, Wartehinweis bleibt Kommentar.

Vor Dispatch lesend Anmeldung, Repository-/Referenzzugriff, Schreibfähigkeit und lokalen Kanal einschließlich CLI-Zugriffsvertrag prüfen. Fehlende Fähigkeit: Wartend mit Grund und „Erneut prüfen“, null Ereignisprompts. Tatsächlicher 403 beim Schreiben bleibt fehlgeschlagene OutgoingAction und verhindert Erfolgsabschluss. Nur ein nach erfolgreicher Vorprüfung und Übergabe eintretender Kanalverlust führt zu VerarbeitungUngeklärt/Annahme-Timeout statt erneuter automatischer Sendung. Nach Wiederherstellung werden derselbe Attempt und ausstehende Aktionen abgeglichen; kein zweiter Ereignisprompt.

accepted.json bestätigt Annahme; complete.json bestätigt Verarbeitung genau dieses Attempts und listet fertige ActionIds oder explizit noRemoteAction mit Begründung. Verarbeitet erst nach dieser Meldung und erfolgreich abgeglichenen angeforderten Aktionen. Wiederholte Meldungen idempotent. Dies ist korrelierter KI-Protokollnachweis, keine Qualitätsbewertung der Antwort. Ruhezeit, Flush, CLI-Exit und Laufstatus ersetzen ihn nicht. Standardtimeouts: 2 Minuten Annahme, 30 Minuten Abschluss, konfigurierbar. Danach ungeklärt, kein automatischer Neuversand; spätere gültige Meldung kann auflösen. Nach Abschluss nächstes Ereignis, Session bleibt verfügbar und Aufgabe Gestartet. Kein automatischer Aufgabenabschluss.

## 6. WPF-Registrierung und Bedienung (PC-04, PC-07)

In App.xaml.cs `ScmEventMonitoringService` nach PeriodicBackgroundService-Muster sowie `ScmEventExchangeService` als Hosted Services registrieren; Start erst nach DB-Initialisierung/Migration. Pro Poll/Dispatch/Exchange eigener Scope mit scoped DbContext und fachlichen Diensten; kein DbContext in Singleton. Session-/Aufgabenkoordinator und Änderungsbenachrichtigung Singleton; Cancellation lässt persistierte Zustände bestehen.

MainWindow.xaml erhält Seitenleiste „SCM-Ereignisse“ mit wartender Anzahl, AutomationId ScmEventQueueNavigation. MainWindowViewModel.NavigateToScmEventQueueCommand setzt neues ScmEventQueueViewModel als Content; DataTemplate/DI und ScmEventQueueView.xaml ergänzen. Liste: Aufgabe, Repository, Issue/PR, Art, Eingang, Status; Detail: Text, Ursprung, Fehler, Rücknavigation zu Dashboard/Aufgabe. Nach Commit Benachrichtigung auf Dispatcher, Abonnements bereinigen. Leerzustand „Keine wartenden SCM-Ereignisse“; Verarbeitet über Filter einblendbar.

OpenEventCommand/Doppelklick öffnet Detail und aktiviert erlaubte unpausierte Aufgabe. Ohne laufende CLI immer `IDialogService.ShowPluginSelectionDialogAsync`/`PluginSelectionDialogService` auch bei EINEM Plugin; nicht ResolvePluginViaDialogAsync mit Single-Plugin-Abkürzung. Auswahl bestätigt exakten Prefix und optional Projektstandard, danach gemeinsamer Dispatch. Abbruch erhält Eintrag und erneutes Öffnen zeigt Dialog. Keine verfügbare CLI: „Keine aktivierte CLI verfügbar“ plus Einstellungsnavigation. Startfehler am Eintrag, „Erneut versuchen“.

Inzwischen laufende Sitzung: Dialog bietet vorhandene Sitzung mit Hinweis „An laufende CLI übergeben“; Bestätigung führt zu Reuse unter Lease. Anderes Plugin startet nicht parallel und beendet nichts automatisch, sondern zeigt Hinweis auf regulären manuellen Wechsel. Beschäftigte Sitzung wartet. Während Dialog/Start Commands gesperrt, Claims verhindern Poll-/Doppelklickrennen. Abbruch erzeugt keinen weiteren Warte-Kommentar.

Beendet/Archiviert: Detail lesbar, „Wartend – Aufgabe beendet/archiviert; Start gesperrt“, Start/Aktivierung deaktiviert. Dieses Feature führt keinen Reaktivierungsablauf ein und verweist auf keinen unbelegten. Pause ebenso anzeigen; nach regulärer Aufhebung erneut prüfen.

Weitere Detailaktionen: Erneut prüfen (Auth/Berechtigung/Fehler), Eingabebereitschaft bestätigen (nur lebender unklarer Run), Status erneut prüfen und bewusst bestätigter Neuversand bei Ungewissheit. Tatsächliche WPF-Commands/Dialogs verwenden. Falls RaiseUiActionRequested eingesetzt wird, jede Aktion in tatsächlicher zugehöriger WPF-View behandeln; keine unbelegten Blazor-Handler.

## 7. Implementierungsfolge und Testzugang (PC-08)

1. Optionale Verträge, GitHub-Client, DTO-/Versionslogik und Provider-/Validierungstests.
2. Migration, Inbox/Outbox/Checkpoint/Claims/Wiederanlauf und echte SQLite-Tests.
3. Strikte Auswahl, gemeinsamer Start-/Sessionkoordinator, sichere Eingabe, Austausch-/Abschlussprotokoll und Integrationstests.
4. WPF-Host/Navigation/Queue/Dialog, ViewModeltests und echte UI-E2Es.
5. Abnahmematrix, relevante Regressionen und Reviews; Dokumentation zu Pollinggrenzen, Berechtigungen, Rückantwortprotokoll, gesperrten Aufgaben und Ungewissheit.

Neue Testdateien im vorhandenen Testaufbau:

- Softwareschmiede.Tests/Application/Services/ScmEventValidationServiceTests.cs und ScmEventOrchestratorTests.cs.
- Softwareschmiede.Tests/Infrastructure/Plugins/GitHubEventSourceTests.cs für API-Fixtures/Pagination/Versionen und ausgehende Aufrufe.
- Softwareschmiede.IntegrationTests/Services/ScmEventPersistenceTests.cs mit echter temporärer SQLite-Datei und Altbestandsmigration vom unmittelbar vorherigen Migrationsstand.
- Softwareschmiede.IntegrationTests/Services/ScmEventDispatchTests.cs für echten Application-Ablauf mit gefaktem SCM/kontrollierter Session und Arbeitsbereichen.
- Softwareschmiede.Tests/Application/Services/ScmEventExchangeServiceTests.cs sowie App/ViewModels/ScmEventQueueViewModelTests.cs.
- Softwareschmiede.IntegrationTests/Services/ScmEventCliExchangeAccessTests.cs: reale Codex-/ClaudeCli-Adapter und StartSpec mit kontrollierten Konfigurationsfixtures plus separater eingeschränkter Windows-Testprozess; konkrete Zugriffsnachweise siehe unten.
- Softwareschmiede.Tests/E2E/E2E_ScmEventQueue.cs als Teil der bestehenden End2EndTest-Struktur, E2E/Views/ScmEventQueueView.cs und konsolidierter Fact ScmEvents_QueueUndVerarbeitung_E2E mit Trait Scenario=ScmEvents.

### Isolierte WPF-E2E-Infrastruktur

ScmEventE2ETestConfiguration/ScmEventE2ETestKontext unter App/Services/Testing nach Update-Fixture-Muster: ausschließlich aktiv, wenn isolierter Testdatenbankpfad UND valide Szenariodatei unter frischer Testwurzel gesetzt. Keine Produktionscredentials laden. Fake-GitHub-API liest kontrollierte REST-/GraphQL-Seiten; append-only Aufrufprotokoll belegt Reaktionen, Kommentare und KI-Aktionen ohne Secrets. Echter Parser/Monitor/Validierung/Migration/SQLite/Orchestrator/Dialog laufen im separaten WPF-Prozess. Aufgaben/Referenzen dürfen vorbereitet werden, Inbox-/Queuezeilen ausdrücklich nicht.

Vorhandenen KiSimulator und SimulatedPseudoConsoleProcessLauncher um Ready/Busy/Exit/Fehler und Promptaufzeichnung erweitern. Simulator liest request.json und schreibt accepted/actions/complete durch produktiven Dateikanal, bleibt anschließend lebendig. Simulation/Pipe-Transport ermöglicht Pflicht-WPF-E2Es ohne ConPTY; echte Adapter zusätzlich durch Transkript-/Transporttests prüfen. Fixturesteuerung nur über Testverzeichnis, keine öffentliche Produkt-Test-API. Related Szenarien in minimaler Testmethodenzahl und derselben App bündeln; zweiter Appstart nur für Persistenzneustart.

### Verbindlicher CLI-Kanalnachweis (PC-06-R1)

Für beide realen Adapter in `ScmEventCliExchangeAccessTests` denselben Vertrag parametrisiert prüfen: die produktiven StartSpec-/Konfigurationsauswerter müssen das tatsächlich erlaubte Arbeitsverzeichnis und die effektive Dateitool-Freigabe liefern; freie CLI-Argumente, verändertes Arbeitsverzeichnis, Read-only, Deny, deaktivierte Tools und unbekannte Profile dürfen nicht durch pauschales Supported übergangen werden. Positive Fixtures bilden Codex workspace-write und ClaudeCli acceptEdits ohne Deny ab; negative Fixtures behalten Nutzerregeln bytegleich. Kein Live-KI-Aufruf und keine Nutzercredentials.

Den Dateitransport mit einem separaten Windows-Prozess unter beschränktem Token und ausschließlich innerhalb der frischen Test-Arbeitswurzel erlaubten Schreibrechten prüfen. Zugriff auf ein benachbartes simuliertes App-Daten-Verzeichnis muss im selben Prozess nachweislich fehlschlagen; anschließend request lesen, accepted und KI-Aktion schreiben, Hostresultat lesen und complete schreiben. Host nimmt genau einen simulierten GitHub-Kommentar am gespeicherten Ursprung an und beendet den Eventlauf erst danach. Sowohl Rename-Veröffentlichung als auch vollständige Write-Dateien durchlaufen. Der Test darf nicht auf gewöhnliche unbeschränkte Benutzerrechte zurückfallen; fehlende Durchsetzbarkeit der Testbeschränkung ist ein fehlgeschlagener Pflichtnachweis.

Zwei Lebenszyklen abdecken: neuer Start mit begrenzter Arbeitswurzel sowie bereits lebender Run, dessen unveränderte Startfreigabe App-Daten ausschließt, aber den arbeitsbereichslokalen Kanal einschließt; zweiter Fall braucht keinen Neustart. Negativ: Verweigerung des Kanals trotz Host-Schreibbarkeit beziehungsweise unbekannter Alt-Run ergibt Wartend mit lesbarer Ursache und null Ereignisprompts/Abschlüssen. Nach korrigierten Rechten plus „Erneut prüfen“ beziehungsweise regulärem Neustart mit geprüftem Vertrag genau eine Zustellung. Runwechsel verwirft alte Zugriffsbeschreibungen; späterer Zugriffsausfall nach Zustellung bleibt ungeklärt und wird nicht automatisch wiederholt. Ausschluss aus Git, Symlink-/Pfadschutz und keine Secretweitergabe ebenfalls nachweisen.

### Verbindliche UI-E2E-Szenarien

E1: MainWindow → SCM-Ereignisse → gültiger Owner-Kommentar über Fake-API ohne Standard trotz installiertem Plugin → ein Eintrag, eine 👀-Reaktion, ein Warte-Kommentar; Replay unverändert. Detail/Ursprung/Rücknavigation prüfen.

E2: Neu-Aufgabe öffnen → aktivieren und echter Auswahldialog auch bei Einzelplugin → abbrechen → erneut öffnen → Auswahl → Arbeitsbereich/Start/exakt ein Prompt mit Inhalt/Ursprung. Zuerst Zugestellt, dann InBearbeitung, erst nach Simulatorabschluss Verarbeitet. Aufgabe Gestartet, CLI lebt. Folgeereignis derselben Sitzung ohne zweiten Prozess.

E2-Filter: Neben dem abgeschlossenen Ereignis ein weiteres tatsächlich aufgenommenes wartendes Ereignis vorbereiten. In der Übersicht „Verarbeitet“ wählen → abgeschlossener Eintrag sichtbar, Status und Ursprung korrekt; zurück zu „Wartend“ → ausschließlich wartender Bestand und korrekte Anzahl sichtbar, abgeschlossener Eintrag nicht mehr enthalten. Filterwechsel verändert weder Aufgabenstatus noch Prompt-/Kommentaranzahl.

E3: Aktive Aufgabe ohne Session → Dialog → simulierter Startfehler → verständlich sichtbar/wiederöffnbar → Retry erfolgreich. Doppelklick plus Poll höchstens ein Dispatch. Inzwischen laufende Session nach Dialogbestätigung wiederverwenden; keine CLI zeigt Hinweis/Einstellungen ohne Datenverlust.

E3-Bereitschaft: Echte Aufnahme für lebende Simulator-Session mit gültigem Kanalvertrag, aber unbekannter Eingabebereitschaft → Detail zeigt Wartend und den betroffenen Run. „Eingabebereitschaft bestätigen“ für diesen Run betätigen → genau ein Ereignisprompt trotz weiterem Poll/Doppelklick, anschließend reguläre Statusfolge. Zweiter Fall: Detail zeigt Run A, Fixture lässt A enden und Run B starten, bevor die Bestätigung für A verarbeitet wird → Meldung „CLI-Sitzung hat sich geändert“, kein Prompt an B, neue Bestätigung ausdrücklich erforderlich; diese sendet genau einmal an B. Ein verweigerter Kanalvertrag bleibt trotz Bereitschaftsbestätigung gesperrt.

E3-Ungewissheit: Fixture erzeugt einen tatsächlich begonnenen Promptwrite mit ungewissem Ergebnis und ohne Annahmedatei → Detail zeigt ZustellungUngewiss samt Grund. „Status erneut prüfen“ wählen → Zustand bleibt ungewiss, kein zusätzlicher Prompt, keine neue AttemptId. „Neu senden“ öffnen → sichtbarer Hinweis auf mögliche Doppelbearbeitung → abbrechen → Zustand/Attempt unverändert. Erneut öffnen und bestätigen → genau eine neue AttemptId und ein zusätzlicher Ereignisprompt; Poll und Doppelklick erzeugen keinen weiteren Versuch. Simulator bestätigt ausschließlich den neuen Attempt → Verarbeitet sichtbar. Alter verspäteter Abschluss darf den neuen Attempt nicht abschließen. Separater Fall mit verspäteter gültiger Meldung des noch aktuellen Attempts: „Status erneut prüfen“ löst den Zustand ohne Neuversand auf.

E4: Beendete UND archivierte sowie pausierte Aufgabe → lesbarer Eintrag/Sperrgrund, kein Start/Statuswechsel. Nach regulärer Aufhebung ausschließlich der Pause erneut zustellbar. Keine implizite Reaktivierung.

E5: Über echte Aufnahme wartenden Eintrag erzeugen, eigene Test-App regulär schließen und dieselbe isolierte DB neu starten → Eintrag erhalten, Dialog/Verarbeitung möglich; Replay ohne zweiten Hinweis.

E6: Issue und PR je als Kommentar-/Beschreibungsvariante. Beschreibung vollständig im Prompt, null Empfangsreaktionen. KI-Rückantwort über Kanal an gespeicherten Ursprung; fehlende Berechtigung/Abschluss sichtbar ungeklärt, nicht verarbeitet. Keine programmseitige Abschlussreaktion.

E6-Wiederaufnahme: Bereits tatsächlich aufgenommenen, noch nicht zugestellten Eintrag durch Test-401 beziehungsweise fehlende Repository-Schreibberechtigung sperren → Detail zeigt Anmeldung/Berechtigung als Ursache, null Ereignisprompts. Testanmeldung/-berechtigung ausschließlich in der isolierten Fake-API wiederherstellen → im Detail „Erneut prüfen“ → genau eine reguläre Zustellung und erfolgreicher Abschluss; vorhandene 👀-Reaktion/Warte-Kommentar bleiben jeweils einmalig. Zusätzlich echten Fake-403 bei einer KI-Antwort nach Annahme erzeugen → Aktionsfehler sichtbar, kein Verarbeitet; Schreibrecht wiederherstellen → „Erneut prüfen“ → nur ausstehende Aktion erfolgreich, derselbe Attempt wird verarbeitet, kein zusätzlicher Prompt/Hinweiskommentar. Fehlender Abschluss bleibt ungeklärt bis zur gültigen Abschlussmeldung.

E6-Kanal: Lebende Session mit verweigertem/unklarem Kanalvertrag → Detail zeigt den Kanalgrund vor jeder Ereigniszustellung. Korrigierbare lokale Rechte über Fixture wiederherstellen → „Erneut prüfen“ → genau eine Zustellung. Für einen Run mit unveränderbarer Read-only-Konfiguration über die reguläre Aufgabenbedienung beenden und mit unterstützter Konfiguration starten, zur Queue zurückkehren und erneut prüfen → neuer geprüfter Run erhält genau einen Prompt, alter Run keinen. Der eingeschränkte Prozessnachweis aus PC-06-R1 bleibt zusätzlich verbindlich.

### Vollständige Abnahmematrix

| Kriterium / Befund | Test und erwarteter Nachweis |
|---|---|
| Owner/Trigger, Issue/PR, Kommentar/Beschreibung | Validation-/SourceTests, E1/E6: case-insensitive, fremder Autor trotz gültigem Viewer, Fremdeditor/fehlender Editor; ungültig=null Writes/Inbox-Aufträge/Starts |
| PC-01 Versionen/Scope | SourceTests: Initialtrigger, neue Referenz nach Cursor, Metadatenzeit ohne Textwechsel, echter Wechsel/Kommentaränderung, verschiedene Provider/Hosts/Repos/Kommentararten, Replay ohne Duplikat |
| 👀/Wartehinweis | Source/OrchestratorTests, E1/E6: eine Viewer-Reaktion pro Kommentar, keine Beschreibungsempfangsreaktion, Markerabgleich nach Timeout, keine Selbsttrigger/Abschlussreaktion |
| Aktivierung/aktiv bleiben | DispatchTests, E2/E4: Neu→Gestartet, aktive unverändert, Pause/Beendet/Archiviert unverändert, Abschluss nie Beendet |
| Laufende CLI/Standardstart | DispatchTests: lebende Session, neuer/vorhandener Klon, Projekt vor Global, kein Standard trotz installiertem Plugin, deaktiviert/entfernt/Executable ungültig ohne Ersatzstart; vorhandene Session trotz fehlendem Standard |
| PC-04 Start/Bereitschaft | Adapter-/DispatchTests und E3-Bereitschaft: Ready/Busy/unbekannt, rungebundene Bestätigung und Runwechsel, noch nicht bereiter Start, Exit vor/bei Write, zwei Events FIFO, Pause, manuelle Parallelstarts; Rollback erhält vorhandenen Klon |
| Queue/Dialog und PC-08-R1 Filter | ViewModeltests, E1–E5 einschließlich E2-Filter: Navigation, Einzelplugin, Abbruch/erneut öffnen, keine Auswahl, Fehler/Retry, Doppelklick/Poll, lebende Sitzung, Sperren, Neustart; Verarbeitet→Wartend ohne Seiteneffekt |
| PC-02 Persistenz/Crash | PersistenceTests: Folgeseitenfehler, gleiche Zeiten, Absturz vor/nach Commit, Reaction-/Comment-POST und Promptwrite, abgelaufene Claims, zwei Consumer/Queue gegen Poll; keine automatische Wiederholung bei Ungewissheit |
| PC-05 Ereignisende | Exchange-/DispatchTests, E2/E6: Flush ohne Annahme, Annahme ohne Abschluss, richtiger/falscher Event-/Attempt-/Run-Abschluss, weiterlebende CLI/Folgeprompt; Exit/Fehler/Timeout, spätere gültige Meldung |
| PC-06 Rückantwort/Sicherheit | Exchange-/Provider-/Adaptertests, E6: KI-Kommentar/Reaktion/PR zum richtigen Ziel, 403, bestehende Session ohne Kanal, keine Tokens in Logs/Prompt/Args/Queue, Quotes/Mehrzeiler/ESC/NUL/C0/C1 als Daten, Pfad-/Symlinkschutz |
| PC-06-R1 realer CLI-Kanal | ScmEventCliExchangeAccessTests und E6-Kanal: Codex/ClaudeCli-Startverträge, begrenzter neuer/bestehender Run ohne App-Daten-Zugriff, Antwort und Abschluss; gesperrter/unbekannter Zugriff vor Dispatch, geprüfte Wiederaufnahme, kein Rechte-Bypass |
| PC-08-R1 Ungewissheit | E3-Ungewissheit und Persistence-/DispatchTests: Statusprüfung ohne Prompt, Neuversand abbrechen/bestätigen, genau eine neue AttemptId, keine automatische Wiederholung, verspätete korrelierte Auflösung |
| Fehler/Wiederaufnahme und PC-08-R1 Berechtigung | Source-/Outboxtests und E6-Wiederaufnahme: 401 bis Reauth gesperrt, 403 vor/nach Zustellung, Erneut prüfen nach wiederhergestelltem Recht ohne doppelten Prompt/-kommentar; 429 Retry-After und Backoff auf Serviceebene |
| Erweiterbarkeit/Migration | zweiter Fakeprovider durch gleiche Pipeline, bestehender Provider ohne Fähigkeit; echte SQLite-Migration erhält alte Aufgaben/Referenzen, Neustart ohne Inboxverlust |

### Ausführung und Freigabe

Windows mit .NET Desktop SDK/Runtime und WPF-Testdesktop. Vor Tests vollständiger erfolgreicher Build, nie --no-build. Tests im Vordergrund; keine background-dotnet-test-Prozesse. Jeder Agent-Sandbox-Testaufruf setzt SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 gemäß Repositoryanweisung. Neue Pflicht-E2Es verwenden Simulation und dürfen davon nicht übersprungen werden; für menschliche Testläufe die Sandboxvariable nicht empfehlen.

```powershell
dotnet build Softwareschmiede.slnx
$env:SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS='1'; dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface" --logger "trx;LogFileName=scm-regular.trx"
$env:SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS='1'; dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj --filter "FullyQualifiedName~ScmEvent" --logger "trx;LogFileName=scm-integration.trx"
$env:SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS='1'; dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Scenario=ScmEvents" --logger "trx;LogFileName=scm-e2e.trx"
$env:SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS='1'; dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category=OsInterface" --logger "trx;LogFileName=scm-osinterface-regression.trx"
$env:SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS='1'; dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj --filter "FullyQualifiedName~EntwicklungsprozessServiceTests" --logger "trx;LogFileName=scm-start-integration-regression.trx"
```

Der getrennte OS-Interface-Lauf muss insbesondere die bestehenden Szenarien `E2E_PluginAuswahlUndWechsel` (Auswahl/Wechsel), `E2E_PluginProjectDefault` (Projekt vor Global), `E2E_AufgabeStarten` (Start/Arbeitsbereich), `E2E_AufgabePausieren` (Pause-Guards), `E2E_TaskDetailNavigation` und `E2E_TaskWechselUeberMenue` (Navigation nach Queue-Erweiterung) erfassen; deren tatsächliche Fact-Namen und Ergebnisse im Bericht zuordnen, da die Dateien Teil der konsolidierten End2EndTest-Klasse sind. Der reguläre Tests-Lauf deckt ausdrücklich `PluginSelectionServiceTests`, `EntwicklungsprozessServiceTests` einschließlich WorkingDirectoryValidation/BasisBranch/Initialisierungsskript, `KiAusfuehrungsServiceTests` einschließlich WorkingDirectory/InSourceDirectory, `PseudoConsoleSessionTests_WritePromptAsync` und `SimulatedPseudoConsoleProcessLauncherTests` ab. Bestehende `EntwicklungsprozessServiceTests` im IntegrationTests-Projekt über den zusätzlichen Befehl ausführen; der reine ScmEvent-Filter würde sie nicht erfassen.

ConPTY-Skips mit Testnamen und Ursache ehrlich dokumentieren; sie ersetzen weder neue Pflicht-E2Es noch den eingeschränkten Kanalnachweis. E2E-Timeout zuerst App-Startlog auf Exception prüfen. Keine fremde/selbsthostende App zum Freimachen eines Builds beenden.

Alle E1–E6 einschließlich E2-Filter, E3-Bereitschaft, E3-Ungewissheit, E6-Wiederaufnahme und E6-Kanal müssen im konsolidierten UI-Test ausgeführt und erfolgreich sein; jede Teilsequenz erhält einen eigenen Nachweiseintrag im Testbericht. Zusätzlich muss der Pflicht-Kanalnachweis für beide realen Adapter einschließlich eingeschränktem Prozess erfolgreich sein. Fehlende, übersprungene, nicht ausgeführte oder fehlgeschlagene Pflicht-E2Es sind Abnahmefehler, nicht durch Unit-/Integrationstests kompensierbar. Berichte nennen Befehle, Ergebnisse, TRX, Regressionen und Matrixabdeckung. Keine echten GitHub-Schreibtests und keine Nutzercredentials als Voraussetzung.

## Definition of Done

Alle präzisierten Akzeptanzkriterien und PC-01 bis PC-08 implementiert und durch zugeordnete Tests belegt. Migration, Build, relevante Regressionen und Pflicht-E2Es erfolgreich. Plan-/UI-/Code-Reviews ohne offene Befunde, Dokumentation beschreibt Pollinggrenzen und ungewisse Zustellung wahrheitsgemäß. Kein Verarbeitet allein durch WritePromptAsync. Ereignisabschluss beendet weder Aufgabe noch weiterverwendbare CLI.

## Offene Punkte

Keine.
