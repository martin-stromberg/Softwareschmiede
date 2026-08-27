# Bestandsaufnahme: Feature-Flag-Integration für autonome Aufgaben

Diese Bestandsaufnahme dokumentiert die **bestehende Architektur und Implementierung der autonomen Aufgaben**, bezogen auf die Anforderung, ein Feature-Flag (`AutonomAufgabenOptions.Enabled`) in den Anwendungseinstellungen zu integrieren und als UI-Schalter zu exponieren.

**Analysierte Anforderung:** `docs/features/task/issue-205-47562196e8894189b9b9980fcd48a71c-automatisierte-produktentwickl/requirement.md`

---

## Zusammenfassung

### Wesentliche Befunde

1. **Feature-Flag existiert bereits:**
   - `AutonomAufgabenOptions.Enabled` (default: `true`) ist definiert
   - Konfigurierbar via `appsettings.json` Sektion `AutonomAufgaben:Enabled`
   - **Aktuell wird es aber NIRGENDS als Gate abgefragt** – Guard-Klauseln fehlen komplett

2. **Autonome Aufgaben-Flow:**
   - Start-Point: `TaskDetailViewModel` → Button "Autonome Aufgabe starten"
   - Orchestration: `AutonomAufgabeStartService` öffnet Dialog
   - Dialog: `AutonomAufgabeInitialisierungsDialogViewModel` (nutzt bereits `_options`, aber nicht für Gating)
   - Initialisierung: `AutonomAufgabenInitialisierungsService` (nutzt `_options` für limits, nicht für Gating)
   - Agent-Start: `ProjektleiterAgentService` → `KiAusfuehrungsService`

3. **Nicht-autonomer Weg ("einfaches Starten") ist separat implementiert:**
   - `EntwicklungsprozessService.ProzessStartenAsync()` – Repository-Setup nur
   - `EntwicklungsprozessService.ProzessStartenUndCliStartenAsync()` – Setup + CLI-Start kombiniert
   - Diese Services sind **unabhängig** von autonomen Aufgaben und brauchen **kein Feature-Flag-Gating**

4. **Settings-Persistierung:**
   - `AppEinstellungService` mit Key-Value-DB-Persistierung ist etabliert
   - Pattern für Boolean-Settings: `SetBoolSettingAsync()`, `GetBoolSettingAsync()`
   - `SettingsViewModel` zeigt vorbildliches Pattern für UI-Integration

5. **Kritische Lücke:** 
   - `IOptions<AutonomAufgabenOptions>` wird in mehreren ViewModels/Services nicht injiziert
   - `ProjektleiterAgentService`, `TaskDetailViewModel` brauchen Injection
   - Guard-Klauseln fehlen an 3 kritischen Stellen

---

## Detailanalyse

- [Datenmodellklassen](inventory/models.md)
  - `AutonomAufgabenOptions` (Feature-Flag-Träger)
  - `AutonomAufgabeKonfiguration` (Entity der autonomen Aufgabe)
  - `Aufgabe` (Navigation zur autonomen Konfiguration)
  - `AppEinstellung` (für Settings-Persistierung)

- [Logik-Services](inventory/logic.md)
  - `AutonomAufgabenInitialisierungsService` (kritischer Gating-Punkt)
  - `ProjektleiterAgentService` (kritischer Gating-Punkt, neue Injection nötig)
  - `AutonomAufgabeStartService` (kritischer Gating-Punkt)
  - `EntwicklungsprozessService` (nicht-autonomer Weg, kein Gating nötig)
  - `KiAusfuehrungsService` (neutrale CLI-Verwaltung)
  - `AppEinstellungService` (Pattern für Settings-Persistierung)

- [ViewModels](inventory/viewmodels.md)
  - `TaskDetailViewModel` (kritischer Gating-Punkt, neue Injection nötig)
  - `AutonomAufgabeInitialisierungsDialogViewModel` (bereits `_options` injiziert)
  - `SettingsViewModel` (vorbildlich für UI-Integration, neue Property nötig)

- [Enums](inventory/enums.md)
  - `AufgabeStatus`, `AufgabeAusfuehrungsStatus`, `PersistenzModus`, `AufgabeLaufStatus`, `BenachrichtigungsModus`

- [Tests](inventory/tests.md)
  - Existierende Tests: `AutonomAufgabenInitialisierungsServiceTests`, `EntwicklungsprozessServiceTests`, E2E-Tests
  - Fehlende Tests: Feature-Flag-Gating Tests (alle Kategorien)

- [Integrationspunkte und Gating-Strategie](inventory/integration-points.md)
  - Detaillierte Beschreibung aller Guard-Klauseln-Stellen
  - Settings-Persistierungs-Pattern zum Nachahmen
  - Dependency-Injection-Änderungen
  - Fallback-Logik für nicht-autonomen Weg

---

## Architektur-Übersicht: Autonome Aufgaben vs. Nicht-autonomer Weg

```
┌─────────────────────────────────────────────────────────────────┐
│ TaskDetailViewModel                                             │
│ - Property: IsAutonomAufgabe (prüft _aufgabe?.AutonomKonfiguration)
│ - Button: "Autonome Aufgabe starten" (nur wenn IsAutonomAufgabe)
│ - Benötigt: IOptions<AutonomAufgabenOptions> (neu!)             │
└────────┬────────────────────────────────────────────────────────┘
         │
         ├─── Fallback: Button "Einfach starten"
         │    │
         │    └──→ EntwicklungsprozessService.ProzessStartenUndCliStartenAsync()
         │         - Repository-Setup (kein Feature-Flag!)
         │         - CLI-Start mit IKiPlugin
         │         - KiAusfuehrungsService verwaltet Prozess
         │
         └─── Autonomer Weg: Button "Autonome Aufgabe starten"
              │
              └──→ GUARD CHECK: if (!_options.Value.Enabled) → Fehler
                   │
                   └──→ AutonomAufgabeStartService.StarteAsync()
                        - Öffnet Dialog
                        - GUARD CHECK: if (!_options.Value.Enabled) → Fehler
                        │
                        └──→ AutonomAufgabeInitialisierungsDialogViewModel
                             - Lädt Branch-Liste, Promptvorlagen
                             - Nutzt bereits _options für Defaults
                             │
                             └──→ AutonomAufgabenInitialisierungsService.InitialisiereAsync()
                                  - GUARD CHECK: if (!_options.Enabled) → Exception
                                  - Erstellt Arbeitsverzeichnis
                                  - Klont Repository
                                  - Schreibt permissions.json, state.json
                                  - Speichert AutonomAufgabeKonfiguration
                                  │
                                  └──→ ProjektleiterAgentService.StarteAgentAsync()
                                       - GUARD CHECK: if (!_options.Value.Enabled) → Exception (neu!)
                                       - Startet Projektleiter-Skill
                                       - Startet CLI via KiAusfuehrungsService
                                       - Sendet Initial-Prompt
```

---

## Änderungen notwendig für Feature-Flag-Gating

### 1. Neue Guard-Klauseln (3 Orte)

**AutonomAufgabeStartService.StarteAsync()**
```csharp
if (!_autonomAufgabenOptions.Value.Enabled)
{
    return new AutonomAufgabeStartResult(
        aufgabe, 
        "Autonome Aufgaben sind in den Einstellungen deaktiviert.",
        null);
}
```

**AutonomAufgabenInitialisierungsService.InitialisiereAsync()**
```csharp
if (!_options.Enabled)
{
    throw new InvalidOperationException("Autonome Aufgaben sind nicht aktiviert.");
}
```

**ProjektleiterAgentService.StarteAgentAsync()** (neue Injection!)
```csharp
if (!_autonomAufgabenOptions.Value.Enabled)
{
    throw new InvalidOperationException("Autonome Aufgaben sind nicht aktiviert.");
}
```

### 2. Neue Dependency Injections

- **ProjektleiterAgentService**: `IOptions<AutonomAufgabenOptions>` hinzufügen
- **TaskDetailViewModel**: `IOptions<AutonomAufgabenOptions>` hinzufügen

### 3. UI-Binding für Feature-Flag

**TaskDetailViewModel:**
```csharp
public bool IsAutonomAufgabenEnabled => 
    _autonomAufgabenOptions?.Value.Enabled ?? false;

// Binding existierender Properties ändern:
public bool ShowAutomatisierungPanel => 
    IsAutonomAufgabe && IsAutonomAufgabenEnabled;
```

**SettingsViewModel:**
```csharp
private bool _isAutonomAufgabenEnabled = true;
public bool IsAutonomAufgabenEnabled
{
    get => _isAutonomAufgabenEnabled;
    set => SetProperty(ref _isAutonomAufgabenEnabled, value);
}
```

**AppEinstellungService:**
```csharp
public const string AutonomAufgabenEnabledKey = "autonomeaufgaben.enabled";
```

**SettingsView.xaml:**
```xaml
<CheckBox IsChecked="{Binding IsAutonomAufgabenEnabled}" 
          Content="Autonome Aufgaben aktivieren"/>
```

### 4. SettingsViewModel Load/Save

**LadenCommand:**
```csharp
IsAutonomAufgabenEnabled = 
    (await _einstellungService.GetBoolSettingAsync(
        AppEinstellungService.AutonomAufgabenEnabledKey)) ?? true;
```

**SpeichernCommand:**
```csharp
await _einstellungService.SetBoolSettingAsync(
    AppEinstellungService.AutonomAufgabenEnabledKey, 
    IsAutonomAufgabenEnabled);
```

### 5. Fehlende Tests

Siehe [Tests-Dokumentation](inventory/tests.md) für vollständige Liste.

---

## Bekannte Constraints und Patterns

### Konfigurierungs-Quellen (Priorität)

1. **Höchste:** `AppEinstellung` (DB, via SettingsViewModel)
2. **Mittlere:** Umgebungsvariable `AutonomAufgaben__Enabled=true/false`
3. **Niedrigste:** `appsettings.json` Sektion `AutonomAufgaben:Enabled`

**Pattern:** `IOptions<AutonomAufgabenOptions>` aus DI werden via Standard .NET Configuration Binding aufgelöst und können zur Laufzeit nicht verändert werden. **Die DB-persistierte Einstellung in `AppEinstellung` muss zur Laufzeit zusätzlich abgefragt werden** (wie in `SettingsViewModel`).

### Settings-Persistierungs-Pattern (etabliert)

- `AppEinstellungService` bietet typsichere Wrapper: `GetBoolSettingAsync()`, `SetBoolSettingAsync()`
- Alle Anwendungs-Settings (Design-Mode, Plugin-Defaults, IDE-Plugin-Reihenfolge) nutzen diesen Service
- Boolean-Settings werden als String `"True"` / `"False"` persistiert

### Feature-Flag für nicht-autonomen Weg

- **KEIN Gating für `EntwicklungsprozessService`** – Dieser Service ist neutral und wird von beiden Wegen genutzt
- **KEIN Gating für `KiAusfuehrungsService`** – Dieser Service ist neutral und verwaltet nur CLI-Prozesse

### Fallback-Verhalten

- Wenn Feature-Flag deaktiviert ist und Nutzer versucht "Autonome Aufgabe starten" → Guard-Check → Fehler/Warnung
- Nutzer kann stattdessen "Einfach starten" nutzen → `EntwicklungsprozessService` startet unverändert

---

## Empfohlene Implementierungs-Reihenfolge

1. **Neue Guard-Klauseln schreiben** (3 Services)
2. **Dependency Injections hinzufügen** (2 ViewModels/Services)
3. **SettingsViewModel erweitern** (Property, Load/Save)
4. **AppEinstellungService Konstante** (Schlüssel)
5. **SettingsView.xaml erweitern** (UI-Schalter)
6. **Tests schreiben** (Unit-, Integration-, E2E)
7. **Dokumentation / Changelog** aktualisieren

---

## Dateien im Detailinventar

- `inventory/models.md` – Datenmodelle
- `inventory/logic.md` – Services und deren Methoden
- `inventory/viewmodels.md` – ViewModels und Settings-Pattern
- `inventory/enums.md` – Enum-Definitionen
- `inventory/tests.md` – Test-Übersicht
- `inventory/integration-points.md` – Detaillierte Gating-Strategie und Implementierungs-Details
