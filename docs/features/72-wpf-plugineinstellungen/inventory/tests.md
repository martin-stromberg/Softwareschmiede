# Tests

## Testklassen

### `PluginSettingsServiceIntegrationTests`
Datei: `src/Softwareschmiede.Tests/ServiceIntegration/PluginSettingsServiceIntegrationTests.cs`

E2E-Tests für `PluginSettingsService` mit In-Memory Credential Store.

| Testmethode | Was wird getestet? |
|---|---|
| `SetValue_SpeichertWert_UndGetValue_LaeadtIhn()` | Plugin-Einstellung wird gespeichert und kann geladen werden |
| `HasValue_GibtFalse_OhneGespeichertenWert()` | `HasValue` gibt False zurück für nicht gespeicherte Werte |
| `HasValue_GibtTrue_NachSpeichern()` | `HasValue` gibt True zurück nach Speichern |
| `DeleteValue_EntferntGespeichertenWert()` | Gespeicherte Werte können gelöscht werden |
| `GetValue_GibtNull_OhneGespeichertenWert()` | `GetValue` gibt null zurück für nicht gespeicherte Werte |

Abhängigkeiten:
- `InMemoryCredentialStore` (Hilfklasse im selben File)
- `FakeSettingsPlugin` (Hilfklasse im selben File)

---

## Hilfsmethoden und Fake-Klassen

### `InMemoryCredentialStore`
Datei: `src/Softwareschmiede.Tests/ServiceIntegration/PluginSettingsServiceIntegrationTests.cs`

In-Memory Implementierung von `ICredentialStore` für Tests.

| Methode | Beschreibung |
|---|---|
| `GetCredential(key)` | Gibt Wert aus In-Memory Dictionary zurück oder null |
| `SetCredential(key, value)` | Speichert Wert in In-Memory Dictionary |
| `DeleteCredential(key)` | Entfernt Wert aus Dictionary |

### `FakeSettingsPlugin`
Datei: `src/Softwareschmiede.Tests/ServiceIntegration/PluginSettingsServiceIntegrationTests.cs`

Fake-Plugin-Implementierung für Tests. Implementiert `IPlugin`.

| Eigenschaft | Wert |
|---|---|
| `PluginName` | Von Constructor-Parameter |
| `PluginPrefix` | Von Constructor-Parameter |
| `PluginType` | `PluginType.DevelopmentAutomation` |

| Methode | Rückgabewert |
|---|---|
| `GetSettingGroups()` | Leeres Array |

---

## Nicht vorhanden (erforderlich für Feature 72)

- `SettingsViewModelTests` — Tests für die neuen Commands und Properties in `SettingsViewModel`:
  - Test für `ScmPluginSelectedCommand`: Plugin-Wechsel lädt korrekten Setting-Groups
  - Test für `KiPluginSelectedCommand`: Plugin-Wechsel lädt korrekten Setting-Groups
  - Test für `SpeichernAsync`: Speichert Standard-Plugins (`DefaultScmPlugin`, `DefaultKiPlugin`) und alle Einstellungswerte korrekt
  - Test für `LadenAsync`: Lädt Standard-Plugins und Plugin-Einstellungen korrekt

- Integration-Tests für SettingsView UI-Rendering (evtl. mit WPF/XAML-Test-Framework)
