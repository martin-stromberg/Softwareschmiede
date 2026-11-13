# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan lückenhaft

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Anzahl laufender Automatisierungen am unteren Seitenleisten-Rand anzeigen; „laufend" = `KiAusfuehrungsZustand.Laeuft`, „Wartet" zählt nicht | `LaufendeAutomatisierungenZaehlung` (Application-Layer) + `LaufendeAutomatisierungenAnzahl` im VM aus `AktiveAufgabenListe`; `TextBlock` `AutoShutdownLaufendeAnzahl` in `MainWindow.xaml`; Klassifikation wird dafür in den Shared-Layer verschoben (Umsetzungsschritte 2, 6, 9, 10) | `ZaehleAsync_KlassifiziertAlleZustaende`, `LaufendeAnzahlUndSichtbarkeiten`, E2E-Teilschritt „Fußzeile zeigt 1 laufend" | Abgedeckt |
| Aktivierungs-Checkbox nur bei laufenden Automatisierungen sichtbar/aktivierbar | `AutoShutdownUiSichtbar` = Anzahl > 0 ∨ aktiviert ∨ Countdown aktiv (begründete Erweiterung gegenüber Wortlaut, damit Deaktivieren im Countdown möglich — in „Offene Punkte" 7 dokumentiert) | `LaufendeAnzahlUndSichtbarkeiten`, E2E-Teilschritt „Checkbox erscheint" | Abgedeckt |
| Bei aktivierter Checkbox: Toleranzzeit-Eingabefeld (Minuten) + Force-Checkbox „Herunterfahren erzwingen" | `AutoShutdownOptionenSichtbar` = `AutoShutdownAktiviert`; XAML: `AutoShutdownToleranzMinuten`-TextBox + `AutoShutdownErzwingen`-CheckBox | `LaufendeAnzahlUndSichtbarkeiten`, `ToleranzValidierung`, E2E-Teilschritt | Abgedeckt |
| Sichtbarer Countdown über Toleranzzeit, sobald keine CLI-Ausführung mehr aktiv | Orchestrator armiert `CountdownEndeUtc` bei enabled && Anzahl 0; VM zeigt `AutoShutdownCountdownText` „Herunterfahren in mm:ss" mit 1-s-`DispatcherTimer` | `CountdownStartet_WennAnzahlAufNullFaellt`, `ShutdownErstNachToleranzablauf`, `DeaktivierenRuftSetEnabled` (Countdown-Text), E2E-Teilschritt | Abgedeckt |
| Jeder Statuswechsel während Countdown → Abbruch + Neuauswertung | Trigger `RunningCountChanged`, `LaufdatenChanged`, 5-s-Sicherheitstick → koaleszierte `EvaluiereAsync`; Anzahl > 0 → Disarmierung. **Dokumentierte Abweichung:** Bei weiterhin 0 läuft der Countdown mit unverändertem Endzeitpunkt weiter statt neu zu armieren (Verhungern-Schutz gegen 30-s-Heartbeat-Events, Designentscheidung) | `CountdownAbbruchBeiWiederanlauf`, `CountdownBleibtBeiUnveraenderterAnzahl`, E2E-Teilschritt CLI-Neustart | Abgedeckt |
| Countdown-Ende → OS-Shutdown (optional erzwungen) | `OnCountdownAbgelaufenAsync` mit Final-Recheck → `RequestShutdownAsync(erzwingen)`; Windows-Mapping `/s /f /t 0` vs. `/s /t 0` | `ShutdownErstNachToleranzablauf`, `FinalRecheckVerhindertShutdown`, `WindowsArgsMitUndOhneForce`, E2E-Teilschritt Log-Eintrag | Abgedeckt |
| Force-Flag + Toleranz persistiert, beim nächsten Start vorausgewählt | `AppEinstellungService`-Schlüssel `autoshutdown.force`/`autoshutdown.toleranz.minuten`; `AutoShutdownEinstellungenService` (Singleton→Scoped); `AutoShutdownEinstellungenLadenAsync` im VM | `Get/Set-DefaultsUndClamp`, `EinstellungenWerdenGeladenNichtAktiviert`, E2E-Teilschritt nach `RestartAppPreservingDatabase` | Abgedeckt |
| Aktivierungszustand der ersten Checkbox nicht persistiert | Scope-Ausschluss 1 (bestätigt); `AutoShutdownAktiviert` bleibt beim Laden `false` | `EinstellungenWerdenGeladenNichtAktiviert`, E2E-Teilschritt „Aktivierungs-Checkbox aus" | Abgedeckt |
| Erststart ohne persistierte Werte → Defaults, kein Vorbelegen | Defaults Force `false`, Toleranz `5`; `AppEinstellung`-Datensätze erst beim ersten `SetSettingAsync` | `Get/Set-DefaultsUndClamp`, Akzeptanzszenario 5 | Abgedeckt |
| Kein Shutdown bei App-Ende vor Countdown-Ablauf | Countdown nicht persistiert, verworfen (Scope-Klärung, „Umgang mit fehlenden Objekten") | Implizit über fehlende Persistenz; kein dedizierter Test nötig | Abgedeckt |
| Verhalten bei eingeklappter Navigation (48 px) | Scope-Ausschluss 7: kompletter Block per `IsNavigationExpanded`-Binding ausgeblendet | Kein dedizierter Test (etabliertes Fußzeilen-Muster) | Abgedeckt |
| E2E: kein echter OS-Shutdown in Tests | `AufzeichnenderSystemShutdownService`, registriert bei gesetztem `SOFTWARESCHMIEDE_TEST_DB_PATH` (gate für alle E2E-Starts) | `ZeichnetAufrufMitForceFlagAuf` + E2E-Log-Nachweis | Abgedeckt |
| Konsolidierter E2E-Happy-Path vom Checkbox-Auslöser bis zur Shutdown-Aufzeichnung inkl. Abbruch, Deaktivierung, Persistenz über Neustart | `E2E_AutoShutdown.cs`, eine Testmethode, ein `LaunchApp` + ein `RestartAppPreservingDatabase` | `AutoShutdown_CountdownAbbruchAufzeichnungUndPersistenz_E2E` | **Lücke** — Schrittfolge nicht ausführbar (siehe unten) |

## Fehlende oder unvollständige Testanforderungen

- [ ] E2E `AutoShutdown_CountdownAbbruchAufzeichnungUndPersistenz_E2E`: Der beschriebene Schritt „Checkbox deaktivieren → Countdown weg → **reaktivieren** + Toleranz „0"" ist nach der eigenen Sichtbarkeitsregel nicht ausführbar. Nach dem Deaktivieren gilt `AutoShutdownUiSichtbar` = `Anzahl > 0 || aktiviert || Countdown` = `false` (Anzahl 0, deaktiviert, kein Countdown) — die Checkbox ist ausgeblendet und kann nicht erneut aktiviert werden. Der Shutdown-Auslösungs-/Force-Flag-/Log-Eintrag-Nachweis hängt von diesem Schritt ab. Korrektur nötig, z. B.: nach dem Deaktivieren die CLI erneut starten (Checkbox wird wieder sichtbar) → reaktivieren → Toleranz „0" setzen → CLI stoppen → Countdown mit Toleranz 0 → Log-Eintrag; alternativ die Sequenz so umstellen, dass der Auslösungsnachweis ohne Re-Aktivierung bei Anzahl 0 erfolgt.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Gesamtfluss: Anzahl-Anzeige → Checkbox → Optionen → Countdown → Abbruch → Deaktivierung → Auslösung → Persistenz über Neustart | Eine konsolidierte Methode `AutoShutdown_CountdownAbbruchAufzeichnungUndPersistenz_E2E` in `E2E_AutoShutdown.cs`, eingebunden als eigener `[SkippableFact]` in `MainTest.cs`; Hilfsmittel `ResolveSystemShutdownLogPfad`, `WaitForSystemShutdownEintragAsync`, `MenuView`-Zugriffe, `SetupProjectMitNeuerAufgabe`, KiSimulator-CLI (alle im Repo verifiziert vorhanden) | Lücke — Reaktivierungs-Schritt bei Anzahl 0 im beschriebenen Ablauf nicht auslösbar (siehe oben) |
| Ungültige Toleranz-Eingabe (Fehlertext, kein Persistieren) | `MainWindowViewModelTests.ToleranzValidierung`; im E2E optional | Abgedeckt |
| Kein echter OS-Shutdown in E2E | `AufzeichnenderSystemShutdownService` über `SOFTWARESCHMIEDE_TEST_DB_PATH`-Gate | Abgedeckt |

## Fehlende oder unvollständige Planbestandteile

- [ ] Inkonsistenz zwischen der geplanten Sichtbarkeitsregel `AutoShutdownUiSichtbar` (`Anzahl > 0 || aktiviert || CountdownEndeUtc != null`) und dem E2E-Szenario bzw. Akzeptanzszenario 3: Nach „Checkbox deaktivieren → Countdown weg" bei Anzahl 0 ist die Checkbox unsichtbar — die im E2E-Fluss beschriebene Nutzeraktion „reaktivieren" kann den nächsten Schritt nicht auslösen. Der Plan muss entweder die E2E-Schrittfolge korrigieren (Re-Aktivierung nur bei sichtbarer Checkbox, d. h. bei laufender Automatisierung oder armiertem Countdown) oder die Sichtbarkeitsregel explizit anders festlegen (z. B. Checkbox bleibt nach einmaligem Anzeigen bis zum nächsten Nicht-Null-Übergang sichtbar).

## Hinweise

- **Semantik „Abbruch bei Statuswechsel":** Die Anforderung sagt wörtlich, jeder Statuswechsel „bricht [den Countdown] ab und löst eine Neuauswertung aus". Der Plan implementiert bewusst: bei unveränderter Anzahl 0 bleibt der Endzeitpunkt stehen (kein Re-Arming mit voller Toleranz), um Verhungern durch die ~30-s-Heartbeat-`LaufdatenChanged`-Ereignisse zu vermeiden. Das ist plausibel und gut begründet (Designentscheidungen), weicht aber vom Wortlaut ab — bei der Implementierung sicherstellen, dass Akzeptanzszenario 8 und die Testnamen diese Semantik konsistent widerspiegeln.
- Der 1-s-`DispatcherTimer` für das Herunterzählen des `AutoShutdownCountdownText` (Restzeit sinkt im mm:ss-Text) hat keinen dedizierten VM-Test — nur Gefüllt/Geleert über `ZustandGeaendert`. Optional als `MainWindowViewModelTests`-Ergänzung mit dem injizierten `TimeProvider` nachrüstbar.
- Für den E2E-Test prüfen, ob das Stoppen der KiSimulator-CLI über den UI-Stop-Pfad oder über Prozessende erfolgt; der KiSimulator startet eine interaktive Session (ConPTY) — in dieser Sandbox greift `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` nur für die markierten ConPty-Tests; der neue Test muss ohne ConPTY-Befehlseingabe auskommen (reine Prozess-Lebenszyklus-Beobachtung ist unproblematisch).
