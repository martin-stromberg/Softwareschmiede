← [Zurück zur Übersicht](index.md)

# Autonome Aufgaben — Installation und Konfiguration

## Voraussetzungen

- Softwareschmiede-Anwendung muss auf dem neuesten Stand sein (inkl. dieser Migrations)
- .NET Runtime 6.0+ (falls als Konsolenapp)
- Git muss auf dem System installiert sein und im `PATH` erreichbar
- Datenbank muss migriert sein (siehe **Datenbank-Migrationen** unten)
- Mindestens 100 MB freier Festplattenspeicher für Arbeitsverzeichnisse

## Installationsschritte

### 1. Datenbank aktualisieren

Führe die Datenbank-Migrationen aus:

```bash
dotnet ef database update --project src/Softwareschmiede
```

Dies erstellt folgende neue Tabellen:
- `AutonomAufgabeKonfigurationen`
- `UnteragentSpezifikationen`
- `SkillDefinitionen`

Und fügt folgende Spalten hinzu:
- `Aufgaben.AutonomKonfigurationId` (uniqueidentifier, FK)
- `AutonomAufgabeKonfigurationen.ProjektleiterAgentId` (string, nullable)
- `AutonomAufgabeKonfigurationen.SessionPauseUtc` (datetime2, nullable)
- `AutonomAufgabeKonfigurationen.AktiveUnteragenten` (int, nullable)

### 2. Anwendung neu starten

Starten Sie die Softwareschmiede-Anwendung neu, damit alle Services initialisiert werden.

### 3. Feature-Flag prüfen

Die Funktion ist standardmäßig aktiviert. Sie wird über zwei Ebenen gesteuert:

1. **Deployment-Zeit** (`appsettings.json`, gebunden über `IOptions<AutonomAufgabenOptions>`, erfordert Neustart):

   ```json
   "AutonomAufgaben": {
     "Enabled": true,
     "DefaultTokenBudget": 500000,
     "DefaultRuntimeLimitMinutes": 480,
     "HeartbeatTimeoutSeconds": 300,
     "MaxConcurrentSubagents": 5,
     "SkillAutoGenerationEnabled": false
   }
   ```

2. **Laufzeit / GUI** (`AppEinstellung`-Eintrag mit Schlüssel `autonomeaufgaben.enabled`, Konstante `AppEinstellungService.AutonomAufgabenEnabledKey`, kein Neustart erforderlich): Checkbox „Autonome Aufgaben aktivieren" in **Einstellungen → Allgemein → Automatisierung**, gebunden an `SettingsViewModel.IsAutonomAufgabenEnabled`; ohne gespeicherten Eintrag gilt der Fallback `true`.

Ist `AutonomAufgaben:Enabled` auf `false` gesetzt, greifen die Guard-Klauseln in `AutonomAufgabeStartService.StarteAsync()`, `AutonomAufgabenInitialisierungsService.InitialisiereAsync()` und `ProjektleiterAgentService.StarteAgentAsync()` (siehe **Business Rules**): Statt des Initialisierungsdialogs bzw. der Agent-Ausführung wird ein Fehlerresultat (UI-Einstiegspunkt) bzw. eine `InvalidOperationException` mit der Meldung aus `AutonomAufgabenOptions.DisabledErrorMessage` ("Autonome Aufgaben sind nicht aktiviert.") zurückgegeben. Die GUI-Checkbox schreibt aktuell ausschließlich in die DB-Einstellung — sie ändert nicht den `appsettings.json`-Wert und wird von den Guard-Klauseln der Backend-Services (die gegen `IOptions<AutonomAufgabenOptions>` prüfen) nicht ausgewertet; sie steuert stattdessen die UI-Sichtbarkeit (`TaskDetailViewModel.IsAutonomAufgabenEnabled`, `ShowAutomatisierungPanel`).

## Konfiguration

Die Funktion wird über `appsettings.json` konfiguriert:

| Parameter | Typ | Standardwert | Zweck |
|-----------|-----|--------------|-------|
| `AutonomAufgaben:Enabled` | bool | `true` | Feature-Flag zum Aktivieren/Deaktivieren |
| `AutonomAufgaben:DefaultTokenBudget` | int | `500000` | Standardbudget für neue Autonome Aufgaben |
| `AutonomAufgaben:DefaultRuntimeLimitMinutes` | int | `480` | Standard-Laufzeitlimit (8 Stunden) |
| `AutonomAufgaben:HeartbeatTimeoutSeconds` | int | `300` | Timeout in Sekunden für Heartbeat-Unterbrechungserkennung |
| `AutonomAufgaben:MaxConcurrentSubagents` | int | `5` | Maximale Anzahl gleichzeitig laufender Unteragenten |
| `AutonomAufgaben:SkillAutoGenerationEnabled` | bool | `false` | Standard für automatische Skill-Generierung |
| `AutonomAufgaben:WorkingDirectoryBase` | string | `{AppData}/AutonomAufgaben` | Basis-Verzeichnis für Arbeitsverzeichnisse |

### Beispiel-Konfiguration für Entwicklung

```json
{
  "AutonomAufgaben": {
    "Enabled": true,
    "DefaultTokenBudget": 100000,
    "DefaultRuntimeLimitMinutes": 60,
    "HeartbeatTimeoutSeconds": 30,
    "MaxConcurrentSubagents": 2,
    "SkillAutoGenerationEnabled": false,
    "WorkingDirectoryBase": "C:/temp/autonomous-tasks"
  }
}
```

### Beispiel-Konfiguration für Produktion

```json
{
  "AutonomAufgaben": {
    "Enabled": true,
    "DefaultTokenBudget": 500000,
    "DefaultRuntimeLimitMinutes": 480,
    "HeartbeatTimeoutSeconds": 300,
    "MaxConcurrentSubagents": 5,
    "SkillAutoGenerationEnabled": true,
    "WorkingDirectoryBase": "/var/autonomous-tasks"
  }
}
```

## Umgebungsvariablen

Keine speziellen Umgebungsvariablen erforderlich. Die Konfiguration erfolgt vollständig über `appsettings.json`.

> **Hinweis für Sandbox-Umgebungen**: `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` kann bei E2E-Tests mit ConPTY gesetzt werden.

## Überprüfung der Installation

### 1. Feature-Availability prüfen

Öffnen Sie die Anwendung und erstellen Sie eine neue Aufgabe. Sie sollten einen Button zum Initialisieren einer Autonomen Aufgabe sehen.

Falls nicht:
- Überprüfen Sie `AutonomAufgaben:Enabled` in `appsettings.json`
- Überprüfen Sie die Anwendungs-Logs auf Fehler beim Service-Startup

### 2. Arbeitsverzeichnis testen

Initialisieren Sie eine Autonome Aufgabe. Das System sollte:
- Ein Arbeitsverzeichnis unter dem konfigurierten Basis-Pfad erstellen
- Das Git-Repository klonen
- `state.json`, `plan.md`, `progress.md`, `governance.md` und `permissions.json` erzeugen

Falls nicht:
- Überprüfen Sie Git-Zugang und -Netzwerk
- Überprüfen Sie Festplattenspeicher und Schreibberechtigungen
- Siehe **Fehlerbehebung**

### 3. Datenbank-Einträge prüfen

Nach Initialisierung sollten DB-Einträge vorhanden sein:
- Ein neuer Eintrag in `AutonomAufgabeKonfigurationen`
- Die verknüpfte `Aufgabe` hat `AusfuehrungsStatus = AutonomAufgabe`

Prüfen Sie per SQL:

```sql
SELECT TOP 5 * FROM AutonomAufgabeKonfigurationen ORDER BY ID DESC;
SELECT * FROM Aufgaben WHERE AusfuehrungsStatus = 3; -- 3 = AutonomAufgabe
```

### 4. Agent-Runtime testen

Starten Sie einen Projektleiter-Agenten für die initialisierte Aufgabe. Die Anwendung sollte:
- Den Agent mit dem Initialprompt starten
- Regelmäßig Heartbeats aktualisieren
- Token-Budget verbrauchen und (bei Erreichen) pausieren

Falls nicht:
- Überprüfen Sie Agent-Runtime-Infrastruktur und -Logs
- Siehe **Fehlerbehebung**

## Migration von bestehenden Aufgaben

Bestehende (reguläre) Aufgaben werden **nicht automatisch** konvertiert. Sie können weiterhin normal verwendet werden.

Falls Sie eine bestehende Aufgabe in eine Autonome Aufgabe konvertieren möchten:
1. Erstellen Sie eine neue Aufgabe mit ähnlicher Anforderung
2. Initialisieren Sie diese als Autonome Aufgabe
3. Die alte Aufgabe kann gelöscht oder archiviert werden

## Deinstallation

Falls Sie die Funktion deaktivieren möchten:

1. Setzen Sie `AutonomAufgaben:Enabled` auf `false` in `appsettings.json`
2. Starten Sie die Anwendung neu
3. Die UI wird das Initialisierungs-Feature nicht mehr anzeigen
4. Bestehende Autonome Aufgaben können nicht mehr gestartet werden

Die Datenbank-Tabellen können manuell gelöscht werden (optional):
```sql
DROP TABLE UnteragentSpezifikationen;
DROP TABLE SkillDefinitionen;
DROP TABLE AutonomAufgabeKonfigurationen; -- enthält u.a. ProjektleiterAgentId, SessionPauseUtc, AktiveUnteragenten

ALTER TABLE Aufgaben DROP COLUMN AutonomKonfigurationId;
```

Dies ist aber nicht erforderlich, um die Funktion zu deaktivieren.

## Troubleshooting bei Installation

| Problem | Lösung |
|---------|--------|
| **Migrations schlagen fehl** | Überprüfen Sie DB-Verbindung und Datenbankversion; führen Sie `dotnet ef database update` erneut aus |
| **Feature-Button ist nicht sichtbar** | Überprüfen Sie `Enabled` Flag und starten Sie App neu |
| **Arbeitsverzeichnis wird nicht erstellt** | Überprüfen Sie `WorkingDirectoryBase`-Pfad und Schreibberechtigungen |
| **Git-Klon schlägt fehl** | Überprüfen Sie Git-Installation und Netzwerkzugang zu Repository |
| **Fehler beim Agent-Start** | Überprüfen Sie Agent-Runtime-Logs und verfügbare Ressourcen |
