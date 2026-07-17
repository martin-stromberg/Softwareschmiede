# Test-Übersicht

## Problematische E2E-Tests mit direktem `WindowsCredentialStore`-Zugriff

### `E2E_TaskExecutionCommandLineParameters`
Datei: `src/Softwareschmiede.Tests/E2E/E2E_TaskExecutionCommandLineParameters.cs`

**Testmethode:** `AufgabeStarten_MitCodexCommandLineParametersImStore_KiSimulatorStartetKorrekt_E2E()`

**Ablauf:**
1. Zeile 23-24: `new WindowsCredentialStore().SetCredential("Softwareschmiede.Codex.CommandLineParameters", "--test-regression-flag")`
2. Zeile 25: `ConfirmLocalDirectoryGitInitInSourceDirectory()` - Setup für LocalDirectoryPlugin
3. Zeile 27-31: `SetupProjectMitNeuerAufgabe()`, `StartenUndPluginWaehlen()` - Start der Aufgabe mit KI Simulator
4. Zeile 33-34: `WaitForElement()` - Prüfung, dass Stoppen-Button erscheint
5. Zeile 36: `new WindowsCredentialStore().DeleteCredential("Softwareschmiede.Codex.CommandLineParameters")`

**Problem:**
- Wenn das Test zwischen Zeile 24 und Zeile 36 fehlschlägt oder ausfällt (z.B. bei `WaitForElement` Timeout), wird `DeleteCredential` nie aufgerufen
- Der Test-Parameter `--test-regression-flag` bleibt dauerhaft im Credential Store gespeichert
- Zerstört produktive Codex-Konfiguration bis manuell bereinigt

**Cleanup-Mechanik:** Nur ein abschließender `DeleteCredential`-Aufruf, keine Backup/Restore

---

### `E2E_SettingsCommandLineParameters`
Datei: `src/Softwareschmiede.Tests/E2E/E2E_SettingsCommandLineParameters.cs`

**Testmethode:** `Einstellungen_SpeichertUndLaeadtCommandLineParameters_E2E()`

**Ablauf:**
1. Zeile 39-40: App starten, Automation-Handle erstellen
2. Zeile 41: `expectedValue = $"--test-{Guid.NewGuid():N}"` - eindeutiger Test-Wert generieren
3. Zeile 43: `OpenKiSettingsWithCodexCli()` - Öffne Codex-Einstellungen
4. Zeile 45-46: CommandLineParameters-TextBox finden und mit `expectedValue` füllen
5. Zeile 48: `SaveSettings()` - Einstellungen speichern (schreibt via UI in Credential Store)
6. Zeile 50-51: Navigiere zu Dashboard und zurück zu Einstellungen
7. Zeile 53: `OpenKiSettingsWithCodexCli()` erneut
8. Zeile 55-56: Verifiziere, dass der Wert noch da ist
9. **Zeile 58: `new WindowsCredentialStore().DeleteCredential("Softwareschmiede.Codex.CommandLineParameters")`**

**Problem:**
- Der Wert wird via UI gespeichert (Line 48), aber keine Backup des ursprünglichen Wertes vor Änderung
- Wenn Test zwischen Zeile 48 und Zeile 58 fehlschlägt, wird Cleanup übersprungen
- Der eindeutige Test-Wert (GUID) mit `--test-` Präfix bleibt dauerhaft erhalten

**Cleanup-Mechanik:** Nur ein abschließender `DeleteCredential`-Aufruf ohne Versuch, ursprüngliche Konfiguration zu restorieren

---

### `E2E_AufgabeStarten`
Datei: `src/Softwareschmiede.Tests/E2E/E2E_AufgabeStarten.cs`

**Testmethode:** `AufgabeStarten_KlontRepositoryUndStartetCli_E2E()`

**Ablauf:**
1. Zeile 32-35: `SetupProjectMitNeuerAufgabe()` - Projekt initialisieren
2. Zeile 38: `StartenUndPluginWaehlen()` - Erster Start-Versuch (erwartet Fehler, da ConfirmGitInitInSourceDirectory nicht gesetzt)
3. Zeile 41-42: Fehlermeldung prüfen
4. **Zeile 45: `new WindowsCredentialStore().SetCredential("LocalDirectoryPlugin.ConfirmGitInitInSourceDirectory", "true")`**
5. Zeile 48: `StartenUndPluginWaehlen()` - Zweiter Start-Versuch (sollte funktionieren)
6. Zeile 51-52: Stoppen-Button prüfen
7. Zeile 55-60: Status und Fehler-Abwesenheit verifizieren

**Problem:**
- Zeile 45 setzt `LocalDirectoryPlugin.ConfirmGitInitInSourceDirectory` direkt im Credential Store
- **KEIN Cleanup/Restore am Ende des Tests!**
- Wenn dieser Test mit der Einstellung endet, ändern alle folgenden Tests ihr Verhalten für LocalDirectoryPlugin (InSourceDirectory-Modus ist jetzt best-effort freigegeben)
- Keine Isolation zwischen Tests

**Cleanup-Mechanik:** Gar keine - `SetCredential` ohne Delete

---

## Gemeinsame Muster

| Aspekt | E2E_TaskExecutionCommandLineParameters | E2E_SettingsCommandLineParameters | E2E_AufgabeStarten |
|--------|---------|---------|---------|
| **SetCredential-Aufruf** | Zeile 23-24 | Via UI, implizit | Zeile 45 |
| **Backup original?** | Nein | Nein | Nein |
| **DeleteCredential am Ende?** | Ja (Zeile 36) | Ja (Zeile 58) | **Nein** |
| **Fehlerresistenz** | Mittel (try/finally?) | Niedrig | Keine |
| **Test-Isolation** | Keine garantiert | Keine garantiert | Keine garantiert |

---

## Bestehende Test-Infrastruktur

### `WpfTestBase`
Datei: `src/Softwareschmiede.Tests/E2E/WpfTestBase.cs`

**Zweck:** Basisklasse für alle WPF-E2E-Tests. Verwaltet Test-Datenbank, App-Launch/-Shutdown, FlaUI-Automation.

**Relevante Details:**
- Zeile 64-77: `DeleteTestDatabase()` - Löscht Test-DB nach Test
- Zeile 84-99: `LaunchApp()` - Startet die App mit separatem Prozess, setzt `SOFTWARESCHMIEDE_TEST_DB_PATH`
- Test ist gekennzeichnet mit `[Collection("E2E")]` um parallele Ausführung zu verhindern

**Hinweis:** Es gibt **keinen zentralisierten Credential-Store-Cleanup** in der Basisklasse. Jeder Test kümmert sich selbst darum, oder kümmert sich gar nicht drum.

