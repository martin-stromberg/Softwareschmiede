# Einstellungen — Datenmodell

## Entitäten

### `AppEinstellung`

Schlüssel-Wert-Tabelle für anwendungsweite Einstellungen.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Key` | `string` | Einstellungsschlüssel (Primärschlüssel) |
| `Value` | `string?` | Einstellungswert |

Bekannte Schlüssel:

| Schlüssel | Typ | Beschreibung |
|-----------|-----|--------------|
| `WorkDir` | string | Lokales Arbeitsverzeichnis für Repository-Klons |
| `DefaultScmPlugin` | string | Prefix des Standard-SCM-Plugins |
| `DefaultKiPlugin` | string | Prefix des Standard-KI-Plugins |
| `DarkModeEnabled` | bool | Dark Mode aktiviert (`"true"` / `"false"`) |
| `WindowPosition.X` | int | Fenster-X-Position (Pixel) |
| `WindowPosition.Y` | int | Fenster-Y-Position (Pixel) |
| `WindowPosition.Width` | int | Fensterbreite (Pixel) |
| `WindowPosition.Height` | int | Fensterhöhe (Pixel) |
| `NotificationMode` | Enum | Benachrichtigungsmodus (`Deaktiviert` / `Banner` / `Ton`) |
| `NotificationAudioPath` | string | Pfad zur Benachrichtigungs-Audiodatei (MP3/WAV) |
| `LogLevel` | Enum | Logging-Granularität (`Debug` / `Information`) |

### `BenachrichtigungsEinstellung`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Primärschlüssel |
| `Modus` | `BenachrichtigungsModus` | Immer / Nie / NurBeiFehler |
| `Kanal` | `BenachrichtigungsKanal` | Kanal (z.B. Audio, System) |

### `BenachrichtigungsAudioDatei`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Primärschlüssel |
| `Pfad` | `string` | Dateipfad zur Audiodatei |
| `Aktiv` | `bool` | Ob die Datei verwendet wird |

### `BenachrichtigungsDispatchLog`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Primärschlüssel |
| `AufgabeId` | `Guid` | FK → Aufgabe |
| `Zeitstempel` | `DateTimeOffset` | Zeitpunkt der Benachrichtigung |
| `Entscheidung` | `BenachrichtigungsEntscheidung` | Gesendet / Unterdrückt |
| `Kanal` | `BenachrichtigungsKanal` | Genutzter Kanal |

### `PluginKonfiguration`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Primärschlüssel |
| `PluginPrefix` | `string` | FK → Plugin-Präfix |
| `FieldKey` | `string` | Einstellungsschlüssel des Felds |
| `Value` | `string?` | Verschlüsselter Wert (Windows Credential Store) |
