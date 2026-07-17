# Interfaces

## `ICliUpdateSafetyService`

**Datei:** `src/Softwareschmiede/Application/Services/Updates/UpdateInterfaces.cs` (Zeile 33–38)

Beschreibung (Zeile 33): "Bewertet aktive CLI-Aufgaben vor einem Update."

| Methode | Parameter | Rückgabewert | Zweck | Zeile |
|---------|-----------|--------------|-------|-------|
| `CheckAsync` | `CancellationToken ct = default` | `Task<CliUpdateSafetyResult>` | Prüft, welche aktiven Aufgaben das Programmupdate blockieren (Aufgaben mit laufender CLI, die nicht auf Eingabe warten) | 37 |

### Implementierung

Wird von `CliUpdateSafetyService` implementiert (siehe [Logik-Services](logic.md#cliupdatesafetyservice)).

### Abhängigkeiten

Das Interface hat keine direkten Abhängigkeiten auf andere Interfaces. Es wird durch DI registriert und vom Update-Orchestrierungscode (z. B. `IUpdateService`) konsumiert.

---

## Weitere Update-Service-Interfaces (Kontext)

Diese Interfaces sind im selben File definiert und gehören zum Gesamt-Update-System (siehe `UpdateInterfaces.cs`):

| Interface | Zweck |
|-----------|-------|
| `IApplicationVersionProvider` | Liest die lokal installierte Programmversion |
| `IUpdateReleaseClient` | Ruft neueste stabile Release-Information ab |
| `IUpdateService` | Orchestriert Update-Prüfung, Vorbereitung und Start |
| `IUpdatePackageService` | Verwaltet Download, Entpacken und Validierung eines Update-Pakets |
| `IUpdateScriptService` | Erzeugt und startet das externe Update-Skript |
| `IUpdateProcessLauncher` | Startet externe Update-Prozesse testbar gekapselt |
| `IApplicationShutdownService` | Kapselt das geordnete Beenden der Anwendung nach gestartetem Updater |

Von diesen ist nur **`ICliUpdateSafetyService`** direkt relevant für die vorliegende Anforderung.
