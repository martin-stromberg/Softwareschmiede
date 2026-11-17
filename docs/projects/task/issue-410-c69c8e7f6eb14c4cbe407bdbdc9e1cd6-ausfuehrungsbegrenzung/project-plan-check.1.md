# Planprüfung

## Ergebnis
**Status:** Projektplan lückenhaft

Der Plan bildet alle belegten Anforderungen der Issues #410, #411 und #412 sowie die
abgeleiteten Akzeptanzkriterien und Nicht-Ziele strukturell vollständig auf sechs
wohlgeformte Entwicklungsschritte ab. Zwei Punkte sind jedoch fachlich relevant und im
Plan nicht ausreichend festgelegt (Details unten): die Semantik der Priorität-1-Klasse
„beendete Aufgaben" gegenüber der permanenten Startsperre beendeter Aufgaben, und die
Verknüpfung der Aufbereitungs-Aufgabe mit `IssueReferenz.SourceKey` gegenüber dem
Unique-Index bzw. der Mehrdeutigkeitsauflösung des `ScmEventTaskResolver`. Beide können
bei naiver Umsetzung zu fehlerhaftem Verhalten führen und sind in keinem
Akzeptanzkriterium abgesichert.

## Abgleich Anforderung ↔ Entwicklungsschritte

| Anforderung | Abgedeckt durch Schritt(e) | Vollständig? |
|---|---|---|
| #410: Konfigurierbares Ausführungslimit, global und/oder je KI-Plugin, in den Einstellungen | 2 | Ja (Settings-UI globaler Wert + Plugin-Konfigurationsfläche, `AppEinstellung`-Persistenz, AK) |
| #410: Auslöser 1 — Prüfung bei Eingang eines SCM-Ereignisses | 2 | Ja (Eingang liegt zwangsläufig im Poll-/Aufnahmezyklus; Dispatch-Auswahl wird umgestellt) |
| #410: Auslöser 2 — Prüfung bei erkannter Beendigung/Pausierung einer SCM-ausgeführten Aufgabe | 3 | Ja (Andocken an `RunningCountChanged` + Pausen-Setzpfade; verifiziert: `IRunningAutomationStatusSource.RunningCountChanged` existiert, `KiAusfuehrungsService.cs:85`) |
| #410: Prüflogik — Zählung laufender CLI-Ausführungen, Limit erreicht → nichts zu tun | 2 | Ja (`KiAusfuehrungsService.GetRunningCount`/`GetLaufendeAufgabenIds` verifiziert, Z. 111-143; Pro-Plugin-Attribution über `Aufgabe.KiPluginPrefix` nötig, da `CliProcessHandle` kein `PluginPrefix` hat — verifiziert Z. 853-892) |
| #410: Prioritätenreihenfolge (pausierte/beendete Aufgaben → fällige Verzögerungs-Ereignisse zeitaufsteigend → Eingangsreihenfolge) | 2 | **Teilweise** — Reihenfolge ist als AK verankert, aber die Klasse „beendete Aufgaben" ist semantisch nicht aufgelöst (siehe Lücke 1) |
| #410 (abgeleitet): laufende Ausführungen werden nicht unterbrochen; Ereignis bleibt vorgemerkt | 2 | Ja (explizite AK) |
| #411: Fallback-Kette Projekt-/Aufgaben-Standard → globaler Standard bei nicht ausführbarem Plugin | 1 | Ja (Ist-Defekt verifiziert: `PluginSelectionService.cs:103-126` gibt deaktivierte Prefixe ungeprüft zurück; `ScmEventOrchestrator.cs:167` + Catch Z. 250-255 → `long.MaxValue`-Hartsperre) |
| #411: Kein Start / kein willkürliches Ersatz-Plugin, wenn auch globaler Default nicht verfügbar | 1 | Ja (AK: ehrlicher Wartegrund, wiederaufnehmbar, explizite Benutzerwahl bleibt streng) |
| #412: Funktion aktivier-/deaktivierbar | 6 (+ Konfig-Lesen in 5) | Ja (AK) |
| #412: Standard-KI-Plugin für die Funktion konfigurierbar | 6 | Ja (eigene Einstellung + Fallback analog #411, Vorschlag 11; schließt Lücke L7) |
| #412: Überwachte Repositories konfigurierbar | 5 (Flag) + 6 (Auswahl-UI) | Ja (`GitRepository`-Flag + Migration als AK; `GitRepository`/`Aktiv` verifiziert, `GitRepository.cs:22`) |
| #412: Neu angelegte Issues werden automatisch erkannt und analysiert | 5 (Erkennung) + 6 (Analyse) | Ja, mit Vorbehalt — die Aufgabenverknüpfung ist technisch unterspezifiziert (siehe Lücke 2) |
| #412: Titel + Beschreibung an KI-Plugin; Prompt mit definierten Qualitätskriterien; optimierte Anforderungsbeschreibung | 6 | Ja (alle zehn Kriterien im Schritttext) |
| #412: Beschreibungsersetzung remote; Titel unverändert | 4 (Schnittstelle) + 6 (Anwendung) | Ja (Lücke L5 korrekt erkannt: `ScmResponseKind { Comment, Reaction, PullRequest }`, `ScmEvents.cs:55`; `IScmEventResponder` mit `Execute`/`Reconcile` verifiziert) |
| #412: Analyse-Kommentar optional, separat schaltbar | 6 | Ja (AK; vorhandener `ScmResponseKind.Comment`-Outbox-Pfad) |
| #412: Fehler protokolliert, keine Issue-Änderung | 6 | Ja (AK: Log + Protokolleintrag, kein Fehler-Kommentar) |
| Nicht-Ziele: Titel unverändert; kein willkürliches Plugin; keine Unterbrechung laufender Ausführungen; keine Änderung am Ereigniskanal; keine Retro-Analyse; kein Kommentar bei deaktivierter Option; keine manuelle Queue-Eingriffe | 1, 2, 4, 5, 6 | Ja (alle explizit in Entscheidungen/AK verankert; interner Aufbereitungs-Event verändert den externen Trigger-Pfad nicht — Dispatch-Revalidierung toleriert „Kein Trigger" bereits, `ScmEventOrchestrator.cs:153`) |
| Betroffene Bereiche/Abläufe/Konfiguration aus requirement.md | 1–6 | Ja (SCM-Ereignisverarbeitung, Einstellungen, KI-Plugin-Verwaltung, SCM-/Repository-Anbindung inkl. Migrationen vollständig adressiert) |

## Abhängigkeitsprüfung

- **Zyklenfreiheit:** Alle Abhängigkeiten verweisen ausschließlich auf frühere Schritte
  (1→—, 2→1, 3→2, 4→—, 5→1, 6→1,2,4,5). Keine Zyklen.
- **2→1:** Fachlich stichhaltig — die aktivierungsbewusste Auflösung aus Schritt 1 muss
  vor der Pro-Plugin-Limitzählung liegen, damit das tatsächlich gestartete Plugin
  attributiert werden kann; vermeidet Reibung am selben Codepfad.
- **3→2:** Korrekt — Schritt 3 löst die in Schritt 2 gebaute Startprüfung erneut aus.
- **5→1:** Sinnvoll, wenngleich implizit: Ein in Schritt 5 eingereihtes internes Ereignis
  würde die bestehende Dispatch-Schleife sofort erreichen; ohne die
  aktivierungsbewusste Auflösung aus Schritt 1 könnte es in die alte
  `long.MaxValue`-Hartsperre laufen. Die Begründung steht nicht explizit im
  Abhängigkeitsfeld, ist aber aus dem Schritttext nachvollziehbar.
- **6→1,2,4,5:** Vollständig und notwendig (Fallback-Kette, Limit-Einbindung,
  Body-Update-Schnittstelle, Erkennung/Auftrag). Eine Abhängigkeit 6→3 ist nicht nötig
  (Schritt 3 ist generisch und wirkt ohnehin auf SCM-ausgeführte Aufgaben).
- **Keine vergessene implizite Abhängigkeit** festgestellt (insb. benötigt Schritt 5
  weder 2 noch 4: Erkennung/Einreihung sind davon funktional unabhängig).

## Fehlende oder unvollständige Punkte

1. **Priorität-1-Klasse „beendete Aufgaben" ist semantisch unaufgelöst (#410, Schritt 2).**
   `BlockReason` sperrt den Start für `AufgabeStatus.Beendet`/`Archiviert` **permanent**
   (`ScmEventOrchestrator.cs:531-533`), und es existiert kein Reaktivierungsmechanismus
   (der Resolver ordnet Issue-Ereignisse sogar statusunabhängig zu,
   `ScmEventTaskResolver.cs:63-68`). Der Plan spricht von „Fortsetzungs-/
   Reaktivierungsereignissen pausierter oder beendeter Aufgaben", legt aber zugleich
   fest, dass nur „startbare" Ereignisse priorisiert werden. Damit bleibt unklar, ob
   „beendet" den Aufgabenstatus (dann wäre Priorität 1 für diese Teilmenge unerreichbar
   bzw. ein Reaktivierungsmechanismus zu bauen, der nirgends geplant ist) oder den
   Ausführungszustand (`AusfuehrungsStatus.Beendet` bei weiterhin aktiver Aufgabe —
   dann konsistent startbar) meint. Als Lifecycle-Eingabe ist das eine fachlich
   relevante Entscheidung ohne verbindliche Festlegung; kein Akzeptanzkriterium sichert
   das Verhalten für ein Ereignis an einer `AufgabeStatus.Beendet`-Aufgabe ab.

2. **Verknüpfung der Aufbereitungs-Aufgabe über `IssueReferenz` kollidiert potenziell mit
   Unique-Index und Mehrdeutigkeitsauflösung (#412, Schritt 5).** `IssueReferenz.SourceKey`
   trägt einen Unique-Index (`SoftwareschmiededDbContext.cs:263-268`), und
   `ResolveIssueAsync` ordnet Trigger-Ereignisse jeder Aufgabe mit passender
   `IssueReferenz.IssueNummer` zu — bei zwei Treffern `Ambiguous`
   (`ScmEventTaskResolver.cs:63-68`). Der Plan sagt nur „Verknüpfung über IssueReferenz"
   und „Zweck-Markierung", legt aber nicht fest:
   - Trägt die Aufbereitungs-Aufgabe den kanonischen `SourceKey` (`IssueSourceKey`)?
     Dann wird sie faktisch die Stamm-Aufgabe des Issues — spätere reguläre
     Trigger-Ereignisse würden auf die Aufbereitungs-Aufgabe aufsetzen statt eine
     Arbeitsaufgabe anzulegen.
   - Oder trägt sie keinen/abweichenden `SourceKey` bei gesetzter `IssueNummer`? Dann
     kann später eine reguläre Aufgabe zum selben Issue entstehen, und der Resolver
     findet **zwei** Aufgaben mit derselben `IssueNummer` → `Ambiguous` → die reguläre
     Ereignisverarbeitung für dieses Issue wäre blockiert.
   Kein Akzeptanzkriterium deckt das Verhalten späterer Trigger-Ereignisse auf dasselbe
   Issue ab (z. B. „ein `$Softwareschmiede`-Kommentar am aufbereiteten Issue startet
   weiterhin regulär").

## Hinweise (optional)

- **Verifizierte Anker/Lückenbehauptungen (stichprobenartig, alle zutreffend):**
  Dispatch-Schleife ohne Limit/Priorisierung in `ScmEventMonitoringService.cs:229-248`
  (reine `Sequence`-Ordnung); `ResolveDevelopmentAutomationPluginWithProjectScopeAsync`
  prüft Aktivierung nicht (`PluginSelectionService.cs:103-126`); Strict-Auflösung wirft
  `InvalidOperationException` (Z. 88-94) → harte Sperre im Orchestrator (Z. 250-255);
  `CliProcessHandle` ohne `PluginPrefix` (`KiAusfuehrungsService.cs:853-892`);
  `ScmIncomingEvent.AufgabeId` nicht-nullbar (`ScmIncomingEvent.cs:16`);
  `ScmResponseKind` ohne Body-Update (`ScmEvents.cs:55`);
  `ScmEventObservation.CreatedAt`/`ContentKey` vorhanden (Z. 30-48) — stützt die
  Erkennungslogik von Schritt 5; `IScmEventResponder` mit `Reconcile`+`Execute`
  (`IScmEventResponder.cs`); `IssueReferenz.SourceKey`-Unique-Index
  (`SoftwareschmiededDbContext.cs:267`); `ScmDispatchFixture` existiert.
- Die Bewusst-sein-Begrenzung „Aufbereitung faktisch GitHub-only" (L9) ist ehrlich im
  Plan benannt und durch Schritt-5-AK („Repositories ohne ereignisfähiges SCM-Plugin
  lösen nichts aus") abgesichert.
- Pluginbezogene Limits: Der Plan kombiniert `AppEinstellung`-Persistenz mit Anzeige
  „auf der jeweiligen Plugin-Konfigurationsfläche" — zu beachten, dass der
  Plugin-Einstellbereich plugindeklarierte Felder (`PluginSettingEntry`) rendert; ein
  hostseitiges Limit ist als `AppEinstellung`-Schlüssel (Muster
  `plugins.sessionlimit.<prefix>`) näherliegend. Kein Muss-Fehler, aber
  präzisierungswürdig.
- Übergabetauglichkeit: Die Schrittbeschreibungen sind jeweils fachlich eigenständig
  verständlich (Ziel, Kontext, AK, Bereiche). Verweise auf „verbindliche Vorschläge"
  sind im Schritttext jeweils inhaltlich ausgeschrieben — kein externes Nachschlagen
  erforderlich.
