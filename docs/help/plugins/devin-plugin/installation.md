← [Zurück zur Übersicht](index.md)

# Devin-Plugin - Installation und Konfiguration

## Voraussetzungen

| Komponente | Erforderlich | Beschreibung |
|-----------|-----------|--------------|
| Devin CLI | Ja | Lokaler CLI-Agent mit dem Aufruf `devin` |
| Devin-Konto mit CLI-Zugriff | Ja | Anmeldung erfolgt in der Devin CLI |
| Git-Arbeitsverzeichnis | Ja | Die Softwareschmiede startet Devin im lokalen Aufgaben-Repository |

## Devin CLI installieren

Installiere die Devin CLI gemaess offizieller Dokumentation: https://docs.devin.ai/cli.

Unter Windows kann die Installation per Installer oder in PowerShell erfolgen:

```powershell
irm https://static.devin.ai/cli/setup.ps1 | iex
```

Nach der Installation muss der Befehl `devin` in PowerShell, Windows Terminal oder Git Bash verfuegbar sein:

```powershell
devin --version
```

## Anmeldung

Die Authentifizierung gehoert zur Devin CLI und wird nicht in der Softwareschmiede konfiguriert.

```powershell
devin auth login
```

Folge anschliessend den Anweisungen der CLI. Den Status kannst du mit folgendem Befehl pruefen:

```powershell
devin auth status
```

Die Softwareschmiede speichert fuer Devin keine Tokens, API Keys oder Authentifizierungs-Umgebungsvariablen.

## Plugin-Einstellungen

Oeffne in der Softwareschmiede **Einstellungen -> Plugins** und waehle **Devin CLI**.

| Feld | Credential-Key | Beschreibung |
|------|----------------|--------------|
| Devin CLI Pfad | `Softwareschmiede.Devin.ExecutablePath` | Optionaler absoluter Pfad zur `devin`-Executable. Leer bedeutet: `devin` wird ueber `PATH` aufgeloest. |
| Kommandozeilenparameter | `Softwareschmiede.Devin.CommandLineParameters` | Optionale Zusatzparameter fuer jeden Devin-Aufruf. |

Der `ExecutablePath` ist nur noetig, wenn `devin` nicht ueber `PATH` gefunden wird oder bewusst eine bestimmte Installation verwendet werden soll.

## CLI-Parameter

Das Plugin startet ohne Zusatzparameter eine interaktive Devin-Sitzung:

```powershell
devin
```

Ein Prompt oder Devin-Optionen koennen ueber die Aufgaben-Ausfuehrung und ueber das Feld **Kommandozeilenparameter** an die CLI weitergegeben werden. Relevante Devin-Parameter sind unter anderem:

| Parameter | Bedeutung |
|-----------|-----------|
| `<prompt>` | Initiale Nachricht fuer eine neue Sitzung |
| `--continue`, `-c` | Letzte Sitzung im aktuellen Verzeichnis fortsetzen |
| `--resume <SESSION_ID>`, `-r <SESSION_ID>` | Bestimmte Sitzung fortsetzen |
| `--print [PROMPT]`, `-p [PROMPT]` | Antwort ausgeben und Prozess beenden |
| `--prompt-file <FILE>` | Initialen Prompt aus Datei laden |
| `--model <MODEL>` | Modell fuer die Sitzung setzen |
| `--permission-mode <MODE>` | Berechtigungsmodus der Devin CLI setzen |

Bei interaktiven Laeufen bleibt die eingebettete Terminaloberflaeche die Schnittstelle fuer Ein- und Ausgaben.
