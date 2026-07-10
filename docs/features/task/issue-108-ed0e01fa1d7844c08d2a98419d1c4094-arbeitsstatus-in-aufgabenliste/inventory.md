# Bestandsaufnahme: Arbeitsstatus in Aufgabenliste

Diese Bestandsaufnahme analysiert den bestehenden Code hinsichtlich der Anforderung, dass aktive Aufgaben in der Navigationsseitenleiste und im Dashboard ihren KI-Ausführungsstatus mit automatischer Aktualisierung anzeigen sollen.

## Zusammenfassung

### Was ist bereits vorhanden:
- **Status-Berechnung:** Der `KiAusfuehrungsStatusConverter` implementiert bereits die Logik zur Berechnung des Status (▶ Läuft, ⏸ Wartet, ✓ Bereit) basierend auf `AktiveRunId`, `LastHeartbeatUtc` und `AufgabeStatus`
- **Heartbeat-Mechanismus:** Die `Aufgabe`-Entity enthält `AktiveRunId` und `LastHeartbeatUtc` als Eigenschaften, und es gibt bereits `UpdateHeartbeatAsync()` in `AufgabeService`
- **ObservableCollections:** `MainWindowViewModel` und `DashboardViewModel` haben bereits `AktiveAufgabenListe` ObservableCollections definiert
- **Aktive Aufgaben abrufen:** `AufgabeService.GetAktiveAufgabenAsync()` ruft alle Aufgaben mit Status Gestartet oder Wartend ab
- **Recovery-Service:** `AufgabeRecoveryService` erkennt verwaiste Aufgaben mit abgelaufenem Heartbeat (5 Minuten)
- **Events:** `KiAusfuehrungsService` publiziert `CliProcessStatusChanged`-Event bei Prozessstart/-stopp
- **Tests:** Umfangreiche Tests für Converter, Services und ViewModels sind bereits vorhanden

### Was fehlt oder ist unklar:
- **Automatische Aktualisierungsmechanik:** Es gibt keine sichtbare Timer- oder Event-basierte Aktualisierung der `AktiveAufgabenListe` im MainWindow/Dashboard
- **Event-Abonnement in ViewModels:** Die ViewModels abonnieren das `CliProcessStatusChanged`-Event nicht zur automatischen Aktualisierung bei Statuswechseln
- **Collection-Update-Logik:** Keine klare Mechanik zum Aktualisieren einzelner Items in der ObservableCollection bei Heartbeat-Änderungen
- **Synchronisierung:** Keine erkennbare Koordination zwischen MainWindowViewModel und DashboardViewModel für die gemeinsame Aufgabenliste
- **UI-Thread-Sicherheit:** Keine expliziten Dispatcher-Aufrufe zum UI-Thread bei Collection-Updates sichtbar

## Details

- [Datenmodelle](inventory/models.md)
- [Services und Logik](inventory/logic.md)
- [Enums und Erweiterungen](inventory/enums.md)
- [Interfaces](inventory/interfaces.md)
- [Tests](inventory/tests.md)
