# Einstellungen — Beschreibung

## Zweck

Die Einstellungsseite (`/einstellungen`) ermöglicht die zentrale Konfiguration der Anwendung ohne Code-Änderungen. Sie deckt vier Bereiche ab:

1. **Plugin-Einstellungen** — Tokens und Zugangsdaten für SCM- und KI-Plugins
2. **Standard-Plugins** — Welches Plugin bei neuen Aufgaben standardmäßig genutzt wird
3. **Arbeitsverzeichnis** — Wo geklonte Repositories lokal abgelegt werden
4. **Benachrichtigungen** — Ob und wie der Anwender bei abgeschlossenen KI-Läufen benachrichtigt wird
5. **Automatisches Herunterfahren** — Ob das System nach dem letzten KI-Lauf heruntergefahren wird

## Funktionsweise

### Arbeitsverzeichnis

Das Arbeitsverzeichnis bestimmt, wo die Softwareschmiede lokale Repository-Klons anlegt. Der Pfad wird als `AppEinstellung` in der SQLite-Datenbank gespeichert. Ist kein Verzeichnis konfiguriert oder ist der Pfad ungültig, greift ein Fallback auf das Temp-Verzeichnis. Der Fallback wird im Aufgabenprotokoll protokolliert.

### Benachrichtigungen

Der `KiAufgabenBenachrichtigungsHub` publiziert `KiAufgabenAbschlussEreignis` nach jedem KI-Lauf. Der `BenachrichtigungsAuditService` entscheidet, ob und auf welchem Kanal eine Benachrichtigung ausgelöst wird. Jede Benachrichtigungsentscheidung wird als `BenachrichtigungsDispatchLog` persistiert.

### Automatisches Herunterfahren

Der `AutoShutdownOrchestrator` lauscht auf `IRunningAutomationStatusSource.RunningCountChanged`. Wenn die Anzahl aktiver Läufe auf 0 sinkt und die Einstellung aktiv ist, wird `ISystemShutdownService.Shutdown` aufgerufen.

## Einschränkungen

- Plugin-Einstellungen werden im Windows Credential Store gespeichert — nicht plattformübergreifend portierbar.
- Arbeitsverzeichnis-Änderungen wirken sich erst auf neue Aufgaben aus; laufende Aufgaben behalten ihren Klonpfad.
