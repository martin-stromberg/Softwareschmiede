# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

Zweite Prüfung nach Korrektur. Die im ersten Check (`plan-check.1.md`) gemeldete Lücke — nicht ausführbarer E2E-Schritt „reaktivieren" bei per Sichtbarkeitsregel ausgeblendeter Checkbox — ist sauber geschlossen: Die E2E-Schrittfolge startet nach dem Deaktivieren bei Anzahl 0 nun die CLI erneut (Anzahl > 0 → Checkbox wieder sichtbar), bevor reaktiviert wird. Zusätzlich dokumentiert eine neue Designentscheidung („Reaktivierung bei ausgeblendeter Checkbox", `plan.md` Z. 39) explizit, dass eine Re-Aktivierung bei Anzahl 0 fachlich nicht vorgesehen ist, und Akzeptanzszenario 3 (`plan.md` Z. 323) spiegelt dieselbe Regel wider. Die Sichtbarkeitsregel `AutoShutdownUiSichtbar` blieb bewusst unverändert — konsistent mit dem Anforderungswortlaut („nur, wenn es solche gibt").

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Anzahl laufender Automatisierungen am unteren Seitenleisten-Rand anzeigen; „laufend" = `KiAusfuehrungsZustand.Laeuft`, „Wartet" zählt nicht | `LaufendeAutomatisierungenZaehlung` (Application-Layer, klassifiziert `GetAktiveAufgabenAsync`-Kandidaten) + `LaufendeAutomatisierungenAnzahl` im VM aus `AktiveAufgabenListe`; `TextBlock` `AutoShutdownLaufendeAnzahl`; Klassifikation wird dafür in den Shared-Layer verschoben (Schritte 2, 6, 9, 10) | `ZaehleAsync_KlassifiziertAlleZustaende`, `LaufendeAnzahlUndSichtbarkeiten`, E2E-Teilschritt „Fußzeile zeigt 1 laufend" | Abgedeckt |
| Aktivierungs-Checkbox nur bei laufenden Automatisierungen sichtbar/aktivierbar | `AutoShutdownUiSichtbar` = Anzahl > 0 ∨ aktiviert ∨ Countdown aktiv (begründete Erweiterung für Deaktivierbarkeit im Countdown — Offene Punkte 7) | `LaufendeAnzahlUndSichtbarkeiten`, E2E-Teilschritte „Checkbox erscheint" / „Checkbox bei Anzahl 0 ausgeblendet" | Abgedeckt |
| Bei aktivierter Checkbox: Toleranzzeit-Eingabefeld (Minuten) + Force-Checkbox „Herunterfahren erzwingen" | `AutoShutdownOptionenSichtbar` = `AutoShutdownAktiviert`; XAML: `AutoShutdownToleranzMinuten`-TextBox + `AutoShutdownErzwingen`-CheckBox | `LaufendeAnzahlUndSichtbarkeiten`, `ToleranzValidierung`, E2E-Teilschritt | Abgedeckt |
| Sichtbarer Countdown über Toleranzzeit, sobald keine CLI-Ausführung mehr aktiv | Orchestrator armiert `CountdownEndeUtc` bei `enabled && Anzahl == 0`; VM zeigt `AutoShutdownCountdownText` „Herunterfahren in mm:ss" mit 1-s-`DispatcherTimer` | `CountdownStartet_WennAnzahlAufNullFaellt`, `ShutdownErstNachToleranzablauf`, `DeaktivierenRuftSetEnabled` (Countdown-Text), E2E-Teilschritt | Abgedeckt |
| Jeder Statuswechsel während Countdown → Abbruch + Neuauswertung | Trigger `RunningCountChanged`, `LaufdatenChanged`, 5-s-Sicherheitstick → koaleszierte `EvaluiereAsync`; Anzahl > 0 → Disarmierung. Dokumentierte Abweichung: bei unveränderter Anzahl 0 bleibt der Endzeitpunkt stehen (Verhungern-Schutz gegen ~30-s-Heartbeat-Events) | `CountdownAbbruchBeiWiederanlauf`, `CountdownBleibtBeiUnveraenderterAnzahl`, E2E-Teilschritt CLI-Neustart | Abgedeckt |
| Countdown-Ende → OS-Shutdown (optional erzwungen) | `OnCountdownAbgelaufenAsync` mit Final-Recheck → `RequestShutdownAsync(erzwingen)`; Windows-Mapping `/s /f /t 0` vs. `/s /t 0` | `ShutdownErstNachToleranzablauf`, `FinalRecheckVerhindertShutdown`, `WindowsArgsMitUndOhneForce`, E2E-Teilschritt Log-Eintrag mit `erzwingen=true` | Abgedeckt |
| Force-Flag + Toleranz persistiert, beim nächsten Start vorausgewählt | `AppEinstellungService`-Schlüssel `autoshutdown.force`/`autoshutdown.toleranz.minuten`; `AutoShutdownEinstellungenService` (Singleton→Scoped); `AutoShutdownEinstellungenLadenAsync` im VM | `Get/Set-DefaultsUndClamp`, `EinstellungenWerdenGeladenNichtAktiviert`, E2E-Teilschritt nach `RestartAppPreservingDatabase` | Abgedeckt |
| Aktivierungszustand der ersten Checkbox nicht persistiert | Scope-Ausschluss 1 (bestätigt); `AutoShutdownAktiviert` bleibt beim Laden `false` | `EinstellungenWerdenGeladenNichtAktiviert`, E2E-Teilschritt „Aktivierungs-Checkbox aus" | Abgedeckt |
| Erststart ohne persistierte Werte → Defaults, kein Vorbelegen | Defaults Force `false`, Toleranz `5` (Entscheidung der offenen Frage, dokumentiert); Datensätze erst beim ersten `SetSettingAsync` | `Get/Set-DefaultsUndClamp`, Akzeptanzszenario 5 | Abgedeckt |
| Kein Shutdown bei App-Ende vor Countdown-Ablauf | Countdown nicht persistiert, wird verworfen (Scope-Klärung „Umgang mit fehlenden Objekten") | Implizit über fehlende Persistenz | Abgedeckt |
| Verhalten bei eingeklappter Navigation (48 px) | Scope-Ausschluss 7 (bestätigt): kompletter Block per `IsNavigationExpanded`-Binding ausgeblendet | Etabliertes Fußzeilen-Muster, kein dedizierter Test erforderlich | Abgedeckt |
| E2E: kein echter OS-Shutdown in Tests | `AufzeichnenderSystemShutdownService`, registriert bei gesetztem `SOFTWARESCHMIEDE_TEST_DB_PATH` (Gate aller E2E-Starts, verifiziert: `App.xaml.cs` Z. ~271–275 analog `IProzessStarter`-Zweig) | `ZeichnetAufrufMitForceFlagAuf` + E2E-Log-Nachweis | Abgedeckt |
| Konsolidierter E2E-Happy-Path: Checkbox → Optionen → Countdown → Abbruch → Deaktivierung → Auslösung → Persistenz | `E2E_AutoShutdown.cs`, eine Testmethode, ein `LaunchApp` + ein `RestartAppPreservingDatabase`; **korrigierte Schrittfolge** (Reaktivierung erst nach CLI-Neustart bei Anzahl > 0) | `AutoShutdown_CountdownAbbruchAufzeichnungUndPersistenz_E2E` | Abgedeckt |

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Gesamtfluss: Anzahl-Anzeige → Checkbox → Optionen → Countdown → Abbruch → Deaktivierung (inkl. Ausblendung) → CLI-Neustart → Reaktivierung → Auslösung mit Force → Persistenz über Neustart | Eine konsolidierte Methode `AutoShutdown_CountdownAbbruchAufzeichnungUndPersistenz_E2E` in `E2E_AutoShutdown.cs`, eigener `[SkippableFact]` in `MainTest.cs`; Hilfsmittel `ResolveSystemShutdownLogPfad`, `WaitForSystemShutdownEintragAsync`, `MenuView`-Zugriffe, `SetupProjectMitNeuerAufgabe`, KiSimulator-CLI (Existenz aller referenzierten Artefakte im Repo verifiziert) | Abgedeckt |
| Ungültige Toleranz-Eingabe (Fehlertext, kein Persistieren) | `MainWindowViewModelTests.ToleranzValidierung`; im E2E optional | Abgedeckt |
| Kein echter OS-Shutdown in E2E | `AufzeichnenderSystemShutdownService` über `SOFTWARESCHMIEDE_TEST_DB_PATH`-Gate | Abgedeckt |
| Countdown-Abbruch bei Statuswechsel (CLI-Neustart während Countdown) | E2E-Teilschritt „CLI erneut starten → Countdown verschwindet" + Unit-Test `CountdownAbbruchBeiWiederanlauf` | Abgedeckt |
| Deaktivierung während Countdown + Ausblendung der Checkbox bei Anzahl 0 | E2E-Teilschritt „Checkbox deaktivieren → Countdown weg und Checkbox ausgeblendet" + Unit-Test `DeaktivierungWaehrendCountdown` | Abgedeckt |
| Persistenz-/Wiederherstellungsnachweis über App-Neustart | E2E-Teilschritt nach `RestartAppPreservingDatabase` (Force gesetzt, Toleranz „0", Aktivierung aus) | Abgedeckt |

## Hinweise

- **Semantik „Abbruch bei Statuswechsel":** Die Anforderung sagt wörtlich, jeder Statuswechsel „bricht [den Countdown] ab und löst eine Neuauswertung aus". Der Plan implementiert bewusst: bei unveränderter Anzahl 0 bleibt der Endzeitpunkt stehen (kein Re-Arming mit voller Toleranz), um Verhungern durch die ~30-s-Heartbeat-`LaufdatenChanged`-Ereignisse zu vermeiden. Plausibel und gut begründet (Designentscheidung + Akzeptanzszenario 8), weicht aber vom Wortlaut ab — bei der Implementierung sicherstellen, dass Testnamen und Verhalten diese Semantik konsistent widerspiegeln.
- Der 1-s-`DispatcherTimer` für das Herunterzählen des `AutoShutdownCountdownText` hat keinen dedizierten VM-Test — nur Gefüllt/Geleert über `ZustandGeaendert`. Optional als `MainWindowViewModelTests`-Ergänzung mit injiziertem `TimeProvider` nachrüstbar.
- Der E2E-Test hält die Konsolidierungsregel ein (eine Methode, ein App-Lifecycle + ein Restart) und kommt ohne ConPTY-Befehlseingabe aus (reiner Prozesslebenszyklus) — in dieser Sandbox lauffähig.
- Scope-Ausschlüsse 1–7 tragen alle Status `bestätigt` mit Begründung; inhaltlich plausibel (Aktivierungs-Nichtpersistenz als Sicherheitsaspekt, Countdown-Start trotz wartender Prozesse verhindert dauerhafte Blockade, Force nur Windows mangels Unix-Äquivalent).
