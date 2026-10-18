← [Zurück zur Übersicht](index.md)

# GitHub-Ereignisüberwachung — Technischer Ablauf

## Übersicht

`ScmEventMonitoringService` fragt regelmäßig die lokal verknüpften SCM-Referenzen sowie repository-weit alle Referenzen aktivierter Projekt-Repositories ab. `GitHubPlugin` liest Quelltexte und Metadaten, `ScmEventStore` persistiert Beobachtungen und `ScmEventOrchestrator` validiert und verteilt gültige Einträge. Die Queue-Oberfläche verwendet denselben Orchestrator für manuelle Aktionen und Sitzungswahl.

## Ablauf

### 1. Referenzen und Quellen abfragen

Der Monitor lädt aktive lokale Aufgaben samt Repository, Issue-Referenz und Pull Requests sowie alle aktivierten, an Projekten erfassten `GitRepository`-Einträge. Er überspringt Provider ohne `IScmEventSource`. Jede lokale Alias-Adresse wird über `GetEventRepositoryAsync` in die kanonische Repository-Identität aufgelöst; erst bei vollständig gelungener Auflösung werden Ereignisse gelesen. Anschließend enumeriert `GitHubPlugin.EnumerateReferencesAsync` jedes aufgelöste Repository: offene Issues und Pull Requests immer, geschlossene nur bei `updatedAt` innerhalb des Aktivitätsfensters (`enumeration/`-Checkpoint, Watermark abzüglich zweier Poll-Intervalle). Alle Referenzen — aufgabenverknüpft wie enumeriert — durchlaufen danach denselben `ReadEventsAsync`-Pfad. Für GitHub werden Beschreibung, Issue-/PR-Kommentare, PR-Zeilenkommentare und veröffentlichte Reviews gelesen. REST-Seiten und GraphQL-Verbindungen werden vollständig abgearbeitet; Quell-Checkpoints führen den jeweiligen Wasserstand.

Beteiligte Komponenten:
- `ScmEventMonitoringService.RunOnceAsync` — ermittelt verknüpfte und repository-weite Referenzen, fragt Quellen ab und startet Dispatches.
- `ScmEventMonitoringService.EnumerateRepositoryAsync` — Repository-Enumeration mit eigenem `enumeration/`-Checkpoint und Backoff; Fehler blockieren aufgabenverknüpfte Referenzen nicht.
- `GitHubPlugin.GetEventRepositoryAsync` — löst Repository, angemeldeten Benutzer und Schreibberechtigung auf.
- `GitHubPlugin.EnumerateReferencesAsync` — listet offene und im Aktivitätsfenster bewegte geschlossene Referenzen.
- `GitHubPlugin.ReadEventsAsync` — liest Textstände und Versionsmetadaten.

### 2. Version und Identität prüfen

Gehört eine Referenz zu keiner lokalen Aufgabe und enthält der gelesene Batch mindestens eine qualifizierte Beobachtung, löst `ScmEventTaskResolver.ResolveAsync` die Aufgabe auf: Es lädt die Anlagedaten über `IScmEventSource.ReadIssueAsync`/`ReadPullRequestAsync`, sucht vorhandene Aufgaben über die Referenzschlüssel und legt bei Fehlschlag genau eine Aufgabe im Projekt des aufgelösten `GitRepository` an (`CreateFromIssueEventAsync` mit `SourceKey`-Unique-Index bzw. `CreateFromPullRequestAsync` mit dem `(Provider, RepositoryId, PullRequestNumber)`-Index — bei Anlagerennen gewinnt die bestehende Anlage). Auflösungsfehler laufen in den Quell-Checkpoint-Backoff.

`ScmEventStore.AcceptBatchAsync` vergleicht Inhaltshashes und Revisionen, ordnet Beobachtungen einer eindeutigen Aufgabe zu und speichert Inbox-Eintrag, Snapshot und Checkpoint. `ScmEventValidationService.GetRejection` verlangt den case-insensitiven Trigger `$Softwareschmiede`, die Übereinstimmung des angemeldeten Nutzers mit dem Repository-Owner und die Übereinstimmung des tatsächlichen Autors/Bearbeiters mit diesem Nutzer. Ein nicht bestimmbarer Editor oder eine mehrdeutige lokale Zuordnung erzeugt keinen frei zugeordneten Auftrag.

### 3. Bestätigung und CLI-Zustellung

`ScmEventOrchestrator.DispatchAsync` beansprucht eine Aufgaben-Lease. Issue-Kommentare und PR-Kommentare erhalten über `ScmEventOutbox` eine `eyes`-Reaktion; veröffentlichte PR-Reviews werden durch GitHub GraphQL (`addReaction` auf dem Review-Node) bestätigt. Beschreibungen erhalten keine Empfangsreaktion. Scheitert eine Reaktion, wird kein Ersatzkommentar für genau diese Reaktion erzeugt; ein separater Warte-Kommentar kann bei wartender Verarbeitung dennoch hinzukommen.

Beendete, archivierte oder pausierte Aufgaben werden nicht gestartet — auch nicht bei Wiederverwendung durch `ScmEventTaskResolver`. Jede neue, nicht blockierte Aufgabe wird pro Zustellversuch automatisch gestartet (`Neu` → `Gestartet`): ohne `LokalerKlonPfad` über `EntwicklungsprozessService.ProzessStartenUndCliStartenAsync` (Klon, Branch, CLI-Start), sonst über `CliNeustartenAsync` — das gilt unverändert auch für automatisch angelegte Aufgaben, die ebenfalls bereits ohne Standard-CLI entstehen und deren Ereignis dann zustellbar wartend bleibt. Eine lebende Sitzung wird bevorzugt; andernfalls wird das Aufgaben-, Projekt- oder globale Standard-KI-Plugin aufgelöst. Eine Plugin-Auswahl allein bedeutet keine Kanalunterstützung: Der `IScmPromptSessionAdapter` erkennt derzeit Codex `v0.114.0`, Claude Code `v2.1.0`, Devin CLI `v3000.11.92` und den eigens gestarteten SCM-Simulator. Codex erfordert eine sicher auflösbare `workspace-write`-Freigabe, Claude eine sicher auflösbare `acceptEdits`-Freigabe ohne entgegenstehende Regeln, Devin einen sicher auflösbaren `--permission-mode`-Wert `accept-edits`, `dangerous` oder `bypass` (auch via `DEVIN_PERMISSION_MODE`) ohne Deny-/Ask-Regeln in `~/.devin/config.json`/`~/.config/devin/config.json`/`<Arbeitsverzeichnis>/.devin/config.json`. Whitelistbar sind zudem `--respect-workspace-trust true|false`, `--model`, `-c`/`--continue`, `-r`/`--resume` und `--sandbox`; jeder andere Startparameter führt zu `Unknown`. Zusätzlich muss das tatsächliche Dateitool den Lesen-/Schreiben-/Umbenennenachweis am rungebundenen Antwortkanal bestehen — die Klassifizierung allein ist nie eine Freigabe. Fehlt einer dieser Nachweise, wird kein Ereignisprompt gesendet und der Eintrag wartet. Andere KI-Plugin-Präfixe, darunter GitHub Copilot, liefern `Unknown`.

Vor dem Schreiben prüft der `IScmPromptSessionAdapter` Eingabebereitschaft und den isolierten Antwortkanal. Der Austausch enthält Originaltext, Ursprung und IDs für Ereignis, Versuch und Lauf. Das erfolgreiche Schreiben bedeutet nur „Zugestellt“; erst korrelierte Annahme, KI-Aktionen und Abschlussmeldung ergeben „Verarbeitet“.

### 4. UI-Aktionen und Wiederaufnahme

`ScmEventQueueViewModel` lädt gespeicherte Ereignisse und bietet Öffnen, erneute Prüfung, Bereitschaftsbestätigung, Statusprüfung und bestätigten Neuversand an. Fehlgeschlagene Remoteaktionen können separat erneut versucht werden. Zustellung mit ungewissem Ergebnis löst keinen automatischen Prompt-Neuversand aus.

## Diagramm

```mermaid
flowchart TD
    A[ScmEventMonitoringService] --> B[GitHubPlugin: Quellen lesen]
    B --> C[ScmEventStore: Snapshots und Inbox]
    C --> D[ScmEventValidationService]
    D -->|gültig und eindeutig| E[ScmEventOrchestrator]
    E --> F[CLI-Antwortkanal]
    E --> G[ScmEventOutbox]
    G --> H[GitHubPlugin: Reaktion oder Antwort]
    C --> I[SCM-Ereignisqueue]
    I --> E
```

## Fehlerbehandlung

Providerfehler erhalten Fehlerart, Diagnose und nächste Prüfzeit am Checkpoint. Authentifizierungs- oder Berechtigungsfehler sperren automatische Wiederholungen bis zu einer gezielten Prüfung. Ausgangsaktionen sind idempotent gekennzeichnet und werden vor Wiederholung abgeglichen. Ein unterbrochener oder nicht sicher bestätigter CLI-Schreibversuch wechselt in einen ungewissen Status; nach Statusprüfung ist ein Neuversand nur nach sichtbarer Warnung möglich.
