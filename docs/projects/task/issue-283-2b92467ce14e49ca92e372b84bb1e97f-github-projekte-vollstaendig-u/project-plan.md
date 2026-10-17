# Projektplan – GitHub-Projekte vollständig überwachen und Ereignisse ohne bestehende Aufgabe ausführen

**Aufgaben-ID:** 2b92467c-e14e-49ca-92e3-72b84bb1e97f
**Verknüpftes Issue:** #283
**Branch:** `task/issue-283-2b92467ce14e49ca92e372b84bb1e97f-github-projekte-vollstaendig-u`
**Grundlagen:** [`requirement.md`](requirement.md), [`inventory.md`](inventory.md) mit Detailanalysen unter [`inventory/`](inventory/)

---

## Übersicht

Für jedes als Projekt erfasste und aktivierte GitHub-Repository sollen GitHub-Ereignisse (neue Issues, neue Pull Requests, Kommentare, Reviews) vollständig überwacht werden – nicht mehr nur Referenzen, die bereits an einer lokalen Aufgabe hinterlegt sind. Ein qualifiziertes Ereignis ohne lokale Aufgabe soll eine passende Aufgabe anlegen bzw. eine vorhandene wiederverwenden; die Aufgabe wird nach den bestehenden Sicherheits- und Owner-Prüfungen automatisch gestartet und der Auftrag an die konfigurierte Standard-CLI zugestellt. Ohne Standard-CLI bleibt das Ereignis mit einer bedienbaren Aufforderung wartend. Zusätzlich wird der beobachtete Fehler behoben, bei dem die Zustellung nach erstmaliger Vertrauenswürdigkeitsabfrage des Arbeitsverzeichnisses dauerhaft auf „Wartend" hängen bleibt; dabei ist zu prüfen, ob die CLI mit sicheren Startparametern gestartet werden kann, um die Abfrage kontrolliert zu vermeiden.

**Betroffene Bereiche:**
- SCM-Ereignisüberwachung (Polling, Referenzliste, Checkpoints, Deduplizierung)
- GitHub-Plugin (Ereignisquelle: Erweiterung um repository-weite Referenz-Erkennung)
- Aufgabenverwaltung (automatische Anlage, Wiederverwendung, Duplikatschutz)
- CLI-Zustellung (Dispatch-Pipeline, Eingabebereitschaft, Dateitool-Kanalprobe, Startparameter)
- SCM-Ereignisübersicht (Bestandsschutz der vorhandenen Warte-/Aktionsmechanik)

**Nicht betroffen / ausdrücklich unverändert (Bestandsschutz, FR-9):** Trigger-Regel `$Softwareschmiede`, Repository-Owner-/Bearbeiter-Validierung, Token-Berechtigungsprüfung pro Zustellung, deduplizierte/idiempotente Remote-Zustellung über die Outbox, bestehende Funktionen der SCM-Ereignisübersicht. Keine neuen Trigger-Arten, keine Webhooks, keine Überwachung nicht erfasster oder deaktivierter Repositories, nur GitHub als Quelle.

---

## Grobe Vorgehensentscheidungen

1. **Bestehenden Polling-Mechanismus erweitern, kein neuer Überwachungskanal.** Die repository-weite Überwachung wird in den vorhandenen 60-Sekunden-Poll des `ScmEventMonitoringService` integriert und nutzt weiterhin Checkpoints (Watermarks), Content-Snapshots und die `EventKey`-Deduplizierung. Begründung: Die Infrastruktur für Fehler-Backoff, Mehrdeutigkeitsschutz, Validierung bei der Aufnahme und Wiederanlauf nach Neustart ist bewährt und getestet; ein zweiter Mechanismus (z. B. Webhooks) würde Doppelstrukturen, neue Sicherheitsfragen und Betriebsanforderungen schaffen, die die Anforderung nicht verlangt (Annahme A2/Q7).

2. **Repository-weite Referenzerkennung über den SCM-Ereignisvertrag, nicht über die flachen Plugin-Listen-APIs.** Die vorhandenen `IGitPlugin`-Methoden `GetIssuesAsync`/`GetOpenPullRequestsAsync` liefern keine der für die Validierung nötigen Metadaten (Autor-/Editor-Identität, `node_id`, Viewer-/Owner-Kontext). Die Erweiterung setzt deshalb am Ereignispfad an (`IScmEventSource`/`GitHubPlugin.Events`), damit enumerierte Referenzen dieselben `ScmEventObservation`-Daten liefern wie referenzbasierte Reads und unverändert durch `ScmEventValidationService` und `ScmEventStore` laufen. `GetEventRepositoryAsync` liefert bereits die kanonische Repository-Identität und wird wiederverwendet.

3. **„Finde-oder-lege-an"-Auflösung der Aufgabe vor bzw. bei der Inbox-Aufnahme.** `ScmIncomingEvent.AufgabeId` ist nicht-nullable; die Invariante „jedes Inbox-Ereignis gehört zu genau einer existierenden Aufgabe" wird bewusst beibehalten, statt das Datenmodell zu lockern. Begründung: Die gesamte nachgelagerte Pipeline (Dispatch, Lease, UI-Aktionen, Statusmodell) baut auf dieser Invariante auf – ein vorgeschalteter Auflösungsschritt (Wiederverwendung über `IssueReferenz.IssueNummer` bzw. `PullRequestReferenz`-Schlüssel, sonst Anlage über die vorhandenen `AufgabeService`-Bausteine mit `ProjektId` aus `GitRepository.ProjektId`) macht den Orchestrator-Pfad für neue Aufgaben ohne Umbau nutzbar. Duplikatschutz inkl. Nebenläufigkeit folgt den vorhandenen Mustern (UI-/Service-Dedup, `DbUpdateException`-Absicherung). Mehrdeutige Zuordnungen (mehrere Aufgaben auf derselben Referenz) bleiben wie heute eine Diagnose ohne Inbox-Eintrag – keine Automatik-Auswahl.

4. **Trust-Deadlock ursächlich beheben, Prüfauftrag zu Startparametern im selben Schritt.** Der beobachtete Dauer-„Wartend"-Zustand hat mit hoher Wahrscheinlichkeit eine einzelne Ursache: Die rungebundene Dateitool-Kanalprobe wird genau einmal gesendet; geht sie im Vertrauensdialog der CLI verloren (z. B. weil „Eingabebereitschaft bestätigen" zu früh gedrückt wurde), friert der Kanalnachweis permanent ein und kein UI-Weg löst ihn wieder (siehe `inventory/trust-deadlock.md`). Der Bugfix sorgt für eine zuverlässige Fortsetzung (z. B. Probe-Wiederholung/-Reset mit Begrenzung, Erkennung des Vertrauensdialogs als eigenen Wartegrund, Absicherung der manuellen Bestätigung) – unabhängig davon, ob der Dialog zusätzlich vermieden werden kann. Der Prüfauftrag (FR-8) wird innerhalb desselben Schritts mit begründeter Empfehlung und – nur wenn mit dem Sicherheitsvertrag des Kanals (`ScmCliAccessEvaluator`-Whitelist) vereinbar – Umsetzung geliefert. Begründung: Beide Anforderungen betreffen denselben Zustandsautomaten; ein getrennter Recherche-Schritt ohne Liefergegenstand ist nicht vorgesehen, und ein Trust-Bypass ohne Fix des hängenden Zustands würde nur die Startphase, nicht den Fehlerfall abdecken.

5. **Auto-Start und Zustellung über den bestehenden Orchestrator-Pfad.** Der Dispatch startet bereits Aufgaben (`Neu` → `Gestartet`, Klon/Branch/CLI-Start über `EntwicklungsprozessService`), prüft pro Zustellung Owner und `CanWrite` erneut und behandelt fehlende Standard-CLIs bereits als wartendes, bedienbares Ereignis. Für neu angelegte Aufgaben wird derselbe Pfad wiederverwendet; der letzte Schritt härtet und verifiziert die Kette Ende-zu-Ende (inkl. Wartend-Verhalten ohne Standard-CLI und Bestandsschutz-Regression). Begründung: maximale Wiederverwendung der geprüften Sicherheitslogik, kein paralleler Startpfad.

6. **Reihenfolge: unabhängiger Bugfix zuerst, dann Leseseite, dann Schreibseite, dann Ende-zu-Ende-Härtung.** Schritt 1 (Trust-Fortsetzung/Startparameter) ist fachlich und technisch unabhängig und als Bugfix sofort mergebar. Schritt 2 erweitert nur die Erkennungsseite (repo-weites Lesen); erst Schritt 3 macht die erkannten Ereignisse ohne Aufgabe durch Anlage/Wiederverwendung wirksam. Schritt 4 stellt den Gesamtablauf inkl. Wartend-Verhalten ohne Standard-CLI sicher und verifiziert den Bestandsschutz. Jeder Schritt ist separat mergebar und testbar; kein Schritt verschlechtert das bisherige Verhalten.

7. **„Aktiviert" bleibt `GitRepository.Aktiv` beim Erfassen; kein neuer UI-Schalter.** Die Erfassung über `ProjektService.AddRepositoryAsync` setzt `Aktiv = true`; ein nachträglicher De-/Aktivierungsweg existiert nicht und ist nicht Teil der Anforderung. Repositories ohne `Aktiv`-Flag oder ohne GitHub-Plugin werden nicht überwacht (bisherige Regel, erweitert nur auf Repo-Ebene).

---

## Entwicklungsschritte

### Schritt 1: Verlässliche Fortsetzung der CLI-Zustellung nach der Vertrauenswürdigkeitsabfrage (inkl. Prüfauftrag Startparameter)

**Fachliche Beschreibung:**

Beim ersten Start einer KI-CLI in einem neuen Aufgaben-Arbeitsverzeichnis (jede Aufgabe erhält ein frisches Verzeichnis) kann die CLI eine Vertrauenswürdigkeitsabfrage des Ordners anzeigen (z. B. „Do you trust the files in this folder?"). Aktuell kann die SCM-Zustellung danach dauerhaft im Status „Wartend" verbleiben, ohne dass ein UI-Weg die Lage löst:

- Der SCM-Antwortkanal sendet genau einmal pro CLI-Lauf eine lokale Dateitool-Kanalprobe in die Terminal-Sitzung. Wird dieser Probe-Prompt abgeschickt, während die CLI noch im Vertrauensdialog steht – insbesondere, weil die UI-Aktion „Eingabebereitschaft bestätigen" die Bereitschaft erzwingen kann, obwohl die CLI gar nicht am Eingabeprompt steht –, geht die Eingabe im Dialog verloren.
- Alle folgenden Prüfungen (automatischer Re-Dispatch der wartenden Einträge, „Erneut prüfen", „Öffnen / CLI auswählen") fragen nur noch dieselbe hängende Probe ab; der eigentliche Auftrags-Prompt wird nie geschrieben. Der Wartegrund lautet dann sinngemäß „Nachweis des tatsächlichen Dateitool-Zugriffs steht aus …", und auch die Bestätigungsaktion wird nicht mehr angeboten.
- Verantwortliche Bausteine (Orientierung): `src/Softwareschmiede/Application/Services/ScmPromptSessionAdapter.cs` (Eingabebereitschaft, `_accessProbe`, `ConfirmReady`), `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs` (Dispatch-Pipeline, Schritte Eingabebereitschaft → Kanalnachweis → Prompt schreiben; `InvalidOperationException` setzt `NextAttemptUtc = long.MaxValue` und stoppt automatische Wiederholungen), SCM-Ereignisübersicht (`ScmEventQueueView`/`-ViewModel`).

Zu liefern ist eine Korrektur, nach der die Anwendung die Zustellung nach der manuellen Bestätigung des Vertrauensdialogs zuverlässig fortsetzen kann – ohne dass der Benutzer einen nicht wirksamen Wiederholungsweg suchen muss. Fachlich zulässige Ansätze (Auswahl oder Kombination im Umsetzungsschritt):

- eine verloren gegangene oder nie abgeschlossene Kanalprobe erkennen und begrenzt bzw. nach definierter Zeit erneut zustellen oder zurücksetzen – unter Wahrung der Regel, dass keine Ereignisinhalte vor dem Kanalnachweis an die CLI gehen;
- den Vertrauensdialog als eigenen, verständlichen Wartezustand erkennen (statt generisch „Eingabebereitschaft unbekannt"), damit nach seinem Verschwinden gezielt erneut geprüft wird;
- die manuelle Eingabebereitschafts-Bestätigung so absichern, dass sie nicht mitten in einem Blockierdialog erzwungen werden kann bzw. der danach versendete Probekontext wiederherstellbar ist;
- sicherstellen, dass einmal auf „kein automatischer Retry" (`NextAttemptUtc = long.MaxValue`) gesetzte Ereignisse nach der Korrektur wieder einen wirksamen Fortsetzungsweg haben.

Zusätzlich ist der Prüfauftrag umzusetzen: Es ist zu prüfen, ob die unterstützten CLIs (Claude Code, OpenAI Codex) mit sicheren, passenden Startparametern oder Host-seitig steuerbaren Konfigurationen für das Aufgaben-Arbeitsverzeichnis gestartet werden können, sodass die Vertrauenswürdigkeitsabfrage kontrolliert vermieden oder vor dem ersten Auftrags-Prompt deterministisch abgeschlossen wird. Kandidaten (zu verifizieren, als Vermutung aus der Bestandsanalyse): Vorbelegung des Trust-Status in der benutzerbezogenen CLI-Konfiguration (für Claude Code z. B. `hasTrustDialogAccepted` in `~/.claude.json` pro Projektverzeichnis), entsprechende Codex-Konfigurationen (`config.toml`, `approval_policy`/`sandbox_mode`). Randbedingung: Die Zulässigkeitsprüfung des Antwortkanals (`ScmCliAccessEvaluator`) akzeptiert heute nur eng definierte Parameter (Claude: `--permission-mode acceptEdits`, `--model`; Codex: `--sandbox`, `--ask-for-approval`, `--model`); ein gefundener Parameter müsste dort bewusst ergänzt werden. Ergebnis des Prüfauftrags ist eine dokumentierte, begründete Empfehlung; wenn ein sicherer Weg existiert, der den Sicherheitsvertrag des Kanals nicht aushebelt (insbesondere kein pauschales Überspringen sämtlicher Berechtigungen), ist dieser umzusetzen. Der Kanalnachweis per Dateitool-Probe bleibt in jedem Fall erhalten – ein Trust-Vermeidungsweg ändert nur die Startphase.

**Akzeptanzkriterien:**

- Nach der manuellen Bestätigung der Vertrauenswürdigkeitsabfrage setzt die Anwendung die Zustellung zuverlässig fort: spätestens über den bestehenden automatischen Wiederholungs-Dispatch der wartenden Einträge, ohne CLI-Neustart und ohne Datenbankeingriff.
- Der Sollbruch ist reproduzierbar abgesichert: Ein Szenario „Kanalprobe geht verloren / Vertrauensdialog blockiert" führt nicht mehr zu einem permanenten „Wartend" ohne wirksame Aktion (Nachweis über Unit-/Integrationstests, z. B. über die bestehende Simulator-/Fixture-Infrastruktur mit steuerbarem Bildschirmpuffer).
- Die Aktionen „Erneut prüfen" und „Öffnen / CLI auswählen" in der SCM-Ereignisübersicht sind im Fehlerfall wieder wirksam, oder es existiert eine eindeutige, bedienbare Alternativaufforderung.
- Der Prüfauftrag ist mit dokumentierter, begründeter Empfehlung abgeschlossen (welche CLI, welche Parameter/Konfiguration, Sicherheitsbewertung); falls ein sicherer Weg existiert, ist er umgesetzt und die Parameter-Whitelist der Kanalprüfung entsprechend – und nur dort – erweitert.
- Bestandsschutz: Keine Ereignisinhalte werden vor dem Kanalnachweis an die CLI gesendet; bestehende Zustellungs- und Statuslogik (inkl. deduplizierter Zustellung) bleibt erhalten.

**Abhängigkeiten:** keine

**Betroffene Bereiche:** CLI-Zustellung, SCM-Ereignisübersicht

### Schritt 2: Repository-weite GitHub-Ereigniserkennung

**Fachliche Beschreibung:**

Heute baut der SCM-Ereignis-Monitor (`ScmEventMonitoringService`, 60-Sekunden-Polling) seine Beobachtungsliste ausschließlich aus Issue-/Pull-Request-Referenzen auf, die bereits an lokalen Aufgaben hinterlegt sind (nur Aufgaben mit `GitRepository.Aktiv == true`). Referenzen ohne Aufgabe – neue Issues, neue Pull Requests, Kommentare und Reviews an bisher unbekannten Referenzen – werden nie abgefragt; es existiert kein Pfad, der ein ganzes Repository enumeriert (die Ereignisquelle `IScmEventSource.ReadEventsAsync` arbeitet immer auf genau einer Referenz mit fester Nummer).

In diesem Schritt wird die Erkennungsseite repository-weit gemacht:

- Für jedes an einem Projekt erfasste GitHub-Repository mit `Aktiv == true` werden alle relevanten Referenzen (offene Issues und Pull Requests, ggf. inkl. der vom Umsetzungsschritt fachlich begründeten Grenze für geschlossene Referenzen) in die Überwachung einbezogen – zusätzlich zu den bisher referenzbasierten Beobachtungen.
- Die Erweiterung liefert für jede erkannte Referenz dieselben Ereignis-Metadaten wie der bisherige Pfad (Autor-/Bearbeiter-Identität, Knoten-IDs, Zeitstempel, Repository-Identität), damit Trigger-/Owner-Validierung, Snapshot-/EventKey-Deduplizierung und Checkpoint-/Backoff-Mechanik unverändert greifen. Die vorhandenen flachen Plugin-Listen-APIs (`GetIssuesAsync`/`GetOpenPullRequestsAsync`) reichen dafür nicht aus und sind nicht als Datenquelle für die Validierung zu verwenden.
- Der bestehende Polling-, Checkpoint- und Fehlerbehandlungsrahmen (Watermark, `SinceUtc`, Backoff/Sperre bei Authentifizierungs-/Berechtigungs-/Dauerfehlern, „Quellen erneut prüfen") gilt auch für die neu erkannten Quellen; es wird kein neuer Überwachungsmechanismus eingeführt.
- Nur GitHub ist Quelle; andere Plugin-Typen werden wie bisher übersprungen.
- **Bewusste Zwischenstufe:** In diesem Schritt ändert sich die Aufgabenzuordnung noch nicht. Ereignisse, deren Referenz zu genau einer Aufgabe gehört, laufen wie bisher in die Inbox; Ereignisse ohne zuordenbare Aufgabe werden – wie bisher bei nicht auflösbaren Zuordnungen – noch nicht als zustellbare Inbox-Einträge wirksam (Schritt 3 schließt diese Lücke). Der Schritt ist dennoch eigenständig testbar (Enumerationsaufrufe, Metadaten-Vollständigkeit, unveränderte Inbox für Aufgaben-Referenzen, Checkpoint-Verhalten) und mergebar ohne Funktionsverlust.

**Akzeptanzkriterien:**

- Für ein aktiviertes, als Projekt erfasstes GitHub-Repository werden Issues und Pull Requests repository-weit in die Ereignisüberwachung einbezogen – nachweisbar unabhängig davon, ob eine lokale Aufgabe existiert.
- Neu erkannte Referenzen liefern vollständige Metadaten für die bestehende Validierung (Trigger `$Softwareschmiede`, Owner-/Bearbeiter-Gleichheit, Eigen-Antwort-Erkennung).
- Checkpoints, Snapshot-/EventKey-Deduplizierung und Fehler-Backoff funktionieren für repo-weit erkannte Quellen wie für referenzbasierte; wiederholte Polls erzeugen keine Duplikate.
- Für Referenzen mit vorhandener Aufgabe ist das beobachtete Verhalten unverändert (Inbox-Aufnahme, Validierung, Dispatch-Selektion).
- Nicht erfasste, nicht aktivierte oder Nicht-GitHub-Repositories werden nicht überwacht; die repository-weite Auflösung scheitert bei Ambiguität/Token-Fehlern kontrolliert in den bestehenden Diagnosepfad.

**Abhängigkeiten:** keine

**Betroffene Bereiche:** SCM-Ereignisüberwachung, GitHub-Plugin (Ereignisquelle)

### Schritt 3: Automatische Aufgabenanlage und -wiederverwendung aus qualifizierten Ereignissen

**Fachliche Beschreibung:**

Ein qualifiziertes Ereignis (unveränderte Regeln: Trigger `$Softwareschmiede` im Text, angemeldeter Benutzer = Repository-Owner = Bearbeiter der Textversion, keine eigene Antwort) soll auch dann verarbeitet werden, wenn noch keine lokale Aufgabe zur Referenz existiert. Dafür ist ein servicenaher Auflösungsschritt „finde passende Aufgabe oder lege sie an" einzuführen, der vor bzw. bei der Inbox-Aufnahme wirkt – die Invariante, dass jedes Inbox-Ereignis eine existierende Aufgabe besitzt (`ScmIncomingEvent.AufgabeId` nicht-nullable), bleibt erhalten.

Fachliche Regeln der Auflösung:

- **Wiederverwendung:** Gehört die Referenz zu genau einer Aufgabe (Issue-Referenz über `IssueReferenz.IssueNummer` + zugehöriges `GitRepository`; PR-Referenz über `PullRequestReferenz`-Schlüssel Provider + RepositoryId + PullRequestNumber), wird diese Aufgabe wiederverwendet. Es entstehen keine Duplikate – auch nicht bei wiederholten Polling-Läufen, erneut eintreffenden Ereignissen derselben Referenz oder nebenläufiger Verarbeitung (Dedup-Schlüssel plus geeignete Absicherung gegen Anlagerennen, analog dem vorhandenen `DbUpdateException`-Muster bei PR-Anlagen).
- **Anlage:** Existiert keine passende Aufgabe, wird sie angelegt: Projekt aus `GitRepository.ProjektId` des überwachten Repositories, Titel und Auftragstext aus den Ereignisdaten (Issue-/PR-Titel plus der qualifizierte Text, z. B. Issue-/PR-Beschreibung bzw. auslösender Kommentar, mit Herkunftsverweis/URL); die bestehenden Anlagebausteine `AufgabeService.CreateFromIssueAsync`/`CreateFromPullRequestAsync` inkl. der PR-Body-Bereinigung sind zu nutzen bzw. servicenah zugänglich zu machen. Die angelegte Aufgabe trägt die Issue- bzw. PR-Referenz, sodass Folgeereignisse derselben Referenz sie wiederverwenden.
- **Mehrdeutigkeit:** Mehrere passende Aufgaben auf derselben Referenz bleiben wie heute eine Diagnose („Mehrdeutige Zuordnung zu Aufgaben") ohne Inbox-Eintrag – es wird keine automatische Auswahl eingeführt.
- **Nicht-qualifizierte Ereignisse** (kein Trigger, Owner-/Bearbeiter-Verstoß, eigene Antworten) erzeugen weder Aufgabe noch Inbox-Eintrag; ihr Snapshot-/Diagnose-Verhalten bleibt wie bisher.
- Die Anlage startet die Aufgabe noch nicht – automatischer Start und Zustellung laufen über die bestehende Dispatch-Pipeline (Sicherstellung und End-to-End-Absicherung in Schritt 4).

**Akzeptanzkriterien:**

- Ein qualifiziertes Ereignis zu einem neuen Issue oder Pull Request in einem überwachten Repository führt zur Anlage genau einer passenden lokalen Aufgabe mit Issue-/PR-Referenz.
- Ein qualifizierter Kommentar oder Review zu einer Referenz ohne bestehende Aufgabe führt zur Anlage einer passenden Aufgabe, die die Referenz trägt.
- Existiert bereits eine passende Aufgabe, wird sie wiederverwendet; wiederholte oder nebenläufige Ereignisse derselben Referenz erzeugen keine zweite Aufgabe.
- Nicht-qualifizierte Ereignisse erzeugen keine Aufgabe und keinen Inbox-Eintrag; bei mehrdeutiger Zuordnung bleibt die bisherige Diagnose ohne Inbox-Eintrag erhalten.
- Neu angelegte Aufgaben erscheinen in der Aufgabenverwaltung des richtigen Projekts mit nachvollziehbarem Titel, Auftragstext und Quellverweis.
- Bestandsschutz: Validierungsregeln, Snapshot-/EventKey-Deduplizierung und Checkpoint-Verhalten bleiben unverändert; für Referenzen mit bestehender Aufgabe ändert sich der Aufnahmepfad nicht.

**Abhängigkeiten:** 2

**Betroffene Bereiche:** Aufgabenverwaltung, SCM-Ereignisüberwachung

### Schritt 4: Durchgängiger automatischer Start und Zustellung der neuen Aufgaben inklusive Wartend-Verhalten ohne Standard-CLI

**Fachliche Beschreibung:**

Die in Schritt 3 automatisch angelegte oder wiederverwendete Aufgabe soll denselben Start- und Zustellungspfad durchlaufen wie bisherige aufgabenverknüpfte Ereignisse – ohne neuen Parallelmechanismus:

- Nach den vorhandenen Sicherheits- und Owner-Prüfungen pro Zustellversuch (erneute Repository-Identitäts-/`CanWrite`-Prüfung, Trigger-/Bearbeiter-Validierung, Blocker-Prüfung beendeter/archivierter/pausierter Aufgaben) wird die Aufgabe automatisch gestartet (Status `Neu` → `Gestartet`; Klon, Branch und CLI-Start über `EntwicklungsprozessService`) und der Auftrag an die aufgelöste Standard-CLI zugestellt (Auflösungskette: Aufgabe → Projekt-Default → globaler Default).
- Fehlt eine Standard-CLI, bleibt das Ereignis wartend mit der eindeutigen, bedienbaren Aufforderung (heute: „Keine Standard-CLI konfiguriert. Ereignis öffnen und CLI auswählen." plus Aktionen „Öffnen / CLI auswählen" und „Erneut prüfen" in der SCM-Ereignisübersicht). Für neu angelegte Aufgaben ist nachzuweisen, dass dieser Weg wirksam ist: Nach Auswahl bzw. Konfiguration einer CLI läuft die Zustellung an.
- Fachlich zu entscheiden und zu dokumentieren: ob die Aufgabenanlage aus Schritt 3 auch bei fehlender Standard-CLI erfolgt (empfohlen: ja – das Ereignis bleibt wartend und ist nach CLI-Konfiguration zustellbar, die Aufgabe ist sichtbar) oder die Anlage bis zur CLI-Auflösung zurückgestellt wird. In beiden Varianten muss gelten: keine Duplikatanlage, keine verlorenen Ereignisse, bedienbare Aufforderung.
- Der Schritt enthält die Bestandsschutz-Verifikation (FR-9) für das Gesamtfeature: Trigger-Regeln, Repository-Owner-Prüfung, Token-Berechtigungen (inkl. erneuter `CanWrite`-Prüfung und Wartehinweis-Kommentaren), deduplizierte/idiempotente Zustellung über die Outbox und die Funktionen der SCM-Ereignisübersicht bleiben erhalten – abgesichert durch die bestehenden Tests plus neue, in die konsolidierte SCM-E2E-Methode(n) integrierte Szenarien für „Ereignis ohne Aufgabe → Anlage → Auto-Start → Zustellung" sowie „fehlende Standard-CLI → wartend → CLI-Auswahl → Zustellung". Neue FlaUI-Methoden sind nur soweit nötig zu ergänzen und in die vorhandene konsolidierte Struktur einzuordnen (Laufzeit der E2E-Suite minimal halten).

**Akzeptanzkriterien:**

- Eine automatisch angelegte oder wiederverwendete Aufgabe wird nach den bestehenden Sicherheits- und Owner-Prüfungen automatisch gestartet und der Auftrag an die konfigurierte Standard-CLI zugestellt – nachweisbar Ende-zu-Ende (Ereignis ohne Aufgabe → Aufgabe gestartet → Prompt zugestellt → Verarbeitung via Antwortkanal).
- Ohne konfigurierte Standard-CLI bleibt das Ereignis mit eindeutiger, bedienbarer Aufforderung wartend; nach CLI-Auswahl (oder CLI-Konfiguration + „Erneut prüfen") wird zugestellt.
- Blockierte Aufgaben (beendet, archiviert, pausiert) werden auch im neuen Pfad nicht automatisch gestartet; ihr Ereignis bleibt mit Grund wartend (bisheriges Verhalten).
- Bestandsschutz nachgewiesen: Trigger-/Owner-/Token-Regeln, deduplizierte Zustellung und SCM-Ereignisübersicht sind durch unverändert grüne Bestandstests plus neue Szenarien abgesichert; die SCM-E2E-Abdeckung bleibt konsolidiert.
- Die getroffene Entscheidung zum Zeitpunkt der Aufgabenanlage bei fehlender Standard-CLI ist dokumentiert und testabgedeckt.

**Abhängigkeiten:** 3

**Betroffene Bereiche:** CLI-Zustellung, Aufgabenverwaltung, SCM-Ereignisübersicht

---

## Übersichtstabelle

| # | Titel | Abhängigkeiten | Betroffene Bereiche |
|---|-------|----------------|---------------------|
| 1 | Verlässliche Fortsetzung der CLI-Zustellung nach der Vertrauenswürdigkeitsabfrage (inkl. Prüfauftrag Startparameter) | – | CLI-Zustellung, SCM-Ereignisübersicht |
| 2 | Repository-weite GitHub-Ereigniserkennung | – | SCM-Ereignisüberwachung, GitHub-Plugin |
| 3 | Automatische Aufgabenanlage und -wiederverwendung aus qualifizierten Ereignissen | 2 | Aufgabenverwaltung, SCM-Ereignisüberwachung |
| 4 | Durchgängiger automatischer Start und Zustellung der neuen Aufgaben inklusive Wartend-Verhalten ohne Standard-CLI | 3 | CLI-Zustellung, Aufgabenverwaltung, SCM-Ereignisübersicht |

---

## Offene Punkte

Die offenen Fragen Q1–Q7 der Anforderung sind aus Anforderung und Bestand jeweils mit einem Vorschlag belegbar; eine Klärung mit dem Auftraggeber ist vor Umsetzung der betreffenden Schritte empfohlen, blockiert den Plan aber nicht.

- **Q1 – Qualifizierter Auslöser (betrifft Schritt 3):** Vorschlag: exakt die bestehenden Validierungsregeln des SCM-Ereignisdienstes – Trigger `$Softwareschmiede` (Groß-/Kleinschreibung egal) im Text, angemeldeter Benutzer = Repository-Owner = Bearbeiter der betreffenden Textversion, keine eigenen Antwort-Marker. Begründung: Die Anforderung verweist nur auf „qualifizierter Auslöser" und schützt die bestehenden Trigger-Anforderungen ausdrücklich; neue Trigger-Arten sind Nicht-Ziel. Ereignisse ohne Qualifikation erzeugen weiter nur Snapshot/Diagnose, keine Aufgabe.
- **Q2 – Inhalte der automatisch angelegten Aufgabe (betrifft Schritt 3):** Vorschlag: Titel aus dem Issue-/PR-Titel (sinnvoll gekürzt), Auftragstext aus dem qualifizierten Inhalt – bei Issue-/PR-Ereignissen die Beschreibung, bei Kommentar-/Review-Auslösern der auslösende Text plus Einordnung der Referenz – jeweils mit Herkunftsverweis (URL, Referenznummer). Für PRs die vorhandene Body-Bereinigung (HTML-/Marker-Entfernung aus `CreateFromPullRequestAsync`) wiederverwenden. Begründung: orientiert sich an den bestehenden UI-Anlagepfaden, damit auto- und manuell angelegte Aufgaben gleich aussehen.
- **Q3 – Ereignistypen im Detail (betrifft Schritt 2):** Vorschlag: alle heute gelesenen Inhaltsarten repository-weit überwachen – Beschreibung, Issue-/Diskussions-Kommentare, Review-Kommentare an PRs und Review-Entscheide. Begründung: „Kommentare und Reviews" in der Anforderung ist am plausibelsten als „alles, was die Quelle bereits liefern kann" zu lesen; eine Einschränkung auf Top-Level-Kommentare hätte keinen erkennbaren fachlichen Grund.
- **Q4 – Bedienbare Aufforderung bei fehlender Standard-CLI (betrifft Schritt 4):** Vorschlag: vorhandene Darstellung beibehalten – wartender Eintrag in der SCM-Ereignisübersicht mit Grund „Keine Standard-CLI konfiguriert. Ereignis öffnen und CLI auswählen." und den Aktionen „Öffnen / CLI auswählen" sowie „Erneut prüfen". Diese Aktionen müssen nach der Korrektur nachweislich wirksam sein (für neu angelegte Aufgaben verifizieren). Eine zusätzliche Benachrichtigung außerhalb der Übersicht ist nicht gefordert.
- **Q5 – Fortsetzung nach Trust-Bestätigung (betrifft Schritt 1):** Vorschlag: automatische Fortsetzung über den bestehenden Wiederholungs-Dispatch (wartende Einträge ohne gesetzte Sperre werden alle 60 s erneut versucht), sobald der hängende Kanalproben-Zustand behoben und der Vertrauensdialog als eigener Wartegrund erkannt ist; der manuelle Weg („Erneut prüfen") bleibt als sofort wirksame Alternative. Begründung: „zuverlässig fortsetzen können" wird damit am stärksten ausgelegt, ohne neue Bedienwege zu erfinden.
- **Q6 – CLI-Startparameter (betrifft Schritt 1):** Vorschlag: für Claude Code zuerst die Host-seitige Vorbelegung des Verzeichnis-Trust-Status in `~/.claude.json` (`hasTrustDialogAccepted` pro Arbeitsverzeichnis) prüfen, für Codex die `config.toml`-Konfiguration (`approval_policy`/`sandbox_mode`); ein gefundener Weg darf den Kanal-Sicherheitsvertrag nicht unterlaufen – kein Vorschlag wie ein pauschales „alle Berechtigungen überspringen"-Flag. Ergebnis dokumentieren; Umsetzung nur bei nachgewiesener Vereinbarkeit mit der Parameter-Whitelist der Kanalprüfung.
- **Q7 – Überwachungsmechanismus (betrifft Schritt 2):** Vorschlag: bestehender Polling-Mechanismus (60 s, konfigurierbar) mit Checkpoints/Snapshots/Dedup; kein Webhook o. Ä. Begründung: Wiederverwendung der bewährten Fehler-/Deduplizierungslogik, keine neuen Betriebsanforderungen; die Anforderung nennt keinen anderen Ansatz.
- **Zusatz – Umgang mit Ereignissen an geschlossenen oder sehr alten Referenzen (betrifft Schritt 2):** Vorschlag: repository-weite Enumeration auf offene Issues/PRs beschränken, sofern keine gegenteilige Vorgabe; geschlossene Referenzen sind für die Trigger-getriebene Aufgabenanlage ohnehin kaum relevant. Bei Umsetzung fachlich begründet entscheiden und dokumentieren.
