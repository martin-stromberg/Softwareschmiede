# Datenmodell

## `PluginKonfiguration`
Datei: `src/Softwareschmiede/Domain/Entities/PluginKonfiguration.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige ID der Plugin-Konfiguration |
| `PluginTyp` | `string` | Typ des Plugins (z.B. "GitHub", "GitHubCopilot") |
| `PluginKategorie` | `PluginKategorie` (enum) | Kategorie: Git oder Ki |
| `AnzeigeName` | `string` | Benutzerfreundlicher Name des Plugins |
| `CredentialStoreKey` | `string` | Schlüssel im Credential Store für API-Token/Passwort |
| `BaseUrl` | `string?` | Optionale Basis-URL für Self-Hosted-Instanzen |
| `Aktiviert` | `bool` | **Aktivierungsstatus des Plugins** (Standardwert: `true`) |

### Hinweise
- Das `Aktiviert`-Feld existiert bereits, wird jedoch **nicht in der Filterlogik berücksichtigt**.
- Die Entität ist auf **einzelne Plugin-Konfigurationen** bezogen (nicht auf die Plugins selbst, die zur Laufzeit geladen werden).

## `AppEinstellung`
Datei: `src/Softwareschmiede/Domain/Entities/AppEinstellung.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige ID der Einstellung |
| `Schluessel` | `string` | Maschinenlesbarer Schlüssel (z.B. "repositories.workdir") |
| `Wert` | `string?` | Optionaler Wert; null/leer bedeutet Default verwenden |
| `AktualisiertAm` | `DateTimeOffset` | Zeitpunkt der letzten Aktualisierung |

### Hinweise
- Aktuell wird `AppEinstellung` als einfacher Key-Value-Store genutzt (über `AppEinstellungService`).
- Der Aktivierungsstatus der Plugins wird **nicht persistiert** — dies muss ggf. über eine neue Spalte oder eine separate Tabelle hinzugefügt werden.
