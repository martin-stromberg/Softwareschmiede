# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan lückenhaft

Datum: 2026-10-10. Erneute unabhängige Prüfung des überarbeiteten `plan.md` gegen `requirement.md`, `inventory.md`, beide Inventardetails und `plan-check.1.md`.

Die Überarbeitung schließt den überwiegenden Teil der acht früheren Befunde. Zwei abgegrenzte Ergänzungen bleiben: eine konkrete Zugriffsvoraussetzung des neuen CLI-Dateikanals und die E2E-Abdeckung der neu geplanten Wiederaufnahmeaktionen. Es ist keine erneute Entscheidung über die sechs bestätigten Produktregeln erforderlich.

Verbindliche Prüfgrundlage bleiben Polling lokal verknüpfter GitHub-Kontexte, case-insensitiver Trigger, vollständiger neuer Beschreibungstext ohne Empfangsreaktion, authentifizierter GitHub-Nutzer als hinreichende Owner-Identität, kurzer Warte-Kommentar und keine automatische Reaktivierung beendeter/archivierter Aufgaben.

Dies ist eine Vollständigkeitsprüfung des Plans. Produktcode wurde nicht geändert; Implementierungstests wurden nicht ausgeführt.

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| GitHub-Issue-/PR-Ereignisse nur mit Trigger und berechtigtem tatsächlichem Autor/Bearbeiter verarbeiten | Abschnitte 1–2: optionale Fähigkeiten, stabile Identitäten, case-insensitive Prüfung, GraphQL-Editor, Versionen und Quellen | Validation-/SourceTests; Matrix zu Fremdautor, Fremdeditor, Initialtrigger, Metadatenänderung, Pagination und Replay; E1/E6 | Abgedeckt |
| Gültigen Kommentar mit 👀 bestätigen; Beschreibung ohne Empfangsreaktion | Abschnitt 3: getrennte OutgoingAction, Viewer-Reaktionsabgleich, keine Reaktion für Beschreibungen | Source-/OrchestratorTests, E1/E6; Fehler-/Crashfälle in PersistenceTests | Abgedeckt |
| Erlaubte Aufgabe aktivieren, aktive Aufgabe aktiv lassen; bestätigte Sperren beachten | Abschnitt 4, Schritte 1 und 4; Abschnitt 6: Pause und Beendet/Archiviert bleiben erhalten | DispatchTests, E2/E4 | Abgedeckt |
| Ereignis an vorhandene lebende CLI weitergeben | Abschnitt 4: Session-Lookup, gemeinsamer Koordinator, Bereitschaftsadapter und sicherer Write-Pfad | Adapter-/DispatchTests, E2/E3; Exit-/Busy-/FIFO-Fälle | Abgedeckt für Zustellung; Rückkanalvoraussetzung siehe PC-06-R1 |
| Ohne Session Standard-CLI mit Kontext und Ursprungshinweis starten | Abschnitt 4: strikter Aufgaben-/Projekt-/Globalresolver, Verfügbarkeit, neuer/bestehender Klon, Start-Rollback | DispatchTests; E2 prüft den Auswahlstart; automatische Standardentscheidung auf Serviceebene | Abgedeckt für Start und Prompt; Rückkanalvoraussetzung siehe PC-06-R1 |
| Ohne Standard in dauerhafte Queue aufnehmen und Warte-Kommentar ausgeben | Abschnitte 3–4: Inbox, Outbox, fehlender/defekter Standard ohne Ersatzstart, Markerabgleich | E1/E5, Source-/Orchestrator-/PersistenceTests | Abgedeckt |
| Queue öffnen, nötigenfalls aktivieren, CLI im Dialog auswählen und Prompt senden | Abschnitt 6: Navigation, View/ViewModel, Dialog auch mit einem Plugin, Abbruch/Retry, Claims | E1–E5; neue Zusatzaktionen aus Abschnitt 6 noch ohne vollständige E2E-Zuordnung | Lücke: PC-08-R1 |
| KI entscheidet über Rückantwort im Ursprung; keine zusätzliche programmseitige Abschlussreaktion | Abschnitt 5: strukturierte KI-Aktionen, validiertes Ziel, Token nur beim Host | Exchange-/Provider-/Adaptertests, E6; Simulator nutzt produktiven Hostkanal, belegt aber nicht dessen Zugriff aus eingeschränkten realen CLI-Sitzungen | Lücke: PC-06-R1 |
| Ereignislauf erst nach KI-Verarbeitung beenden; Aufgabe bleibt bis explizitem Abschluss aktiv | Abschnitte 3 und 5: Zugestellt/Annahme/Abschluss getrennt, Event-/Attempt-/Run-Korrelation, Timeout ohne Neuversand | Exchange-/DispatchTests, E2/E6; kein Verarbeitet allein durch Flush | Abgedeckt im Protokoll; dessen Nutzbarkeit hängt an PC-06-R1 |
| Weitere SCM-Anbieter können dasselbe Prozessmodell verwenden | Abschnitt 1: optionale Source-/Responder-Fähigkeiten; gemeinsame Application-Pipeline | Zweiter Fakeprovider, bestehender Provider ohne Fähigkeit, Kompatibilitäts-/Migrationstests | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

- [ ] **PC-06-R1 – Dateikanal aus dem tatsächlichen CLI-Zugriffskontext nachweisen.** Zusätzlich zu Host-Dateitests und Simulator prüfen, wie die unterstützten Adapter für Codex und ClaudeCli den Austauschpfad lesen und schreiben dürfen. Ein neuer Start mit beschränktem Arbeitsbereich sowie eine bereits laufende Sitzung ohne Zugriff auf App-Daten müssen als Fälle benannt sein. Der positive Nachweis darf auf überprüften Start-/Adapterverträgen und einem entsprechend eingeschränkten Testprozess beruhen; Live-KI-Aufrufe oder Nutzercredentials sind nicht erforderlich. Negativfall: fehlender Zugriff verursacht einen verständlichen wiederaufnehmbaren Zustand und keinen erfolgreichen Ereignisabschluss. Ein Simulator mit gewöhnlichen Benutzerrechten allein belegt die zusätzliche CLI-Zugriffsvoraussetzung nicht.
- [ ] **PC-08-R1 – Die neuen sichtbaren Wiederaufnahmeaktionen durch konkrete E2E-Schritte ergänzen.** Erforderlich sind Setup, Nutzeraktion und sichtbares Ergebnis für: (a) unbekannte Eingabebereitschaft → für den sichtbaren Run bestätigen → genau eine Zustellung; Runwechsel vor Bestätigung verhindert Anwendung auf den neuen Run; (b) ungewisse Zustellung → Status erneut prüfen ohne Zusatzprompt → manuellen Neuversand abbrechen/bestätigen → bei Bestätigung genau eine neue AttemptId, keine automatische Wiederholung; (c) fehlende Berechtigung/Anmeldung → sichtbarer Sperrgrund → Testberechtigung wiederherstellen → Erneut prüfen → regulärer Fortgang ohne zusätzlichen Hinweis/Prompt. E3 prüft bisher Startfehler-Retry, E6 nur den sichtbaren Fehlerzustand; das deckt diese eigenständigen Commands nicht ab. Den Verarbeitet-Filter und dessen Rückkehr zum wartenden Bestand in einen bestehenden E2E aufnehmen. Szenarien können gemäß Repositoryvorgabe im bestehenden konsolidierten Test gebündelt werden.
- [ ] **PC-08-R1 – Betroffene Regressionen konkret zuordnen.** Der reguläre Gesamtlauf ist bereits konkret. Für den zusätzlich geforderten relevanten OS-Interface-Lauf die betroffenen bestehenden Szenarien benennen, insbesondere `E2E_PluginAuswahlUndWechsel`, `E2E_PluginProjectDefault`, `E2E_AufgabeStarten`, `E2E_AufgabePausieren` sowie die betroffenen Navigationsszenarien `E2E_TaskDetailNavigation`/`E2E_TaskWechselUeberMenue`. Die Änderungen an Start/Resolver/Eingabe betreffen außerdem die bestehenden `PluginSelectionServiceTests`, `EntwicklungsprozessServiceTests` samt Arbeitsbereichs-/Initialisierungsvarianten, `KiAusfuehrungsServiceTests`, `PseudoConsoleSessionTests_WritePromptAsync` und `SimulatedPseudoConsoleProcessLauncherTests`; deren Zuordnung zum regulären Lauf genügt. Vorgeschriebene ConPTY-Skips bleiben transparent zu dokumentieren und ersetzen keine neuen Pflicht-E2Es.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Hauptfenster → Queue → Detail/Ursprung → Rücknavigation; Eingang ohne Standard, Bestätigung, Wartehinweis | E1 | Abgedeckt |
| Neue Aufgabe aktivieren; Dialog mit Einzelplugin abbrechen, wieder öffnen, starten; Zustell-/Verarbeitungszustände verfolgen | E2 | Abgedeckt |
| Startfehler, Retry, Doppelklick/Pollrennen, inzwischen laufende CLI und keine CLI verfügbar | E3 | Abgedeckt |
| Beendete/archivierte/pausierte Aufgabe gesperrt; Pause regulär aufheben | E4 | Abgedeckt |
| App-Neustart mit echtem aufgenommenem wartendem Eintrag; anschließende Dialogverarbeitung | E5 | Abgedeckt |
| Issue/PR und Kommentar/Beschreibung; KI-Rückantwort an Ursprung; fehlende Berechtigung/Abschluss sichtbar | E6 | Abgedeckt für den simulierten Kanal; reale Zugriffsvoraussetzung separat nachzuweisen |
| Unbekannte Bereitschaft für einen bestimmten Run ausdrücklich bestätigen | Kein konkretes E1–E6-Szenario | Lücke: PC-08-R1 |
| Ungewisse Zustellung prüfen, Neuversand abbrechen und bestätigt auslösen | Kein konkretes E1–E6-Szenario | Lücke: PC-08-R1 |
| Nach behobener Anmeldung/Berechtigung über Erneut prüfen fortfahren | E6 endet beim Fehlerzustand | Lücke: PC-08-R1 |
| Verarbeitete Einträge einblenden und zum wartenden Bestand zurückkehren | Filter in Abschnitt 6, kein zugeordneter E2E-Schritt | Lücke: PC-08-R1 |
| Automatisches Routing zur schon laufenden beziehungsweise konfigurierten Standard-CLI | DispatchTests sowie Folgeereignis in E2 | Kein eigener UI-Auslöser; Serviceintegration ist für die Routingentscheidung angemessen |

## Fehlende oder unvollständige Planbestandteile

- [ ] **PC-06-R1 – P1: Zugriff auf den neuen Antwort-/Abschlusskanal ist für reale CLIs nicht konkret hergestellt.** Bezug: `plan.md` Zeilen 78 und 85–93. Das private Austauschverzeichnis liegt ausdrücklich unter App-Daten außerhalb des Repositorys. Der Plan erklärt, dass neue und bereits laufende CLIs denselben Pfad mit Dateitools ohne Änderung ihrer Umgebung oder Neustart nutzen. Die vorhandenen Startpfade setzen jedoch das Arbeitsverzeichnis auf den lokalen Klon und übernehmen frei konfigurierte CLI-Parameter; sie etablieren keinen Vertrag über eine weitere erlaubte Schreibwurzel (`CodexPlugin.cs`, `BuildProcessStartInfo`, Zeilen 105–125; `ClaudeCliPlugin.cs`, Zeilen 129–149). Host-Schreibbarkeit und Benutzer-ACL belegen nicht, dass Dateitools der gestarteten CLI dort schreiben dürfen. Damit können gerade die zusätzlich eingeführten Pflichtmeldungen accepted/actions/complete unerreichbar bleiben, obwohl Auswahl, Healthcheck, Ready und Promptversand erfolgreich waren. Der Annahme-Timeout beschreibt die Folge, stellt den regulären Rückantwortkanal aber nicht her.

  **Erforderliche Ergänzung:** Pro unterstütztem CLI-Adapter die Zugriffsvoraussetzung und ihre konkrete Herstellung beziehungsweise Prüfung festlegen. Möglich sind etwa ein ausdrücklich zugänglicher, nicht versionierter Austauschort oder eine begrenzte Einbindung des Austauschverzeichnisses in die unterstützte Startkonfiguration. Für bereits laufende Sitzungen den erreichbaren Pfad beziehungsweise einen tatsächlich ausführbaren Wiederaufnahmeweg beschreiben; fehlende Fähigkeit nicht pauschal erst als erfolgreiche Zustellung mit späterem Timeout behandeln. Bestehende Schutzregeln und Nutzereinstellungen nicht pauschal abschalten. Die konkrete Wahl ist eine Integrationsentscheidung und erfordert keine erneute Freigabe der sechs Produktregeln. Der Plan muss mindestens einen funktionsfähigen Normalpfad für die ausdrücklich unterstützten realen CLIs einschließlich Antwort und Abschluss benennen.

- [ ] **PC-08-R1 – P2: Testplan schließt die neu hinzugefügten Benutzeraktionen noch nicht ein.** Bezug: `plan.md` Zeilen 66, 78, 99, 107 und 132–176. Die Commands selbst sind als Umsetzungsaufgaben ausreichend benannt. Für Bereitschaftsbestätigung, Statusprüfung/Neuversand und Wiederaufnahme nach Anmeldung/Berechtigung fehlen jedoch die konkreten UI-Flüsse in den verbindlichen E1–E6-Szenarien; der Verarbeitet-Filter ist ebenfalls nicht zugeordnet. Unit-/Integrationseinträge „Wiederaufnahme“ und „UI-Grund“ reichen für diese Nutzeraktionen nicht als E2E-Nachweis. Die oben genannten Szenarien und Regressionen in den verbindlichen Testplan aufnehmen und der Freigabebedingung zuordnen.

## Hinweise

Der Stand der früheren Befunde ist nachvollziehbar verbessert:

| Früherer Befund | Ergebnis der erneuten Prüfung |
|---|---|
| PC-01 | Auf Planungsebene geschlossen: Quellen/Kommentararten, Pagination, Erstbeobachtung, Editor, Textversionen und Pollinggrenze konkret. |
| PC-02 | Auf Planungsebene geschlossen: Inbox vor Seiteneffekten, atomare Aufnahme/Checkpoint, Leases, Outbox-Abgleich, ungewisse Zustellung, Retry und Crashnachweise konkret. |
| PC-03 | Auf Planungsebene geschlossen: konkreter Resolver, Präzedenz, Aktivierung/Health/Executable und kein stiller Ersatzstart. |
| PC-04 | Auf Planungsebene geschlossen: vorhandener WritePrompt-Pfad präzisiert, Arbeitsbereich/Start/Rollback/Ready/Host-Scopes beschrieben. Neue Kanalvoraussetzung separat unter PC-06-R1. |
| PC-05 | Auf Planungsebene geschlossen: korreliertes Annahme-/Abschlussprotokoll und klare Trennung vom Byte-Flush; Nutzbarkeit des Kanals siehe PC-06-R1. |
| PC-06 | Teilweise geschlossen: sicherer Hosttransport, strukturierte Aktionen, Secretgrenze und Ursprungsbindung konkret; reale CLI-Zugriffsvoraussetzung bleibt PC-06-R1. |
| PC-07 | Auf Planungsebene geschlossen: konkrete WPF-Navigation/Commands/Dialog-/Fehlerpfade und ehrliche Sperre ohne unbelegten Reaktivierungsweg. |
| PC-08 | Weitgehend geschlossen: isolierter API-Fake im WPF-Prozess, produktive Pipeline, Persistenz, Prompt-/Aktionsprotokoll, Befehle und Matrix. Ergänzende UI-Aktionen/Regressionen bleiben PC-08-R1. |

Die GitHub-Grundannahmen wurden zusätzlich mit Primärdokumentation abgeglichen: `PullRequestReview` und `PullRequestReviewComment` unterstützen den vorgesehenen GraphQL-Kommentar-/Reaktionskontext; es ergibt sich daraus kein weiterer Planbefund. Siehe [GitHub GraphQL Pull requests](https://docs.github.com/en/graphql/reference/pulls#pullrequestreview) und [GitHub GraphQL Reactable](https://docs.github.com/en/graphql/reference/reactions#reactable).

Nach Ergänzung von PC-06-R1 und PC-08-R1 ist die unabhängige Planprüfung erneut auszuführen. Weitere Produktfragen oder Implementierungscode sind zum Schließen dieser beiden Planbefunde nicht erforderlich.
