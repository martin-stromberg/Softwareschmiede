# Tests

## Testklassen

### `KiAusfuehrungsStatusConverterTests`
Datei: `src\Softwareschmiede.Tests\App\Converters\KiAusfuehrungsStatusConverterTests.cs`

Testet den Converter, der die Aufgabe in den Status-String konvertiert.

| Testmethode | Was wird getestet |
|------------|------------------|
| `Convert_ShouldReturnLaeuftString_WhenAktiveRunIdPresentAndHeartbeatRecent()` | Rückgabe "▶ Läuft" wenn `AktiveRunId` gesetzt und Heartbeat aktuell (< 5 Min) |
| `Convert_ShouldReturnWartetString_WhenStatusIstWartend()` | Rückgabe "⏸ Wartet" wenn `Status == AufgabeStatus.Wartend` |
| `Convert_ShouldReturnBereitString_WhenNoActiveRunOrHeartbeatExpired()` | Rückgabe "✓ Bereit" wenn kein aktiver Lauf oder Heartbeat abgelaufen (> 5 Min) |
| `Convert_ShouldReturnEmptyString_WhenValueIsNotAufgabe()` | Rückgabe leerer String wenn kein `Aufgabe`-Objekt übergeben |
| `ConvertBack_ShouldThrowNotSupportedException()` | ConvertBack wirft `NotSupportedException` |

---

### `AufgabeRecoveryServiceTests`
Datei: `src\Softwareschmiede.IntegrationTests\Services\AufgabeRecoveryServiceTests.cs`

Testabdeckung für die Recovery-Logik (verwaiste Aufgaben-Erkennung).

**Erwartete Tests:**
- Erkennung von Recovery-Kandidaten (Status aktiv/wartend, Heartbeat > 5 Min alt)
- Manuelle Recovery mit Concurrency-Conflict-Handling
- Validierung von erlaubten Status-Übergängen während Recovery

---

### `AufgabeServiceTests`
Datei: `src\Softwareschmiede.IntegrationTests\Services\AufgabeServiceTests.cs`

Testabdeckung für Service-Methoden, insbesondere:

**Erwartete Tests:**
- `GetAktiveAufgabenAsync()` – Abruf aktiver Aufgaben
- `GetAktiveUndWartendeCountAsync()` – Zählen nach Status
- `UpdateHeartbeatAsync()` – Heartbeat-Aktualisierung
- `GetHeartbeatAgeMinutesAsync()` – Alter des Heartbeats
- Verschiedene Status-Übergänge und Validierungen

---

### `DashboardViewModelTests`
Datei: `src\Softwareschmiede.Tests\App\ViewModels\DashboardViewModelTests.cs`

Testabdeckung für das Dashboard ViewModel.

**Erwartete Tests:**
- Laden der Dashboard-Daten (`LadenAsync`)
- Behandlung von Fehlern beim Laden
- Recovery-Kandidaten-Scan
- Navigation zu aktiven Aufgaben
- Initialisierung mit gemeinsamer Aufgabenliste

---

### `MainWindowViewModelTests`
Datei: `src\Softwareschmiede.Tests\App\ViewModels\MainWindowViewModelTests.cs`

Testabdeckung für das Hauptfenster ViewModel.

**Erwartete Tests:**
- Navigation zwischen Ansichten (Dashboard, Projekte, Einstellungen)
- `AktiveAufgabenAktualisierenAsync()` – Aktualisierung der aktiven Aufgabenliste
- Dark-Mode Toggle
- Navigation zu Aufgabendetails

---

### `KiAusfuehrungsServiceTests`
Datei: `src\Softwareschmiede.Tests\Application\Services\KiAusfuehrungsServiceTests.cs`

Testabdeckung für die KI-Ausführungs-Service-Logik.

**Erwartete Tests:**
- CLI-Prozess-Start und -Stopp
- Event-Auslösung (`CliProcessStatusChanged`, `RunningCountChanged`)
- Prozess-Monitoring und Exit-Code-Verarbeitung
- Heartbeat-Tracking

---

## Hilfsmethoden und Testinfrastruktur

### `DatabaseFixture`
Datei: `src\Softwareschmiede.IntegrationTests\Infrastructure\DatabaseFixture.cs`

Hilfreiche Test-Infrastructure für Datenbank-Tests:
- Testkonfiguration von Entity Framework
- Setup von Testdatenbanken (In-Memory oder SQLite)
- Transaktionales Rollback nach Tests

### ObservableCollection Test-Erweiterungen
Datei: `src\Softwareschmiede.App\Extensions\ObservableCollectionExtensions.cs`

Erweiterungsmethode `ReplaceAll()` für ObservableCollections:
- Wird von `MainWindowViewModel.AktiveAufgabenAktualisierenAsync()` genutzt
- Ersetzt den gesamten Inhalt der Collection und triggert UI-Updates

---

## Testabdeckungs-Lücken (bezogen auf Anforderung)

Basierend auf der Anforderung nach **automatischer Aktualisierung** des Status:

- **Keine erkennbaren Tests für:** Periodische/ereignisgesteuerte Aktualisierung der `AktiveAufgabenListe` bei Heartbeat-Wechseln
- **Keine erkennbaren Tests für:** ViewModels, die `CliProcessStatusChanged` abonnieren und die Collection aktualisieren
- **Keine erkennbaren Tests für:** Synchronisierung zwischen MainWindow- und Dashboard-Aufgabenlisten
- **Keine erkennbaren Tests für:** UI-Thread-Sicherheit beim Aktualisieren von ObservableCollections

Diese Lücken deuten darauf hin, dass die automatische Aktualisierungsmechanik noch implementiert werden muss.
