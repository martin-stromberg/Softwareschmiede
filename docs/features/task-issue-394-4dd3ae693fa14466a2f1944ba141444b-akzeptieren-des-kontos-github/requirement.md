# Übersetzte Anforderung: Konto `github-actions` als akzeptierte Autor-Identität der SCM-Ereignisvalidierung

**Quelle:** Issue #394 (martin-stromberg/Softwareschmiede) / Aufgaben-ID `4dd3ae69-3fa1-4466-a2f1-944ba141444b`
**Branch:** `task/issue-394-4dd3ae693fa14466a2f1944ba141444b-akzeptieren-des-kontos-github`

## Fachliche Zusammenfassung

Die eventgetriebene Aufgabenausführung (SCM-Ereignisüberwachung, `docs/help/plugins/github-ereignisse/`) akzeptiert eine beobachtete Textversion aktuell nur, wenn deren Autor bzw. letzter Bearbeiter dem Repository-Owner entspricht (`ScmEventValidationService.GetRejection`, Prüfung „Textversion stammt nicht vom Repository-Owner"). Issues und Kommentare, die durch eine GitHub-Action entstanden sind, werden von GitHub als Autor `github-actions` geführt (API-Login `github-actions[bot]` bzw. `app/github-actions`) und damit verworfen — obwohl sie fachlich legitime Beauftragungen darstellen (Referenzfall: `martin-stromberg/Tankradar#62`, dessen Issue-Body die frei stehende `@softwarewerkstatt`-Erwähnung enthält, die im Modus `SeparatesKonto` der Trigger ist). Der Validierungsvertrag ist so zu erweitern, dass neben dem Repository-Owner auch das Konto `github-actions` als vertrauenswürdiger Akteur gilt und solche Textversionen automatisch zur Aufgabenanlage und -ausführung führen.

## Fachlicher Scope

- **Verarbeitungseinheit:** Eine beobachtete SCM-Textversion (`ScmEventObservation` — Issue-/PR-Beschreibung, Issue-Kommentar, Review-Kommentar, Pull-Request-Review) am Schritt der Akteurs-Identitätsprüfung. Zum Scope gehört ausschließlich die Entscheidung „Autor/Bearbeiter = vertrauenswürdige Identität" in `ScmEventValidationService.GetRejection`; nicht zum Scope gehören Trigger-Erkennung, Duplikaterkennung, Reaktions-/Marker-Regeln und die Zustellung selbst.
- **Auslösende Ereignisse:** Der reguläre Poll-Zyklus des `ScmEventMonitoringService` liest neue Textversionen (`ReadEventsAsync`); eine Textversion mit gültigem Trigger (Keyword `$Softwareschmiede` im Modus `Benutzerkonto`, frei stehende `@`-Erwähnung des separaten Kontos im Modus `SeparatesKonto`), deren Autor bzw. letzter Bearbeiter das GitHub-Konto `github-actions` ist, soll künftig die Identitätsprüfung bestehen statt mit der Diagnose „Textversion stammt nicht vom Repository-Owner" abgelehnt zu werden.
- **Erwartete Wirkung:** Eine von `github-actions` verfasste Textversion mit gültigem Trigger wird angenommen: Es entsteht ein `ScmIncomingEvent`, bei nicht zugeordneter Referenz läuft die Aufgabenanlage über `ScmEventTaskResolver.ResolveAsync`, und das Ereignis wird über den `ScmEventOrchestrator` an die konfigurierte CLI zugestellt — also die automatische Aufgabenausführung wie bei Owner-Autorschaft. In der SCM-Ereignisübersicht erscheint die Ablehnungsdiagnose „Textversion stammt nicht vom Repository-Owner" für `github-actions`-Autorenschaft nicht mehr; die Revalidierung beim Dispatch (`ScmEventOrchestrator`, Wartegrund „Angemeldeter Benutzer ist nicht mehr der bestätigte Repository-Owner") lässt solche Ereignisse ebenfalls passieren.
- **Umgang mit fehlenden Objekten:**
  - *Zugehörige Aufgabe nicht vorhanden:* **erzeugen** — unverändert über den bestehenden `ScmEventTaskResolver`-Pfad (Trigger-Beobachtung führt zur Aufgabenanlage); kein neuer Mechanismus.
  - *Autor-/Bearbeiter-Identität nicht bestimmbar:* **verwerfen** — die bestehende Regel „Bearbeiter nicht bestimmbar" bleibt unverändert; `github-actions`-Akzeptanz setzt eine bestimmbare Identität voraus.
  - *Textversion wurde zwischenzeitlich vom Owner bearbeitet (oder umgekehrt):* Die Prüfung richtet sich weiter an den aktuellen Akteur (`Editor*` bei `LastEditedAt`, sonst `Author*`); die neue Identität `github-actions` tritt als gleichwertige Alternative neben den Owner — sowohl als Autor als auch als Bearbeiter.
  - *Provider ohne `github-actions`-Konzept (Bitbucket, LocalDirectory):* **unverändert** — die Identität `github-actions` ist GitHub-spezifisch; ob die Akzeptanz an `ScmEventRepository.ProviderPrefix`/`Host` gebunden wird, ist Entwurfsentscheidung (siehe Offene Fragen).
- **Scope-Ausschlüsse:**
  - Keine Umsetzung zeitlicher Auftragsbestandteile: Die im auslösenden Kommentar enthaltene Bitte „warte 3 Stunden" ist organisatorischer Text im Beauftragungs-Prompt und kein anzuwendendes Scheduling-Feature — die Aufgabe startet wie bisher sofort; eine zeitverzögerte Ausführung wird nicht gebaut (Randnotiz der Anforderung).
  - Keine generelle Freigabe beliebiger Bot- oder App-Konten: Andere Automatisierungs-Identitäten (z. B. `dependabot[bot]`, fremde GitHub Apps) bleiben ausgeschlossen, sofern nicht explizit anders entschieden — nur `github-actions` wird akzeptiert.
  - Keine Änderung des Trigger-Vertrags: Keyword-/Mentions-Regeln, Frei-stehend-Regel, `BereitsReagiertRejection`, Marker-Regel „Eigene Antwort" und die Viewer-=Owner-Regel im Modus `Benutzerkonto` bleiben unverändert.
  - Keine neue Benutzerinteraktion: Es wird keine zusätzliche Auswahl, Bestätigung oder UI-Aktion eingeführt; die Diagnose- und Wartegrundtexte der Ereignisübersicht bleiben grundsätzlich bestehen (allenfalls Wortlaut-Anpassung).
  - Keine Änderung an Ereignis-Deduplizierung, Checkpoints, Reaktionsversand oder am Antwortkanal.

## Betroffene Klassen und Komponenten

### Logikklassen / Services

- `ScmEventValidationService` (`src/Softwareschmiede/Application/Services/ScmEventValidationService.cs`, `GetRejection`, Zeilen ~49–57) — zentrale Änderungsstelle: Die Akteursprüfung (`SameIdentity(actorId, actorLogin, repository.OwnerId, repository.Owner)`) ist um die zusätzlich akzeptierte Identität `github-actions` zu erweitern, ggf. als benannte Konstante/Whiteliste vertrauenswürdiger Akteure. Alle drei Aufrufstellen (`ScmEventStore.AcceptBatchAsync` ~Z. 62, `ScmEventMonitoringService` ~Z. 187, `ScmEventOrchestrator.DispatchAsync` ~Z. 137) profitieren automatisch.
- `ScmEventOrchestrator`, `ScmEventStore`, `ScmEventMonitoringService` (`src/Softwareschmiede/Application/Services/`) — keine Logikänderung erwartet; allenfalls Wartegrund-/Diagnosetexte an den erweiterten Vertrag anpassen.

### Datenmodell / Value Objects

- `ScmEventObservation` / `ScmEventRepository` (`src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs`) — voraussichtlich unverändert: `AuthorId`/`AuthorLogin`/`EditorId`/`EditorLogin` werden bereits geliefert; die Abgrenzung auf den GitHub-Provider ist über `Repository.ProviderPrefix` (GitHub: `Softwareschmiede.GitHub`) und `Host` möglich.

### Provider-Plugins

- `GitHubPlugin` (`plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.Events.cs`, `AddObservation` ~Z. 232–264, GraphQL-`TextFields` Z. 13) — zu verifizieren: `author { login ... on Node { id } }` liefert für App-/Bot-Autoren den Login `github-actions[bot]`; falls die Unterscheidung App vs. Benutzer relevant wird, wäre `__typename` am `author` zu ergänzen (Entwurfsentscheidung, siehe Offene Fragen).

### UI-Komponenten

- Keine. Die SCM-Ereignisübersicht zeigt Diagnosen automatisch; kein neues Steuerelement erforderlich.

### Dokumentation

- `docs/help/plugins/github-ereignisse/business-rules.md` — Abschnitt „Trigger und Repository-Owner" und der `SeparatesKonto`-Validierungsvertrag sind um die akzeptierte Identität `github-actions` zu ergänzen; ggf. `ablauf-anwender.md` und `troubleshooting.md` nachziehen.

### Tests

- `ScmEventValidationServiceTests` (`src/Softwareschmiede.Tests/Application/Services/ScmEventValidationServiceTests.cs`) — neue Fälle: `github-actions`-Autor akzeptiert (beide Kontomodi, je nach Entscheidung), `github-actions`-Bearbeiter akzeptiert, fremde Bot-Logins weiterhin abgelehnt, unbekannte Identität weiterhin „Bearbeiter nicht bestimmbar".
- Integrationstests (`src/Softwareschmiede.IntegrationTests/Services/`, z. B. `ScmEventDispatchTests`, `ScmEventMonitoringServiceTests_*`, `ScmEventPersistenceTests`) — Annahme- und Zustellpfad mit `github-actions`-Autorschaft nachweisen; bestehende Fixture-Autoren (`ScmDispatchFixture`) ggf. ergänzen.
- E2E: nicht erforderlich (kein UI-Verhalten geändert).

## Implementierungsansatz

- **Erweiterungspunkt:** Einzige fachliche Änderung in `ScmEventValidationService.GetRejection` — die Identitätsentscheidung „Actor = Owner" wird zu „Actor = Owner ODER Actor = vertrauenswürdiges Automatisierungskonto". Naheliegend ist ein eigener Vergleich neben `SameIdentity`, der die bekannten GitHub-Login-Schreibweisen (`github-actions[bot]`, `app/github-actions`, ggf. `github-actions`) abdeckt; die Schreibweisen sind als Konstanten zu hinterlegen.
- **Wirkungskette:** Da `GetRejection` an allen drei Prüfstellen (Aufnahme-Diagnose, Trigger-Erkennung bei unzugeordneten Referenzen, Revalidierung beim Dispatch) verwendet wird, greift die Erweiterung ohne weitere Aufrufer-Änderung end-to-end.
- **Provider-Bindung:** Die Akzeptanz sollte auf GitHub-Ereignisquellen beschränkt werden können (Vergleich gegen `Repository.ProviderPrefix`/`Host`); alternativ provider-agnostisch per Login-Match — zu entscheiden (Offene Fragen).
- **Abhängigkeiten:** Keine neuen Interfaces oder Entities erwartet; keine Migration, da keine Persistenz ändert. `dotnet format`-Konformität und bestehende Namenskonventionen (deutsche Fachbegriffe, XML-Docs) beachten.

## Konfiguration

- Von der Anforderung ist **keine** Konfigurierbarkeit gefordert; die Akzeptanz von `github-actions` kann fest verdrahtet werden (analog zum verdrahteten `DefaultTriggerKeyword`). Ob die Menge vertrauenswürdiger Automatisierungskonten perspektivisch als Anwendungseinstellung (Ebene `AppEinstellungService`, Muster `ScmTriggerKeywordKey`) ausbaubar sein soll, ist als Entwurfsfrage zu klären — nicht Voraussetzung für dieses Issue.

## Offene Fragen

1. **Kontomodus:** Soll die `github-actions`-Akzeptanz in beiden Überwachungsmodi gelten (`Benutzerkonto` mit Keyword-Trigger und `SeparatesKonto` mit `@`-Erwähnung) oder nur im `SeparatesKonto`-Pfad, in dem der Referenzfall auftrat? Annahme: beide Modi, da dieselbe Akteursprüfung — zu bestätigen.
2. **Identifikationsmerkmal:** Reicht der Login-Vergleich (`github-actions[bot]`, `app/github-actions`), oder soll zusätzlich/ stattdessen der Actor-Typ (`__typename` = `Bot`/`App` am GraphQL-`author`) bzw. die Node-ID der GitHub-Actions-App geprüft werden? Relevanz für GitHub Enterprise Server (abweichende Logins/IDs) ist zu bewerten.
3. **Provider-Bindung:** Soll die Regel hart auf `ProviderPrefix == "Softwareschmiede.GitHub"` (und ggf. `Host == "github.com"`) beschränkt werden, oder gilt der Login-Match provider-übergreifend?
4. **Erweiterbarkeit:** Bewusst nur `github-actions` hart akzeptieren, oder die Prüfung als kleine interne Whitelist vertrauenswürdiger Automatisierungskonten auslegen, damit spätere Kundenwünsche (z. B. weitere CI-Konten) ohne Strukturbruch ergänzbar sind?
5. **Sicherheitsimplikation dokumentieren:** Ein von `github-actions` verfasster Text stammt aus einem Workflow des Repositories — die Vertrauensstellung ist damit „das Repository selbst ruft den Agenten". Ist das als dokumentierte Betriebsregel (Hinweis in `business-rules.md`) ausreichend, oder wird eine opt-in-Absicherung gewünscht?
