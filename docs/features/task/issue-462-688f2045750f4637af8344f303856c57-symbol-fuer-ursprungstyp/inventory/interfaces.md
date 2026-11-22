# Interfaces — Symbol für Auftragsart auf den Aufgabenkacheln (Issue #462)

## `IPlugin`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IPlugin.cs`

Gemeinsamer Plugin-Vertrag; der `PluginSymbol`-Mechanismus ist das etablierte Muster für Unicode-Glyphen auf der Aufgabenkachel.

| Member | Parameter | Rückgabewert | Zweck |
|--------|-----------|--------------|-------|
| `FallbackSymbol` | — | `const string = "◻"` | Fallback-Glyph für Plugins ohne eigenes `PluginSymbol` und für nicht auflösbare Plugin-Referenzen (Z. 17) |
| `PluginName` | — | `string` | Anzeigename (Tooltip-/HelpText-Text „SCM: {0}" / „KI: {0}") (Z. 20) |
| `PluginPrefix` | — | `string` | Credential-Store-/Auflösungs-Prefix (Z. 26) |
| `GetSettingGroups()` | — | `IReadOnlyList<PluginSettingGroup>` | Einstellungsfelder (Z. 32) |
| `PluginType` | — | `PluginType` | Typ zur Zuordnung im `IPluginManager` (Z. 35) |
| `PluginSymbol` | — | `string` | Unicode-Glyph für die Symbolzeile; **Default-Interface-Member** `=> FallbackSymbol` (binärkompatibel für bereits kompilierte Plugin-DLLs, Z. 42). `GitPluginBase`/`CliAiPluginBase` deklarieren es `virtual`; alle acht Repo-Plugins überschreiben es (`📁`, `🧪`, `✈`, `🐙`, `🤖`, `◈`, `✳`, `🪣`) |

## `IPluginManager`
Datei: `src/Softwareschmiede/Domain/Interfaces/IPluginManager.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetSourceCodeManagementPlugins()` | — | `IReadOnlyList<IGitPlugin>` | SCM-Plugins für `ResolvePlugin` (Z. 7) |
| `GetDevelopmentAutomationPlugins()` | — | `IReadOnlyList<IAiPlugin>` | KI-Plugins für `ResolvePlugin` (Z. 10) |

## `IActiveWorkItemsService`
Datei: `src/Softwareschmiede/Application/Services/IActiveWorkItemsService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetActiveWorkItemsAsync` | `CancellationToken` | `Task<List<WorkItem>>` | Datenquelle der aktiven Aufgabenliste (Started/Waiting, Top 20, `Include`s `Project`/`GitRepository`); implementiert von `WorkItemService` (Z. 1208), aufgerufen in `RefreshActiveWorkItemsAsync` |

## Converter-Verträge (XAML-gebunden)

| Klasse | Datei | Vertrag |
|--------|-------|---------|
| `NullOrEmptyToVisibilityConverter` | `src/Softwareschmiede.App/Converters/AppConverters.cs` Z. 87 | `IValueConverter`: `null`/leerer `string` → `Collapsed`. In `App.xaml` Z. 18 als `StaticResource` registriert; steuert Badge-`Border` (`KindLabel`) und Plugin-Glyph-`TextBlock`s |
| `EnumToBoolConverter` | `src/Softwareschmiede.App/Converters/AppConverters.cs` | `IValueConverter` + `ConvertBack`: RadioButton-`IsChecked` ↔ `WorkItemListMode` via `ConverterParameter` |
| `AiExecutionStatusSymbolConverter` / `AiExecutionStatusConverter` | `src/Softwareschmiede.App/Converters/` | Status-Glyph (▶/⏸/⏳/✓) bzw. Volltext für Tooltip/HelpText — unverändert laut Scope |
