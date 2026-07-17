# Anforderungsanalyse: Verlorene Codex-Parameter

## Fachliche Zusammenfassung

Der Test `AufgabeStarten_MitCodexCommandLineParametersImStore_KiSimulatorStartetKorrekt_E2E` speichert Kommandozeilenparameter für das Codex-Plugin im Windows Credential Store (`Softwareschmiede.Codex.CommandLineParameters`), ohne zuvor bestehende produktive Parameter zu sichern oder zu berücksichtigen. Dies führt dazu, dass produktiv konfigurierte Parameter überschrieben oder verloren gehen, insbesondere wenn der Test mehrfach hintereinander läuft oder bei unerwarteter Unterbrechung (z.B. Fehlerfall vor `DeleteCredential`). Das Testdesign muss isoliert werden, um den Credential Store nicht zu verunreinigen.

## Betroffene Klassen und Komponenten

### Tests
- `E2E_TaskExecutionCommandLineParameters` — Der E2E-Regressionstest, der das Problem verursacht
- Evtl. weitere E2E-Tests, die auf `WindowsCredentialStore` zugreifen

### Infrastruktur & Services
- `WindowsCredentialStore` — Schnittstelle zum Windows Credential Manager; Speicher für Plugin-Einstellungen
- `ICredentialStore` — Interface für Credential-Speicherung (bereits in Unit-Tests gemockt, aber E2E-Tests verwenden den realen Store)
- `PluginSettingsService` — Lädt Einstellungen aus dem Credential Store via `ICredentialStore.GetCredential()`
- `CodexPlugin` — Nutzt `ICredentialStore` zur Abfrage von `CommandLineParameters`

### Enums & ValueObjects
- (keine neuen)

### UI-Komponenten / ViewModels
- (keine betroffenen; Issue liegt in Test-Infrastruktur, nicht in Produktivcode)

## Implementierungsansatz

### Problemursache
Der Test verwendet einen **realen** `WindowsCredentialStore` statt eines Mocks, und speichert Parameter ohne Cleanup-Garantie:
```csharp
new WindowsCredentialStore().SetCredential(
    "Softwareschmiede.Codex.CommandLineParameters", "--test-regression-flag");
// ...
new WindowsCredentialStore().DeleteCredential("Softwareschmiede.Codex.CommandLineParameters");
```

Falls `DeleteCredential` nie erreicht wird (Test-Fehler, Timeout), bleiben die Test-Parameter dauerhaft gespeichert und überschreiben produktive Konfiguration.

### Empfohlene Lösungen

**Option A (Empfohlen): Setup/Teardown mit Retten bestehender Parameter**
- Vor dem Test: Bestehenden Wert speichern (falls vorhanden)
- Während des Tests: Test-Wert setzen
- Nach dem Test (Teardown): Alten Wert wiederherstellen

Umsetzung: `IAsyncLifetime` oder `[SetUp]` / `[TearDown]` mit `try`/`finally`

**Option B: Credential Store mocken statt realer Zugriff**
- Test-Klasse injiziert einen Mock-`ICredentialStore` in die getestete Komponente
- Real-Credential-Store wird nicht berührt
- Voraussetzung: Wird der Store in `KiSimulator` / KI-Simulator-Plugin Konstruktor via DI injiziert?

**Option C: Test-Isolation über Alternate Data Stream oder Test-Namensraum**
- Nutze einen Test-spezifischen Credential-Store-Namespace (z.B. `"Softwareschmiede.Codex.CommandLineParameters.Test"`)
- Produktiver Code würde diesen nicht abfragen
- Weniger robust, da hart-codierte Test-Konstanten

### Zu prüfende Abhängigkeiten
- Wie wird `ICredentialStore` in `KiAusfuehrungsService.StartCliAsync` / dem KI-Simulator instanziiert?
- Ist der `WindowsCredentialStore` hartverdrahtet (neue Instanz im Test) oder kann er injiziert werden?
- Welche anderen E2E-Tests greifen auf `WindowsCredentialStore.SetCredential` zu und haben das gleiche Problem?

## Konfiguration

Das Feature ist kein Konfigurationsmerkmal, sondern eine **Test-Cleanup-Anforderung**. Keine Runtime-Konfiguration nötig.

## Offene Fragen

1. **Wird `ICredentialStore` in `CodexPlugin` / `KiSimulatorPlugin` bereits via DI injiziert?**
   - Falls ja: Kann der Test einen Mock injizieren?
   - Falls nein: Müsste zunächst auf DI umgestellt werden (größere Änderung)

2. **Gibt es weitere E2E-Tests, die `WindowsCredentialStore.SetCredential` direkt aufrufen?**
   - `E2E_SettingsCommandLineParameters.cs` erwähnt CommandLineParameters
   - `E2E_AufgabeStarten.cs` setzt `LocalDirectoryPlugin.ConfirmGitInitInSourceDirectory`
   - Alle sollten das gleiche Cleanup-Problem haben?

3. **Sollten Credential-Manager-Einträge durch eine Fixture (z.B. `IAsyncLifetime`) zentralisiert gereinigt werden?**
   - Statt einzeln in jedem Test `DeleteCredential` aufrufen

4. **Ist es produktiv üblich, dass `Softwareschmiede.Codex.CommandLineParameters` bereits gespeichert ist?**
   - Falls ja: Test wird diese Konfiguration zerstören und muss restorieren
   - Falls nein: Einfach sicherstellen, dass Test nicht speichert, was nicht gehört

## Priorität der Lösung

**Hoch:** Das Problem zerstört aktive produktive Konfigurationen während Test-Läufe. Ein Test sollte den Produktivzustand nicht verunreinigen.
