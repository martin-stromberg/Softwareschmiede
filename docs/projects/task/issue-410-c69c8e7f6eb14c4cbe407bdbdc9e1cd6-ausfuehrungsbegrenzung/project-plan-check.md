# Planprüfung

## Ergebnis
**Status:** Projektplan vollständig

Beide in der Erstprüfung (`project-plan-check.1.md`) beanstandeten Lücken sind in der
überarbeiteten Fassung sauber geschlossen und die gewählten Festlegungen wurden gegen
den Codebestand verifiziert:

1. **Priorität-1-Semantik (#410, Entscheidung 2 / Schritt 2):** „beendet" ist jetzt
   verbindlich als `Aufgabe.AusfuehrungsStatus == Beendet` bei weiterhin aktivem
   `Aufgabe.Status` (`Neu`/`Gestartet`/`Wartend`) definiert, „pausiert" als gesetztes
   `PausiertBisUtc`; `AufgabeStatus.Beendet`/`Archiviert` ist explizit ausgenommen
   (dauerhafte `BlockReason`-Sperre, `ScmEventOrchestrator.cs:531-533`; kein
   Reaktivierungsmechanismus im Scope). Die Definition ist fachlich stimmig — sie
   deckt sich exakt mit dem Domain-Startbarkeitskriterium
   `DarfAusfuehrungStarten` (`AufgabeAusfuehrungsStatusExtensions.cs:7-9`: Start
   erlaubt bei `Status ≠ Beendet/Archiviert && AusfuehrungsStatus ∈
   {NichtGestartet, Beendet}`) — und durch Akzeptanzkriterium 4 von Schritt 2
   abgesichert (beide Richtungen: Fortsetzung wird priorisiert, abgeschlossene
   Aufgabe bleibt gesperrt).

2. **Isolation der Aufbereitungs-Aufgabe (#412, Entscheidung 4 / Schritt 5):** Die
   Kollisionsanalyse trifft den Code exakt — `ResolveIssueAsync` löst über
   `IssueReferenz.IssueNummer` auf, nicht über `SourceKey`, und liefert bei zwei
   Treffern `Ambiguous` (`ScmEventTaskResolver.cs:63-68`; statusunabhängige
   Abfrage); `FindActiveContextTaskIdsAsync` matcht den PR-Kontext ebenfalls über
   `IssueNummer` (Z. 132-144). Die geplante doppelte Gegenmaßnahme ist korrekt und
   vollständig: (a) markierte Aufbereitungs-Aufgaben werden in beiden Resolver-
   Abfragen per Zweck-Filter ausgeschlossen (verhindert Fehlzuordnung und
   `Ambiguous`-Sperren); (b) der `SourceKey` liegt im eigenen Namensraum
   `…/IssueAufbereitung/{number}` statt des kanonischen `…/Issue/{number}` aus
   `IssueSourceKey` (`ScmEventTaskResolver.cs:172-176`) — der Unique-Index
   (`SoftwareschmiededDbContext.cs:267`) kollidiert damit nicht mit der späteren
   Stamm-Aufgabe und sichert zugleich die Einmaligkeit der Aufbereitung je Issue.
   Akzeptanzkriterium 5 von Schritt 5 deckt das Verhalten späterer
   Trigger-Ereignisse auf dasselbe Issue ab.

Der volle Abgleich (siehe Tabelle) ist ohne verbleibende Teil-Abdeckung; die
Nachbesserungen haben keine neuen Inkonsistenzen erzeugt.

## Abgleich Anforderung ↔ Entwicklungsschritte

| Anforderung | Abgedeckt durch Schritt(e) | Vollständig? |
|---|---|---|
| #410: Konfigurierbares Ausführungslimit, global und/oder je KI-Plugin, in den Einstellungen | 2 | Ja (globaler Wert im Automatisierungs-Abschnitt, Plugin-Limits auf Plugin-Fläche, `AppEinstellung`-Persistenz, AK) |
| #410: Auslöser 1 — Prüfung bei Eingang eines SCM-Ereignisses | 2 | Ja (Eingang liegt im Poll-/Aufnahmezyklus; Dispatch-Auswahl wird auf priorisierte, slotbegrenzte Auswahl umgestellt) |
| #410: Auslöser 2 — Prüfung bei erkannter Beendigung/Pausierung einer SCM-ausgeführten Aufgabe | 3 | Ja (Andocken an `RunningCountChanged`/`IRunningAutomationStatusSource` + Pausen-Setzpfade; kein neuer Poller, Poll bleibt Fallback) |
| #410: Prüflogik — Zählung laufender CLI-Ausführungen, Limit erreicht → nichts zu tun, Ereignis bleibt vorgemerkt | 2 | Ja (`KiAusfuehrungsService.GetRunningCount`/`GetLaufendeAufgabenIds`; Pro-Plugin-Attribution über `Aufgabe.KiPluginPrefix`/`ScmPromptSessionAdapter.PluginPrefix`, da `CliProcessHandle` kein Prefix trägt; ehrlicher Wartegrund, kein Verwerfen) |
| #410: Prioritätenreihenfolge (pausierte/beendete Aufgaben → fällige Verzögerungs-Ereignisse zeitaufsteigend → Eingangsreihenfolge) | 2 | **Ja** — Klasse 1 ist jetzt verbindlich definiert (`PausiertBisUtc` gesetzt bzw. `AusfuehrungsStatus.Beendet` bei `Status` `Neu`/`Gestartet`/`Wartend`; explizit nicht `AufgabeStatus.Beendet`/`Archiviert`), Klasse 2 nutzt den bestehenden `ScmDelayDirective`/`NextAttemptUtc`-Mechanismus, Klasse 3 `Sequence`; AK 3+4 sichern Reihenfolge und Semantik ab |
| #410 (abgeleitet): laufende Ausführungen werden nicht unterbrochen; Limit wirkt nur auf Starts; Hostaktionen/Reconcile unbegrenzt | 2 | Ja (explizite AK; Startfreigabe unmittelbar vor CLI-Start, auch für UI-ausgelöste Starts) |
| #411: Fallback-Kette — deaktiviertes/nicht verfügbares Projekt-/Aufgaben-Standard → globaler Standard | 1 | Ja (Ist-Defekt korrekt benannt: `ResolveDevelopmentAutomationPluginWithProjectScopeAsync` prüft Aktivierung nicht, `PluginSelectionService.cs:103-126`; Strict-Auflösung wirft `InvalidOperationException` → `long.MaxValue`-Hartsperre, `ScmEventOrchestrator.cs:167, 250-253`) |
| #411: Kein Start / kein willkürliches Ersatz-Plugin, wenn auch globaler Default nicht verfügbar; „nicht verfügbar" umfasst deaktiviert/nicht installiert/nicht ladbar | 1 | Ja (AK: ehrlicher, wiederaufnehmbarer Wartegrund statt Hartsperre; explizite Benutzerwahl bleibt streng) |
| #412: Funktion aktivier-/deaktivierbar | 6 (+ Konfig-Lesen in 5) | Ja (AK) |
| #412: Standard-KI-Plugin für die Funktion konfigurierbar | 6 | Ja (eigene Einstellung + Fallback analog #411; Wiederverwendung der aktivierungsbewussten Auflösung aus Schritt 1) |
| #412: Überwachte Repositories konfigurierbar | 5 (Flag + Migration) + 6 (Auswahl-UI) | Ja (`GitRepository`-Flag als EF-Migration als AK) |
| #412: Neu angelegte Issues werden automatisch erkannt und analysiert | 5 (Erkennung) + 6 (Analyse) | Ja — Baseline/Retro-Schutz, `CreatedAt`-Vergleich, Dedup über `EventKey` + `SourceKey`-Namensraum; **Lücke 2 geschlossen** (Isolations-AK) |
| #412: Titel + Beschreibung an KI-Plugin; Prompt mit definierten Qualitätskriterien; optimierte Anforderungsbeschreibung | 6 | Ja (alle zehn Kriterien im Schritttext; Prompt fest hinterlegt, Vorschlag 14) |
| #412: Beschreibungsersetzung remote; Titel unverändert | 4 (Schnittstelle) + 6 (Anwendung) | Ja (Lücke L5 korrekt: kein Body-Update in `ScmResponseKind`/`IScmEventResponder` vorhanden → neue Antwort-Art inkl. Reconcile über Outbox mit deterministischer ID; GitHub `PATCH issues/{n}`) |
| #412: Analyse-Kommentar optional, separat schaltbar | 6 | Ja (AK; vorhandener `ScmResponseKind.Comment`-Outbox-Pfad) |
| #412: Fehler protokolliert, keine Issue-Änderung | 6 | Ja (AK: Anwendungs-Log + Protokolleintrag am Vorgang, kein Fehler-Kommentar) |
| Nicht-Ziele: Titel unverändert; kein willkürliches Plugin; keine Unterbrechung laufender Ausführungen; keine Änderung am Ereigniskanal; keine Retro-Analyse; kein Kommentar bei deaktivierter Option; keine manuellen Queue-Eingriffe; kein Reaktivierungsmechanismus für abgeschlossene Aufgaben | 1, 2, 4, 5, 6 | Ja (alle explizit in Entscheidungen/AK verankert; internes trigger-freies Ereignis verändert den externen Trigger-Pfad nicht — Dispatch-Revalidierung toleriert „Kein Trigger"/`KeineErwaehnung` bereits, `ScmEventOrchestrator.cs:153`; für `Description`-Kind entfällt die Receipt-Reaktion, Z. 95-102) |
| Betroffene Bereiche/Abläufe/Konfiguration aus requirement.md | 1–6 | Ja (SCM-Ereignisverarbeitung, Einstellungen, KI-Plugin-Verwaltung, SCM-/Repository-Anbindung inkl. EF-Migrationen vollständig adressiert; GitHub-only-Begrenzung ehrlich benannt und per AK abgesichert) |

## Abhängigkeitsprüfung

- **Zyklenfreiheit:** Alle Abhängigkeiten verweisen ausschließlich auf frühere
  Schritte (1→—, 2→1, 3→2, 4→—, 5→1, 6→1,2,4,5). Keine Zyklen.
- **2→1:** Stichhaltig — die aktivierungsbewusste Auflösung aus Schritt 1 muss vor
  der Pro-Plugin-Limitzählung liegen, damit das tatsächlich gestartete Plugin
  (`ScmPromptSessionAdapter.PluginPrefix`) attributiert werden kann.
- **3→2:** Korrekt — Schritt 3 löst die in Schritt 2 gebaute Startprüfung erneut aus.
- **5→1:** Weiterhin sinnvoll und notwendig (verhindert, dass intern eingereihte
  Aufbereitungs-Ereignisse in die alte `long.MaxValue`-Hartsperre laufen). Die
  Nachbesserungen in Schritt 5 (Resolver-Filter, eigener SourceKey-Namensraum)
  erzeugen **keine** neue Abhängigkeit — `ScmEventTaskResolver` wird von keinem
  anderen Schritt angefasst.
- **6→1,2,4,5:** Vollständig und notwendig (Fallback-Kette, Limit-Einbindung,
  Body-Update-Schnittstelle, Erkennung/Auftrag). Keine Abhängigkeit 6→3 nötig —
  Schritt 3 ist ein generischer Auslöser und wirkt auf SCM-ausgeführte Aufgaben
  ohnehin.
- **Keine vergessene implizite Abhängigkeit:** Schritt 5 benötigt weder 2 noch 4
  (Erkennung/Einreihung sind davon funktional unabhängig); Schritt 5 und 2
  berühren zwar beide `ScmEventMonitoringService`, jedoch disjunkte Abschnitte
  (Beobachtungspfad vs. Dispatch-Auswahl).

## Fehlende oder unvollständige Punkte

Keine. Beide Lücken der Erstprüfung sind fachlich sauber und code-verifiziert
geschlossen; der Abgleich ist vollständig, die Abhängigkeiten konsistent, und jede
Schrittbeschreibung ist lifecycle-tauglich (eigenständig verständlich: Ziel,
Kontext, Akzeptanzkriterien, Abhängigkeiten, betroffene Bereiche).

## Hinweise (optional)

- **`PausiertBisUtc`-Formulierung in Entscheidung 2/Schritt 2:** Der Plan
  definiert „pausiert" als „`PausiertBisUtc` gesetzt". Die Entity-Dokumentation
  präzisiert: ein vergangener Zeitpunkt bedeutet „nicht pausiert"
  (`Aufgabe.cs:97-103`), und `SetPauseAsync(null)` hebt die Pause explizit auf
  (`AufgabeService.cs:853-892`). Bei der Umsetzung ist daher festzulegen, ob die
  Klasse-1-Erkennung `PausiertBisUtc != null` (einmal pausiert → Fortsetzungs-
  charakter, deckt sich mit der Plan-Intention) oder `> jetzt` (aktuell pausiert)
  prüft. Beide Lesarten sind vertretbar; die erste passt besser zur
  Fortsetzungs-Semantik, da eine abgelaufene Pause das Ereignis sonst
  unvermittelt in Klasse 3 fallen ließe. Kein Muss-Fehler — die fachliche
  Festlegung ist jetzt vorhanden.
- **Aufbereitungs-Aufgabe erbt Aufgaben-Machinerie (implizit, unkritisch):**
  Der Dispatch benötigt `GitRepositoryId` (`ScmEventOrchestrator.cs:138`,
  `task.GitRepositoryId!.Value`) und startet bei leerem `LokalerKlonPfad` den
  regulären Prozesspfad inkl. Klon (`ProzessStartenUndCliStartenAsync`,
  Z. 170-172). Zudem nimmt `ScmTaskCompletionMonitoringService` (pro Aufgabe,
  `ScmTaskCompletionMonitoringService.cs:52-63, 198-212`) die Aufbereitungs-
  Aufgabe ins Abschlussmonitoring auf — per Aufgabe, ohne Mehrdeutigkeit, daher
  keine Kollision; das Auto-Beenden bei geschlossenem Issue ist vermutlich sogar
  erwünscht. Die Zweck-Markierung sollte bei Umsetzung auch das
  Ausführungsprofil steuern (Analyse-Prompt, ggf. kein Klon/Branch nötig).
- **Weitere `IssueNummer`-Konsumenten sind unkritisch:** Neben den beiden im
  Plan benannten Resolver-Abfragen nutzen `ScmTaskCompletionMonitoringService`,
  `EntwicklungsprozessService` und `AufgabeService` `IssueReferenz.IssueNummer`
  ausschließlich aufgabenbezogen (keine Referenz→Aufgabe-Mehrdeutigkeit). Die
  benannte Isolationsfläche ist damit vollständig.
- **Pluginbezogene Limit-UI:** Der Plan kombiniert `AppEinstellung`-Persistenz
  mit Anzeige „auf der jeweiligen Plugin-Konfigurationsfläche". Der
  Plugin-Einstellbereich rendert plugindeklarierte Felder
  (`PluginSettingEntry`); ein hostseitiges Limit als `AppEinstellung`-Schlüssel
  (Muster `plugins.sessionlimit.<prefix>`) ist näherliegend. Präzisierungs-
  hinweis, kein Muss (aus Erstprüfung übernommen, weiterhin gültig).
- **`SourceKey`-Länge:** `IssueReferenz.SourceKey` ist auf 700 Zeichen begrenzt
  (`SoftwareschmiededDbContext.cs:266`); der `IssueAufbereitung`-Namensraum ist
  davon weit entfernt — unproblematisch.
- **Verifizierte Anker/Lückenbehauptungen dieser Prüfung (alle zutreffend):**
  `AktivenLaufBeendenAsync` setzt `AusfuehrungsStatus.Beendet`
  (`AufgabeService.cs:1044-1066`, aufgerufen aus `CliProcessManager` bei
  Prozessende); `SetPauseAsync` erlaubt Pausen nur bei `Neu`/`Gestartet`/`Wartend`
  (`AufgabeService.cs:865`); `IssueReferenz.AufgabeId` unique (eine Referenz je
  Aufgabe, `SoftwareschmiededDbContext.cs:255`); `ScmEventTaskResolver.cs:63-68,
  132-144, 172-176`; `BlockReason` `ScmEventOrchestrator.cs:531-533`;
  `IssueReferenz.SourceKey`-Doc bestätigt kanonisches Format
  (`IssueReferenz.cs:30-32`).
