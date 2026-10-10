# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

Datum: 2026-10-10. Unabhängige abschließende Prüfung des aktuellen `plan.md` gegen `requirement.md`, `inventory.md`, beide Inventardetails sowie `plan-check.1.md` und `plan-check.2.md`.

Alle präzisierten Akzeptanzkriterien sind mit ausführbaren Umsetzungsschritten und konkreten Nachweisen abgedeckt. Die zuletzt offenen Befunde PC-06-R1 und PC-08-R1 sind auf Planungsebene geschlossen. Es verbleiben keine offenen Produktentscheidungen oder erforderlichen Planergänzungen. Der Plan kann für den Planungscommit und die anschließende Implementierung freigegeben werden.

Verbindliche Prüfgrundlage sind die bestätigten Entscheidungen: Polling lokal verknüpfter GitHub-Kontexte, case-insensitiver Trigger, vollständiger neuer Beschreibungstext ohne Empfangsreaktion, authentifizierter GitHub-Nutzer als hinreichende Owner-Identität, kurzer Warte-Kommentar und keine automatische Reaktivierung beendeter/archivierter Aufgaben. Diese Präzisierungen werden nicht erneut als Lücken der ursprünglichen Anforderung gewertet.

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| GitHub-Issue-/PR-Ereignisse mit Trigger nur vom berechtigten tatsächlichen Autor/Bearbeiter verarbeiten | Abschnitte 1–2: optionale Quellenverträge, stabile Identitäten, Autor-/Editorprüfung, konkrete REST-/GraphQL-Quellen, Versionsvergleich, Referenzzuordnung und dokumentierte Pollinggrenzen | Validation-/SourceTests; Abnahmematrix zu Fremdautor/-editor, fehlendem Editor, Trigger, Initialbeobachtung, neuen Referenzen, Kommentaränderung, Pagination und Replay; E1/E6 | Abgedeckt |
| Gültigen Kommentar mit 👀 bestätigen; Beschreibung gemäß Entscheidung ohne Empfangsreaktion | Abschnitt 3: getrennte OutgoingAction, Viewer-Reaktionsabgleich und wiederaufnehmbare Fehler; Beschreibung ausdrücklich ausgeschlossen | Source-/Orchestrator-/PersistenceTests und E1/E6; Wiederholung/Absturz ohne zusätzliche Reaktion | Abgedeckt |
| Erlaubte Aufgabe aktivieren und aktive Aufgabe aktiv lassen; bestätigte Sperren beachten | Abschnitt 4, Schritte 1 und 4: bestehende Statusübergänge, Pause-/Abschlussprüfungen und begrenzter Start-Rollback; Abschnitt 6: sichtbare Sperren | DispatchTests, E2/E4 einschließlich Neu→Gestartet, Pause, Beendet und Archiviert | Abgedeckt |
| Ereignis an bereits laufende CLI weitergeben | Abschnitt 4: konkrete Session-/WritePrompt-Schnittstelle, gemeinsamer Koordinator, Run-/Lebendigkeitsprüfung, Bereitschaft und FIFO; Abschnitt 5: geprüfter Kanal für bestehende Runs | Adapter-/DispatchTests, E2-Folgeereignis, E3-Bereitschaft und E6-Kanal; eingeschränkter Prozess für bestehende Sitzung | Abgedeckt |
| Ohne Session eine verfügbare Standard-CLI mit Kontext, Inhalt und Ursprungshinweis starten | Abschnitt 4: strikter Resolver mit Aufgaben-/Projekt-/Globalpräzedenz, Health-/Executable-Prüfung, neue/bestehende Arbeitsbereiche und sicherer Prompt; Abschnitt 5: Codex-/ClaudeCli-Zugriffsvertrag | Dispatch-/Adaptertests für Standardstart und fehlende/defekte Standards, neue/bestehende Klone und Ready; ScmEventCliExchangeAccessTests | Abgedeckt |
| Ohne Standard dauerhaften Queue-Eintrag und Wartehinweis erzeugen | Abschnitte 3–4: Inbox vor Seiteneffekten, kein beliebiger Ersatzstart, Outbox mit markergebundenem Warte-Kommentar | E1/E5, Source-/Orchestrator-/PersistenceTests; installiertes Plugin ohne Standard, Replay und Neustart ohne Duplikate | Abgedeckt |
| Queue öffnen, nötigenfalls aktivieren, CLI über Auswahl-Dialog starten und regulären Prompt senden | Abschnitt 6: konkrete WPF-Navigation, View/ViewModel/Commands, Dialog auch bei einem Plugin, Abbruch, Fehler, Reuse und Wiederaufnahme | E1–E5 einschließlich E2-Filter, E3-Bereitschaft und E3-Ungewissheit; E6-Wiederaufnahme/-Kanal; ergänzende ViewModeltests | Abgedeckt |
| KI wählt die Rückantwort im Ursprung; Programm erzeugt keine zusätzliche Abschlussreaktion | Abschnitt 5: strukturierte Kommentar-/Reaktions-/PR-Aktionen oder begründeter Abschluss ohne Remoteaktion, hostseitiger Credential-Zugriff, feste Ursprungsbindung und erreichbarer Austauschkanal | Exchange-/Provider-/Adaptertests, E6 und E6-Wiederaufnahme; beide realen Adapter plus eingeschränkter Prozess, keine Secrets/Steuerzeichen als Befehle | Abgedeckt |
| Ereignisprozess endet nach KI-Verarbeitung; Aufgabe bleibt bis zum expliziten Abschluss aktiv | Abschnitte 3 und 5: Zugestellt, InBearbeitung und Verarbeitet getrennt; Event-/Attempt-/Run-Korrelation, Abschluss erst nach bestätigten Aktionen, kein automatischer Neuversand bei Ungewissheit | Exchange-/DispatchTests, E2/E6 und E3-Ungewissheit; Flush ohne Annahme, Annahme ohne Abschluss, falsche/alte IDs, Timeout/Exit und verspätete gültige Meldung | Abgedeckt |
| Weitere SCM-Systeme können denselben Prozess verwenden | Abschnitt 1: optionale IScmEventSource-/IScmEventResponder-Fähigkeiten und providerneutrale Application-Pipeline; bestehende Anbieter ohne Fähigkeit bleiben verwendbar | Zweiter Fakeprovider, Provider ohne Ereignisfähigkeit, bestehende Datenbankmigration mit erhaltenen Aufgaben/Referenzen | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

Keine.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Hauptfenster → SCM-Ereignisse → tatsächlich aufgenommenes Ereignis → Detail/Ursprung → Rücknavigation; ohne Standard mit Bestätigung und Wartehinweis | E1 mit isolierter Fake-API, produktiver Aufnahme und ausgehendem Aufrufprotokoll | Abgedeckt |
| Neu-Aufgabe öffnen/aktivieren, Einzelplugin-Dialog abbrechen und erneut öffnen, Auswahl/Start, Zustellung und Abschluss verfolgen | E2 mit tatsächlichem WPF-Dialog, Arbeitsbereich und lebender Simulator-Session | Abgedeckt |
| Verarbeitete Ereignisse einblenden und zum wartenden Bestand zurückkehren | E2-Filter mit zwei tatsächlich aufgenommenen Ereignissen, sichtbarer Anzahl und unveränderten Nebenwirkungen | Abgedeckt |
| Startfehler wiederholen, Doppelklick/Pollrennen, inzwischen laufende Session verwenden und fehlende CLI anzeigen | E3 | Abgedeckt |
| Unbekannte Eingabebereitschaft für genau den sichtbaren Run bestätigen; Runwechsel und verweigerten Kanal beachten | E3-Bereitschaft mit sichtbarem Wartestatus, alter/neuer Run-ID und genau einer zulässigen Zustellung | Abgedeckt |
| Ungewisse Zustellung ohne Neuversand prüfen; Neuversand abbrechen/bestätigen und spätere Meldungen zuordnen | E3-Ungewissheit mit Hinweis auf Doppelbearbeitung, Attempt-Prüfung und ausbleibender automatischer Wiederholung | Abgedeckt |
| Beendete, archivierte oder pausierte Aufgabe öffnen; Sperre anzeigen und Pause regulär aufheben | E4 ohne automatische Reaktivierung | Abgedeckt |
| App mit wartendem Eingang schließen/neustarten und erhaltenen Eintrag anschließend verarbeiten | E5 mit derselben isolierten SQLite-Datenbank und ohne direkt eingefügte Inboxzeilen | Abgedeckt |
| Issue/PR jeweils als Kommentar/Beschreibung verarbeiten, KI-Antwort im Ursprung nachweisen und fehlenden Abschluss sichtbar halten | E6; Beschreibung vollständig, keine Empfangs- oder automatische Abschlussreaktion | Abgedeckt |
| Anmeldung/Schreibberechtigung wiederherstellen und über Erneut prüfen fortfahren, einschließlich Antwortfehler nach Zustellung | E6-Wiederaufnahme; vor Zustellung genau ein Prompt, nach Zustellung nur ausstehende Aktion desselben Attempts | Abgedeckt |
| Nicht verfügbaren CLI-Kanal anzeigen, korrigierte Rechte erneut prüfen beziehungsweise regulär mit geeigneter Konfiguration neu starten | E6-Kanal; kein Prompt an gesperrten alten Run, genau ein Prompt an frisch geprüften Run | Abgedeckt |
| Automatisches Routing zur laufenden oder zur konfigurierten Standard-CLI | Kein eigener UI-Auslöser; konkrete Dispatch-/Adapterintegrationstests und E2-Folgeereignis prüfen Routing und Folgen | Abgedeckt; eigener E2E für die reine Routingentscheidung nicht erforderlich |

## Fehlende oder unvollständige Planbestandteile

Keine.

## Hinweise

Die Vorgängerbefunde sind wie folgt geschlossen:

| Befund | Abschluss der Prüfung |
|--------|-----------------------|
| PC-01 | Quellen, Kommentararten, Autor-/Editorzuordnung, Versionen, Erstbeobachtung, neue Referenzen, Pagination und Erfassungsgrenzen stehen in Abschnitten 1–2 mit passenden Tests. |
| PC-02 | Abschnitt 3 plant atomare Aufnahme/Checkpoint, dauerhafte Inbox/Outbox, Claims/Leases, Aufgabenserialisierung, rekonstruierbare Nebenwirkungen, ungewisse Zustellung und Retry. SQLite-/Crash-/Parallelitätstests prüfen die konkret zugesagte Garantie. |
| PC-03 | Abschnitt 4 benennt den strikten Resolver, Präzedenz und Verfügbarkeitsprüfungen einschließlich verbotenen stillen Ersatzstarts; Gegenfälle stehen in der Matrix. |
| PC-04 | Konkreter vorhandener WritePrompt-Pfad, neuer/bestehender Arbeitsbereich, Rollback, manuelle Parallelstarts, Bereitschaft und WPF-Scopes sind geplant. Die unzutreffende Inventarvermutung zur fehlenden Eingabeschnittstelle ist ausdrücklich präzisiert. |
| PC-05 | Annahme, Zustellung und KI-Abschluss sind korreliert und getrennt; Flush/Exit/Leerlauf gelten nicht als Verarbeitung. Alte oder verspätete Meldungen und Ungewissheit sind mit Nachweisen berücksichtigt. |
| PC-06 und PC-06-R1 | Abschnitt 5 ersetzt den App-Daten-Pfad durch einen ausgeschlossenen Pfad im tatsächlichen CLI-Arbeitsbereich. Adapter erfassen und prüfen effektive Schreib-/Dateitool-Rechte für Codex und ClaudeCli, einschließlich Overrides/Deny/Unknown. Neue und bestehende Runs, Sperre vor Dispatch und konkrete Wiederaufnahme sind beschrieben. Reale Adapterverträge plus ein nachweislich eingeschränkter Windows-Prozess müssen Antwort und Abschluss bei gleichzeitig verweigertem App-Daten-Zugriff belegen. Ein gewöhnlich privilegierter Simulator reicht ausdrücklich nicht. |
| PC-07 | WPF-Navigation, Commands, Dialog auch bei einem Plugin, Abbruch/Fehler/Reuse und gesperrte Aufgaben sind konkret. Kein unbelegter Reaktivierungsablauf wird vorausgesetzt. |
| PC-08 und PC-08-R1 | Produktive Aufnahme im isolierten WPF-Prozess, Transport-Fake, Simulator, SQLite, konkrete Testdateien/Befehle und Abnahmematrix sind vorhanden. E2-Filter, E3-Bereitschaft, E3-Ungewissheit, E6-Wiederaufnahme und E6-Kanal schließen die zuvor fehlenden UI-Nachweise. Betroffene vorhandene Auswahl-/Start-/Pause-/Navigations- sowie Service-/Terminaltests sind benannt; der zusätzliche Integrationstestlauf erfasst bestehende EntwicklungsprozessServiceTests. |

Der Plan enthält die erforderlichen Fixtures, getrennte reguläre und OS-Interface-Testläufe sowie den vorgeschalteten vollständigen Build. Die genannten bestehenden Regressionsdateien wurden im Repository verifiziert. Neue Pflicht-E2Es und der eingeschränkte Kanalnachweis sind Freigabebedingungen; fehlende oder übersprungene Ausführung darf später nicht als bestanden gelten. Vorgeschriebene ConPTY-Skips ersetzen diese Nachweise nicht.

Diese Freigabe bewertet ausschließlich die Vollständigkeit des Plans. Es wurden keine Produktdateien geändert und keine Implementierungs-, Build- oder Testergebnisse vorweggenommen. Die geplante Auswertung effektiver CLI-Rechte und die Durchsetzung der Prozessbeschränkung bleiben in der Implementierung anhand der verbindlichen Tests nachzuweisen; ihre technische Umsetzung erfordert keine erneute Produktentscheidung.
