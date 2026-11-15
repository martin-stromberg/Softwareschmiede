# Umsetzungsplan: Konto `github-actions` als akzeptierte Autor-Identität der SCM-Ereignisvalidierung

## Übersicht

Die Akteurs-Identitätsprüfung in `ScmEventValidationService.GetRejection` akzeptiert künftig neben dem Repository-Owner auch eine kleine interne Whitelist vertrauenswürdiger Automatisierungs-Logins (einziger Eintrag: `github-actions`, in den beiden Schreibweisen `github-actions[bot]` und `app/github-actions`, case-insensitive). Da `GetRejection` an allen drei Prüfstellen (Aufnahme-Diagnose, Trigger-Suche, Dispatch-Revalidierung) verwendet wird, greift die Änderung end-to-end ohne Aufrufer- oder Interface-Änderung — keine Migration, keine Konfiguration, keine UI-Änderung. Dokumentation und Hilfedoku werden angepasst, Tests in Unit- und Integrationslane ergänzt.

## Scope-Klärung

Verbindliche fachliche Abgrenzung vor der Umsetzung — übernommen bzw. konkretisiert aus dem Abschnitt `Fachlicher Scope` der `requirement.md`:

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Eine beobachtete SCM-Textversion (`ScmEventObservation` — Issue-/PR-Beschreibung, Issue-Kommentar, Review-Kommentar, Pull-Request-Review) am Schritt der Akteurs-Identitätsprüfung in `ScmEventValidationService.GetRejection` |
| Auslösende Ereignisse | Regulärer Poll-Zyklus des `ScmEventMonitoringService` (`ReadEventsAsync`): Textversion mit gültigem Trigger (Keyword im Modus `Benutzerkonto`, frei stehende `@`-Erwähnung im Modus `SeparatesKonto`), deren Autor bzw. letzter Bearbeiter das GitHub-Konto `github-actions` ist |
| Erwartete Wirkung | Annahme statt Diagnose „Textversion stammt nicht vom Repository-Owner": `ScmIncomingEvent` entsteht, bei unzugeordneter Referenz läuft die Aufgabenanlage über `ScmEventTaskResolver.ResolveAsync`, die Revalidierung beim Dispatch (`ScmEventOrchestrator.DispatchCoreAsync`) lässt das Ereignis passieren, Zustellung an die konfigurierte CLI wie bei Owner-Autorschaft |
| Umgang mit fehlenden Objekten | *Zugehörige Aufgabe nicht vorhanden:* **erzeugen** — unverändert über `ScmEventTaskResolver`. *Autor-/Bearbeiter-Identität nicht bestimmbar:* **verwerfen** — Regel „Bearbeiter nicht bestimmbar" bleibt unverändert (Akzeptanz setzt bestimmbaren Login voraus). *Zwischenzeitlich bearbeitete Textversion:* **wiederverwenden** — Prüfung richtet sich wie bisher an den aktuellen Akteur (`Editor*` bei `LastEditedAt`, sonst `Author*`); `github-actions` tritt als gleichwertige Alternative neben den Owner, sowohl als Autor als auch als Bearbeiter. *Provider ohne `github-actions`-Konzept (Bitbucket, LocalDirectory):* **unverändert** — beide implementieren `IScmEventSource` nicht und erzeugen keine Beobachtungen |

### Scope-Ausschlüsse

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | Keine Umsetzung zeitlicher Auftragsbestandteile („warte 3 Stunden" im Referenz-Kommentar) | Fachlicher Scope der `requirement.md`: organisatorischer Text im Beauftragungs-Prompt, kein Scheduling-Feature; Aufgabe startet wie bisher sofort | bestätigt (verbindlich aus requirement.md, 2026-11-15) |
| 2 | Keine generelle Freigabe beliebiger Bot-/App-Konten (`dependabot[bot]`, fremde GitHub Apps etc.) | Fachlicher Scope: nur `github-actions` wird akzeptiert; Whitelist ist bewusst klein und intern | bestätigt (verbindlich aus requirement.md, 2026-11-15) |
| 3 | Keine Änderung des Trigger-Vertrags (Keyword-/Mentions-Regeln, Frei-stehend-Regel, `BereitsReagiertRejection`, Marker „Eigene Antwort", Viewer-=Owner-Regel im `Benutzerkonto`) | Fachlicher Scope: ausschließlich die Akteurs-Identität wird erweitert | bestätigt (verbindlich aus requirement.md, 2026-11-15) |
| 4 | Keine neue Benutzerinteraktion, kein Opt-in-Schalter, keine neue Anwendungseinstellung | Orchestrator-Entscheidungen 4+5: interne Konstante statt konfigurierbarer Einstellung; Vertrauensstellung als dokumentierte Betriebsregel in `business-rules.md` | bestätigt (verbindlich, 2026-11-15) |
| 5 | Keine Änderung an Ereignis-Deduplizierung, Checkpoints, Reaktionsversand oder Antwortkanal | Fachlicher Scope | bestätigt (verbindlich aus requirement.md, 2026-11-15) |
| 6 | Keine rückwirkende Aufnahme bereits persistierter Ablehnungen auf **zugeordneten** Referenzen: Ein Snapshot mit gespeicherter Diagnose „Textversion stammt nicht vom Repository-Owner" und `IdentityResolved = true` wird bei unverändertem `BodyHash` nicht erneut bewertet (`ScmEventStore.AcceptBatchAsync` Z. 64–66); erst eine Remote-Textänderung (neuer `BodyHash`) löst die Neubewertung aus | Entwurfsentscheidung: Der Referenzfall (`martin-stromberg/Tankradar#62`) ist eine unzugeordnete Referenz und heilt sich selbst — `UnassignedDiagnostic`-Snapshots sind vom Skip ausgenommen und werden jeden Poll vollständig neu gelesen/bewertet (`ScmEventMonitoringService` Z. 179–182). Für zugeordnete Alt-Fälle (selten: Kommentar einer GitHub Action auf einer bereits aufgabenverknüpften Referenz, die vor diesem Release beobachtet wurde) ist der dokumentierte Wiederanlauf eine Remote-Bearbeitung der Textversion. Eine Datenbankmigration für diesen Einzelfall wäre unverhältnismäßig | bestätigt (Entwurfsentscheidung, 2026-11-15) |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|------------------|------------|
| Akzeptierte Identität | Interne Whitelist `TrustedAutomationLogins` als `private static readonly`-Sammlung in `ScmEventValidationService` mit den Einträgen `"github-actions[bot]"` (REST/Bot-User) und `"app/github-actions"` (GraphQL-App-Autor); Vergleich ausschließlich über `actorLogin` mit `StringComparer.OrdinalIgnoreCase` | Orchestrator-Entscheidungen 2+4: Login-Match genügt (Beobachtungsmodell liefert Login bereits), bleibt GitHub-Enterprise-tauglich (gleicher Login-Namensraum), kein `__typename`-/Node-ID-Abgleich nötig — damit keine Modell- oder Plugin-Änderung. Whitelist statt Einzelkonstante, damit spätere Ergänzungen ohne Strukturbruch möglich sind |
| Provider-Bindung | Provider-agnostischer Login-Match; kein `ProviderPrefix`-/`Host`-Check in der Validierung | Orchestrator-Entscheidung 3: Die Login-Strings sind GitHub-spezifisch; auf anderen Providern kann derselbe Login ohnehin nicht vertrauenswürdig auftreten (keine Sonderrolle dort) — die Kollision ist praktisch irrelevant, ein Provider-Check würde die Anwendungsschicht mit Providerwissen koppeln. Faktisch erzeugt ohnehin nur `GitHubPlugin` Beobachtungen (einzige `IScmEventSource`-Implementierung) |
| Kontomodus | Akzeptanz gilt modusunabhängig in beiden Modi | Orchestrator-Entscheidung 1: dieselbe Akteursprüfung (`SameIdentity` gegen `OwnerId`/`Owner`) sitzt in beiden Modi an derselben Stelle — keine Modus-Verzweigung nötig |
| Identitätsprüfung | Neuer privater Hilfsvergleich `IsTrustedAutomationLogin(string? login)` neben `SameIdentity`; Akteursregel wird zu `SameIdentity(...) \|\| IsTrustedAutomationLogin(actorLogin)` | Nur-Login-Vergleich (keine ID): Die Node-ID von `github-actions[bot]` ist nicht Teil der Akzeptanzregel; ein Login-Match ist ausreichend und hält die Regel klein. Position nach „Bearbeiter nicht bestimmbar", vor „Eigene Antwort" — wie die bestehende Owner-Prüfung |
| Ablehnungsdiagnose | Bestehender Text „Textversion stammt nicht vom Repository-Owner" bleibt unverändert | Fachlicher Scope („allenfalls Wortlaut-Anpassung"): Ein Text, der weder vom Owner noch von einem vertrauenswürdigen Konto stammt, wird weiterhin mit derselben Diagnose abgelehnt — keine Wortlaut-Änderung, keine Auswirkung auf UI-Auswertung oder gespeicherte Diagnosen |
| Provider-Plugin | Keine Änderung am `GitHubPlugin` (`TextFields`, `AddObservation`) | `author { login ... on Node { id } }` liefert den Bot-Login bereits; `__typename` ist laut Entscheidung nicht erforderlich |
| Konfigurierbarkeit | Keine Anwendungseinstellung (kein `AppEinstellungService`-Schlüssel) | Orchestrator-Entscheidung 4: fest verdrahtete interne Konstante/Whitelist, analog zum verdrahteten `DefaultTriggerKeyword`-Standard |
| Rückwirkung Altbestand | Keine Migration/Invalidierung gespeicherter Snapshots | Siehe Scope-Ausschluss 6: unzugeordnete Referenzen (inkl. Referenzfall) heilen über den bestehenden `UnassignedDiagnostic`-Wiederanlauf selbst; zugeordnete Alt-Fälle werden über Remote-Edit neu bewertet |

## Programmabläufe

### Annahme einer `github-actions`-Textversion auf unzugeordneter Referenz (Referenzfall, Modus `SeparatesKonto`)

1. `ScmEventMonitoringService` (SeparatesKonto-Pfad, Z. 130–141) liest die Notifications-Inbox (`IScmNotificationSource.EnumerateNotificationsAsync`); eine Benachrichtigung mit `reason = mention` zu einem Workflow-erzeugten Issue wird zur Beobachtungsquelle.
2. `IScmEventSource.ReadEventsAsync` liefert die Beobachtung mit `AuthorLogin = "github-actions[bot]"` (Bot-Identität, von `GitHubPlugin.AddObservation` unverändert durchgereicht).
3. Bei unzugeordneter Quelle sucht der Poll-Zyklus eine qualifizierte Trigger-Beobachtung: `ScmEventValidationService.GetRejection` gibt `null` zurück — Trigger (frei stehende `@`-Erwähnung des separaten Kontos) vorhanden, keine Kontoreaktion, Akteur bestimmbar, `SameIdentity` gegen Owner scheitert, aber `IsTrustedAutomationLogin(actorLogin)` trifft.
4. `ScmEventTaskResolver.ResolveAsync` legt die Aufgabe zur Referenz an (unverändert, identitätsagnostisch).
5. `ScmEventStore.AcceptBatchAsync` ruft `GetRejection` erneut → `null` → `ScmIncomingEvent` wird persistiert, `ScmContentSnapshot.Diagnostic = null`.
6. `ScmEventOrchestrator.DispatchAsync`/`DispatchCoreAsync` revalidiert mit frisch geladenem `ScmEventRepository` → `null` → Ereignis wird an die konfigurierte CLI zugestellt (Empfangsreaktion auf Kommentare wie bisher; Beschreibungen ohne Reaktion).

Beteiligte Klassen/Komponenten: `ScmEventMonitoringService`, `IScmNotificationSource`, `IScmEventSource`, `GitHubPlugin`, `ScmEventValidationService`, `ScmEventTaskResolver`, `ScmEventStore`, `ScmEventOrchestrator`.

### Annahme einer `github-actions`-Textversion im Modus `Benutzerkonto` (repository-weiter Poll)

1. `ScmEventMonitoringService` enumeriert Referenzen aktiver Repositories (`EnumerateReferencesAsync`) und liest `ReadEventsAsync`.
2. Dieselbe `GetRejection`-Kette: frei stehendes Trigger-Keyword (`$Softwareschmiede` bzw. konfiguriertes Keyword), Viewer-=Owner-Regel (nur `Benutzerkonto`) unverändert, Akteur = `github-actions`-Login → angenommen.
3. Weiter wie oben ab Schritt 4 (Aufgabenanlage bzw. Zuordnung, Inbox, Dispatch).

Beteiligte Klassen/Komponenten: `ScmEventMonitoringService`, `ScmEventValidationService`, `ScmEventTaskResolver`, `ScmEventStore`, `ScmEventOrchestrator`.

### `github-actions`-Kommentar auf zugeordneter Referenz

1. Referenz ist genau einer Aufgabe zugeordnet → `aufgabeId` bekannt → `AcceptBatchAsync` ruft `GetRejection` direkt → `null` → Inbox-Ereignis ohne vorherige Trigger-Suche.
2. Dispatch-Revalidierung (`DispatchCoreAsync` Z. 137–146) läuft mit dem gespeicherten Autor-Login → passiert → Zustellung.

Beteiligte Klassen/Komponenten: `ScmEventStore`, `ScmEventValidationService`, `ScmEventOrchestrator`.

### Weiterhin abgelehnte Fremd-Identitäten

1. Beobachtung mit `AuthorLogin = "dependabot[bot]"` (oder beliebigem sonstigem Nicht-Owner-/Nicht-Whitelist-Login) und gültigem Trigger.
2. `SameIdentity` scheitert, `IsTrustedAutomationLogin` scheitert → unverändert „Textversion stammt nicht vom Repository-Owner" als Snapshot-Diagnose, kein Inbox-Ereignis.

Beteiligte Klassen/Komponenten: `ScmEventValidationService`, `ScmEventStore`.

## Neue Klassen

Keine.

## Änderungen an bestehenden Klassen

### `ScmEventValidationService` (Logikklasse, `src/Softwareschmiede/Application/Services/ScmEventValidationService.cs`)

- **Neue Member:** `private static readonly`-Sammlung `TrustedAutomationLogins` (z. B. `string[]`/`IReadOnlyList<string>`) mit `"github-actions[bot]"` und `"app/github-actions"` — XML-Doc: vertrauenswürdige Automatisierungs-Logins, deren Textversionen dem Repository selbst zugerechnet werden.
- **Neue Methoden:** `private static bool IsTrustedAutomationLogin(string? login)` — `true`, wenn `login` case-insensitive (`OrdinalIgnoreCase`) einem Whitelist-Eintrag entspricht; `null`/leer → `false`.
- **Geänderte Methoden:** `GetRejection` — die Akteursregel (Z. 56–57) wird zu `SameIdentity(actorId, actorLogin, repository.OwnerId, repository.Owner) || IsTrustedAutomationLogin(actorLogin)`; Ablehnungstext unverändert. XML-Doc/Kommentar der Methode um den erweiterten Vertrag ergänzen.

### `GitHubPlugin` / `ScmEventObservation` / `ScmEventRepository` / `ScmEventOrchestrator` / `ScmEventStore` / `ScmEventMonitoringService`

Keine Änderung — die `GetRejection`-Erweiterung wirkt an allen drei Aufrufstellen automatisch; Bot-Logins werden bereits unverändert in `AuthorId`/`AuthorLogin` bzw. `EditorId`/`EditorLogin` geliefert; Wartegrund- und Diagnosetexte bleiben bestehen.

### Test-Helfer

- **`ScmEventValidationServiceTests.Observe` / `ObserveSeparate`** (`src/Softwareschmiede.Tests/Application/Services/ScmEventValidationServiceTests.cs`, Z. 183–201): optionale Parameter `authorLogin`/`editorLogin` ergänzen (heute fest `"owner"`/`null`), damit Bot-Autoren und -Bearbeiter darstellbar sind.
- **`ScmDispatchFixture.AddAsync`** (`src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs`, Z. 168–188): optionale Parameter `authorId`/`authorLogin` ergänzen (heute fest `"U_1"`/`"owner"`), aufrufkompatibel.
- **`GitHubEventSourceTests.Node`** (`src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubEventSourceTests.cs`, Z. 624–637): optionale Parameter `authorId`/`authorLogin` ergänzen (heute fest `U_1`/`owner` im `author`-Knoten).
- **`ScmEventMonitoringServiceTests_Notifications.ConfigureMentionObservations`** (`src/Softwareschmiede.IntegrationTests/Services/ScmEventMonitoringServiceTests_Notifications.cs`, ab Z. 268) und ggf. `ConfigureObservations` in den Monitoring-Testklassen: optionaler Author-Parameter für die aufgebauten Beobachtungen.

## Datenbankmigrationen

Keine.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `actorLogin` (`AuthorLogin`/`EditorLogin` je nach `LastEditedAt`) | Akteur ist legitim, wenn `SameIdentity` gegen `OwnerId`/`Owner` trifft **oder** `actorLogin` case-insensitive in `TrustedAutomationLogins` enthalten ist | Weder Owner noch Whitelist → „Textversion stammt nicht vom Repository-Owner" (unverändert) |
| Akteur bestimmbar | `actorId` und `actorLogin` beide leer → unverändert „Bearbeiter nicht bestimmbar"; ein Whitelist-Match erfordert einen vorhandenen Login | Unverändert |

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **End-to-end-Griff der Änderung:** `GetRejection` wird an drei Stellen aufgerufen (`ScmEventStore.AcceptBatchAsync` Z. 62, `ScmEventMonitoringService` Z. 187, `ScmEventOrchestrator.DispatchCoreAsync` Z. 137–146) — alle profitieren ohne Aufrufer-Änderung; es existiert kein vierter Aufrufer, der eine eigene Identitätsregel pflegt.
- **Sicherheitsmodell:** Jeder GitHub-Actions-Workflow des Repositories (und jede GitHub-App-Identität `app/github-actions`) kann fortan Aufgaben auslösen — die Vertrauensstellung lautet „das Repository selbst ruft den Agenten". Wird als Betriebsregel in `business-rules.md` dokumentiert; kein Opt-in (Orchestrator-Entscheidung 5). Risikoabschätzung: Schreibrecht auf den Workflow-Dateien des Repos impliziert bereits weitreichende Befugnisse; die zusätzliche Auslösefähigkeit ändert die Bedrohungslage nicht wesentlich.
- **Altbestand zugeordneter Referenzen:** Bereits mit „Textversion stammt nicht vom Repository-Owner" verworfene Snapshots werden bei unverändertem `BodyHash` nicht erneut bewertet — dokumentierter Ausschluss (siehe Scope-Ausschluss 6); unzugeordnete Referenzen heilen selbst.
- **`BereitsReagiertRejection` unberührt:** Ein `github-actions`-Kommentar, auf den das Ereignis-Konto bereits reagiert hat, wird weiterhin verworfen — die Akteurs-Akzeptanz ändert nichts am Duplikatschutz.
- **`GetRejection` bleibt die einzige Akteursregel:** `ScmEventOrchestrator.DispatchCoreAsync` (Wartegrund „Angemeldeter Benutzer ist nicht mehr der bestätigte Repository-Owner.") benötigt keine Textanpassung, da `github-actions`-Ereignisse die Revalidierung nun passieren.
- **GitHub Enterprise:** Login-Namensraum identisch — die Akzeptanz funktioniert ohne Host-Abfrage auch auf Enterprise-Hosts.
- **Kein Testbruch erwartet:** Alle Änderungen sind additiv; bestehende Fremd-Identitäts-Tests (`bot-id`, `other`, `dependabot`-artige Logins) bleiben außerhalb der Whitelist und damit abgelehnt.

## Umsetzungsreihenfolge

1. **`ScmEventValidationService` erweitern**
   - Voraussetzungen: Keine (keine neuen Pakete, Interfaces oder Basisklassen).
   - Beschreibung: `TrustedAutomationLogins`-Whitelist und `IsTrustedAutomationLogin` anlegen, Akteursregel in `GetRejection` erweitern, XML-Docs ergänzen.

2. **Unit-Tests `ScmEventValidationServiceTests` ergänzen**
   - Voraussetzungen: Schritt 1 (zu prüfende API steht); vorhandene Helper `Observe`/`ObserveSeparate`.
   - Beschreibung: Helper um optionale `authorLogin`/`editorLogin`-Parameter erweitern; neue Fälle für `github-actions`-Autor (beide Modi, beide Schreibweisen, case-insensitive), `github-actions`-Bearbeiter, Fremd-Bot-Ablehnung.

3. **Plugin-Lesepfad-Test `GitHubEventSourceTests` ergänzen**
   - Voraussetzungen: Schritt 1; `Node`-Helper erweitert.
   - Beschreibung: `Node` um optionale `authorId`/`authorLogin` erweitern; Test, dass ein `github-actions[bot]`-Autorknoten unverändert als `AuthorLogin` in der Beobachtung ankommt (Nachweis der Annahme, auf der der Login-Match beruht).

4. **Integrationstest-Helfer erweitern**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: `ScmDispatchFixture.AddAsync` und `ConfigureMentionObservations`/`ConfigureObservations` um Author-Parameter ergänzen (aufrufkompatibel).

5. **Integrationstests schreiben**
   - Voraussetzungen: Schritte 1, 4.
   - Beschreibung: SeparatesKonto-Referenzfall (Notification → github-actions-Beobachtung → Aufgabenanlage → Zustellung), Benutzerkonto-Pfad (repo-weiter Poll → Annahme → Zustellung), Dispatch-Revalidierung auf zugeordneter Referenz, Fremd-Bot-Ablehnung im Integrationspfad.

6. **Hilfedoku aktualisieren**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: `business-rules.md` (§„Trigger und Repository-Owner" + §„Kontomodus und Notifications-Inbox" + dokumentierte Betriebsregel „das Repository selbst ruft den Agenten"), `troubleshooting.md` (§„Es erscheint kein Ereignis", §„Im Modus ‚Separates Konto' erscheint kein Ereignis", §„Ein vom Workflow erzeugtes Review-Issue löst kein Ereignis aus" — `github-actions[bot]` ist keine Fehlerursache mehr; `REVIEW_ISSUE_TOKEN`-Workaround für diese Ursache entfällt), `ablauf-technisch.md` (Z. 38, Validierungsbeschreibung).

7. **Formatierung, Build und Test-Lanes**
   - Voraussetzungen: Schritte 1–6.
   - Beschreibung: `dotnet format Softwareschmiede.slnx --verify-no-changes` (bzw. fixend ohne Flag), vollständiger `dotnet build Softwareschmiede.slnx`, dann synchron (kein Background-Aufruf) `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"` und analog `src/Softwareschmiede.IntegrationTests`.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `GithubActionsAuthor_IsAccepted` (Theory) | `ScmEventValidationServiceTests` | `AuthorLogin` `github-actions[bot]` / `app/github-actions` / `GITHUB-ACTIONS[BOT]` mit gültigem Keyword-Trigger und Nicht-Owner-`AuthorId` → `GetRejection` `null` (Modus `Benutzerkonto`) |
| `SeparatesKonto_GithubActionsAuthor_IsAccepted` | `ScmEventValidationServiceTests` | `ObserveSeparate` mit `@scm-bot`-Mention und `github-actions[bot]`-Autor → `null` (Modus `SeparatesKonto`) |
| `GithubActionsEditor_IsAccepted` (Theory, beide Modi) | `ScmEventValidationServiceTests` | Bearbeitete Version mit `EditorLogin = "github-actions[bot]"` (Author = Owner bzw. Fremder) → Akteur-Regel trifft über Editor |
| `ForeignBotLogins_StayRejected` (Theory) | `ScmEventValidationServiceTests` | `dependabot[bot]`, `renovate[bot]`, beliebiger Fremd-Login → „Textversion stammt nicht vom Repository-Owner" (beide Modi) |
| `Observe`/`ObserveSeparate` um `authorLogin`/`editorLogin` erweitern | `ScmEventValidationServiceTests` | Parametrisierbare Logins für Bot-Autor/-Bearbeiter; bestehende Aufrufe bleiben gültig (optionale Parameter) |
| `BotAuthor_LoginFlowsIntoObservation` | `GitHubEventSourceTests` | `Node` mit `author { id login = "github-actions[bot]" }` → gelesene `ScmEventObservation.AuthorLogin` enthält den Bot-Login (Nachweis des Login-Durchlaufs im Plugin) |
| `Node` um `authorId`/`authorLogin` erweitern | `GitHubEventSourceTests` | Optionaler Autor-Override (bestehende Aufrufe unverändert) |
| `AddAsync` um `authorId`/`authorLogin` erweitern | `ScmDispatchFixture` (TestSupport) | Beobachtungen mit abweichendem Autor für Dispatch- und Monitoring-Tests |
| `Monitor_GithubActionsAuthoredMention_CreatesTaskAndDispatches` | `ScmEventMonitoringServiceTests_Notifications` (Integration) | Referenzfall SeparatesKonto: `mention`-Notification → `github-actions[bot]`-Beobachtung mit `@scm-bot`-Mention → Aufgabe angelegt, `ScmIncomingEvent` vorhanden, Prompt zugestellt |
| `Monitor_GithubActionsAuthoredTrigger_CreatesTask` | `ScmEventMonitoringServiceTests_RepoWide` (Integration) | Benutzerkonto: repo-weite Enumeration liest Keyword-Beobachtung mit `github-actions[bot]`-Autor auf unzugeordneter Referenz → Aufgabenanlage + Inbox-Ereignis |
| `Dispatch_GithubActionsAuthoredObservation_Delivers` | `ScmEventDispatchTests` (Integration) | `AddAsync` mit `github-actions`-Autor → gespeichertes Ereignis passiert die Dispatch-Revalidierung → Zustellung erfolgt (kein Wartegrund „nicht mehr der bestätigte Repository-Owner") |
| `Monitor_ForeignBotAuthoredTrigger_StillRejected` | `ScmEventMonitoringServiceTests_RepoWide` (Integration) | Fremd-Bot (`dependabot[bot]`) mit gültigem Trigger → Snapshot-Diagnose „Textversion stammt nicht vom Repository-Owner", kein Ereignis, keine Aufgabenanlage |

### Betroffene bestehende Tests

Keine — alle Code- und Helper-Änderungen sind additiv (optionale Parameter, neue Whitelist-Alternative); bestehende Fremd-Identitäts-Fälle (`bot-id`, `other`, `7` etc.) liegen nicht in der Whitelist und bleiben abgelehnt.

### E2E-Tests (primärer Funktionsnachweis)

Keine E2E-Tests erforderlich — die Anforderung berührt keinen Benutzerfluss: Es gibt keine neue oder geänderte UI-Interaktion, kein neues Steuerelement und keinen geänderten Anzeigetext. Die komplette Wirkung (Beobachtung → Validierung → Aufgabenanlage → Zustellung) läuft in den Hintergrunddiensten `ScmEventMonitoringService`/`ScmEventOrchestrator` und wird durch Integrationstests mit echter SQLite-Datenbank und gemocktem Provider-/CLI-Transport (`ScmDispatchFixture`) vom Notification-Eingang bis zur Prompt-Zustellung nachgewiesen — das ist derselbe reale Ausführungspfad wie in der laufenden Anwendung. Die SCM-Ereignisübersicht zeigt lediglich persistierte Diagnosen an, deren Text unverändert bleibt; es gibt keinen neuen anwendersichtbaren Zustand, den nur ein FlaUI-Test abdecken könnte.

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | Happy Path Referenzfall (SeparatesKonto): Workflow-Issue mit `@`-Erwähnung löst Aufgabe aus | GitHub-Actions-Workflow legt Issue mit frei stehender `@`-Erwähnung des separaten Kontos an; GitHub meldet `mention`-Notification | Modus `SeparatesKonto` aktiv; kein lokales `GitRepository`-Mapping-Problem; zu der Referenz existiert **noch keine** Aufgabe und noch kein Snapshot | Poll liest Notification → `ReadEventsAsync` liefert Beobachtung `AuthorLogin = github-actions[bot]` → `GetRejection` `null` → `ScmEventTaskResolver` legt Aufgabe an → Inbox-Ereignis → Dispatch-Revalidierung `null` → CLI-Zustellung | Neue Aufgabe in der Aufgabenverwaltung; Ereignis in der SCM-Ereignisübersicht zugestellt statt mit Owner-Diagnose verworfen | Integrationstest `Monitor_GithubActionsAuthoredMention_CreatesTaskAndDispatches` |
| 2 | Happy Path Benutzerkonto: `github-actions`-Kommentar mit Keyword auf unzugeordneter Referenz | Poll-Zyklus `ReadEventsAsync` | Modus `Benutzerkonto`; Viewer = Owner; Referenz ohne lokale Aufgabe | Trigger-Suche: `GetRejection` `null` (Keyword frei stehend, Autor in Whitelist) → Aufgabenanlage → `AcceptBatchAsync` → Inbox → Dispatch → Zustellung | Aufgabe angelegt, Ereignis zugestellt; Snapshot ohne Ablehnungsdiagnose | Integrationstest `Monitor_GithubActionsAuthoredTrigger_CreatesTask` |
| 3 | `github-actions`-Kommentar auf bereits zugeordneter Referenz | Poll-Zyklus | Referenz gehört genau einer Aufgabe; kein Snapshot zur Textversion | `AcceptBatchAsync` → `GetRejection` `null` → Inbox-Ereignis → `DispatchCoreAsync` revalidiert mit frischem `ScmEventRepository` → `null` → Zustellung (inkl. `eyes`-Reaktion am Kommentar) | Ereignis zugestellt; kein Wartegrund „nicht mehr der bestätigte Repository-Owner" | Integrationstest `Dispatch_GithubActionsAuthoredObservation_Delivers` |
| 4 | Bearbeitete `github-actions`-Textversion | Remote-Edit der Textversion | `LastEditedAt` gesetzt; `EditorLogin = github-actions[bot]` | `GetRejection` wählt Editor als Akteur → Whitelist-Match → `null` | Textversion angenommen | Unit-Test `GithubActionsEditor_IsAccepted` |
| 5 | Fremd-Bot bleibt abgelehnt | Poll liest `dependabot[bot]`-Kommentar mit gültigem Trigger | Beliebige Referenz | `SameIdentity` + `IsTrustedAutomationLogin` scheitern → Diagnose „Textversion stammt nicht vom Repository-Owner" | Kein Inbox-Ereignis, keine Aufgabenanlage; Diagnose sichtbar | Unit `ForeignBotLogins_StayRejected` + Integration `Monitor_ForeignBotAuthoredTrigger_StillRejected` |
| 6 | Altbestand unzugeordneter Referenz (vor diesem Release beobachtet) | Nächster regulärer Poll | Snapshot mit `UnassignedDiagnostic` vorhanden (keine Aufgabe zugeordnet) | `SinceUtc`-Wiederanlauf liest Quelle vollständig → `github-actions`-Beobachtung qualifiziert nun → Aufgabenanlage + Inbox | Zuvor still verworfener Workflow-Text (Referenzfall) wird beim nächsten Poll automatisch aufgenommen | Durch Szenario-1-Testlogik abgedeckt (unzugeordneter Pfad); manuelle Verifikation im realen Referenzfall nach Deployment |
| 7 | Altbestand zugeordneter Referenz (dokumentierte Grenze) | Remote-Bearbeitung der Textversion | Snapshot mit Diagnose „Textversion stammt nicht vom Repository-Owner", `IdentityResolved = true`, Referenz aufgabenzugeordnet | Ohne Remote-Edit: unverändert verworfen (Skip bei gleichem `BodyHash`). Mit Remote-Edit durch `github-actions` oder Owner: neuer `BodyHash` → Neubewertung → Annahme | Nach Remote-Edit Aufnahme; vorher bewusst keine automatische Aufnahme | Scope-Ausschluss 6 (bewusste Grenze, kein automatisierter Test auf „Nicht-Aufnahme" erforderlich; Edit-Pfad durch Szenario 4 abgedeckt) |

## Offene Punkte

Keine.
