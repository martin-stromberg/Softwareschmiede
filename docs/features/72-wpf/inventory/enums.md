# Enums

## `AufgabeStatus`
Datei: `src/Softwareschmiede/Domain/Enums/AufgabeStatus.cs`

| Wert | Beschreibung |
|---|---|
| `Offen` | Aufgabe wurde erstellt und wartet auf Bearbeitung |
| `InBearbeitung` | Aufgabe wird manuell bearbeitet |
| `KiAktiv` | KI-Agent ist aktiv und bearbeitet die Aufgabe |
| `TestsLaufen` | Automatisierte Tests werden ausgeführt |
| `Abgeschlossen` | Aufgabe wurde erfolgreich abgeschlossen |
| `Fehlgeschlagen` | Aufgabe ist fehlgeschlagen |
| `Archiviert` | Aufgabe wurde archiviert und ist nicht mehr aktiv |

**Mapping zur Anforderung:**
- Anforderung spricht von: Neu, Arbeitsverzeichnis eingerichtet, Gestartet, In Arbeit, Wartend, Beendet, Archiviert
- Code hat: Offen, InBearbeitung, KiAktiv, TestsLaufen, Abgeschlossen, Fehlgeschlagen, Archiviert
- Dies ist eine Abweichung von der Anforderung und erfordert Klärung

## `ProjektStatus`
Datei: `src/Softwareschmiede/Domain/Enums/ProjektStatus.cs`

| Wert | Beschreibung |
|---|---|
| `Aktiv` | Projekt ist aktiv in Bearbeitung |
| `Archiviert` | Projekt wurde archiviert |

## `ProtokollTyp`
Datei: `src/Softwareschmiede/Domain/Enums/ProtokollTyp.cs`

| Wert | Beschreibung |
|---|---|
| `Prompt` | Prompt, der an den KI-Agenten gesendet wurde |
| `KiAntwort` | Antwort des KI-Agenten |
| `StatusUebergang` | Statusübergang der Aufgabe |
| `TestErgebnis` | Ergebnis eines Testlaufs |
| `GitAktion` | Git-Aktion (Commit, Push, Branch, etc.) |

## `PluginKategorie`
Datei: `src/Softwareschmiede/Domain/Enums/PluginKategorie.cs`

| Wert | Beschreibung |
|---|---|
| `Git` | Git-Provider Plugin (z.B. GitHub, GitLab) |
| `Ki` | KI-Plugin (z.B. GitHub Copilot, Claude CLI) |

## `BenachrichtigungsModus`
Datei: `src/Softwareschmiede/Domain/Enums/BenachrichtigungsModus.cs`

| Wert | Beschreibung |
|---|---|
| `Deaktiviert` (0) | Benachrichtigungen sind deaktiviert |
| `NurAufgabenseite` (1) | Benachrichtigungen nur auf Aufgabenseite |
| `Global` (2) | Globale Benachrichtigungen |

**Hinweis:** Anforderung spricht von Immer/Nie/NurBeiFehler. Implementierung hat Deaktiviert/NurAufgabenseite/Global. Dies ist eine Abweichung.

## `BenachrichtigungsKanal`
Datei: `src/Softwareschmiede/Domain/Enums/BenachrichtigungsKanal.cs`

| Wert | Beschreibung |
|---|---|
| `Toast` (0) | Toast-Benachrichtigungen |
| `Ton` (1) | Audio-Benachrichtigungen |

**Hinweis:** Anforderung spricht von Audio/System. Implementierung hat Toast/Ton. Dies ist eine Abweichung.

## `BenachrichtigungsEntscheidung`
Datei: `src/Softwareschmiede/Domain/Enums/BenachrichtigungsEntscheidung.cs`

| Wert | Beschreibung |
|---|---|
| `Gesendet` | Benachrichtigung wurde gesendet |
| `Unterdrückt` | Benachrichtigung wurde unterdrückt |

## `DiffType`
Datei: `src/Softwareschmiede/Domain/Enums/DiffType.cs`

| Wert | Beschreibung |
|---|---|
| `Full` | Unified-View (einzelner Stream mit +/- Präfixen) |
| `SideBySide` | Side-by-Side-View (zwei Spalten nebeneinander) |
| `Split` | Split-View (mit Gutter zwischen Original und Neu) |

## `DiffResultStatus`
Datei: `src/Softwareschmiede/Domain/Enums/DiffResultStatus.cs`

| Wert | Beschreibung |
|---|---|
| `Pending` | Diff-Generierung steht aus (in Warteschlange) |
| `Generated` | Diff wurde erfolgreich generiert |
| `Cached` | Diff wurde aus Cache geladen |
| `Error` | Fehler bei der Diff-Generierung |

## `PluginType`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/PluginType.cs`

| Wert | Beschreibung |
|---|---|
| `SourceCodeManagement` | Source-Code-Management-Plugin |
| `DevelopmentAutomation` | Development-Automation-Plugin (KI) |

## `TestStatus`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/TestStatus.cs`

| Wert | Beschreibung |
|---|---|
| `Bestanden` | Test wurde erfolgreich bestanden |
| `Fehlgeschlagen` | Test ist fehlgeschlagen |
| `Uebersprungen` | Test wurde übersprungen |

## `FolgeanweisungsKontextmodus`
Datei: `src/Softwareschmiede/Domain/Enums/FolgeanweisungsKontextmodus.cs`

Enum existiert und wird in `KiAusfuehrungsService` verwendet, Dokumentation erforderlich.

## `VerwerfenAktion`
Datei: `src/Softwareschmiede/Domain/Enums/VerwerfenAktion.cs`

Enum existiert und wird in `AufgabeService.VerwerfenAsync()` verwendet, Dokumentation erforderlich.
