# Drittanbieter-Lizenzen

Softwareschmiede selbst steht unter der [MIT-Lizenz](LICENSE). Diese Datei listet alle
NuGet-Abhängigkeiten der veröffentlichten Anwendung (`Softwareschmiede.App` inkl. der
Domain-/Service-Bibliothek `Softwareschmiede`, `Softwareschmiede.Plugin.Contracts` und aller
mitgelieferten Plugin-Projekte) sowie deren Lizenzen auf. Die Liste wurde per
`dotnet list package --include-transitive` je Projekt ermittelt und manuell um SPDX-Kennungen
ergänzt (Stand: 2026-11-23).

Reine Test-/Build-Werkzeuge, die nie Teil des Publish-Outputs werden, sind in einem eigenen
Abschnitt am Ende dieser Datei aufgeführt.

## Ausgelieferte Abhängigkeiten (direkt + transitiv)

| Paket | Version | Lizenz (SPDX) | Ausgeliefert in |
|---|---|---|---|
| Microsoft.EntityFrameworkCore.Sqlite | 11.0.0 | MIT | Domain/Service, App |
| Microsoft.EntityFrameworkCore | 11.0.0 | MIT | transitiv |
| Microsoft.EntityFrameworkCore.Abstractions | 11.0.0 | MIT | transitiv |
| Microsoft.EntityFrameworkCore.Analyzers | 11.0.0 | MIT | transitiv (Roslyn-Analyzer) |
| Microsoft.EntityFrameworkCore.Relational | 11.0.0 | MIT | transitiv |
| Microsoft.EntityFrameworkCore.Sqlite.Core | 11.0.0 | MIT | transitiv |
| Microsoft.Data.Sqlite.Core | 11.0.0 | MIT | transitiv |
| SQLitePCLRaw.lib.e_sqlite3 | 3.53.3 | Apache-2.0 | Domain/Service, App |
| SQLitePCLRaw.core | 3.0.5 | Apache-2.0 | transitiv |
| SQLitePCLRaw.bundle_e_sqlite3 | 3.0.5 | Apache-2.0 | transitiv |
| SQLitePCLRaw.provider.e_sqlite3 | 3.0.5 | Apache-2.0 | transitiv |
| SQLitePCLRaw.config.e_sqlite3 | 3.0.5 | Apache-2.0 | transitiv |
| SQLite | 3.53.4 | Public Domain (blessing) | transitiv (native SQLite-Bibliothek via `SQLitePCLRaw.lib.e_sqlite3`) |
| Microsoft.Extensions.Caching.Memory | 11.0.0 | MIT | Domain/Service |
| Microsoft.Extensions.DependencyInjection | 11.0.0 | MIT | Domain/Service, App |
| Microsoft.Extensions.Logging | 11.0.0 | MIT | Domain/Service, App |
| Microsoft.Extensions.Logging.Console | 11.0.0 | MIT | App |
| Microsoft.Extensions.Logging.Configuration | 11.0.0 | MIT | transitiv |
| Microsoft.Extensions.Logging.Debug | 11.0.0 | MIT | transitiv |
| Microsoft.Extensions.Logging.EventLog | 11.0.0 | MIT | transitiv |
| Microsoft.Extensions.Logging.EventSource | 11.0.0 | MIT | transitiv |
| Microsoft.Extensions.Hosting | 11.0.0 | MIT | App |
| Microsoft.Extensions.Configuration | 11.0.0 | MIT | transitiv |
| Microsoft.Extensions.Configuration.Binder | 11.0.0 | MIT | transitiv |
| Microsoft.Extensions.Configuration.CommandLine | 11.0.0 | MIT | transitiv |
| Microsoft.Extensions.Configuration.EnvironmentVariables | 11.0.0 | MIT | transitiv |
| Microsoft.Extensions.Configuration.FileExtensions | 11.0.0 | MIT | transitiv |
| Microsoft.Extensions.Configuration.Json | 11.0.0 | MIT | transitiv |
| Microsoft.Extensions.Configuration.UserSecrets | 11.0.0 | MIT | transitiv |
| Microsoft.Extensions.Diagnostics | 11.0.0 | MIT | transitiv |
| Microsoft.Extensions.FileProviders.Physical | 11.0.0 | MIT | transitiv |
| Microsoft.Extensions.FileSystemGlobbing | 11.0.0 | MIT | transitiv |
| Microsoft.Extensions.DependencyModel | 11.0.0 | MIT | transitiv |
| Microsoft.Extensions.Options.ConfigurationExtensions | 11.0.0 | MIT | transitiv |
| Serilog | 4.4.0 | Apache-2.0 | transitiv (App) |
| Serilog.Extensions.Hosting | 11.0.0 | Apache-2.0 | App |
| Serilog.Extensions.Logging | 11.0.0 | Apache-2.0 | transitiv |
| Serilog.Sinks.Console | 6.1.1 | Apache-2.0 | App |
| Serilog.Sinks.File | 7.0.0 | Apache-2.0 | App |

Hinweis: Unter `net11.0` sind die früher separat aufgelösten Abstractions-/Basispakete
(`Microsoft.Extensions.*.Abstractions`, `Microsoft.Extensions.Options`,
`Microsoft.Extensions.Primitives`, `Microsoft.Extensions.Hosting.Abstractions` u. a.) Teil des
Shared Frameworks und erscheinen daher nicht mehr als eigenständige NuGet-Pakete in der
Auflösung.

Alle aufgeführten Lizenzen sind permissiv (MIT bzw. Apache-2.0 mit Patentschutz; `SQLite` steht
als Public Domain sogar vollständig frei) und untereinander sowie mit der MIT-Lizenz des
Hauptprojekts kompatibel.

## Nur Test/Build – nicht ausgeliefert

Die folgenden Pakete werden ausschließlich in `Softwareschmiede.Tests`,
`Softwareschmiede.IntegrationTests` oder als Design-Time-Werkzeug verwendet. Sie sind über
`PrivateAssets=all` (bzw. reine Testprojekt-Referenz ohne Publish-Pfad) von jeglicher Weitergabe in
`release.zip` ausgeschlossen und daher für die Lizenzwahl des veröffentlichten Produkts irrelevant.

| Paket | Version | Lizenz (SPDX) | Hinweis |
|---|---|---|---|
| FlaUI.Core | 5.0.0 | GPL-3.0-or-later | UI-Automatisierung für E2E-Tests; `PrivateAssets=all` verhindert transitive Weitergabe (siehe Schritt „Lizenz-Hygiene FlaUI“) |
| FlaUI.UIA3 | 5.0.0 | GPL-3.0-or-later | s. o. |
| Interop.UIAutomationClient | 10.19041.0 | MIT | transitiv via FlaUI.UIA3 (COM-Interop-Wrapper für UI Automation) |
| xunit.v3 | 4.0.3 | Apache-2.0 | Test-Framework |
| xunit.v3.assert, xunit.v3.common, xunit.v3.core.mtp-v2, xunit.v3.extensibility.core, xunit.v3.mtp-v2, xunit.v3.runner.common, xunit.v3.runner.inproc.console | 4.0.3 | Apache-2.0 | transitiv (xunit.v3-Subpakete) |
| xunit.analyzers | 2.3.0 | Apache-2.0 | transitiv (Roslyn-Analyzer) |
| xunit.runner.visualstudio | 4.0.1 | Apache-2.0 | Test-Runner-Integration |
| Microsoft.NET.Test.Sdk | 18.12.0 | MIT | Test-SDK |
| Microsoft.CodeCoverage, Microsoft.TestPlatform.ObjectModel, Microsoft.TestPlatform.TestHost | 18.12.0 | MIT | transitiv (via `Microsoft.NET.Test.Sdk`) |
| Microsoft.Testing.Platform, Microsoft.Testing.Platform.MSBuild, Microsoft.Testing.Extensions.Telemetry, Microsoft.Testing.Extensions.TrxReport.Abstractions | 2.5.2 | MIT | transitiv (Microsoft Testing Platform) |
| Microsoft.ApplicationInsights | 2.23.0 | MIT | transitiv (Test-Telemetrie via `Microsoft.Testing.Extensions.Telemetry`) |
| Microsoft.Bcl.AsyncInterfaces | 6.0.0 | MIT | transitiv |
| Microsoft.Win32.Registry | 5.0.0 | MIT | transitiv |
| System.Diagnostics.EventLog | 6.0.0 | MIT | transitiv |
| System.Management | 8.0.0 | MIT | transitiv |
| System.Security.AccessControl | 6.0.1 | MIT | transitiv |
| Moq | 4.21.0 | BSD-3-Clause | Mocking-Framework |
| Castle.Core | 5.2.1 | Apache-2.0 | transitiv via Moq (DynamicProxy) |
| coverlet.collector | 10.1.1 | MIT | Code-Coverage-Collector |
| FluentAssertions | 8.11.0 | Kommerzielle Lizenz (Xceed) | Ab Version 8 kein OSI-Standardlizenztext mehr, sondern eine kommerzielle Lizenz von Xceed Software Inc.: kostenlos für Open-Source-Projekte und nicht-kommerzielle Nutzung, kostenpflichtig für kommerzielle Nutzung. Da Softwareschmiede als Open-Source-Projekt veröffentlicht wird und das Paket nur im Testprojekt verwendet wird (nicht Teil des Publish-Outputs), ergibt sich hieraus keine Einschränkung für Nutzer der Anwendung. |
| Microsoft.EntityFrameworkCore.InMemory | 11.0.0 | MIT | In-Memory-Datenbank für Tests |
| Microsoft.Extensions.TimeProvider.Testing | 11.0.1 | MIT | Fakeable `TimeProvider` für deterministische Zeit-Tests |
| Microsoft.EntityFrameworkCore.Design | 11.0.0 | MIT | Design-Time-Werkzeug für EF-Core-Migrationen/Scaffolding, bereits vor diesem Vorhaben mit `PrivateAssets=all` referenziert; bringt eigene, ausschließlich zur Entwicklungszeit benötigte Transitiva mit (u. a. die `Microsoft.CodeAnalysis.*`-Familie, `Humanizer.Core`, `Mono.TextTemplating`, `Microsoft.Build.Framework`, `Microsoft.VisualStudio.SolutionPersistence`, `Microsoft.NET.StringTools`, `System.Composition.*`, `System.CodeDom`) – diese werden hier nicht einzeln aufgeführt, da sie nie Teil des Publish-Outputs werden und ausschließlich dem lokalen `dotnet ef`-Workflow dienen |

## FlaUI (GPL-3.0-or-later) — Einordnung

FlaUI ist die einzige Copyleft-lizenzierte Abhängigkeit im gesamten Repository. Sie wird
ausschließlich in `Softwareschmiede.Tests` für E2E-UI-Automatisierung (Kategorie `E2E`) verwendet,
niemals von `Softwareschmiede`, `Softwareschmiede.App`, `Softwareschmiede.Plugin.Contracts` oder
einem der Plugin-Projekte referenziert. `PrivateAssets=all` (siehe
`src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj`) stellt sicher, dass die
FlaUI-Assemblies nicht transitiv propagieren und nicht im `dotnet publish`-Output von
`Softwareschmiede.App` erscheinen. Die GPL-3.0-Bedingungen wirken sich damit nicht auf das
veröffentlichte Produkt aus.
