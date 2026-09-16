# Übersetzte Anforderung: Erkennen von Limits (Issue #151)

## Fachliche Zusammenfassung

Aufgaben erhalten einen pausierbaren Zustand: Der Anwender kann an einer `Aufgabe` eine Pause bis zu einem Datum mit Uhrzeit einstellen (Vorbelegung: aktueller Zeitpunkt). Eine pausierte Aufgabe wird in der Seitenleisten-Sektion „Aktive Aufgaben" (Programmmenü) mit Status „Pausiert" und Countdown bis zum Pausenende angezeigt; die Aufgaben-Kachel wird dabei abgeschwächt (blasser) dargestellt. Erkennt die Anwendung, dass eine KI-Ausführung wegen eines Session-Limits anhält, wird der Limit-Zeitpunkt pro KI-Plugin persistiert, im `Protokolleintrag` der auslösenden Aufgabe gelistet und automatisch eine Pause auf alle Aufgaben mit laufender Ausführung desselben `KiPluginPrefix` angewendet — ohne eine noch laufende Ausführung zu unterbrechen. Zusätzlich wird die Update-Sicherheitsprüfung (`CliUpdateSafetyService`) angepasst: Für Aufgaben, deren KI-Plugin ein zukünftiges Limit gemeldet hat, entfällt die bisherige Heartbeat-Toleranzzeit.

## Betroffene Klassen und Komponenten

### Datenmodellklassen

- `Aufgabe` (`src/Softwareschmiede/Domain/Entities/Aufgabe.cs`) — neues Feld für den Pausen-Endzeitpunkt, z. B. `PausiertBisUtc` (`DateTimeOffset?`). Persistenz über `SoftwareschmiededDbContext` mit `NullableUnixMillisConverter` (Analog: `VorschlagAusfuehrenAbUtc`, `AutonomAufgabeKonfiguration.SessionPauseUtc`). EF-Core-Migration erforderlich.
- `AufgabeStatus` (`src/Softwareschmiede/Domain/Enums/AufgabeStatus.cs`) — ggf. neuer Wert `Pausiert`, falls der Status als Enum-Wert modelliert wird (Annahme, siehe Offene Fragen); alternativ abgeleiteter Anzeigestatus aus dem Pausen-Zeitstempel.
- `PluginKonfiguration` (`src/Softwareschmiede/Domain/Entities/PluginKonfiguration.cs`) — neues Feld für das zuletzt erkannte Session-Limit des KI-Plugins, z. B. `SessionLimitResetUtc` (`DateTimeOffset?`); alternativ Persistenz über `AppEinstellung` mit Schlüssel pro `KiPluginPrefix` (Annahme, siehe Offene Fragen).
- `Protokolleintrag` / `ProtokollTyp` — bestehender Typ `RateLimit` wird für die Limit-Listung im Aufgabenprotokoll wiederverwendet (ggf. mit Plugin-Bezug im `Inhalt`).

### Logikklassen / Services

- `AufgabeService` — neue Methode zum Setzen/Aufheben der Pause (z. B. `SetPauseAsync(aufgabeId, bisUtc)`); Abfrage aller Aufgaben mit aktiver Ausführung eines bestimmten `KiPluginPrefix`; Einbeziehung des Pausen-Zeitpunkts in `GetAktiveAufgabenAsync`-Ergebnis.
- `ProtokollService.AddCliOutputAsync` / `TryParseRateLimitMarker` (`src/Softwareschmiede/Application/Services/ProtokollService.cs`) — Erweiterungspunkt für die Limit-Erkennung: Beim Erkennen eines Session-Limits wird zusätzlich zum `RateLimit`-Protokolleintrag das Plugin-Limit persistiert und die automatische Pause für alle betroffenen Aufgaben ausgelöst.
- `CliUpdateSafetyService.CheckAsync` (`src/Softwareschmiede/Application/Services/Updates/CliUpdateSafetyService.cs`) — Filterlogik anpassen: Für Aufgaben, deren Plugin ein zukünftiges Limit gespeichert hat, entfällt die Toleranzzeit aus `AufgabeLaufAktivitaet.IstAktiv` (Heartbeat-Fenster `AufgabeRecoveryService.HeartbeatTimeoutMinutes`, aktuell 5 Minuten).
- `KiAusfuehrungsService` — sicherstellen, dass eine automatisch gesetzte Pause einen laufenden CLI-Prozess nicht beendet/unterbricht (kein `StopCli`-Aufruf im Pause-Pfad).
- Neuer oder erweiterter Service für das automatische Pausieren aller Aufgaben eines Plugins (z. B. in `AufgabeService` oder ein dedizierter `KiPluginLimitService` — Annahme).

### Interfaces

- `ICliUpdateSafetyService` — Signatur voraussichtlich unverändert; nur die interne Bewertung in `CliUpdateSafetyService` ändert sich.
- `IKiPlugin` — möglicherweise Erweiterung um Limit-Erkennung, falls die Erkennung pluginspezifisch erfolgen soll (siehe Offene Fragen).

### Enums

- `AufgabeStatus` — ggf. neuer Wert `Pausiert` mit Auswirkungen auf `AufgabeStatusExtensions.IstAktivOderWartend`, `AufgabeService.ValidateStatusTransition` (Übergänge `Gestartet`/`Wartend` ↔ `Pausiert`), `StatusIndicatorControl` und `ProjectDetailViewModel`.
- `ProtokollTyp` — `RateLimit` vorhanden; ggf. kein neuer Typ nötig.

### UI-Komponenten

- `AktiveAufgabePanelItem` (`src/Softwareschmiede.App/ViewModels/AktiveAufgabePanelItem.cs`) — neue Eigenschaft für Pausen-Endzeitpunkt.
- `KiAusfuehrungsStatusConverter` (`src/Softwareschmiede.App/Converters/AppConverters.cs`) — neuer Anzeigestatus „Pausiert" mit Countdown-Text (z. B. `⏸ Pausiert (noch 02:15)`), vor den bestehenden Stati „⏳ Prompt in Wartestellung", „▶ Läuft", „⏸ Wartet", „✓ Bereit" einzureihen.
- `ActiveTasksListControl.xaml` — Kachel bei Pause abgeschwächt darstellen (z. B. reduzierte `Opacity` per `DataTrigger`).
- `MainWindowViewModel` — Countdown-Aktualisierung über den bestehenden 5-Sekunden-Aktualisierungszyklus der aktiven Aufgabenliste (oder feingranularer Timer — Annahme).
- `TaskDetailViewModel` / Ribbon der Aufgabendetailansicht — neue Aktion „Pause einstellen" mit Datum/Uhrzeit-Eingabe, vorbelegt mit dem aktuellen Zeitpunkt; konkrete Platzierung (Ribbon-Gruppe „Aufgabe", Dialog) zu klären.

### Tests

- E2E-Tests (FlaUI) unter `src/Softwareschmiede.Tests/E2E/` für alle drei Schritte; Limit-Erkennung voraussichtlich über das `Softwareschmiede.Plugin.KiSimulator`-Plugin simulierbar (Ausgabe des Markers `[[SOFTWARESCHMIEDE_RATE_LIMIT:<ISO8601>]]`).
- Unit-/Integrationstests: `CliUpdateSafetyServiceTests` (keine Toleranz bei zukünftigem Plugin-Limit), `ProtokollServiceTests` (Plugin-Limit-Persistenz und Aufgaben-Pause bei Marker), `KiAusfuehrungsStatusConverterTests` (Pausiert-Countdown), Statusübergangs-Tests bei neuem `AufgabeStatus`-Wert.

## Implementierungsansatz

- **Pause-Datenmodell:** `Aufgabe` um einen Pausen-Endzeitpunkt erweitern. Bestehender, fachlich ähnlicher Mechanismus ist `AutonomAufgabeKonfiguration.SessionPauseUtc` für Autonome Aufgaben (`SessionManagementService.PauseAufgabeBeiBudgetLimitAsync`) — dieser ist jedoch an die Autonome-Aufgabe-Konfiguration gebunden und nicht direkt für reguläre Aufgaben nutzbar.
- **Manueller Einstiegspunkt (Schritt 1):** Aktion an der Aufgabe (voraussichtlich Ribbon der `TaskDetailView`), die Datum+Uhrzeit abfragt; Vorbelegung `DateTime.Now`. Gesetzter Zeitpunkt wird persistiert; die Seitenleisten-Kachel zeigt „Pausiert" + Countdown und wird abgeblendet.
- **Automatischer Pfad (Schritt 2):** Die Limit-Erkennung hängt am bestehenden CLI-Ausgabe-Protokollierungspfad (`PseudoConsoleSession` → `CliOutputProtokollWriter` → `ProtokollService.AddCliOutputAsync`). Beim Erkennen eines Session-Limits: `RateLimit`-Protokolleintrag mit Limit-Angabe an der auslösenden Aufgabe, Persistenz des Limit-Zeitpunkts am KI-Plugin (`PluginKonfiguration` bzw. `KiPluginPrefix`-bezogen), danach Pause-Setzung auf allen aktiven Aufgaben mit demselben `KiPluginPrefix`. Laufende Prozesse werden dabei nicht gestoppt; die Pause greift erst für nachfolgende Ausführungen/Wiederaufnahmen.
- **Update-Prüfung (Schritt 3):** In `CliUpdateSafetyService.CheckAsync` werden Aufgaben, deren `KiPluginPrefix` ein persistiertes zukünftiges Limit hat, ohne Heartbeat-Toleranz (`AufgabeLaufAktivitaet.IstAktiv` bzw. `HeartbeatTimeoutMinutes`) sofort als nicht blockierend gewertet — d. h. ein frischer Heartbeat allein macht sie nicht mehr „riskant". (Interpretation, siehe Offene Fragen.)
- **Abhängigkeiten:** `AufgabeService`, `ProtokollService`, `PluginManager`/`PluginActivationService` (Auflösung `KiPluginPrefix` → `PluginKonfiguration`), `CliUpdateSafetyService`, Seitenleisten-Refresh in `MainWindowViewModel`.

## Konfiguration

- Kein benutzerseitig konfigurierbares Verhalten aus der Anforderung ableitbar. Der Pausen-Endzeitpunkt ist eine Eingabe pro Aufgabe (Datensatz-Ebene), das Session-Limit ein persistierter Laufzeitzustand pro KI-Plugin (`PluginKonfiguration` bzw. `AppEinstellung` je `KiPluginPrefix`). Kein Feature-Flag erforderlich (Annahme).

## Offene Fragen

1. **Modellierung von „Pausiert":** Soll `Pausiert` ein eigener `AufgabeStatus`-Wert werden (mit neuen Übergängen in `ValidateStatusTransition` und Aufnahme in `IstAktivOderWartend`) oder ein abgeleiteter Anzeigestatus aus einem neuen Zeitstempelfeld an `Aufgabe`, während der Lebenszyklus-Status (`Gestartet`/`Wartend`) bestehen bleibt?
2. **Wirkung der Pause:** Blockiert eine gesetzte Pause fachlich Aktionen (manueller Start, zeitgesteuerter Prompt-Versand via `PromptZeitVersandService`, Recovery durch `AufgabeRecoveryService`, automatischer Neustart nach `Wartend`), oder ist sie rein informativ?
3. **Ende der Pause:** Was passiert beim Ablauf des Countdowns — automatische Wiederaufnahme der KI-Ausführung (z. B. Versand des `VorschlagPrompt` ab `VorschlagAusfuehrenAbUtc`), Rückkehr zum vorherigen Status oder nur Wegfall der Anzeige?
4. **UI-Einstiegspunkt:** Wo genau wird „Pause einstellen" angeboten (Ribbon-Gruppe „Aufgabe" in der Aufgabendetailansicht, Kontextmenü der Seitenleisten-Kachel) und wie sieht die Datum/Uhrzeit-Eingabe aus (eigener Dialog)?
5. **Erkennungsweg des Session-Limits:** Erfolgt die Erkennung über den bestehenden Marker `[[SOFTWARESCHMIEDE_RATE_LIMIT:<ISO8601>]]` in der CLI-Ausgabe, oder müssen pluginspezifische Ausgabemuster der jeweiligen CLIs (z. B. Limit-Meldungen von Claude CLI, Codex, Copilot) erkannt werden — ggf. über eine Erweiterung von `IKiPlugin`?
6. **Persistenzort des Plugin-Limits:** Neue Spalte an `PluginKonfiguration` oder `AppEinstellung`-Schlüssel pro `KiPluginPrefix`? Wird ein abgelaufenes Limit automatisch entfernt/ignoriert?
7. **Update-Prüfung — Richtung der Anpassung:** Ist die Interpretation korrekt, dass Aufgaben mit zukünftigem Plugin-Limit sofort als nicht blockierend gelten (Heartbeat-Toleranz von 5 Minuten entfällt für sie), weil ihre CLI ohnehin bis zum Limit-Zeitpunkt angehalten ist?
8. **Vorzeitiges Aufheben:** Kann/soll der Anwender eine gesetzte Pause manuell vor Ablauf aufheben?
9. **Countdown-Anzeige:** Format und Aktualisierungsintervall des Countdowns in der Kachel (bestehender 5-Sekunden-Refresh ausreichend)?
