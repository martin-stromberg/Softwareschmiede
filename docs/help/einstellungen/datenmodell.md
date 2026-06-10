# Einstellungen — Datenmodell

## Entitäten

### `AppEinstellung`

Schlüssel-Wert-Tabelle für anwendungsweite Einstellungen.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Key` | `string` | Einstellungsschlüssel (Primärschlüssel) |
| `Value` | `string?` | Einstellungswert |

Bekannte Schlüssel:

| Schlüssel | Beschreibung |
|-----------|--------------|
| `WorkDir` | Lokales Arbeitsverzeichnis für Repository-Klons |
| `DefaultScmPlugin` | Prefix des Standard-SCM-Plugins |
| `DefaultKiPlugin` | Prefix des Standard-KI-Plugins |

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
