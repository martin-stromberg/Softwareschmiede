# Anforderungsanalyse: Autonome Aufgaben mit Feature-Flag in Einstellungen

## Fachliche Zusammenfassung

Das Feature der autonomen Aufgaben (Projektleiter-Agent mit `AutonomAufgabeKonfiguration`) soll über ein zentrales Feature-Flag in den Anwendungseinstellungen aktivierbar/deaktivierbar sein. Das Feature ist bereits mit `AutonomAufgabenOptions.Enabled` angelegt und standardmäßig aktiviert. Sollte es deaktiviert sein, wird die Funktionalität für autonome Aufgaben nicht verfügbar gemacht, und stattdessen ermöglicht das System das einfache Starten von Aufgaben mit direkter CLI-Ausführung wie bisher (Fallback-Verhalten für reguläre, nicht-autonome Aufgaben).

## Betroffene Klassen und Komponenten

### Datenmodellklassen
- **`AutonomAufgabenOptions`** (bereits vorhanden)
  - Property `Enabled` (default: `true`) – Konfigurierbar über `AutonomAufgaben.Enabled` in `appsettings.json`
  - Sonstige Properties (`DefaultTokenBudget`, `DefaultRuntimeLimitMinutes`, etc.) bleiben unverändert

- **`AutonomAufgabeKonfiguration`** (bereits vorhanden)
  - Keine neuen Properties erforderlich; die Existenz dieser Entity wird durch `AutonomAufgabenOptions.Enabled` gesteuert

- **`Aufgabe`** (bereits vorhanden)
  - Property `AutonomKonfiguration` (nullable) – Existiert bereits; wird bei deaktiviertem Feature nicht populiert

### Logikklassen / Services
- **`AutonomAufgabenInitialisierungsService`** (bereits vorhanden)
  - Guard-Klauseln oder Dependency-Injection Bedingung: Methoden sollten prüfen, ob `AutonomAufgabenOptions.Enabled == true` ist
  - Bei deaktiviertem Feature: Nicht-verfügbar oder gibt Fehler zurück

- **Services, die autonome Aufgaben initiieren** (z. B. `ProjektleiterAgentService`, `AutonomAufgabenInitialisierungsService`)
  - Einjektion von `IOptions<AutonomAufgabenOptions>` zur Laufzeit-Abfrage des Enabled-Status
  - Logik zum Fallback auf einfache CLI-Ausführung, wenn Feature deaktiviert ist

- **UI-Services / ViewModels** (z. B. `TaskDetailViewModel`, `AutonomAufgabeInitialisierungsDialogViewModel`)
  - Abhängigkeit von `IOptions<AutonomAufgabenOptions>` zur bedingten Anzeige von autonomen Aufgaben-UI-Elementen
  - Fallback: Nur einfache Aufgabenstart-Buttons (ohne Agenten-Initialisierung), wenn Feature deaktiviert ist

### Interfaces
- Keine neuen Interfaces erforderlich (Verwendung von `IOptions<AutonomAufgabenOptions>` genügt)

### Enums
- Keine neuen Enums erforderlich

### UI-Komponenten / Controller
- **`AutonomAufgabeDetailView` / `AutonomAufgabeInitialisierungsDialogView`**
  - Binding an ViewModel-Properties, die vom Enabled-Status abhängen
  - Mögliche neue Properties in ViewModels:
    - `IsAutonomAufgabenEnabled` (computed property, abhängig von `AutonomAufgabenOptions.Enabled`)
    - UI-Visibility/IsEnabled-Binding für autonome Aufgaben-spezifische Buttons/Felder

- **`TaskDetailView`**
  - Bedingte Anzeige des "Autonome Aufgabe starten"-Buttons basierend auf `IsAutonomAufgabenEnabled`

### Tests
- **Unit-Tests**: `AutonomAufgabenInitialisierungsServiceTests`
  - Neuer Test: `WhenEnabledFlagIsFalse_ShouldNotInitializeAutonomousTask()` oder ähnlich
  - Neuer Test: `WhenEnabledFlagIsFalse_ShouldFallbackToSimpleCliExecution()`
  
- **Integration-Tests**: `EntwicklungsprozessServiceTests` / `ProjektleiterAgentServiceTests`
  - Neuer Test: `ProzessStartenAsync_ShouldSkipAutonomInitialization_WhenFeatureFlagDisabled()`
  - Neuer Test: `ProzessStartenAsync_ShouldExecuteSimpleCliStart_WhenFeatureFlagDisabled()`

- **UI-Tests / E2E-Tests**:
  - `E2E_AutonomAufgabenInitialisierung.cs`: Hinzufügen von Tests für deaktiviertes Feature
    - Neuer Test: `WhenAutonomAufgabenDisabled_UIElementsShouldNotBeDisplayed()`
    - Neuer Test: `WhenAutonomAufgabenDisabled_SimpleStartButtonShouldBeAvailable()`

## Implementierungsansatz

### 1. Konfigurationsmanagement
- **Bestehender Mechanismus**: `AutonomAufgabenOptions` ist bereits über `appsettings.json` konfigurierbar und wird via `IOptions<AutonomAufgabenOptions>` injiziert
- **Feature-Flag-Pattern**: Das Feld `Enabled` wird als zentrale Gating-Condition verwendet
- **Abhängigkeiten**: Alle Services, die autonome Aufgaben verwenden, erhalten `IOptions<AutonomAufgabenOptions>` injiziert

### 2. Guard-Klauseln in Services
- **Aktualisierung bestehender Services** (z. B. `AutonomAufgabenInitialisierungsService.RunAsync()`)
  - Zu Beginn: `if (!options.Value.Enabled) { /* Fallback oder Fehler */ }`
  - Fallback-Verhalten: Einfache CLI-Ausführung statt Agenten-Initialisierung

### 3. UI-Binding und Visibility
- **ViewModels** (z. B. `TaskDetailViewModel`, `AutonomAufgabeInitialisierungsDialogViewModel`)
  - Berechnung einer neuen Eigenschaft: `public bool IsAutonomAufgabenEnabled => _autonomAufgabenOptions.Value.Enabled`
  - Binding in XAML: `IsEnabled="{Binding IsAutonomAufgabenEnabled}"` auf relevanten UI-Elementen (Buttons, Input-Felder)

- **Views** (XAML)
  - Bedingte Anzeige oder Deaktivierung von autonomen Aufgaben-Kontrollen
  - Eventuell Info-Text: "Autonome Aufgaben sind in den Einstellungen deaktiviert"

### 4. Fallback-Logik
- **Einfaches Aufgabenstarten**: Sollte bereits im System vorhanden sein (reguläre, nicht-autonome Aufgabenausführung)
- **Kontrollflussverzweigung**: In Services, die Aufgaben starten, nach dem Enabled-Status prüfen:
  - Falls `true`: Normale autonome Aufgaben-Initialisierung
  - Falls `false`: Fallback auf direktes CLI-Starten

### 5. Events und Hooks
- **Keine neuen Events erforderlich**: Das Feature-Flag wird zur Laufzeit abgefragt; eine Rekonfiguration während des Betriebs wird nicht erwartet

## Konfiguration

### Konfigurationsebene
- **Primär**: Anwendungsebene (`appsettings.json`)
  - Sektion: `"AutonomAufgaben"` → `"Enabled": true/false`
  - Umgebungsvariable (optional): `AutonomAufgaben__Enabled=true/false`

- **Sekundär** (optional für zukünftige Erweiterung): 
  - UI-basierte Einstellungen (z. B. im Menü "Einstellungen") mit persistierendem Speicher (z. B. `appsettings.User.json` oder spezifische Einstellungs-Entity)

### Beispiel-Konfiguration
```json
{
  "AutonomAufgaben": {
    "Enabled": true,
    "DefaultTokenBudget": 500000,
    "DefaultRuntimeLimitMinutes": 480,
    "HeartbeatTimeoutSeconds": 300,
    "MaxConcurrentUnteragenten": 5,
    "SkillAutogenerationEnabled": false,
    "MaxClones": 3,
    "MaxFeatureBranches": 10
  }
}
```

### Standardwert
- `AutonomAufgaben.Enabled = true` (Autonome Aufgaben sind standardmäßig aktiviert)

## Offene Fragen

1. **UI-Integration der Feature-Flag-Kontrolle**: Sollen Anwender das Feature-Flag über die GUI an/ausschalten können (z. B. im Menü "Einstellungen"), oder ist es nur via `appsettings.json` konfigurierbar?

2. **Fallback-Verhalten**: Wenn autonome Aufgaben deaktiviert sind und ein Benutzer versucht, eine autonome Aufgabe zu starten, soll:
   - Die gesamte Option nicht angezeigt werden?
   - Ein Fehler/Info-Dialog angezeigt werden?
   - Automatisch ein einfaches Aufgabenstarten durchgeführt werden?

3. **Persistenz während der Laufzeit**: Falls `Enabled` zur Laufzeit via Konfigurationsdatei-Reload wechselt, sollen laufende autonome Aufgaben weiterhin ausgeführt werden oder gestoppt werden?

4. **Dokumentation**: Sollen die Konfigurationsoptionen in Hilfedokumenten (z. B. `docs/help/`) dokumentiert werden?

5. **Bestehende Tests**: Sind die bestehenden Tests für autonome Aufgaben konzipiert, um unabhängig vom `Enabled`-Status zu laufen, oder erfordern sie, dass `AutonomAufgabenOptions.Enabled = true` ist?
