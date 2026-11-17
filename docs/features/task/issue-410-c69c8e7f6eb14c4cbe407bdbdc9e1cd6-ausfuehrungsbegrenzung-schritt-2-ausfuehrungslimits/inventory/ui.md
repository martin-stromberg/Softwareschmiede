# UI-Bestand

## `SettingsView.xaml` (`src/Softwareschmiede.App/Views/SettingsView.xaml`)

- Tab „Automatisierung" (Header Z. 347-353): drei `Expander` — `AufgabenAusfuehrenExpander` (Z. 357; `IsScmMonitoringEnabled`-Checkbox, `Ueberwachungsmodus`-ComboBox, `TriggerKeywordBereich`, `DelayKeywordBereich`, `PromptAnhaengeBereich`), `AufgabenBeendenExpander` (Z. 502), `ProzessablaeufeExpander` (Z. 566). → Platz des globalen Limits: Expander „Aufgaben ausführen" (neues eigenes Feld, nicht an den Monitoring-Schalter gekoppelt).
- Tab „Plugins" (Z. 627-796): links Standard-Comboboxen + Aktivierungslisten (`{PluginPrefix}.Eintrag` als AutomationName je Eintrag); rechts `PluginDetailPanel` (`src/Softwareschmiede.App/Controls/PluginDetailPanel.xaml`) mit `SettingsSource = SelectedPluginSettings` (`PluginSettingGroupsItemTemplate`, Z. 123-149 → Gruppe: Label + `ContentControl` mit `FieldTemplateSelector` + Description).
- `PluginSettingFieldTemplateSelector` (Z. 8-86): Text/Secret/Url/Integer/Boolean/Enum/FilePath/CommandLineParameters-Templates. `AutomationProperties.Name="{Binding Field.Key}"` nur bei Text und Enum — **Integer-Template braucht denselben Namen für E2E** (Z. 34-40).

## `SettingsViewModel` (`src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs`)

- `LadenAsync` (Z. 502): lädt alle `AppEinstellung`-Werte + `LadePluginAktivierungAsync` + `LadeIdePluginAktivierungAsync` + `LadePromptVorlagenAsync`; `KiPlugins` wird Z. 522 gesetzt. Plugin-Limitwerte können hier für alle `KiPlugins`-Prefixe in einer `GetSettingsAsync`-Abfrage vorgeladen werden (`_kiPluginParallelLimits`-Feld), da `LadePluginEinstellungen` synchron ist.
- `SpeichernAsync` (Z. 582): `ValidierePflichtfelder()` → Plugin-Aktivierung → `SpeicherePluginEinstellungen(_selectedPlugin?.Plugin, _selectedPluginSettings)` (**nur selektiertes Plugin-Panel!**) → Skalar-Einstellungen via `SetBoolSettingAsync`/`SetIntSettingAsync`/`SetSettingAsync`.
- `LadePluginEinstellungen(IPlugin)` (Z. 845): `plugin.GetSettingGroups()` → `PluginSettingGroupEntry`s (Werte via `PluginSettingsService.GetValue`). → Host-Gruppe für KI-Plugins hier anhängen (Feldtyp `PluginSettingFieldType.Integer`).
- `ValidierePflichtfelderFuerSettings` (Z. 993): Pflicht + Integer-`TryParse` + Enum-Optionen — erlaubt negative Zahlen; Limitfeld braucht eigene Regel (leer oder ≥ 1).
- `ScmTaskCompletionWaitMinutes`-Muster (string-Property, Trim, `int.TryParse`, Validierung mit deutscher Fehlermeldung) ist die Vorlage für das globale Limitfeld.

## `ScmEventQueueViewModel` (`src/Softwareschmiede.App/ViewModels/ScmEventQueueViewModel.cs`)

- Zeigt `item.Reason` als Wartegrund in der Queue (Z. 376-404; StatusText-Mapping). Ein neuer Limit-Wartegrund erscheint ohne weitere Änderung. `CheckStatusCommand` → `DispatchAsync` (Z. 71), `OpenFromQueueAsync` (Z. 269), `RetryAsync`/`ResendAsync`/`CancelAsync` — alles durch `ScmEventOrchestrator` → Startfreigabe greift automatisch.

## E2E-Hilfs-View
`src/Softwareschmiede.Tests/E2E/Views/SettingsView.cs`: `SwitchTab`, `OpenAufgabenAusfuehren`, `SaveSettings` (wartet „Einstellungen gespeichert."), `SetPluginEnabled`/`IsPluginEnabled` (Klick auf `{prefix}.Eintrag`), `SelectDefaultKiPlugin`, `OpenTestDbContext` (aus `WpfTestBase`, siehe `E2E_SettingsKiPluginPersistence.cs`). Bestehender Automatisierungs-E2E: `E2E_SettingsFeatureFlags.cs` (Struktur + Speichern-Neuladen-Stichprobe).
