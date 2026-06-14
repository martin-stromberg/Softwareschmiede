# Services

## `PluginSettingsService`
Datei: `src/Softwareschmiede/Application/Services/PluginSettingsService.cs`

Service zum Lesen und Schreiben von Plugin-Einstellungen über den `ICredentialStore`. Schlüssel werden als `<PluginPrefix>.<FieldKey>` gespeichert.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetAllPlugins(gitPlugins, kiPlugins)` | `public` | Gibt alle konfigurierten Plugins zurück (Git- und KI-Plugins kombiniert) |
| `GetValue(plugin, field)` | `public` | Gibt den gespeicherten Wert für ein Einstellungsfeld zurück (Key: `<PluginPrefix>.<FieldKey>`) |
| `SetValue(plugin, field, value)` | `public` | Speichert den Wert für ein Einstellungsfeld |
| `DeleteValue(plugin, field)` | `public` | Löscht den gespeicherten Wert für ein Einstellungsfeld |
| `HasValue(plugin, field)` | `public` | Gibt an, ob für ein Feld bereits ein Wert gespeichert ist |
| `BuildKey(plugin, field)` | `private` | Baut den vollständigen Schlüssel aus Plugin-Prefix und Feld-Key |

Abhängigkeiten:
- `ICredentialStore` für Persistierung

---

## `AppEinstellungService`
Datei: `src/Softwareschmiede/Application/Services/AppEinstellungService.cs`

Generischer Service zum Lesen und Schreiben von Anwendungseinstellungen (Key-Value-Paare) in der Datenbank.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetSettingAsync(schluessel, ct)` | `public` | Liest den Wert einer Einstellung. Gibt `null` zurück, wenn kein Wert gespeichert ist |
| `GetIntSettingAsync(schluessel, ct)` | `public` | Liest eine Einstellung als Integer |
| `GetBoolSettingAsync(schluessel, ct)` | `public` | Liest eine Einstellung als Boolean |
| `SetSettingAsync(schluessel, wert, ct)` | `public` | Speichert oder überschreibt eine Einstellung |
| `SetIntSettingAsync(schluessel, wert, ct)` | `public` | Speichert eine Integer-Einstellung |
| `SetBoolSettingAsync(schluessel, wert, ct)` | `public` | Speichert eine Boolean-Einstellung |
| `GetWindowGeometryAsync(ct)` | `public` | Liest alle Fenstergeometrie-Einstellungen in einer einzigen Datenbankabfrage |
| `SetWindowGeometryAsync(geometry, ct)` | `public` | Speichert alle Fenstergeometrie-Einstellungen in einer Transaktion |

Definierte Konstanten-Schlüssel:
- `WindowPositionXKey = "window.position.x"`
- `WindowPositionYKey = "window.position.y"`
- `WindowWidthKey = "window.size.width"`
- `WindowHeightKey = "window.size.height"`
- `DesignModeKey = "ui.designmode.name"`
- `DefaultKiPluginKey = "ki.plugin.default"`
- `LogLevelKey = "logging.level"`

Nicht vorhanden (erforderlich für Feature 72):
- Konstante `DefaultScmPluginKey` für das Standard-SCM-Plugin

Abhängigkeiten:
- `SoftwareschmiededDbContext` für Datenbanktransaktionen
