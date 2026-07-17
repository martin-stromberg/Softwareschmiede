# Interfaces

## `IApplicationVersionProvider`
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateInterfaces.cs`

Öffentliches Interface zur Verwaltung der installierten Programmversion.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetInstalledVersionAsync` | `CancellationToken ct = default` | `Task<InstalledVersionInfo?>` | Liest die lokal installierte Programmversion aus `version.json` oder gibt `null` zurück, wenn die Version nicht prüfbar ist |

**Implementierung:** `ApplicationVersionProvider` (siehe Logic-Bereich)

**DI-Registrierung:** Das Interface ist bereits im DI-Container registriert (siehe `App.xaml.cs`)
