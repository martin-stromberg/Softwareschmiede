# Einstellungen — Beschreibung

## Zweck

Die Einstellungsseite ermöglicht die zentrale Konfiguration der Anwendung ohne Code-Änderungen. Sie deckt folgende Bereiche ab:

1. **Plugin-Einstellungen** — Tokens und Zugangsdaten für SCM- und KI-Plugins
2. **Standard-Plugins** — Welches Plugin bei neuen Aufgaben standardmäßig genutzt wird
3. **Arbeitsverzeichnis** — Wo geklonte Repositories lokal abgelegt werden
4. **Benachrichtigungen** — Ob und wie der Anwender bei abgeschlossenen KI-Läufen benachrichtigt wird
5. **Erscheinungsbild** — Dark Mode ein-/ausschalten

## Funktionsweise

### Arbeitsverzeichnis

Das Arbeitsverzeichnis bestimmt, wo die Softwareschmiede lokale Repository-Klons anlegt. Der Pfad wird als `AppEinstellung` in der SQLite-Datenbank gespeichert. Ist kein Verzeichnis konfiguriert oder ist der Pfad ungültig, greift ein Fallback auf das Temp-Verzeichnis. Der Fallback wird im Aufgabenprotokoll protokolliert.

### Benachrichtigungen

Der `KiAufgabenBenachrichtigungsHub` publiziert `KiAufgabenAbschlussEreignis` nach jedem KI-Lauf. Der `BenachrichtigungsAuditService` entscheidet, ob und auf welchem Kanal eine Benachrichtigung ausgelöst wird. Jede Benachrichtigungsentscheidung wird als `BenachrichtigungsDispatchLog` persistiert.

### Dark Mode

Der `DarkModeService` wechselt das WPF-`ResourceDictionary` zwischen `LightTheme.xaml` und `DarkTheme.xaml`. Der Wechsel erfolgt sofort ohne Neustart. Die Einstellung wird in der `AppEinstellung`-Tabelle gespeichert und beim nächsten Start automatisch angewendet.

Das Design wird in den Einstellungen über die Dropdown-Liste **Design** (Registerkarte „Allgemein") gesteuert. Verfügbare Optionen sind „Hell" und „Dunkel".

### Fenstergeometrie

Position und Größe des Hauptfensters werden automatisch gespeichert, wenn die Anwendung geschlossen wird (`WindowPosition.X`, `WindowPosition.Y`, `WindowPosition.Width`, `WindowPosition.Height`). Beim nächsten Start wird die gespeicherte Geometrie wiederhergestellt.

### Automatisches Herunterfahren

Der `AutoShutdownOrchestrator` lauscht auf `IRunningAutomationStatusSource.RunningCountChanged`. Wenn die Anzahl aktiver CLI-Prozesse auf 0 sinkt und die Einstellung aktiv ist, wird `ISystemShutdownService.Shutdown` aufgerufen.

## Einschränkungen

- Plugin-Einstellungen werden im Windows Credential Store gespeichert — nicht plattformübergreifend portierbar.
- Arbeitsverzeichnis-Änderungen wirken sich erst auf neue Aufgaben aus; laufende Aufgaben behalten ihren Klonpfad.
- Dark Mode wirkt nicht auf eingebettete CLI-Fenster (native Prozesse behalten ihr eigenes Erscheinungsbild).
