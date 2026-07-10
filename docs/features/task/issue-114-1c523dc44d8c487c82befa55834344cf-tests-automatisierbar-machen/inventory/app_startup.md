# Bestandsaufnahme: App-Startup und Exception-Handling

## App.xaml.cs — Startup-Pipeline

**Datei:** `src\Softwareschmiede.App\App.xaml.cs`

Die Anwendung startet über eine **async Startup-Pipeline** mit umfassender Exception-Behandlung und strukturiertem Logging via Serilog.

### OnStartup (Zeilen 28–62)

```csharp
protected override async void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);
    
    // 1. Logger-Konfiguration
    var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
    Directory.CreateDirectory(logDirectory);
    
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Information()
        .WriteTo.Console()
        .WriteTo.File(
            Path.Combine(logDirectory, "softwareschmiede-.log"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14)
        .CreateLogger();
    
    // 2. Exception-Handler registrieren
    DispatcherUnhandledException += OnDispatcherUnhandledException;
    AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
    TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    
    // 3. Startup-Logik
    try
    {
        await StartupAsync(e);
    }
    catch (Exception ex)
    {
        Log.Logger.Fatal(ex, "Fehler beim Starten der Anwendung.");
        MessageBox.Show(
            $"Die Anwendung konnte nicht gestartet werden:\n\n{ex.Message}",
            "Startfehler",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        Shutdown(1);
    }
}
```

### StartupAsync (Zeilen 64–99)

```csharp
private async Task StartupAsync(StartupEventArgs e)
{
    // 1. Host + Dependency Injection konfigurieren
    _host = Host.CreateDefaultBuilder()
        .UseSerilog()
        .ConfigureServices(ConfigureServices)
        .Build();
    
    // 2. Host starten
    await _host.StartAsync();
    Services = _host.Services;
    
    // 3. CliProcessManager initialisieren (nicht-blockierend bei Fehler)
    try
    {
        _host.Services.GetRequiredService<CliProcessManager>();
    }
    catch (Exception ex)
    {
        Log.Logger.Error(ex, "CliProcessManager konnte nicht initialisiert werden. Die Anwendung läuft ohne CLI-Funktionalität weiter.");
    }
    
    // 4. Datenbank-Migrationen ausführen
    using (var scope = _host.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        await db.Database.MigrateAsync();
    }
    
    // 5. MainWindow anzeigen (kritisch — Fehler hier ist Test-Fehler!)
    try
    {
        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }
    catch (Exception ex)
    {
        Log.Logger.Error(ex, "MainWindow konnte nicht angezeigt werden.");
    }
}
```

### Startup-Schritte im Detail

1. **Logger-Konfiguration (OnStartup, Zeilen 32–42)**
   - Serilog-Logger mit **Minimum-Loglevel `Information`**.
   - Ausgaben zu Console und File:
     - **Datei:** `<AppBaseDirectory>/logs/softwareschmiede-.log` (Rolling daily, max 14 Tage).
     - **Rollinginterval:** `RollingInterval.Day`.
     - **Dateinamen-Format:** `softwareschmiede-YYYYMMDD.log` (Serilog standard).

2. **Exception-Handler registrieren (OnStartup, Zeilen 44–46)**
   - `DispatcherUnhandledException` — Exceptions im UI-Thread.
   - `AppDomain.CurrentDomain.UnhandledException` — Exceptions außerhalb UI-Thread.
   - `TaskScheduler.UnobservedTaskException` — Unbeobachtete Task-Exceptions.

3. **Host + DI aufbauen (StartupAsync, Zeilen 66–69)**
   - Microsoft.Extensions.Hosting nutzen.
   - Alle Services in `ConfigureServices()` registrieren.

4. **Host starten (StartupAsync, Zeilen 71–73)**
   - `await _host.StartAsync()` — alle Singleton/Scoped-Services initialisieren.
   - `Services = _host.Services` — für Code-behind-Klassen zugänglich machen.

5. **CliProcessManager initialisieren (StartupAsync, Zeilen 75–82)**
   - `GetRequiredService<CliProcessManager>()`.
   - Falls Exception: wird geloggt, aber App läuft weiter (graceful degradation).

6. **DB-Migrationen (StartupAsync, Zeilen 84–88)**
   - Neuen Scope erstellen.
   - `await db.Database.MigrateAsync()` — Entity Framework Migrationen.
   - **Keine Exception-Behandlung** — falls Migration crasht, propagiert nach oben zu OnStartup.

7. **MainWindow zeigen (StartupAsync, Zeilen 90–98)**
   - `var mainWindow = _host.Services.GetRequiredService<MainWindow>()`.
   - `mainWindow.Show()`.
   - Falls Exception (z. B. XamlParseException): wird geloggt, aber keine MessageBox (App läuft im Hintergrund weiter).

### Exception-Handler

| Handler | Loggruppe | Verhalten |
|---------|-----------|-----------|
| `OnDispatcherUnhandledException` (Zeile 101–105) | UI-Thread | Loggt `Error`, setzt `e.Handled = true` (verhindert App-Crash) |
| `OnAppDomainUnhandledException` (Zeile 107–110) | Background-Thread | Loggt `Error` |
| `OnUnobservedTaskException` (Zeile 112–116) | Task-Scheduler | Loggt `Error`, setzt `e.SetObserved()` |

### Log-Ausgaben

Alle Ausgaben werden über Serilog geloggt:

- **Fatal:** `"Fehler beim Starten der Anwendung."` (OnStartup catch-all).
- **Error:** `"CliProcessManager konnte nicht initialisiert werden. Die Anwendung läuft ohne CLI-Funktionalität weiter."`.
- **Error:** `"MainWindow konnte nicht angezeigt werden."`.
- **Error:** `"Unbehandelte Exception im UI-Thread."`.
- **Error:** `"Unbehandelte Exception außerhalb des UI-Threads."`.
- **Error:** `"Unbeobachtete Task-Exception."`.
- **Error:** `"Fehler beim Beenden des Hosts."` (OnExit).

---

## Log-Dateien und Pfade

### Log-Verzeichnis

**Pfad:** `<AppBaseDirectory>/logs/`

Wird in `OnStartup` (Zeile 32–33) erstellt:
```csharp
var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
Directory.CreateDirectory(logDirectory);
```

Bei Tests:
- **Test-Prozess:** Läuft aus `src/Softwareschmiede.Tests/bin/Debug/<TargetFramework>/`.
- **App-Prozess:** Startet aus `src/Softwareschmiede.App/bin/Debug/<TargetFramework>/Softwareschmiede.App.exe`.
- **App-Log-Verzeichnis:** `src/Softwareschmiede.App/bin/Debug/<TargetFramework>/logs/`.

### Log-Dateinamen

Serilog-Rolling mit `RollingInterval.Day`:
- `softwareschmiede-20260710.log` (für den 10.07.2026).
- `softwareschmiede-20260709.log` (vorheriger Tag).
- Etc.

**Retention:** 14 Tage (alte Logs werden gelöscht).

### Log-Inhalt

Beispiel eines Startup-Fehlers in der Log-Datei:

```
[10:30:45 INF] Application started.
[10:30:46 ERR] MainWindow konnte nicht angezeigt werden.
[10:30:46 ERR] System.Windows.Markup.XamlParseException: The invocation of the constructor on type 'Softwareschmiede.App.Views.MainWindow' threw an exception.
...
```

---

## Bekannte Fehler und ihre Log-Signaturen

| Fehler-Szenario | Log-Signatur | Auswirkung auf Tests |
|-----------------|------------|----------------------|
| XamlParseException in MainWindow | `[ERR] MainWindow konnte nicht angezeigt werden.` | `WpfTestBase.WaitForElement` schlägt mit Timeout fehl (Fenster wird nie sichtbar) |
| DB-Migration schlägt fehl | `[ERR]` + Exception-Stack in Log | `StartupAsync` throws, OnStartup zeigt MessageBox, App `Shutdown(1)` |
| CliProcessManager init fehlgeschlagen | `[ERR] CliProcessManager konnte nicht initialisiert werden. Die Anwendung läuft ohne CLI-Funktionalität weiter.` | App läuft weiter, aber CLI-Features funktionieren nicht |
| Unbehandelte Exception im UI-Thread | `[ERR] Unbehandelte Exception im UI-Thread.` | App läuft weiter (e.Handled = true) |

---

## Abhängigkeiten für die Bestandsaufnahme

### Serilog-Konfiguration
- **Minimum Level:** `Information` — kein Debug-Logging aktiviert.
- **Sinks:** Console + File (Rolling daily).
- **Exception-Serialization:** Vollständiger Stack wird geloggt.

### DI-Services
Siehe `ConfigureServices()` (Zeilen 141–223):
- `SoftwareschmiededDbContext` — SQLite via Entity Framework.
- Services (Domain/Infrastructure/Application).
- ViewModels (alle als Transient).
- `MainWindow` (Transient).

### Database-Kontext
Bei Tests:
- Umgebungsvariable `SOFTWARESCHMIEDE_TEST_DB_PATH` setzt Testdatenbank-Pfad (z. B. `%TEMP%\softwareschmiede_e2e_<GUID>.db`).
- Fallback (Produktion): `%LOCALAPPDATA%\Softwareschmiede\softwareschmiede.db`.

---

## Potenzielle Test-Fehler und ihre Ursachen

### Szenario 1: "MainWindow konnte nicht angezeigt werden" in WpfTestBase

**Beobachtung:** `WpfTestBase.LaunchApp()` wirft `TimeoutException` bei `_application.WaitWhileMainHandleIsMissing()`.

**Mögliche Ursachen:**
1. **XamlParseException** — MainWindow.xaml oder abhängige Ressourcen sind broken.
   - **Diagnose:** Lese die Log-Datei `src/Softwareschmiede.App/bin/Debug/net10.0.../logs/softwareschmiede-*.log` auf `[ERR]`-Einträge.
2. **DB-Migration fehlgeschlagen** — `StartupAsync` throws vor `mainWindow.Show()`.
   - **Diagnose:** Log-Datei auf Exception-Stack vor `"MainWindow konnte nicht angezeigt werden."`.
3. **Timing-Problem** — Fenster ist sichtbar, aber nicht sofort (super selten).
   - **Diagnose:** 2000ms Sleep in `LaunchApp()` (Zeile 93) sollte reichen.

### Szenario 2: ".NET Desktop Runtime nicht gefunden"

**Beobachtung:** Test startet App, aber Runtime-Fehler.

**Mögliche Ursachen:**
1. **Build-Artefakte-Korruption** — `dotnet build` / `dotnet test` schreiben gleichzeitig in `bin/obj`.
   - **Diagnose:** `runtimeconfig.json` in `src/Softwareschmiede.App/bin/Debug/<TargetFramework>/` ist incomplete.
   - **Lösung:** Datei-Lock (bereits implementiert in `dotnet_lock.py` + `test-csharp-startup.ps1`).
2. **.NET Desktop Runtime nicht installiert** — System hat .NET Runtime nicht.
   - **Diagnose:** `dotnet --list-runtimes` prüfen.

### Szenario 3: Test hängt bei "Building solution before dotnet test"

**Beobachtung:** Status zeigt "Building solution...", macht aber nicht weiter.

**Mögliche Ursachen:**
1. **Lock wird nicht freigegeben** — Stop-Hook oder alter `build_before_test.py` halten Lock.
   - **Diagnose:** Prüfe ob Verzeichnis `.claude\.locks\dotnet-build.lock\` existiert.
   - **Lösung:** Manuell löschen (Lock ist nach 300s ohnehin stale).
2. **dotnet build ist wirklich langsam** — großes Projekt, first rebuild.
   - **Diagnose:** Manuelle `dotnet build` ausführen und Dauer messen.
