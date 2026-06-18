# Einstellungen — Installation und Konfiguration

## Arbeitsverzeichnis einrichten

1. In der Seitenleiste auf **Einstellungen** klicken.
2. Registerkarte **Allgemein** öffnen.
3. Im Abschnitt „Arbeitsverzeichnis" einen absoluten Pfad eingeben, z.B. `C:\Dev\Workdir`.
4. Auf **Speichern** klicken.

Das Verzeichnis muss existieren und beschreibbar sein. Unterverzeichnisse werden von der Anwendung automatisch angelegt (`softwareschmiede/<aufgabe-id>/`).

## KI-Kontext-Limits konfigurieren

Die Grenzen für die automatische Kontextkomprimierung können in `appsettings.json` überschrieben werden:

| Parameter | Standardwert | Beschreibung |
|-----------|-------------|--------------|
| `KiKontext:SoftLimitChars` | `12000` | Ab dieser Zeichenzahl wird Komprimierung ausgelöst |
| `KiKontext:HardLimitChars` | `20000` | Ab dieser Zeichenzahl erscheint Warnung |

Beispiel:

```json
{
  "KiKontext": {
    "SoftLimitChars": 15000,
    "HardLimitChars": 25000
  }
}
```

## Benachrichtigungen konfigurieren

Benachrichtigungseinstellungen werden global in der Einstellungsseite verwaltet.

| Modus | Beschreibung |
|-------|-------------|
| `Deaktiviert` | Keine Benachrichtigungen |
| `Banner` | Windows-Toast-Benachrichtigung nach Statuswechsel |
| `Ton` | Audiodatei abspielen (MP3 oder WAV) |

Audiodateien für Ton-Benachrichtigungen werden über den Einstellungsschlüssel `NotificationAudioPath` hinterlegt. Die Wiedergabe erfolgt über den WPF-`MediaPlayer` — kein separates Backend erforderlich.

## Design (Dark Mode) konfigurieren

Das Design wird in den Einstellungen geändert:

1. In der Seitenleiste auf **Einstellungen** klicken.
2. Registerkarte **Allgemein** öffnen.
3. Im Abschnitt „Design" die gewünschte Option aus der Dropdown-Liste wählen:
   - **Hell** — helles Erscheinungsbild
   - **Dunkel** — dunkles Erscheinungsbild
4. Auf **Speichern** klicken.

Die Änderung wird sofort wirksam und beim nächsten Start automatisch beibehalten.

## Überprüfung

Nach dem Speichern des Arbeitsverzeichnisses zeigt die Einstellungsseite den aufgelösten Pfad an. Ist ein Fallback aktiv, erscheint ein Hinweis mit dem Reason-Code.
