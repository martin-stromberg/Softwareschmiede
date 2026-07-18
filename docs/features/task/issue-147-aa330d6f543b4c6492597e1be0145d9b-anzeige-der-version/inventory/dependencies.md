# Projekte und Dependencies

## Betroffenes Testprojekt: `Softwareschmiede.Tests`

**Projektdatei:** `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj`

### NuGet-Abhängigkeiten (direkt referenziert)

| Package | Version | Status | Anmerkung |
|---------|---------|--------|-----------|
| **`bunit`** | **2.7.2** | ⚠️ **Anfällig** (transitive Abhängigkeit) | Zieht transitive Abhängigkeit `AngleSharp 1.4.0` mit bekannter Vulnerability |
| `FlaUI.Core` | 5.0.0 | ✓ OK | E2E-Test-Framework für WPF-Anwendungen |
| `FlaUI.UIA3` | 5.0.0 | ✓ OK | UIA3-Treiber für FlaUI |
| `coverlet.collector` | 10.0.1 | ✓ OK | Code-Coverage-Collector |
| `FluentAssertions` | 8.10.0 | ✓ OK | Assertion-Bibliothek für Tests |
| `Microsoft.EntityFrameworkCore.InMemory` | 10.0.9 | ✓ OK | In-Memory EF Core Provider |
| `Microsoft.Extensions.Logging.Abstractions` | 10.0.9 | ✓ OK | Logging-Abstraktion |
| `Microsoft.Extensions.TimeProvider.Testing` | 10.7.0 | ✓ OK | Testing Utilities für TimeProvider |
| `Microsoft.NET.Test.Sdk` | 18.7.0 | ✓ OK | Test SDK |
| `Moq` | 4.* | ✓ OK | Mocking-Framework |
| `xunit` | 2.9.3 | ✓ OK | Test-Framework |
| `xunit.runner.visualstudio` | 3.1.5 | ✓ OK | Visual Studio Test Runner |
| `Xunit.SkippableFact` | 1.5.23 | ✓ OK | Skippable Tests für xunit |

### Transitive Abhängigkeiten (von `bunit 2.7.2`)

| Package | Aktuelle Version | Anfällige Version | Fix-Version |
|---------|---|---|---|
| **`AngleSharp`** | (durch bunit gezogen) | **1.4.0** | **>= 1.5.0** |

### Security-Vulnerability Details

**Betroffenes Paket:** `AngleSharp 1.4.0`
- **CVE:** CVE-2026-54570
- **GitHub Advisory:** GHSA-pgww-w46g-26qg
- **Severity:** Moderate
- **Vektor:** Transitive Abhängigkeit von `bunit 2.7.2`
- **Fix:** Verfügbar in `AngleSharp >= 1.5.0`

### Getestete Komponenten

Dieses Testprojekt testet die folgenden produktiven Plugin-Projekte:
- `Softwareschmiede.Plugin.BitBucket`
- `Softwareschmiede.Plugin.GitHub`
- `Softwareschmiede.Plugin.LocalDirectory` ← **Enthält den flaky Test**
- `Softwareschmiede.Plugin.GitHubCopilot`
- `Softwareschmiede.Plugin.ClaudeCli`
- `Softwareschmiede.Plugin.Codex`
- `Softwareschmiede.Plugin.KiSimulator`
- `Softwareschmiede.Plugin.Contracts`
- Hauptprojekte: `Softwareschmiede` und `Softwareschmiede.App`

## Produktives Plugin: `Softwareschmiede.Plugin.LocalDirectory`

**Projektdatei:** `plugins/Softwareschmiede.Plugin.LocalDirectory/Softwareschmiede.Plugin.LocalDirectory.csproj`

### NuGet-Abhängigkeiten

| Package | Version | Status |
|---------|---------|--------|
| `Microsoft.Extensions.Logging.Abstractions` | 10.0.9 | ✓ OK |

### Projektabhängigkeiten

| Projekt | Status |
|---------|--------|
| `Softwareschmiede.Plugin.Contracts` | ✓ OK |

**Anmerkung:** Dieses produktive Plugin-Projekt selbst hat keine anfälligen Abhängigkeiten. Die `AngleSharp`-Vulnerability existiert nur im Testprojekt `Softwareschmiede.Tests`.
