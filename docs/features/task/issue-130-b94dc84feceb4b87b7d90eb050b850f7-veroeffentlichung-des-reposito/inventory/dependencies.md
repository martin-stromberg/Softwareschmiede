# Abhängigkeitsmanagement

## NuGet-Abhängigkeiten nach Projekt

### `src/Softwareschmiede/Softwareschmiede.csproj`

Domain- und Service-Layer. Target: `.net10.0`

| Package | Version | Lizenz | Zweck |
|---------|---------|--------|-------|
| `Microsoft.EntityFrameworkCore.Sqlite` | 10.0.9 | Apache 2.0 | SQLite-Datenzugriff via EF Core |
| `SQLitePCLRaw.lib.e_sqlite3` | 2.1.12 | Apache 2.0 | Native SQLite-Bibliothek |
| `Microsoft.EntityFrameworkCore.Design` | 10.0.9 | Apache 2.0 | EF Core Design-Tools (Migrationen) |
| `Microsoft.Extensions.Caching.Memory` | 10.0.9 | MIT | Memory-Cache-Implementierung |
| `Microsoft.Extensions.DependencyInjection` | 10.0.9 | MIT | Dependency-Injection-Container |
| `Microsoft.Extensions.Logging` | 10.0.9 | MIT | Logging-Abstraktion |
| `Microsoft.Extensions.Options` | 10.0.9 | MIT | Configuration-Options-Pattern |
| `Microsoft.Extensions.Hosting.Abstractions` | 10.0.9 | MIT | Host-Abstraktion |

### `src/Softwareschmiede.App/Softwareschmiede.App.csproj`

WPF-Desktopanwendung. Target: `.net10.0-windows10.0.17763.0`

| Package | Version | Lizenz | Zweck |
|---------|---------|--------|-------|
| `Microsoft.Extensions.DependencyInjection` | 10.0.9 | MIT | DI-Container |
| `Microsoft.Extensions.Hosting` | 10.0.9 | MIT | Host-Komponenten |
| `Microsoft.Extensions.Logging` | 10.0.9 | MIT | Logging |
| `Microsoft.Extensions.Logging.Abstractions` | 10.0.9 | MIT | Logging-Abstraktion |
| `Microsoft.Extensions.Logging.Console` | 10.0.9 | MIT | Console-Logging |
| `Serilog.Extensions.Hosting` | 10.0.0 | Apache 2.0 | Serilog-Integration mit Host |
| `Serilog.Sinks.File` | 7.0.0 | Apache 2.0 | Datei-Logging-Sink |
| `Serilog.Sinks.Console` | 6.1.1 | Apache 2.0 | Console-Logging-Sink |
| `Microsoft.EntityFrameworkCore.Sqlite` | 10.0.9 | Apache 2.0 | Datenzugriff |
| `SQLitePCLRaw.lib.e_sqlite3` | 2.1.12 | Apache 2.0 | Native SQLite |

**Projekt-Referenzen:**
- `Softwareschmiede` (Domain/Service)
- `Softwareschmiede.Plugin.Contracts` (Plugin-Interface)
- 7 Plugin-Projekte (Build-Dependencies, keine Assembly-Referenzen)

### `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj`

Unit-, Integration-, BUnit-, E2E-Tests. Target: `.net10.0-windows10.0.17763.0`

| Package | Version | Lizenz | Zweck |
|---------|---------|--------|-------|
| `bunit` | 2.7.2 | MIT | Blazor-Komponenten-Testtoolkit |
| `FlaUI.Core` | 5.0.0 | GPL-3.0-or-later | UI-Automatisierung (Basis) |
| `FlaUI.UIA3` | 5.0.0 | GPL-3.0-or-later | UI-Automatisierung (UIA3-Provider) |
| `coverlet.collector` | 10.0.1 | MIT | Code-Coverage-Collector |
| `FluentAssertions` | 8.10.0 | Apache 2.0 | Fluente Assertion-API |
| `Microsoft.EntityFrameworkCore.InMemory` | 10.0.9 | Apache 2.0 | In-Memory-Datenbank für Tests |
| `Microsoft.Extensions.Logging.Abstractions` | 10.0.9 | MIT | Logging-Abstraktion |
| `Microsoft.Extensions.TimeProvider.Testing` | 10.7.0 | MIT | Fakeable `TimeProvider` für Tests |
| `Microsoft.NET.Test.Sdk` | 18.7.0 | MIT | Test-SDK |
| `Moq` | 4.* | BSD-3-Clause | Mocking-Framework |
| `xunit` | 2.9.3 | Apache-2.0 | Test-Framework |
| `xunit.runner.visualstudio` | 3.1.5 | Apache-2.0 | Visual Studio Runner |
| `Xunit.SkippableFact` | 1.5.23 | MIT | Conditional Test Skip Support |

## Plugin-Projekte (Abhängigkeiten)

Die folgenden Plugin-Projekte werden als Abhängigkeiten referenziert und müssen ebenfalls überprüft werden:

1. `Softwareschmiede.Plugin.BitBucket`
2. `Softwareschmiede.Plugin.GitHub`
3. `Softwareschmiede.Plugin.LocalDirectory`
4. `Softwareschmiede.Plugin.GitHubCopilot`
5. `Softwareschmiede.Plugin.ClaudeCli`
6. `Softwareschmiede.Plugin.Codex`
7. `Softwareschmiede.Plugin.KiSimulator`

**Status:** Noch nicht überprüft auf ihre eigenen NuGet-Abhängigkeiten

## Lizenz-Kompatibilität — Preliminäre Einschätzung

| Lizenz | Pakete | Kompatibilität |
|--------|--------|---|
| **MIT** | DependencyInjection, Extensions.Logging, Options, Hosting.Abstractions, Extensions.TimeProvider.Testing, bunit, coverlet.collector, xunit*, etc. | ✅ Permissiv, sehr Reuse-freundlich |
| **Apache 2.0** | EF Core, SQLite-PCL, Serilog*, FluentAssertions | ✅ Permissiv mit Patent-Schutz |
| **GPL-3.0** | FlaUI (Core, UIA3) | ⚠️ **Copyleft-Lizenz** — Binding für Test-Assembly, potenzielle Auswirkung auf Product-Assembly Lizenzwahl |
| **BSD-3-Clause** | Moq | ✅ Permissiv |

## Probleme

### 🚨 FlaUI GPL-3.0 Kompatibilität

**Status:** ⚠️ **Zu überprüfen**

- **Problem:** FlaUI (Core & UIA3) unter GPL-3.0 ist eine **Copyleft-Lizenz**
- **Auswirkung:** 
  - Kann als Test-Abhängigkeit (PrivateAssets) verwendet werden
  - Aber: Wenn die Anwendung selbst (WPF-App) FlaUI-Funktionalität direkt nutzen würde → Produktionsabhängigkeit → GPL-Anforderung würde das gesamte Projekt anstecken
- **Aktuelle Situation:** FlaUI ist nur im Testprojekt referenziert → sollte OK sein unter MIT/Apache 2.0 Lizenz für das Hauptprojekt
- **Noch zu klären:** 
  - Ist FlaUI wirklich nur in Tests verwendet?
  - Oder gibt es versteckte Produktionsreferenzen?

## Fehlende Dokumentation

### ❌ Abhängigkeits-Übersicht
- **Status:** Keine zentrale Dokumentation
- **Erforderlich:** 
  - `THIRD_PARTY_LICENSES.md` mit allen Abhängigkeiten, Lizenzen, Lizenztexten
  - SPDX-Lizenz-Identifizierung

### ⚠️ Plugin-Abhängigkeiten
- 7 Plugin-Projekte noch nicht überprüft auf ihre jeweils eigenen NuGet-Abhängigkeiten
- Notwendig für vollständige THIRD_PARTY_LICENSES.md

## Sicherheitsaspekte

- ✅ **Kein Hardcoding von API-Keys in Dependencies:** Abhängigkeiten nutzen Environment-Variablen oder Konfiguration
- ✅ **Keine verdächtigen Abhängigkeiten:** Alle Pakete von vertrauenswürdigen Quellen (nuget.org, Microsoft)
- ⚠️ **Keine Dependency-Scanning-Tools:** Keine automatisierten Checks für Know Vulnerabilities (könnte via `dotnet list package --vulnerable` geprüft werden)

## Offene Fragen

1. **Lizenzwahl des Hauptprojekts:** Wie sollte die GPL-3.0-Abhängigkeit (FlaUI) behandelt werden?
   - Option A: Hauptprojekt unter MIT/Apache 2.0, FlaUI nur als Test-Abhängigkeit → OK
   - Option B: Hauptprojekt unter GPL-3.0 (zu restriktiv für andere Zwecke)
   - Option C: FlaUI durch GPL-kompatible Alternative ersetzen (schwierig für UI-Tests)

2. **Transitive Abhängigkeiten:** Vollständige Liste aller transitiven Abhängigkeiten noch nicht erstellt

3. **Vulnerability-Scanning:** Sollen automatisierte Vulnerability-Checks in CI/CD integriert werden?

4. **NuGet Source:** Sind alle NuGet-Pakete aus öffentlichen Feeds (`nuget.org`) oder gibt es private Feeds?
