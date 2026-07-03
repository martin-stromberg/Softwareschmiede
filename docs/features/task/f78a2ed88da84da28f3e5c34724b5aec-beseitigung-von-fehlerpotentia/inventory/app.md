# App.xaml.cs - Globale Exception-Handler und Startup-Pfade

Datei: `src/Softwareschmiede.App/App.xaml.cs`

## Globale Exception-Handler

### Status der Implementierung

| Handler | Registriert | Serilog-Logging | Bemerkung |
|---------|------------|-----------------|-----------|
| `Application.DispatcherUnhandledException` | **NEIN** | — | F1: Nicht vorhanden |
| `AppDomain.CurrentDomain.UnhandledException` | **NEIN** | — | F2: Nicht vorhanden |
| `TaskScheduler.UnobservedTaskException` | **NEIN** | — | F3: Nicht vorhanden |

### Serilog-Konfiguration (vorhanden)

- **Zeile 32–39:** Serilog-Logger ist konfiguriert
  - Konsolen-Output
  - Datei-Logging mit täglichem Rollover
  - Aufbewahrung: 14 Tage
  - Pfad: `logs/softwareschmiede-.log`

## Startup-Pfade

### OnStartup (Zeile 25–55)

- Serilog-Logger wird initialisiert
- Globales try-catch (Zeile 41–54) mit `Log.Logger.Fatal()`
- Bei Exception: MessageBox und `Shutdown(1)`

**Zu schützende Operationen:**

| Zeile | Operationen | Status | Bemerkung |
|-------|------------|--------|-----------|
| 66 | `_host.Services.GetRequiredService<CliProcessManager>();` | **KEIN try-catch** | F4: Nur allgemeines OnStartup-try-catch |
| 70–72 | `db.Database.MigrateAsync()` | Hat try-catch im OnStartup | F5 (Datenbankinitialisierung) |
| 74–75 | `mainWindow.Show()` | **KEIN try-catch** | F5: Nur allgemeines OnStartup-try-catch |

### StartupAsync (Zeile 57–76)

- Wird vom OnStartup aufgerufen
- Host-Erstellung, Start, Service-Abrufe, DB-Migration, MainWindow-Anzeige
- Keine individuelle Exception-Behandlung für kritische Operationen

### OnExit (Zeile 79–99)

- try-catch um `_host.StopAsync()` (Zeile 83–90)
- `Log.CloseAndFlush()` (Zeile 97)

## Abhängigkeiten

- `Microsoft.Extensions.DependencyInjection`
- `Microsoft.Extensions.Logging`
- `Serilog`
- `Softwareschmiede.App.Services`, `Softwareschmiede.App.ViewModels`, `Softwareschmiede.App.Views`
- `Softwareschmiede.Application.Services` → `CliProcessManager`, `KiAusfuehrungsService`
- `Softwareschmiede.Infrastructure.Data` → `SoftwareschmiededDbContext`
