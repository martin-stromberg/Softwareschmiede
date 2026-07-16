# Drittanbieter-Lizenzen

Softwareschmiede selbst steht unter der [MIT-Lizenz](LICENSE). Diese Datei listet alle
NuGet-Abhängigkeiten der veröffentlichten Anwendung (`Softwareschmiede.App` inkl. der
Domain-/Service-Bibliothek `Softwareschmiede`, `Softwareschmiede.Plugin.Contracts` und aller
mitgelieferten Plugin-Projekte) sowie deren Lizenzen auf. Die Liste wurde per
`dotnet list package --include-transitive` je Projekt ermittelt und manuell um SPDX-Kennungen
ergänzt (Stand: 2026-07-16).

Reine Test-/Build-Werkzeuge, die nie Teil des Publish-Outputs werden, sind in einem eigenen
Abschnitt am Ende dieser Datei aufgeführt.

## Ausgelieferte Abhängigkeiten (direkt + transitiv)

| Paket | Version | Lizenz (SPDX) | Ausgeliefert in |
|---|---|---|---|
| Microsoft.EntityFrameworkCore.Sqlite | 10.0.9 | MIT | Domain/Service, App |
| Microsoft.EntityFrameworkCore | 10.0.9 | MIT | transitiv |
| Microsoft.EntityFrameworkCore.Abstractions | 10.0.9 | MIT | transitiv |
| Microsoft.EntityFrameworkCore.Analyzers | 10.0.9 | MIT | transitiv (Roslyn-Analyzer) |
| Microsoft.EntityFrameworkCore.Relational | 10.0.9 | MIT | transitiv |
| Microsoft.EntityFrameworkCore.Sqlite.Core | 10.0.9 | MIT | transitiv |
| Microsoft.Data.Sqlite.Core | 10.0.9 | MIT | transitiv |
| SQLitePCLRaw.lib.e_sqlite3 | 2.1.12 | Apache-2.0 | Domain/Service, App |
| SQLitePCLRaw.core | 2.1.11 | Apache-2.0 | transitiv |
| SQLitePCLRaw.bundle_e_sqlite3 | 2.1.11 | Apache-2.0 | transitiv |
| SQLitePCLRaw.provider.e_sqlite3 | 2.1.11 | Apache-2.0 | transitiv |
| Microsoft.Extensions.Caching.Memory | 10.0.9 | MIT | Domain/Service |
| Microsoft.Extensions.Caching.Abstractions | 10.0.9 | MIT | transitiv |
| Microsoft.Extensions.DependencyInjection | 10.0.9 | MIT | Domain/Service, App |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.9 | MIT | transitiv |
| Microsoft.Extensions.Logging | 10.0.9 | MIT | Domain/Service, App |
| Microsoft.Extensions.Logging.Abstractions | 10.0.9 | MIT | Domain/Service, App, alle Plugin-Projekte |
| Microsoft.Extensions.Logging.Console | 10.0.9 | MIT | App |
| Microsoft.Extensions.Logging.Configuration | 10.0.9 | MIT | transitiv |
| Microsoft.Extensions.Logging.Debug | 10.0.9 | MIT | transitiv |
| Microsoft.Extensions.Logging.EventLog | 10.0.9 | MIT | transitiv |
| Microsoft.Extensions.Logging.EventSource | 10.0.9 | MIT | transitiv |
| Microsoft.Extensions.Options | 10.0.9 | MIT | Domain/Service |
| Microsoft.Extensions.Options.ConfigurationExtensions | 10.0.9 | MIT | transitiv |
| Microsoft.Extensions.Hosting.Abstractions | 10.0.9 | MIT | Domain/Service |
| Microsoft.Extensions.Hosting | 10.0.9 | MIT | App |
| Microsoft.Extensions.Configuration | 10.0.9 | MIT | transitiv |
| Microsoft.Extensions.Configuration.Abstractions | 10.0.9 | MIT | transitiv |
| Microsoft.Extensions.Configuration.Binder | 10.0.9 | MIT | transitiv |
| Microsoft.Extensions.Configuration.CommandLine | 10.0.9 | MIT | transitiv |
| Microsoft.Extensions.Configuration.EnvironmentVariables | 10.0.9 | MIT | transitiv |
| Microsoft.Extensions.Configuration.FileExtensions | 10.0.9 | MIT | transitiv |
| Microsoft.Extensions.Configuration.Json | 10.0.9 | MIT | transitiv |
| Microsoft.Extensions.Configuration.UserSecrets | 10.0.9 | MIT | transitiv |
| Microsoft.Extensions.Diagnostics | 10.0.9 | MIT | transitiv |
| Microsoft.Extensions.Diagnostics.Abstractions | 10.0.9 | MIT | transitiv |
| Microsoft.Extensions.FileProviders.Abstractions | 10.0.9 | MIT | transitiv |
| Microsoft.Extensions.FileProviders.Physical | 10.0.9 | MIT | transitiv |
| Microsoft.Extensions.FileSystemGlobbing | 10.0.9 | MIT | transitiv |
| Microsoft.Extensions.DependencyModel | 10.0.9 | MIT | transitiv |
| Microsoft.Extensions.Primitives | 10.0.9 | MIT | transitiv |
| Serilog | 4.3.0 | Apache-2.0 | transitiv (App) |
| Serilog.Extensions.Hosting | 10.0.0 | Apache-2.0 | App |
| Serilog.Extensions.Logging | 10.0.0 | Apache-2.0 | transitiv |
| Serilog.Sinks.Console | 6.1.1 | Apache-2.0 | App |
| Serilog.Sinks.File | 7.0.0 | Apache-2.0 | App |

Alle aufgeführten Lizenzen sind permissiv (MIT bzw. Apache-2.0 mit Patentschutz) und untereinander
sowie mit der MIT-Lizenz des Hauptprojekts kompatibel.

## Nur Test/Build – nicht ausgeliefert

Die folgenden Pakete werden ausschließlich in `Softwareschmiede.Tests`,
`Softwareschmiede.IntegrationTests` oder als Design-Time-Werkzeug verwendet. Sie sind über
`PrivateAssets=all` (bzw. reine Testprojekt-Referenz ohne Publish-Pfad) von jeglicher Weitergabe in
`release.zip` ausgeschlossen und daher für die Lizenzwahl des veröffentlichten Produkts irrelevant.

| Paket | Version | Lizenz (SPDX) | Hinweis |
|---|---|---|---|
| FlaUI.Core | 5.0.0 | GPL-3.0-or-later | UI-Automatisierung für E2E-Tests; `PrivateAssets=all` verhindert transitive Weitergabe (siehe Schritt „Lizenz-Hygiene FlaUI“) |
| FlaUI.UIA3 | 5.0.0 | GPL-3.0-or-later | s. o. |
| xunit | 2.9.3 | Apache-2.0 | Test-Framework |
| xunit.runner.visualstudio | 3.1.5 | Apache-2.0 | Test-Runner-Integration |
| Xunit.SkippableFact | 1.5.23 | MS-PL | Bedingtes Überspringen von Tests (z. B. ConPTY im CI) |
| Microsoft.NET.Test.Sdk | 18.7.0 | MIT | Test-SDK |
| Moq | 4.* | BSD-3-Clause | Mocking-Framework |
| bunit | 2.7.2 | MIT | Blazor-Komponenten-Tests |
| coverlet.collector | 10.0.1 | MIT | Code-Coverage-Collector |
| FluentAssertions | 8.10.0 | Kommerzielle Lizenz (Xceed) | Ab Version 8 kein OSI-Standardlizenztext mehr, sondern eine kommerzielle Lizenz von Xceed Software Inc.: kostenlos für Open-Source-Projekte und nicht-kommerzielle Nutzung, kostenpflichtig für kommerzielle Nutzung. Da Softwareschmiede als Open-Source-Projekt veröffentlicht wird und das Paket nur im Testprojekt verwendet wird (nicht Teil des Publish-Outputs), ergibt sich hieraus keine Einschränkung für Nutzer der Anwendung. |
| Microsoft.EntityFrameworkCore.InMemory | 10.0.9 | MIT | In-Memory-Datenbank für Tests |
| Microsoft.Extensions.TimeProvider.Testing | 10.7.0 | MIT | Fakeable `TimeProvider` für deterministische Zeit-Tests |
| Microsoft.EntityFrameworkCore.Design | 10.0.9 | MIT | Design-Time-Werkzeug für EF-Core-Migrationen/Scaffolding, bereits vor diesem Vorhaben mit `PrivateAssets=all` referenziert; bringt eigene, ausschließlich zur Entwicklungszeit benötigte Transitiva mit (u. a. die `Microsoft.CodeAnalysis.*`-Familie, `Humanizer.Core`, `Newtonsoft.Json`, `System.Composition.*`, `System.CodeDom`, `Mono.TextTemplating`, `Microsoft.Build.Framework`, `Microsoft.VisualStudio.SolutionPersistence`) – diese werden hier nicht einzeln aufgeführt, da sie nie Teil des Publish-Outputs werden und ausschließlich dem lokalen `dotnet ef`-Workflow dienen |

## FlaUI (GPL-3.0-or-later) — Einordnung

FlaUI ist die einzige Copyleft-lizenzierte Abhängigkeit im gesamten Repository. Sie wird
ausschließlich in `Softwareschmiede.Tests` für E2E-UI-Automatisierung (Kategorie `E2E`) verwendet,
niemals von `Softwareschmiede`, `Softwareschmiede.App`, `Softwareschmiede.Plugin.Contracts` oder
einem der Plugin-Projekte referenziert. `PrivateAssets=all` (siehe
`src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj`) stellt sicher, dass die
FlaUI-Assemblies nicht transitiv propagieren und nicht im `dotnet publish`-Output von
`Softwareschmiede.App` erscheinen. Die GPL-3.0-Bedingungen wirken sich damit nicht auf das
veröffentlichte Produkt aus.
