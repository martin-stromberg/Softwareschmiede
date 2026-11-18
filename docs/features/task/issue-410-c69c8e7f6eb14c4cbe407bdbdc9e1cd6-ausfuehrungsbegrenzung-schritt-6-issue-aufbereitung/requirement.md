# Anforderung (übersetzt) – Entwicklungsschritt 6: Issue-Aufbereitung ausführen

Quelle: Projektplan `docs/projects/task/issue-410-…-ausfuehrungsbegrenzung/project-plan.md`
Schritt 6 (GitHub-Issue **#412**, Teil 2) plus die dort referenzierte
Gesamtakzeptanz von #412. Die Vorgehensentscheidungen 4 und 5 des Projektplans
sind verbindlich.

## Ausgangslage

Schritt 5 hat die „Issue erstellt"-Erkennung und den internen, trigger-freien
Aufbereitungsauftrag geliefert: Pro neu angelegtem Issue in einem freigeschalteten
Repository entsteht eine `Zweck = "IssueAufbereitung"`-markierte Aufgabe mit
`IssueReferenz` im eigenen SourceKey-Namensraum sowie ein internes
`ScmIncomingEvent` (Zweck markiert, `EventKey`-dedupliziert). Das Ereignis ist
bislang geparkt (`NextAttemptUtc = long.MaxValue`, Guard im Orchestrator).

Persistierte Einstellungsschlüssel existieren bereits:
`scm.issueaufbereitung.enabled` (opt-in, Standard `false`),
`scm.issueaufbereitung.plugin` (leer = Fallback auf globales Standard-Plugin),
`scm.issueaufbereitung.comment.enabled` (Standard `true`), sowie das
Repository-Flag `GitRepository.IssueAufbereitungAktiv` — ohne Oberfläche.

## Fachliches Ziel

Ein eingereihter Aufbereitungsauftrag startet automatisch eine KI-Analyse als
reguläre, in die #410-Ausführungsbegrenzung einzählende CLI-Ausführung seiner
Aufgabe (das geparkte interne Ereignis wird dafür „entparkt"/ausführbar gemacht).

1. **Analyse-Ausführung:** Titel und Beschreibung des Issues werden mit einem
   fest hinterlegten Analyse-/Verbesserungs-Prompt an das konfigurierte
   Funktions-Plugin übergeben — Fallback auf das globale Standard-Plugin analog
   der #411-Kette. Qualitätskriterien des Prompts: Verständlichkeit, sprachliche
   Qualität, Vollständigkeit, fehlende Informationen, offene Fragen, technische
   und fachliche Risiken, Zielkonflikte, alternative Lösungsansätze, mögliche
   Fehlinterpretationen. Ist kein ausführbares Plugin verfügbar, startet die
   Ausführung nicht und der Vorgang bleibt mit ehrlichem Wartegrund eingereiht.
2. **Ergebnisrücklauf:** Das Ergebnis (verbesserte Beschreibung, optionale
   Analyse-Dokumentation) kommt über den bestehenden Antwort-/Exchange-Kanal
   zurück; hostseitig werden daraus `IssueBodyUpdate`- und ggf. `Comment`-
   Outbox-Aktionen erzeugt (Schnittstelle aus Schritt 4).
3. **Ergebnisanwendung:** Bei erfolgreicher Analyse ersetzt die Anwendung die
   Issue-Beschreibung remote; der Titel bleibt unverändert. Ist der
   Kommentar-Schalter aktiv, dokumentiert zusätzlich ein Kommentar die
   Analyseergebnisse (Schwachstellen, offene Fragen, Lösungsansätze, Risiken,
   Zielkonflikte, Verbesserungsvorschläge); sonst entsteht kein Kommentar.
4. **Fehlerbehandlung:** Fehler während Analyse oder Rückschreibung werden im
   Anwendungs-Log und als Protokolleintrag am Vorgang festgehalten; das Issue
   bleibt unverändert; kein Fehler-Kommentar am Issue.
5. **Einstellungen-Oberfläche:** Funktionsschalter, Plugin-Auswahl für die
   Aufbereitung, Repository-Auswahl (überwachte Repositories) und
   Kommentar-Schalter — alles persistiert und in den Einstellungen bedienbar.

## Akzeptanzkriterien (vollständige Issue-#412-Akzeptanz)

- [ ] Funktion aktivier-/deaktivierbar; Standard-KI-Plugin für die Aufbereitung
  konfigurierbar mit Fallback auf das globale Standard-Plugin; überwachte
  Repositories auswählbar; Kommentar-Erstellung separat schaltbar — alles
  persistiert und in den Einstellungen bedienbar.
- [ ] Neu angelegte Issues werden automatisch analysiert; Titel und Beschreibung
  gehen an das KI-Plugin; der Prompt adressiert die definierten
  Qualitätskriterien; das Plugin erzeugt eine optimierte Anforderungsbeschreibung.
- [ ] Ursprüngliche Issue-Beschreibung wird remote durch die optimierte Fassung
  ersetzt; Titel unverändert; bei aktivem Kommentar-Schalter dokumentierender
  Kommentar, sonst keiner.
- [ ] Die Analyse-Ausführung unterliegt der Ausführungsbegrenzung aus Schritt 2
  (zählt global und pro Plugin).
- [ ] Fehler werden protokolliert (Log + Protokolleintrag am Vorgang) und führen
  zu keiner Änderung des Issues.
- [ ] Unit-/Integrationstests für Prompt, Ausführungspfad, Ergebnisanwendung,
  Kommentar-Option und Fehlerfälle; `SettingsViewModel`-Tests; E2E-Abdeckung
  (FlaUI, konsolidiert minimal) des Benutzerflusses „Aufbereitung in
  Einstellungen konfigurieren".

## Scope-Klärung

**Verarbeitungseinheit:** Der interne `ScmIncomingEvent`-Aufbereitungsauftrag
(„Issue erstellt" aus Schritt 5) — eine KI-Analyse-Ausführung je Issue.

**Auslöser:** Regulärer Dispatch des eingereihten internen Ereignisses
(Poll-Zyklus / ereignisgesteuerte Startprüfung / manuelle Aktionen der
Ereignisübersicht wie „Erneut prüfen" oder „Öffnen").

**Wirkung:** CLI-Lauf der Aufbereitungs-Aufgabe → `IssueBodyUpdate` (+ optional
`Comment`) remote über die Outbox → Ereignis „Verarbeitet".

**Scope-Ausschlüsse (bestätigt durch verbindliche Projektplan-Annahmen):**

- Kein Ändern des Issue-Titels (explizites Nicht-Ziel).
- Kein Fehler-/Warte-Kommentar am Issue — Warte- und Fehlerzustände bleiben
  Anwendungs-intern sichtbar (Ereignisübersicht + Protokoll + Log). Abgeleitet
  aus Vorschlag 15 („kein Fehler-Kommentar am Issue"); Wartehinweis-Kommentare
  des generischen Pfads werden für Aufbereitungsaufträge nicht erzeugt.
- Kein neuer Beobachtungs-/Ausführungsmechanismus — Wiederverwendung von
  Dispatch, Aufgabenlease, Exchange-Kanal, Outbox und Ausführungsbegrenzung.
- Kein `IIssueTemplateTextGenerator`-Pfad (Einmal-Textgenerierung würde die
  #410-Begrenzung umgehen — verbindliche Entscheidung 4).
- Aufbereitung bleibt GitHub-only (Lücke L9 des Inventars — nur das
  GitHub-Plugin ist ereignisfähig).
- Issue-Erstellungen im Modus „Separates Konto" (Notifications-Inbox) erzeugen
  weiterhin keine Aufbereitungsaufträge (dokumentierte Schritt-5-Entscheidung).

## Offene Punkte

Keine — alle fachlichen Entscheidungen sind über die verbindlichen Annahmen des
Projektplans (Vorschläge 9–15) und die Schritt-4/5-Bausteine geklärt.
