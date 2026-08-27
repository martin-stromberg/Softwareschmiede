# ViewModels

## `TaskDetailViewModel`
Datei: `src\Softwareschmiede.App\ViewModels\TaskDetailViewModel.cs`

ViewModel für die Aufgabendetailansicht. Verwaltet Status, Protokoll, CLI-Prozessstart und Fenstereinbettung. **Zentral für das Feature-Flag-Gating: steuert Sichtbarkeit von autonomen Aufgaben-Elementen.**

**Relevante Abhängigkeiten:**
- `AutonomAufgabenInitialisierungsService` – nicht direkt injiziert, aber über `AutonomAufgabeStartService` (private `_autonomAufgabeStartService`)
- Weitere Services: `AufgabeService`, `ProtokollService`, `KiAusfuehrungsService`, `EntwicklungsprozessService`, `PluginSelectionService`, etc.

**Relevante Properties und Methoden für autonome Aufgaben:**

| Property | Typ | Beschreibung |
|----------|-----|-------------|
| `AutonomAufgabeDetailViewModel` | `AutonomAufgabeDetailViewModel?` | Das Detail-ViewModel der autonomen Aufgabe (null wenn keine autonome Konfiguration existiert) |
| `ShowAutomatisierungPanel` | `bool` (computed) | Gibt an, ob die Automatisierung-Ansicht sichtbar ist (abhängig von `IsAutonomAufgabe`) |
| `IsAutonomAufgabe` | `bool` (computed) | Gibt an, ob die geladene Aufgabe eine autonome Aufgabe ist (prüft `_aufgabe?.AutonomKonfiguration != null`) |

**Wichtig:** Das `TaskDetailViewModel` stellt bereits die Weichen für autonome vs. nicht-autonome Aufgaben (`IsAutonomAufgabe`), zeigt aber das `AutonomAufgabeDetailViewModel` an, ohne aktuell das `Enabled`-Flag aus `AutonomAufgabenOptions` zu prüfen. Dies ist ein Ansatzpunkt für das Feature-Flag-Gating.

---

## `AutonomAufgabeInitialisierungsDialogViewModel`
Datei: `src\Softwareschmiede.App\ViewModels\AutonomAufgabeInitialisierungsDialogViewModel.cs`

ViewModel für den Initialisierungsdialog einer Autonomen Aufgabe. **Ist ein direkter Konsument von `AutonomAufgabenOptions`.**

**Abhängigkeiten:**
- `AutonomAufgabenInitialisierungsService` (_initialisierungsService)
- `IOptions<AutonomAufgabenOptions>` → `_options` **[bereits injiziert!]**
- `ILogger<AutonomAufgabeInitialisierungsDialogViewModel>` (_logger)
- `IPluginManager` (_pluginManager)
- `PromptVorlagenService` (_promptVorlagenService)
- `PromptVorlagenPlatzhalterService` (_promptVorlagenPlatzhalterService)

**Relevante Properties:**

| Property | Typ | Beschreibung |
|----------|-----|-------------|
| `SelectedProjectBranch` | `string?` | Ausgewählter oder neu vergebener Projektbranch |
| `AvailableProjectBranches` | `ObservableCollection<string>` | Verfügbare Remote-Branches |
| `InitialPrompt` | `string` | Initialprompt für Projektleiter |
| `TokenBudget` | `int` | Token-Budget (aus `_options.DefaultTokenBudget` initialisiert) |
| `AllowTokenExtension` | `bool` | Darf Anwender Token-Budget später erweitern? |
| `RuntimeLimitMinutes` | `int` | Laufzeitbegrenzung in Minuten (aus `_options.DefaultRuntimeLimitMinutes` initialisiert) |
| `SelectedPersistenceMode` | `PersistenzModus` | Persistenz-Modus (Standard, SitzungZuruecksetzen) |
| `AutoGenerateSkills` | `bool` | Aus `_options.SkillAutogenerationEnabled` initialisiert |
| `IsSubmitting` | `bool` | Wird gerade eingereicht? |

**Methoden (exemplarisch):**
- `Initialize(Aufgabe)` – Initialisiert ViewModel für eine Aufgabe
- `LadeAsync(CancellationToken)` – Lädt verfügbare Branches und Promptvorlagen
- (Submit-Logik, Branch-Validierung, etc.)

**Event:**
- `CloseRequested` – Wird ausgelöst wenn Dialog geschlossen werden soll

---

## `SettingsViewModel`
Datei: `src\Softwareschmiede.App\ViewModels\SettingsViewModel.cs`

ViewModel für die Einstellungsseite. **Das ist der Ort, wo neue Feature-Flag-Einstellungen exponiert werden sollen.**

**Abhängigkeiten:**
- `AppEinstellungService` (_einstellungService) **[zentral für Settings-Persistierung]**
- `ArbeitsverzeichnisSettingsService` (_arbeitsverzeichnisService)
- `DarkModeService` (_darkModeService)
- `IPluginManager` (_pluginManager)
- `PluginActivationService` (_pluginActivationService)
- `PluginSettingsService` (_pluginSettingsService)
- `PromptVorlagenService` (_promptVorlagenService)
- `ILogger<SettingsViewModel>` (_logger)

**Relevante Properties (für Settings-Pattern):**

| Property | Typ | Beschreibung |
|----------|-----|-------------|
| `Arbeitsverzeichnis` | `string?` | Arbeitsverzeichnis für Repository-Klone |
| `DesignMode` | `string` | Aktuell gewählter Design-Modus |
| `DesignModes` | `IEnumerable<string>` | Alle verfügbaren Design-Modi |
| `DefaultKiPlugin` | `string?` | Standard-KI-Plugin-Prefix |
| `DefaultScmPlugin` | `IGitPlugin?` | Aktuell gewähltes Standard-SCM-Plugin |
| `BenachrichtigungsModus` | `BenachrichtigungsModus` | Benachrichtigungsmodus (Sound, PopUp, Stumm, etc.) |
| `IsLoading` | `bool` | Werden Einstellungen geladen? |
| `FehlerMeldung` | `string?` | Fehlermeldung |
| `ErfolgsMeldung` | `string?` | Erfolgsmeldung nach dem Speichern |
| `PromptVorlagen` | `ObservableCollection<PromptVorlageEntry>` | Editierbare Promptvorlagen |

**Befehle (Commands):**
- `LadenCommand` – Lädt alle Einstellungen
- `SpeichernCommand` – Speichert alle Einstellungen via `_einstellungService`
- `VerwerfenCommand` – Verwirft nicht gespeicherte Einstellungen
- `ScmPluginSelectedCommand` – SCM-Plugin-Wahl
- `KiPluginSelectedCommand` – KI-Plugin-Wahl
- `PluginSelectedCommand` – Plugin-Register-Wahl
- `IdePluginSelectedCommand` – IDE-Plugin-Wahl
- `IdePluginMoveUpCommand` – IDE-Plugin-Reihenfolge ändern

**Persistierungs-Pattern:** Verwendet `AppEinstellungService.SetSettingAsync()`, `SetBoolSettingAsync()`, etc. zum Speichern. Dies ist das zu nachahmende Pattern für neue Feature-Flag-Einstellungen.

**Wichtig:** Dieses ViewModel zeigt bereits ein etabliertes Pattern für:
1. **Laden** von Einstellungen (Befehl, asynchron)
2. **Speichern** von Einstellungen (Befehl, asynchron via `AppEinstellungService`)
3. **Darstellen** von Binär-Schaltern (DesignMode, Benachrichtigungsmodus als Enums/Properties)

---

## TaskDetailView / SettingsView
Dateien: 
- `src\Softwareschmiede.App\Views\TaskDetailView.xaml` (nicht vollständig gelesen)
- `src\Softwareschmiede.App\Views\SettingsView.xaml`
- `src\Softwareschmiede.App\Views\SettingsView.xaml.cs`

**Relevante UI-Muster:**

### SettingsView (Code-Behind)
- `LadenCommand` wird on `Loaded`-Event automatisch ausgelöst
- Plugin-Selektions-Handler leitet Wahl je nach Typ an passende Commands weiter
- Pattern für bedingte Sichtbarkeit (z. B. `IsScmKiPluginContentVisible` vs. `IsIdePluginContentVisible`)

**Pattern zum Nachahmen für Feature-Flag-Schalter:**
1. Property in SettingsViewModel (z. B. `IsAutonomAufgabenEnabled: bool`)
2. XAML-Binding: `IsChecked="{Binding IsAutonomAufgabenEnabled}"`
3. Speicherung via `AppEinstellungService.SetBoolSettingAsync(schluessel, wert)`
4. Laden via `AppEinstellungService.GetBoolSettingAsync(schluessel)` in `LadenCommand`-Ausführung
