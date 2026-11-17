# Projektanforderung: SCM-Ereignisverarbeitung – Ausführungsbegrenzung, Plugin-Fallback, Issue-Aufbereitung

**Quelle:** GitHub-Issues #410 („Ausführungsbegrenzung"), #411 („Deaktivierte KI-Plugins bei SCM-Ereignisausführung"), #412 („Issue-Aufbereitung") / Aufgaben-ID `c69c8e7f-6eb1-4c4c-be40-7bdbdc9e1cd6`
**Branch:** `task/issue-410-c69c8e7f6eb14c4cbe407bdbdc9e1cd6-ausfuehrungsbegrenzung`

**Hinweise zur Einordnung:**
- Alle drei Issues sollen gemeinsam in einem finalen PR gegen den Branch `staging` umgesetzt werden (organisatorischer Rahmen, kein fachlicher Bestandteil).
- Im ursprünglichen Auftrag war irrtümlich Issue #422 genannt, das nicht existiert; als gemeint wurde #412 angenommen.
- **Kennzeichnung im Dokument:** Aussagen, die wörtlich oder sinngemäß aus den Issue-Texten stammen, gelten als *belegt*. Alles, was interpretierend ergänzt wurde, ist explizit als **Annahme** bzw. **abgeleitet** markiert.

## Ziel

Die bisher unbegrenzte, sofortige Ausführung eingehender SCM-Ereignisse wird um eine konfigurierbare Ausführungsbegrenzung (global und/oder je KI-Plugin) mit definierter Priorisierung der Warteschlange ergänzt (#410). Zusätzlich wird die Ausführung robuster gegen deaktivierte KI-Plugins: Ist das Standard-KI-Plugin eines Projekts nicht mehr aktiviert, greift ein Fallback auf das globale Standard-Plugin; ist auch dieses nicht verfügbar, startet die Ausführung nicht (#411). Ergänzend wird eine optional aktivierbare, KI-gestützte Aufbereitung neu angelegter Issues eingeführt, die Titel und Beschreibung analysiert, die Beschreibung durch eine qualitativ hochwertige Anforderungsbeschreibung ersetzt und optional einen dokumentierenden Kommentar erzeugt (#412).

## Fachliche Anforderungen

### Issue #410 – Ausführungsbegrenzung

- **Belegt:** Aktuell wird jedes empfangene SCM-Ereignis direkt ausgeführt. Es wird eine Begrenzung der parallelen Ausführungen benötigt, die in den Einstellungen angegeben werden kann.
- **Belegt:** Die Begrenzung kann global gelten oder für jedes KI-Plugin einzeln.
- **Belegt – Auslöser der Ausführungsstartprüfung:** Die Prüfung findet statt
  1. sobald ein SCM-Ereignis eingeht, und
  2. sobald eine über ein SCM-Ereignis ausgeführte Aufgabe als beendet oder pausiert erkannt wird.
- **Belegt – Prüflogik:** Bei der Prüfung werden die Aufgaben gezählt, bei denen gerade eine CLI-Ausführung stattfindet. Ist das globale oder das pluginbezogene Limit erreicht, gibt es nichts zu tun. Ist noch ein Slot frei, wird das nächste auszuführende Ereignis ermittelt.
- **Belegt – Prioritätenreihenfolge bei der Ereignisermittlung:**
  1. Ereignisse für pausierte oder beendete Aufgaben
  2. Ereignisse mit Angabe einer Verzögerungszeit, deren Ausführungszeit erreicht ist, sortiert nach Zeitpunkt aufsteigend
  3. Sonstige Ereignisse, sortiert in Eingangsreihenfolge

### Issue #411 – Deaktivierte KI-Plugins bei SCM-Ereignisausführung

- **Belegt – Ausgangslage:** Es kann vorkommen, dass die Ausführung einer Aufgabe aufgrund eines SCM-Ereignisses nicht erfolgreich ist, weil das Standard-KI-Plugin des Projekts in den Einstellungen nicht mehr aktiviert ist.
- **Belegt – Fallback-Kette:**
  1. Ist das Standard-KI-Plugin des Projekts nicht mehr aktiviert, wird die Standard-KI-Zuordnung des Projekts ignoriert.
  2. Stattdessen wird das global gesetzte Standard-Plugin gewählt.
  3. Ist auch dieses nicht verfügbar oder nicht aktiv, darf die Ausführung nicht starten.

### Issue #412 – Issue-Aufbereitung (KI-gestützte Optimierung neu angelegter Issues)

- **Belegt – Ziel:** Neu angelegte Issues sollen automatisch analysiert und in eine qualitativ hochwertige, vollständige und umsetzbare Anforderungsbeschreibung überführt werden.
- **Belegt – Funktion:**
  - Die Anwendung überwacht optional neu angelegte Issues in konfigurierten Repositories.
  - Ist die Funktion aktiviert, wird nach Erstellung eines neuen Issues automatisch eine KI-Analyse gestartet.
  - Die Anwendung übermittelt mindestens Titel und Beschreibung des Issues an ein konfiguriertes KI-Plugin.
  - Ein Standard-KI-Plugin für diese Funktion muss in den Einstellungen konfigurierbar sein.
  - Die Anwendung erzeugt einen Prompt, der das KI-Plugin anweist, die bestehende Anforderung zu analysieren und zu verbessern.
  - Die Analyse berücksichtigt insbesondere: Verständlichkeit, sprachliche Qualität, Vollständigkeit, fehlende Informationen, offene Fragen, technische Risiken, fachliche Risiken, Zielkonflikte, alternative Lösungsansätze, mögliche Fehlinterpretationen.
  - Das KI-Plugin erzeugt auf Basis dieser Analyse eine verbesserte und strukturierte Anforderungsbeschreibung.
- **Belegt – Ergebnisanwendung:** Nach erfolgreicher Analyse ersetzt die Anwendung die ursprüngliche Issue-Beschreibung durch die von der KI erzeugte Anforderungsbeschreibung. Der ursprüngliche Titel bleibt unverändert.
- **Belegt – Optionaler Analyse-Kommentar:** Zusätzlich kann ein Kommentar erzeugt werden, der die Ergebnisse der KI-Analyse dokumentiert (identifizierte Schwachstellen, offene Fragen, Brainstorming möglicher Lösungsansätze, erkannte Risiken, Zielkonflikte, Verbesserungsvorschläge). Die Erstellung dieses Kommentars ist separat konfigurierbar.
- **Belegt – Fehlerbehandlung:** Fehler während der Analyse werden protokolliert und führen nicht zur Änderung des Issues.

## Betroffene Bereiche

Aus den Issue-Texten direkt ableitbar (belegt):

- **SCM-Ereignisverarbeitung** – Aufnahme, Warteschlange und Ausführung eingehender SCM-Ereignisse (#410, #411).
- **Einstellungen** – Konfiguration von Ausführungslimits (global/pro Plugin), Plugin-Aktivierung, Standard-Plugin-Zuordnung, Funktionsschalter und Plugin-Auswahl der Issue-Aufbereitung (#410, #411, #412).
- **KI-Plugin-Verwaltung** – Plugin-Aktivierungsstatus, projektbezogene und globale Standard-Plugin-Zuordnung, Plugin-Auswahl für die Ausführung (#411, #412).
- **SCM-/Repository-Anbindung** – Überwachung konfigurierter Repositories auf neu angelegte Issues sowie Schreibzugriff auf Issue-Beschreibung und Kommentare (#412).

**Annahmen** (aus dem Codebestand abgeleitete, nicht aus den Issue-Texten belegte Anknüpfungspunkte — dienen der Orientierung, konkrete Umsetzung bleibt offen):

- `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs`, `ScmEventStore.cs`, `ScmEventMonitoringService.cs` sowie `src/Softwareschmiede/Domain/Entities/ScmIncomingEvent.cs` bilden die bestehende Ereignisverarbeitung und -warteschlange; dort sind Status, `NextAttemptUtc`, `ReceivedUtc` und Eingangsreihenfolge (`Sequence`) bereits abgebildet — voraussichtlich der Ansatzpunkt für die Startprüfung und Priorisierung (#410).
- `src/Softwareschmiede/Application/Services/ScmDelayDirective.cs` implementiert bereits eine Verzögerungs-Direktive (`#verzögert`) mit Zielzeitpunkt — vermutlich die gemeinte „Angabe einer Verzögerungszeit" aus Priorität 2 (#410).
- `src/Softwareschmiede/Application/Services/PluginDefaultSettingsService.cs` / `PluginSelectionService.cs` verwalten bereits globale und projektbezogene Standard-Plugins (`PluginType.DevelopmentAutomation`) — Ansatzpunkt für die Fallback-Kette (#411) und ggf. die Plugin-Konfiguration der Issue-Aufbereitung (#412).
- `src/Softwareschmiede/Application/Services/PluginActivationService.cs` verwaltet den Aktivierungsstatus der Plugins — Ansatzpunkt für die Prüfung „aktiviert/verfügbar" (#411).
- `src/Softwareschmiede/Application/Services/KiPluginLimitService.cs` existiert bereits, behandelt aber pluginseitig gemeldete Session-Limits (Rate-Limit-Pausen), nicht die geforderte Parallelitätsbegrenzung — fachlich verwandt, aber ein anderer Mechanismus (#410).
- `src/Softwareschmiede/Application/Services/AppEinstellungService.cs` / `src/Softwareschmiede/Domain/Entities/AppEinstellung.cs` und `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs` sind die bestehenden Persistenz- bzw. UI-Anker für neue Einstellungen (#410, #412).
- Repository-Bezug: `src/Softwareschmiede/Domain/Entities/GitRepository.cs` / `Projekt.cs` — möglicher Anker für „konfigurierte Repositories" (#412).

## Abläufe

### Ablauf Ausführungsstartprüfung (#410)

1. Auslöser: Eingang eines SCM-Ereignisses **oder** Erkennung, dass eine über ein SCM-Ereignis ausgeführte Aufgabe beendet bzw. pausiert wurde.
2. Zählen der Aufgaben mit aktuell laufender CLI-Ausführung.
3. Ist das globale oder das pluginbezogene Limit erreicht → Prüfung endet ohne Aktion.
4. Andernfalls Ermittlung des nächsten auszuführenden Ereignisses nach der Prioritätenreihenfolge (pausierte/beendete Aufgaben → fällige Verzögerungszeit-Ereignisse aufsteigend → sonstige Ereignisse in Eingangsreihenfolge) und Start der Ausführung.

### Ablauf Plugin-Fallback bei SCM-Ereignisausführung (#411)

1. Vor Start der durch ein SCM-Ereignis ausgelösten Ausführung: Standard-KI-Plugin des Projekts ermitteln.
2. Ist dieses nicht mehr aktiviert → Projektzuordnung ignorieren, global gesetztes Standard-Plugin wählen.
3. Ist das globale Standard-Plugin ebenfalls nicht verfügbar oder nicht aktiv → Ausführung startet nicht.

### Ablauf Issue-Aufbereitung (#412)

1. (Nur wenn aktiviert) Die Anwendung erkennt ein neu angelegtes Issue in einem konfigurierten Repository.
2. Automatischer Start einer KI-Analyse: Übermittlung von mindestens Titel und Beschreibung an das konfigurierte Standard-KI-Plugin mit einem Prompt, der Analyse und Verbesserung der Anforderung anhand der definierten Qualitätskriterien anweist.
3. Bei erfolgreicher Analyse: Die ursprüngliche Issue-Beschreibung wird durch die optimierte Fassung ersetzt; der Titel bleibt unverändert.
4. Optional (separat konfigurierbar): Ein Kommentar am Issue dokumentiert die Analyseergebnisse.
5. Bei einem Fehler während der Analyse: Protokollierung des Fehlers; das Issue bleibt unverändert.

## Konfiguration

**Belegt:**

- Ausführungslimit in den Einstellungen: global und/oder je KI-Plugin (#410).
- Funktion „Issue-Aufbereitung" aktivier-/deaktivierbar (#412).
- Standard-KI-Plugin für die Issue-Aufbereitung konfigurierbar (#412).
- Repositories, in denen neu angelegte Issues überwacht werden, konfigurierbar (#412).
- Erstellung des Analyse-Kommentars separat aktivier-/deaktivierbar (#412).
- Bereits bestehend (Voraussetzung, keine neue Konfiguration): globales Standard-Plugin und projektbezogene Standard-Plugin-Zuordnung sowie Plugin-Aktivierung in den Einstellungen (#411 setzt deren Existenz voraus).

**Abgeleitet (nicht im Issue-Text spezifiziert):**

- Der Wertebereich der Limits (z. B. „leer/0 = unbegrenzt") ist nicht spezifiziert — siehe Offene Fragen.

## Akzeptanzkriterien

### Aus Issue #412 übernommen (belegt)

- Die Funktion kann aktiviert und deaktiviert werden.
- Ein Standard-KI-Plugin kann konfiguriert werden.
- Neu angelegte Issues werden automatisch analysiert.
- Titel und Beschreibung werden an das KI-Plugin übergeben.
- Die Analyse berücksichtigt die definierten Qualitätskriterien.
- Das KI-Plugin erzeugt eine optimierte Anforderungsbeschreibung.
- Die ursprüngliche Issue-Beschreibung wird durch die optimierte Fassung ersetzt.
- Der Issue-Titel bleibt unverändert.
- Die Analyse-Dokumentation kann optional als Kommentar gespeichert werden.
- Fehler während der Analyse werden protokolliert und führen nicht zur Änderung des Issues.

### Abgeleitete Kriterien für Issue #410 (als abgeleitet markiert)

- *[Abgeleitet]* Ein globales Ausführungslimit und je KI-Plugin ein eigenes Limit können in den Einstellungen gesetzt werden.
- *[Abgeleitet]* Bei Eingang eines SCM-Ereignisses und bei Beenden/Pausieren einer SCM-ausgeführten Aufgabe findet die Startprüfung statt.
- *[Abgeleitet]* Bei erreichtem (globalem oder pluginbezogenem) Limit wird keine neue Ausführung gestartet; das Ereignis bleibt zur späteren Ausführung vorgemerkt.
- *[Abgeleitet]* Bei freiem Slot wird das nächste Ereignis exakt in der definierten Prioritätenreihenfolge (pausierte/beendete Aufgaben → fällige Verzögerungs-Ereignisse zeitaufsteigend → sonstige in Eingangsreihenfolge) ausgewählt.
- *[Abgeleitet]* Bereits laufende CLI-Ausführungen werden durch die Begrenzung nicht unterbrochen (das Limit wirkt auf Starts, nicht auf laufende Prozesse).

### Abgeleitete Kriterien für Issue #411 (als abgeleitet markiert)

- *[Abgeleitet]* Bei deaktiviertem Projekt-Standard-KI-Plugin wird die SCM-Ereignisausführung mit dem globalen Standard-Plugin ausgeführt.
- *[Abgeleitet]* Ist auch das globale Standard-Plugin nicht verfügbar oder nicht aktiv, startet die Ausführung nicht (kein Fehlschlag-Start, keine Ausführung mit willkürlichem Ersatz-Plugin).

## Explizite Nicht-Ziele

- **Belegt:** Der Issue-Titel wird durch die Issue-Aufbereitung nicht verändert (#412).
- **Belegt:** Bei fehlgeschlagener/verweigerter Ausführung wird kein anderes als das in der Fallback-Kette definierte Plugin verwendet; die Ausführung startet dann schlicht nicht (#411).
- **Abgeleitet:** Keine Drosselung oder Unterbrechung bereits laufender CLI-Ausführungen — die Begrenzung betrifft nur den Start neuer Ausführungen (#410, folgt aus „Aufgaben, bei denen gerade eine CLI-Ausführung stattfindet, gezählt" + „Slot frei → nächstes Ereignis").
- **Abgeleitet:** Keine inhaltliche Änderung des SCM-Ereigniskanals, der Validierung oder des Ereignis-Formats selbst — die Issues betreffen Auswahl/Zeitpunkt der Ausführung und die Issue-Aufbereitung, nicht die Ereignisaufnahme (#410, #411).
- **Abgeleitet:** Keine manuellen Eingriffsmöglichkeiten in die Warteschlange (z. B. Neu-Sortierung durch den Anwender) — nicht gefordert (#410).
- **Abgeleitet:** Keine KI-Analyse bestehender (älterer) Issues und keine nachträgliche Aktualisierung bei Issue-Änderungen — der Trigger ist ausschließlich „neu angelegtes Issue" (#412).
- **Abgeleitet:** Kein Kommentar bei deaktivierter Kommentar-Option und keine sonstige Sichtbarmachung der Analyse am Issue — nur Beschreibungsersetzung plus optionaler Kommentar sind gefordert (#412).

## Offene Fragen

1. **#410 – Zusammenwirken der Limits:** Gelten globales und pluginbezogenes Limit gleichzeitig (d. h. ein Start erfordert freien Slot in beiden), oder ist die Konfiguration alternativ (entweder global oder pro Plugin)? Der Text („kann global sein oder für jedes KI-Plugin einzeln"; „ist das globale oder pluginbezogene Limit erreicht") ist hier nicht eindeutig. **Vorschlag:** Beide Limits können gleichzeitig aktiv sein; das restriktivere gewinnt (Start nur bei freiem Slot in beiden).
2. **#410 – Bezug des pluginbezogenen Limits:** Auf welches Plugin bezieht sich die Zählung — das der Aufgabe/des Ereignisses zugeordnete KI-Plugin? **Vorschlag:** Zählung je `KiPluginPrefix` der betroffenen Aufgaben (Annahme anhand bestehender Plugin-Zuordnung).
3. **#410 – Semantik von Priorität 1:** Was genau sind „Ereignisse für pausierte oder beendete Aufgaben" — Ereignisse, die eine pausierte Aufgabe fortsetzen, bzw. die zu einer beendeten Aufgabe gehören? Warum haben diese Vorrang? **Vorschlag:** Interpretation als Fortsetzungs-/Reaktivierungsereignisse bestehender Aufgaben, die vorrangig abgearbeitet werden sollen, damit angefangene Arbeit abgeschlossen wird, bevor neue Aufgaben Slots belegen.
4. **#410 – Verhalten ohne freien Slot:** Der Text sagt nur „gibt es nichts zu tun". Bleibt das Ereignis in der Warteschlange, bis die nächste Startprüfung (durch Beenden/Pausieren oder neuen Ereigniseingang) es erneut versucht? **Vorschlag:** Ja — Ereignisse verbleiben wartend; es gibt kein Verwerfen, da ein Verwerfen der Ereignissemantik widersprechen würde.
5. **#410 – Verzögerungszeit vs. bestehende `#verzögert`-Direktive:** Ist die in Priorität 2 genannte „Verzögerungszeit" der bereits implementierte `ScmDelayDirective`-/`NextAttemptUtc`-Mechanismus? **Vorschlag:** Ja, Wiederverwendung des vorhandenen Felds/Mechanismus (Annahme, kein neuer Verzögerungskanal).
6. **#410 – Konfiguration der Limits:** Welche Werte sind zulässig (Ganzzahl ≥ 1)? Bedeutet leer/0 „unbegrenzt" bzw. „deaktiviert"? Wo in den Einstellungen (globaler Bereich vs. Plugin-Detail)? **Vorschlag:** Ganzzahliges Limit ≥ 1; leer = unbegrenzt/deaktiviert; pluginbezogene Limits auf der jeweiligen Plugin-Konfigurationsfläche.
7. **#411 – Begriff „nicht verfügbar":** Umfasst das neben „nicht aktiviert" auch „nicht installiert/nicht ladbar/fehlende Berechtigung"? **Vorschlag:** Ja — jeder Zustand, in dem das Plugin nicht tatsächlich ausführbar ist, zählt als „nicht verfügbar".
8. **#411 – Ergebnis eines verweigerten Starts:** Was passiert mit dem SCM-Ereignis, wenn die Ausführung nicht starten darf — verbleibt es wartend (für einen erneuten Versuch nach Konfigurationsänderung), wird es mit Fehlerstatus abgeschlossen oder verworfen? Wird der Anwender informiert? **Vorschlag:** Ereignis mit ehrlichem Warte-/Fehlergrund versehen und protokollieren (konsistent zum bestehenden Wartegrund-Konzept), kein stilles Verwerfen.
9. **#412 – Ereignisquelle für „neu angelegtes Issue":** Erfolgt die Erkennung über den bestehenden SCM-Ereigniskanal (dann müsste dieser Issue-Erstellungsereignisse liefern) oder über einen separaten Polling-/Beobachtungsmechanismus? **Vorschlag:** An den bestehenden SCM-Ereignismechanismus andocken, sofern die SCM-Plugins Issue-Erstellung als Ereignis melden; andernfalls dedizierter Beobachtungsdienst — vor Umsetzung zu klären.
10. **#412 – „Konfigurierte Repositories":** Wo wird die Repository-Auswahl konfiguriert — global in den Einstellungen oder pro Projekt/Git-Repository? **Vorschlag:** Pro überwachtem Repository/Projekt (an die bestehende Repository-Konfiguration anknpüfend), mit globalem Funktionsschalter.
11. **#412 – Standard-KI-Plugin: dediziert oder geteilt?** Ist das konfigurierbare Standard-KI-Plugin der Issue-Aufbereitung eine eigene Einstellung oder dasselbe globale Standard-Plugin wie für Aufgaben? Gilt hier ebenfalls die Fallback-Kette aus #411? **Vorschlag:** Eigene Einstellung für die Aufbereitung mit Fallback auf das globale Standard-Plugin, analog zur #411-Kette.
12. **#412 – Einordnung der Analyse-Ausführung vs. #410-Limit:** Läuft die KI-Analyse als reguläre Aufgabe/CLI-Ausführung und unterliegt damit der Ausführungsbegrenzung aus #410? **Vorschlag:** Ja, sofern sie über denselben Ausführungspfad läuft — das Limit würde sonst durch die neue Funktion ausgehebelt.
13. **#412 – Schleifensicherung:** Die ersetzte Issue-Beschreibung ist selbst eine Issue-Änderung. Darf ein solches Änderungsereignis keine erneute Analyse auslösen? **Vorschlag:** Auslöser strikt auf das Erstellungsereignis begrenzen (bzw. von der Anwendung selbst verursachte Änderungen ignorieren); der Text fordert ohnehin nur „nach Erstellung".
14. **#412 – Prompt-Gestaltung:** Ist der Analyse-/Verbesserungs-Prompt fest in der Anwendung hinterlegt oder konfigurierbar (z. B. Prompt-Vorlage)? **Vorschlag:** Fest hinterlegt mit den im Issue genannten Kriterien; Konfigurierbarkeit ist nicht gefordert.
15. **#412 – Protokollierungsziel bei Fehlern:** Wohin werden Analysefehler protokolliert — Anwendungs-Log, Aufgaben-/Ereignisprotokoll, sichtbare Benachrichtigung? **Vorschlag:** Anwendungs-Log plus Protokolleintrag am zugehörigen Verarbeitungsvorgang; kein Kommentar am Issue (dieser ist der dokumentierenden Analyse vorbehalten).
