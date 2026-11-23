# SCM-Ereignisabläufe — verbindliche Übersicht je Ereignisart

Diese Seite beschreibt den tatsächlichen Ablauf jeder SCM-Ereignisart vom Eingang bis zum
Abschluss (Issue #457). Sie ist normativ mit der Implementierung abgeglichen; Begriffe in
`Klammern` benennen die tragenden Komponenten bzw. persistierten Werte.

## Gemeinsame Pipeline aller Ereignisse

1. **Eingang:** Der Poll (`ScmEventMonitoringService`, Standard 60 s) enumeriert Referenzen
   aktiver Repositories und liest neue Inhalte (Kommentare, Beschreibungen, Notifications).
2. **Qualifizierung:** Trigger-Keyword (Modus „Benutzerkonto") bzw. freistehende
   `@`-Erwähnung (Modus „Separates Konto"), Owner-/Vertrauenslisten-Prüfung,
   Reaktions-Dedupe (`GetRejection`).
3. **Registrierung:** Das qualifizierte Ereignis wird als `ScmIncomingEvent` persistiert —
   der `EventKey`-Unique-Index dedupliziert über Polls, Neustarts und Anlagerennen hinweg.
4. **Aufgabenzuordnung:** `ScmEventTaskResolver` ordnet das Ereignis genau einer Aufgabe zu
   (Details je Ereignisart unten). Einmal zugeordnete `AufgabeId` ändert sich nicht mehr.
5. **Zustellung:** `ScmEventOrchestrator.DispatchAsync` serialisiert je Aufgabe
   (Vorgänger-Gating nach `ScmIncomingEventStatus.NonBlocking`, `ScmTaskLease`,
   Ausführungsbegrenzung mit ehrlichem Wartegrund). Bei freiem Platz wird die Aufgabe
   gestartet (Klon, Branch), die CLI hochgefahren und — nur bei frisch gestarteter Sitzung —
   einmalig die Access-Probe (Datei-Lese-/Schreibtest) ausgeführt; danach wird der Prompt in
   den privaten Antwortkanal geschrieben (`Zugestellt`). Vor jeder Schreibung gilt die
   **Stabilisierungsfrist** (`DeliveryStabilityPeriod` = 10 s, fest verdrahtet): Der Prompt
   geht erst raus, wenn die Sitzung seit mindestens so lange ununterbrochen am verifizierten
   Eingabeprompt steht (`adapter.ReadySinceUtc`, bei jedem Übergang in
   `ScmPromptReadyStatus.Ready` neu gestempelt). Ist sie kürzer bereit, endet der Versuch
   `Wartend` mit `NextAttemptUtc = ReadySinceUtc + Frist` (generischer Pfad mit
   `waiting`-Hostkommentar, Aufbereitungspfad ohne); der Folgeversuch läuft über den
   normalen Zyklus — ein Statuswechsel innerhalb der Frist lässt sie faktisch neu
   anlaufen, nie wartet ein Versuch in sich.
6. **Bearbeitung:** `accepted.json` → `InBearbeitung`; `complete.json` → Outbox-Aktionen
   (Kommentar, Reaktion, PR, `issueBodyUpdate`) → `Verarbeitet`.
7. **Abschluss:** `ScmTaskCompletionMonitoringService` beendet die Aufgabe, sobald ihre
   SCM-Referenz final geschlossen/gemergt ist (siehe Ablauf D).

**Duplikatgarantie:** Pro Referenz entsteht höchstens eine Aufgabe — Issue-Anlagen
deduplizieren über `IssueReference.SourceKey` (`IssueNumber` + kanonischem Schlüssel),
PR-Anlagen über den Unique-Index `Provider` + `RepositoryId` + `PullRequestNumber`,
Alerts über `AlertReference.SourceKey`. Die Issue-Aufbereitung ist seit Issue #463 in
dieselbe Garantie eingeschlossen: Kommentar-Auflösung und Aufbereitungs-Erkennung
deduplizieren symmetrisch auf dieselbe Aufgabe (zweistufige Auswertung bzw.
Bestandssuche, beide unter dem prozessinternen Issue-Gate
`ScmEventTaskResolver.IssueGateKey` auf `ScmTaskCoordinator.Shared`) — ein
Neuanlage-Szenario erzeugt keine zwei Aufgaben mehr. Bereits bestehende
Altbestand-Paare (reguläre + zweckmarkierte Aufgabe derselben Issue-Nummer) werden
nicht bereinigt; ihre neuen Kommentare laufen weiter auf die reguläre Aufgabe.

## Ablauf A: „Issue erstellt"

Es gibt zwei getrennte Erkennungspfade:

- **Mit Issue-Aufbereitung (opt-in):** `ScmIssuePreparationMonitoringService` entdeckt das
  neue Issue repository-weit, liest Beschreibung und reiht den Aufbereitungsauftrag ein —
  dedupliziert über den `issueaufbereitung-issue/`-Checkpoint. Unter dem Issue-Gate prüft
  `DetectAndEnqueueAsync` zuerst den Bestand: Existiert bereits eine geeignete Aufgabe
  derselben Issue-Nummer (auf einem aufbereitungs-freigeschalteten Repository, nicht
  beendet/archiviert; reguläre Aufgabe zuerst, dann `CreatedAt`), wird das interne
  Ereignis an sie gebunden (`WorkItemId` gesetzt, keine Neuanlage); erst ohne Treffer
  entsteht die eigene **Aufbereitungsaufgabe** (`Purpose = IssueAufbereitung`,
  Auftragsart `Issue-Aufbereitung`, eigener `PreparationSourceKey`). Das interne Ereignis
  ist trigger-frei, sofort
  fällig und läuft über `DispatchPreparationAsync`: Plugin-Auflösung (Aufbereitungs-Plugin
  mit Fallback-Kette), Ausführungsbegrenzung, CLI-Start, Access-Probe, fester
  Analyse-Prompt. Danach werden gelieferte `issueBodyUpdate`-/`comment`-Aktionen über die
  idempotente Outbox ausgeführt; der Auftrag gilt als `Verarbeitet`. Mit der Zustellung des
  Analyse-Prompts wechselt die Aufgabenart zu `Anweisung`; `Purpose` bleibt zur
  Routing-Isolation unverändert. Vor Prompt-Zustellung und vor der Rückschreibung wird der
  Live-Issue-Status erneut geprüft — ein inzwischen geschlossenes Issue erhält keine
  Remote-Änderung. Konkurrieren fällige Ereignisse derselben Prioritätsklasse um einen
  Ausführungsplatz, erhält das zweckmarkierte Aufbereitungs-Ereignis den Vortritt
  (`ScmDispatchPriority.OrderForDispatch`: Klasse → Klasse-2-`NextAttemptUtc` →
  `Purpose`-Tiebreak → `Sequence`); innerhalb derselben Aufgabe ordnet ohnehin die
  Vorgängerprüfung.
- **Ohne Issue-Aufbereitung:** Eine neue Issue-Anlage erzeugt kein Ereignis — außer die
  Beschreibung selbst ist qualifiziert (Trigger-Keyword bzw. Erwähnung des separaten
  Kontos): dann durchläuft sie denselben Pfad wie Ablauf B (Anweisungsaufgabe aus der
  Beschreibung).

**Abweichung zur ursprünglichen Skizze:** Der Sketch sah eine Anweisungsausführung direkt
bei Issue-Anlage vor. Umgesetzt ist die getrennte Aufbereitungsaufgabe mit eigenem,
deterministischem Analyse-Prompt — bewusstes Produktdesign (Isolation, Schleifenschutz,
opt-in).

## Ablauf B: Trigger-Kommentar in einem Issue

1. Qualifizierter Kommentar (bzw. qualifizierte Beschreibung) → `ScmIncomingEvent`.
2. `ScmEventTaskResolver.ResolveIssueAsync` wertet die Aufgaben der Issue-Nummer
   **zweistufig** aus (unter dem Issue-Gate `IssueGateKey`, das den Check-then-Create-Abschnitt
   gegen den Aufbereitungs-Erkennungspfad serialisiert): Genau eine **reguläre** Aufgabe
   → zugeordnet; mehrere reguläre → mehrdeutig; keine reguläre und genau eine
   **zweckmarkierte** Aufbereitungsaufgabe → zugeordnet; mehrere zweckmarkierte →
   mehrdeutig; kein Kandidat → Anlage aus den Ereignisdaten
   (`CreateFromIssueEventAsync`, Auftragsart `Anweisung`, `IssueSourceKey`-Dedup). Ein
   Trigger auf einem Issue mit nur einer Aufbereitungsaufgabe ordnet sich ihr damit zu
   statt eine zweite Aufgabe anzulegen; existiert zusätzlich eine reguläre Aufgabe
   (Altbestand-Paar), gewinnt unverändert die reguläre.
3. Beendete/archivierte Aufgaben werden nicht reaktiviert — ein Folgetrigger legt eine neue
   Aufgabe an; pausierte Aufgaben gelten als aktiv, ihr Ereignis wartet regulär.
4. Zustellung über die gemeinsame Pipeline (Wartegrund statt Anlageduplikat bei
   mehrdeutiger Zuordnung).

## Ablauf C: Trigger-Kommentar in einem Pull Request

1. Qualifizierter Kommentar im PR → `ScmIncomingEvent` mit PR-Referenz.
2. Aufgabenzuordnung in dieser Reihenfolge:
   - **Persistierte `PullRequestReference`** einer aktiven Aufgabe → diese Aufgabe.
   - **Kontextzuordnung** ohne persistierte Referenz (typisch: der PR wurde eben von der
     laufenden Aufgabe erstellt): eindeutiger **Branch-Abgleich** (PR-Head-Branch =
     `BranchName` der Aufgabe, nur desselbe Repository, kein Fork) **oder**
     **Closing-Direktive** (`Closes`/`Fixes`/`Resolves #N` im PR-Text auf die
     Issue-Nummer der Aufgabe). Genau ein Treffer → Zuordnung ohne Zweitanlage.
   - **Kein Kontext** → neue Aufgabe mit `PullRequestReference` der Rolle `ReviewSource`
     (Auftragsart `Pull Request`) — die Aufgabe reviewt/bearbeitet den PR selbst.
   - **Widersprüchliche Signale** (Branch → Aufgabe A, Direktive → Aufgabe B) → Ereignis
     bleibt mit Mehrdeutigkeitsdiagnose unzugeordnet.
3. Zustellung über die gemeinsame Pipeline. Die implizite Kontextzuordnung erzeugt keine
   dauerhafte Referenz; die finale Verknüpfung erfolgt über `pullRequestNumber` in der
   Abschlussmeldung der Aufgabe.

**Abweichung zur Skizze:** Statt „zugehöriges Issue ermitteln" (API-Abfrage PR → Issue)
nutzt die Implementierung Branch- und Closing-Signale — sie ist robuster (keine
zusätzliche API-Berechtigung nötig) und deckt denselben fachlichen Fall ab.

## Ablauf D: „Issue geschlossen" / „Pull Request geschlossen oder gemergt"

Das Schließen ist kein Eingangs-Ereignis, sondern Aufgabe der Abschlussüberwachung
(`ScmTaskCompletionMonitoringService`, Schalter `scm.taskcompletion.enabled`, Scope
`scm.taskcompletion.scope`):

1. **Referenzprüfung:** `DetermineClosureAsync` prüft je Aufgabe — alle zugehörigen
   Pull Requests final (`Closed`/`Merged`; `Merged` ist endgültig, `Closed` wird
   weitergeprüft) bzw. das Issue geschlossen (Issue-Prüfung immer unter dem
   Benutzerkonto). Offene Events der Aufgabe in blockierenden Stati verschieben die
   Prüfung ehrlich.
2. **Wiedervorlage:** Beim ersten Abschluss-Fund wird `ScmCompletionDetectedUtc` gesetzt
   und ein Protokolleintrag erzeugt; die Aufgabe erhält die konfigurierte Wartezeit
   (`scm.taskcompletion.wait.minutes`). Wird die Referenz wieder geöffnet, wird der
   Countdown ehrlich zurückgesetzt (Protokolleintrag).
3. **Abschluss:** Nach Ablauf der Wartezeit läuft `DevelopmentProcessService.CompleteAsync`
   unter der Aufgabensperre: Die CLI wird beendet (wartend oder aktiv — der Versuch einer
   wartenden CLI kostet keinen Retry), der lokale Klon wird gelöscht, die Aufgabe geht auf
   `Completed`.
4. **Fehlerbudget:** Fehlschläge (offene To-Dos, gesperrter Klon, Provider-Fehler) teilen
   ein Budget von 10 Versuchen mit exponentiellem Backoff (1 → 60 min) je
   Fehlersignatur; der erkannte Abschlusszeitpunkt bleibt persistiert. Nach Erschöpfung
   wird die Überwachung sichtbar ausgesetzt (Protokolleintrag, ggf. mit
   Sperr-Prozessdiagnose) statt endlos weiterzupollen — Ursache beheben, manuell
   abschließen oder Anwendungsneustart setzt sie fort.

## Bewusste Nicht-Ziele / dokumentierte Abweichungen

- **Keine Aufgabe pro Issue ohne Auslöser:** Eine Issue-Anlage ohne Trigger und ohne
  aktivierte Aufbereitung bleibt folgenlos — siehe Ablauf A.
- **Aufbereitung und Anweisung teilen sich seit Issue #463 dieselbe Aufgabe:** Das interne
  Aufbereitungs-Ereignis bindet an eine geeignete bestehende Aufgabe des Issues (und ein
  Trigger-Kommentar an die zweckmarkierte Aufbereitungsaufgabe), statt eine zweite
  anzulegen — die Anfrage läuft als Folgeereignis in derselben Sitzung. Ausgenommen
  bleiben Altbestand-Paare (Kommentare gehen an die reguläre Aufgabe) sowie die Fälle,
  in denen keine geeignete Bestandsaufgabe existiert (beendet/archiviert oder Repository
  nicht aufbereitungs-freigeschaltet): Dann entsteht weiterhin eine eigene
  Aufbereitungsaufgabe.
- **Access-Probe-Reihenfolge:** Die Probe läuft nur bei frisch erkannter Sitzung einmalig
  vor der ersten Prompt-Zustellung — nicht vor jedem Ereignis; eine bestehende, am Prompt
  wartende CLI erhält Folgeereignisse direkt.
- **„Issue geschlossen" ohne `pullRequestNumber`:** Meldet eine Aufgabe keinen PR, endet
  die Überwachung an der Issue-Referenz — ein später separat erstellter PR wird nicht
  nachträglich verknüpft.
