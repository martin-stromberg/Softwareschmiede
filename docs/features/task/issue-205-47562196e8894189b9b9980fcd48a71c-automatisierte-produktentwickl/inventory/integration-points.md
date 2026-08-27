# Integrationspunkte und Feature-Flag-Gating

Diese Datei dokumentiert die Schlüsselstellen, wo das `AutonomAufgabenOptions.Enabled`-Flag abgefragt und zum Gating verwendet werden sollte.

---

## Kritische Integrationspunkte

### 1. AutonomAufgabeStartService.StarteAsync()
**Datei:** `src\Softwareschmiede.App\Services\AutonomAufgabeStartService.cs`

**Aktuelle Logik:**
- Zeigt `AutonomAufgabeInitialisierungsDialogViewModel` an
- Lädt aktualisierte Aufgabe
- Erzeugt `AutonomAufgabeDetailViewModel`

**Gating-Punkt:**
- **Zu Beginn prüfen:** `if (!_autonomAufgabenOptions.Value.Enabled) { return fehlerResult; }`
- Benötigt Injection: `IOptions<AutonomAufgabenOptions>`
- Fallback-Verhalten: Fehlertext oder Alternative anzeigen (z. B. "Autonome Aufgaben sind deaktiviert")

---

### 2. AutonomAufgabenInitialisierungsService.InitialisiereAsync()
**Datei:** `src\Softwareschmiede\Application\Services\AutonomAufgabenInitialisierungsService.cs`

**Aktuelle Logik:**
- Erstellt Arbeitsverzeichnisstruktur
- Klont Repository
- Schreibt `state.json` und `permissions.json`
- Speichert `AutonomAufgabeKonfiguration` in DB

**Gating-Punkt:**
- **Zu Beginn prüfen:** `if (!_options.Enabled) { throw new InvalidOperationException("..."); }`
- Alternative: Guard-Klausel mit aussagekräftiger Exception
- **Aktuell:** `_options` ist bereits injiziert! Nur `if`-Abfrage hinzufügen.

---

### 3. ProjektleiterAgentService.StarteAgentAsync()
**Datei:** `src\Softwareschmiede\Application\Services\ProjektleiterAgentService.cs`

**Aktuelle Logik:**
- Startet Projektleiter-Agent-Prozess über `KiAusfuehrungsService`
- Sendet Initial-Prompt verzögert

**Gating-Punkt:**
- **Zu Beginn prüfen:** Benötigt `IOptions<AutonomAufgabenOptions>` Injection (aktuell nicht vorhanden!)
- `if (!_options.Value.Enabled) { throw InvalidOperationException(...); }`
- Dies ist eine **neue Dependency-Injection**, die hinzugefügt werden muss.

---

### 4. TaskDetailViewModel UI-Sichtbarkeit
**Datei:** `src\Softwareschmiede.App\ViewModels\TaskDetailViewModel.cs`

**Aktuelle Logik:**
- Property `IsAutonomAufgabe` gibt an, ob Aufgabe autonom ist (prüft `_aufgabe?.AutonomKonfiguration != null`)
- Property `ShowAutomatisierungPanel` zeigt Automatisierungs-Ansicht an

**Gating-Punkt:**
- **Bedingung erweitern:** `public bool IsAutonomAufgabenEnabled => _autonomAufgabenOptions?.Value.Enabled ?? false;`
- **Bedingung ändern:** `public bool ShowAutomatisierungPanel => IsAutonomAufgabe && IsAutonomAufgabenEnabled;`
- **Benötigt Injection:** `IOptions<AutonomAufgabenOptions>` (aktuell nicht vorhanden!)
- **XAML-Binding:** Buttons/UI-Elemente an `IsAutonomAufgabenEnabled` binden oder berechnete Property nutzen

---

### 5. SettingsViewModel – UI für Feature-Flag
**Datei:** `src\Softwareschmiede.App\ViewModels\SettingsViewModel.cs`

**Neue Funktionalität:**
- **Property hinzufügen:** `IsAutonomAufgabenEnabled` (bool, mit Setter für Binding)
- **Loading:** In `LadenCommand`-Ausführung:
  ```csharp
  var enabled = await _einstellungService.GetBoolSettingAsync("autonomeaufgaben.enabled") ?? true;
  IsAutonomAufgabenEnabled = enabled;
  ```
- **Saving:** In `SpeichernCommand`-Ausführung:
  ```csharp
  await _einstellungService.SetBoolSettingAsync("autonomeaufgaben.enabled", IsAutonomAufgabenEnabled);
  ```

**UI-Pattern:** Vergleichbar mit `DesignMode` oder `BenachrichtigungsModus`

---

### 6. Fallback-Logik in TaskDetailViewModel
**Datei:** `src\Softwareschmiede.App\ViewModels\TaskDetailViewModel.cs` (Command-Handler)

**Szenario:** Benutzer klickt auf "Autonome Aufgabe starten", Feature ist aber deaktiviert

**Implementierung:**
- Guard-Check in `StartAutonomousTaskCommand` (oder ähnlich):
  ```csharp
  if (!_autonomAufgabenOptions.Value.Enabled)
  {
      FehlerMeldung = "Autonome Aufgaben sind in den Einstellungen deaktiviert.";
      return;
  }
  // ... normale Logik
  ```
- Alternativ: Button deaktivieren (`IsEnabled="{Binding IsAutonomAufgabenEnabled}"`)

---

## Settings-Persistierungs-Pattern

Zur Persistierung des Feature-Flags in der UI (SettingsViewModel):

1. **Schlüssel-Konstante definieren** in `AppEinstellungService`:
   ```csharp
   public const string AutonomAufgabenEnabledKey = "autonomeaufgaben.enabled";
   ```

2. **Property im ViewModel:**
   ```csharp
   private bool _isAutonomAufgabenEnabled = true; // default
   public bool IsAutonomAufgabenEnabled
   {
       get => _isAutonomAufgabenEnabled;
       set => SetProperty(ref _isAutonomAufgabenEnabled, value);
   }
   ```

3. **Laden** (in `LadenCommand`):
   ```csharp
   IsAutonomAufgabenEnabled = 
       (await _einstellungService.GetBoolSettingAsync(AppEinstellungService.AutonomAufgabenEnabledKey)) ?? true;
   ```

4. **Speichern** (in `SpeichernCommand`):
   ```csharp
   await _einstellungService.SetBoolSettingAsync(
       AppEinstellungService.AutonomAufgabenEnabledKey, 
       IsAutonomAufgabenEnabled);
   ```

5. **XAML-Binding:**
   ```xaml
   <CheckBox IsChecked="{Binding IsAutonomAufgabenEnabled}" Content="Autonome Aufgaben aktivieren" />
   ```

---

## Dependency-Injection der neuen Abhängigkeiten

Services/ViewModels, die `IOptions<AutonomAufgabenOptions>` noch nicht injiziert haben, müssen folgende Änderungen erfahren:

1. **ProjektleiterAgentService:**
   - Constructor: `IOptions<AutonomAufgabenOptions> options`
   - `_options = options.Value;`
   - Guard in `StarteAgentAsync()`: `if (!_options.Enabled) throw ...;`

2. **TaskDetailViewModel:**
   - Constructor: `IOptions<AutonomAufgabenOptions>? options = null` oder ähnlich
   - Neues Property: `public bool IsAutonomAufgabenEnabled => _options?.Value.Enabled ?? false;`

3. **SettingsViewModel:**
   - Constructor: evtl. `IOptions<AutonomAufgabenOptions> autonomAufgabenOptions` (informativ)
   - Property `IsAutonomAufgabenEnabled` für UI-Binding

---

## Fallback-Logik: Nicht-autonomer Weg bleibt unverändert

**Wichtig:** Der nicht-autonome Weg (einfaches Starten einer Aufgabe mit CLI-Ausführung) muss unabhängig vom Feature-Flag funktionieren.

**Relevant:**
- `EntwicklungsprozessService.ProzessStartenAsync()` – Diese Methode darf **NICHT** geprüft werden
- `EntwicklungsprozessService.ProzessStartenUndCliStartenAsync()` – Diese Methode darf **NICHT** geprüft werden
- `KiAusfuehrungsService` – Unverändert, wird von beiden Wegen genutzt

**Szenario bei Feature-Deaktivierung:**
1. Nutzer versucht, "Autonome Aufgabe initialisieren" → Guard in `AutonomAufgabeStartService` → Fehler/Warnung
2. Nutzer wählt stattdessen "Einfach starten" → `EntwicklungsprozessService.ProzessStartenUndCliStartenAsync()` → Funktioniert unverändert
3. CLI startet normal über KI-Plugin (nicht über Projektleiter-Agent)

---

## Zusammenfassung der Guard-Klauseln

| Klasse | Methode | Guard-Typ | Aktion |
|--------|---------|-----------|--------|
| `AutonomAufgabeStartService` | `StarteAsync()` | Früh | Fehler oder Fallback anzeigen |
| `AutonomAufgabenInitialisierungsService` | `InitialisiereAsync()` | Früh | `InvalidOperationException` werfen |
| `ProjektleiterAgentService` | `StarteAgentAsync()` | Früh | `InvalidOperationException` werfen |
| `TaskDetailViewModel` | (UI-Binding) | Bedingter Guard | UI-Elemente deaktivieren/verstecken |
| `SettingsViewModel` | `LadenCommand`, `SpeichernCommand` | Keine (neue Einstellung) | Flag laden/speichern |
