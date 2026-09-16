# Bestandsaufnahme: Erkennen von Limits (Issue #151)

Analysiert wurde der bestehende Code rund um Rate-Limit-Erkennung in der CLI-Ausgabe, den Aufgaben-Lebenszyklus (`Aufgabe`, `AufgabeStatus`), die KI-Plugin-Auflösung über `KiPluginPrefix` sowie die Anzeige aktiver Aufgaben in der Seitenleiste — bezogen auf die Anforderung in `requirement.md` (pausierbare Aufgaben bei KI-Session-Limits).

## Zusammenfassung

**Bereits vorhanden:**

- **Rate-Limit-Marker-Erkennung:** `ProtokollService.TryParseRateLimitMarker` / `ParseRateLimitMarker` erkennen `[[SOFTWARESCHMIEDE_RATE_LIMIT:ISO8601_DATETIME]]` in CLI-Ausgabezeilen. Bei Fund legt `AddCliOutputAsync` zusätzlich zur `CliOutput`-Zeile einen `Protokolleintrag` mit `ProtokollTyp.RateLimit` an (inkl. optionalem Reset-Zeitstempel im Inhaltstext).
- **Wartender Aufgabenstatus:** `AufgabeStatus.Wartend` existiert bereits mit der Dokumentation „CLI hat Rate-Limit erreicht; wartet auf Wiederaufnahme". `ValidateStatusTransition` erlaubt `Gestartet → Wartend` und `Wartend → Gestartet`. `AufgabeStatusExtensions.AktivOderWartendStatus` enthält `Gestartet` und `Wartend`; alle Abfragen aktiver Aufgaben (`AufgabeService.GetAktiveAufgabenAsync`, `GetAktiveUndWartendeCountAsync`, `AufgabeRecoveryService`) nutzen dieses gemeinsame Prädikat.
- **Persistenz-Konventionen:** `SoftwareschmiededDbContext` definiert `UnixMillisConverter` und `NullableUnixMillisConverter` (SQLite-taugliche Unix-Millis-Speicherung für `DateTimeOffset`/`DateTimeOffset?`); bereits für `LastHeartbeatUtc`, `LetzterCliStartUtc`, `VorschlagAusfuehrenAbUtc`, `AutonomAufgabeKonfiguration.SessionPauseUtc` u. a. in Benutzung.
- **Referenz-Pausenmechanismus:** `AutonomAufgabeKonfiguration.SessionPauseUtc` (nullable `DateTimeOffset`) mit Setzen/Leeren in `SessionManagementService` (`PauseAufgabeBeiBudgetLimitAsync`, `SetzeFortAsync`, `PruefeAusfuehrungAsync`) — jedoch ausschließlich für Autonome Aufgaben.
- **Plugin-Identität pro Aufgabe:** `Aufgabe.KiPluginPrefix` speichert den gewählten KI-Plugin-Prefix; `PluginSelectionService`/`PluginActivationService` lösen Prefix → Plugin auf (Enabled-Flag über `AppEinstellung`-Schlüssel `plugins.enabled.<prefix>`).
- **Generischer Key/Value-Store:** `AppEinstellung` (`Schluessel`/`Wert`/`AktualisiertAm`) mit `AppEinstellungService` inkl. Konventionen für Prefix-Schlüssel.
- **Lauf-Aktivitätsprüfung:** `AufgabeLaufAktivitaet.IstAktiv` (Heartbeat < 5 Min., `AufgabeRecoveryService.HeartbeatTimeoutMinutes = 5`); wird u. a. von `CliUpdateSafetyService.CheckAsync` und `KiAusfuehrungsStatusConverter` verwendet.
- **CLI-Prozessverwaltung:** `KiAusfuehrungsService` (Start/Stop/Heartbeat, `CliProcessStatusChanged`-Event) plus `CliProcessManager` (Heartbeat-Timer 30 s, Persistenz von `AktiveRunId`/`LaufStatus`).
- **UI-Grundgerüst:** `ActiveTasksListControl.xaml` zeigt Aufgabenkacheln mit Status via `KiAusfuehrungsStatusConverter` („⏳ Prompt in Wartestellung", „✓ Bereit", „▶ Läuft", „⏸ Wartet"); `MainWindowViewModel` aktualisiert die Liste alle 5 s per `DispatcherTimer` über `IAktiveAufgabenService.GetAktiveAufgabenAsync`. `TaskDetailView.xaml` besitzt eine RibbonGroup „Aufgabe" mit Speichern/Löschen/Starten/Beenden.
- **KI-Simulator-Plugin:** `plugins/Softwareschmiede.Plugin.KiSimulator` (`PluginPrefix = "Softwareschmiede.KiSimulator"`, `ProviderDateiPraefix = "simulator"`, `SupportsSessionContinuation() == false`), im Testmodus von `PluginManager` freigegeben.
- **E2E-Infrastruktur:** `WpfTestBase` (FlaUI, eigene SQLite-Test-DB via `SOFTWARESCHMIEDE_TEST_DB_PATH`, Credential-Store-Snapshot, App-Log-Diagnose) sowie gebündelte E2E-Läufe in `End2EndTest` (`RunGeneralTests`, `RunConPtyTests`).

**Offensichtlich noch nicht vorhanden:**

- Kein „Pausiert-bis"-Zeitstempel auf `Aufgabe` und kein `AufgabeStatus.Pausiert`-Enumwert (Suche nach `pausier`/`PausiertBis` außerhalb des autonomen Bereichs ohne Treffer).
- Kein persistierter Session-Reset-/Limit-Zeitstempel auf `PluginKonfiguration` (Entity enthält nur Identitäts-/Konfigurationsfelder, kein Zeitfeld).
- `ProtokollService` persistiert den Reset-Zeitpunkt nur als Text im `RateLimit`-Protokolleintrag — keine Plugin-weite Speicherung, keine automatische Pausierung anderer Aufgaben mit gleichem `KiPluginPrefix`.
- Kein Pausier-Pfad in `KiAusfuehrungsService`/`CliProcessManager` (Stoppen = Prozess beenden; „kein Unterbrechen laufender CLI-Prozesse" ist aktuell implizit gegeben, da es keinerlei automatische Statusänderung nach Marker-Erkennung gibt).
- Kein Pause-Command/-Dialog in `TaskDetailViewModel`/`TaskDetailView.xaml`; keine Countdown-/Abgedimmt-Darstellung in `ActiveTasksListControl`/`AktiveAufgabePanelItem`.
- `PromptZeitVersandService` (zeitgesteuerte Prompts) ist rein in-memory und nicht pausbewusst.

**Test-Ausgangszustand:** Alle vier ausgeführten Testlanes grün — Lane 1 (Unit/App, `Category!=OsInterface`): 1528/1529 bestanden, 1 übersprungen (plattformabhängig); Lane 2 (`Category=OsInterface`): 48/50 bestanden, 2 übersprungen (ConPTY-Skip via `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`); Lane 3 (IntegrationTests): 69/69; Lane 4 (IntegrationTests `OsInterface`): 9/9. Keine nachgewiesenen Fehlschläge. Details und Nachweise in [tests.md](inventory/tests.md).

## Details

- [Datenmodell](inventory/models.md)
- [Logik](inventory/logic.md)
- [Enums](inventory/enums.md)
- [Interfaces](inventory/interfaces.md)
- [Tests](inventory/tests.md)
