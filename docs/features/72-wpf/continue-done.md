# Offene Aufgaben

Erstellt am: 2026-06-12
Abbruchgrund: Manuell ergänzt – E2E-Tests wurden im automatisierten Zyklus übersprungen

Die folgenden Aufgaben müssen im nächsten Lauf bearbeitet werden.

## E2E-Tests mit FlaUI implementieren

Diese Tests sind primär für die lokale Entwicklung gedacht. CI ist zweitrangig.

- [x] **FlaUI-Pakete zum Testprojekt hinzufügen:**
  - `FlaUI.Core` – Kern-Abstraktion für UI Automation
  - `FlaUI.UIA3` – UIA3-Provider (empfohlen für moderne WPF-Apps)
  - Zieldatei: `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj`
  - Das Target Framework muss auf `net10.0-windows` (oder `net10.0-windows10.0.17763.0`) angepasst werden, damit WinRT/UI-Automation-Typen verfügbar sind.

- [x] **`WpfTestBase` anlegen** (`src/Softwareschmiede.Tests/E2E/WpfTestBase.cs`):
  - Startet `Softwareschmiede.App.exe` als Prozess
  - Wartet auf das Hauptfenster (z. B. `Application.WaitWhileMainHandleIsMissing()`)
  - Gibt einen `FlaUI.Core.Application`-Handle zurück
  - Beendet den Prozess nach dem Test (`Dispose`)
  - Nutzt eine SQLite-In-Memory- oder temporäre Datei-DB (via Umgebungsvariable `SOFTWARESCHMIEDE_TEST_DB_PATH`), damit Tests keine echten Daten berühren

- [x] **Testklassen in `WpfE2EPlaceholderTests.cs` ausimplementieren** (Skip-Marker entfernen):
  - `ProduktErstellenUndAufgabeHinzufuegen_E2E` — Hauptfenster öffnen, Projekt anlegen, Aufgabe erstellen, Titel in der Liste prüfen
  - `AufgabeStarten_RepositoryKlonen_BranchErstellen_E2E` — Aufgabe starten, Status-Übergang `ArbeitsverzeichnisEingerichtet` → `Gestartet` in der UI prüfen
  - `CliProzessStartenUndFensterEinbetten_E2E` — CLI-Prozess starten, `ProcessWindowHost`-Panel ist nicht leer (Handle ≠ 0)
  - `DarkModeAktivierenUndPersistieren_E2E` — Dark Mode via Settings-Toggle aktivieren, App neu starten, Dark Mode ist noch aktiv
  - `RecoveryBannerNachHeartbeatTimeout_E2E` — Aufgabe in `InArbeit` versetzen, Heartbeat veralten lassen, Recovery-Banner erscheint

- [x] **Trait `[Trait("Category", "E2E")]`** auf alle E2E-Tests setzen, damit sie lokal mit `dotnet test --filter Category=E2E` selektiv ausführbar sind und im CI-Standardlauf übersprungen werden können (falls gewünscht).
