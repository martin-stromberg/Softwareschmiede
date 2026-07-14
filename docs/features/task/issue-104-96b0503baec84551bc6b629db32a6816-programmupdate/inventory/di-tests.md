# DI, Tests und Testluecken

## DI-Registrierung

Die WPF-App baut ihren Host in `src/Softwareschmiede.App/App.xaml.cs`.

Relevante Stellen:

- `App.xaml.cs:65-74`: Host-Aufbau und `Services`.
- `App.xaml.cs:145-239`: zentrale `ConfigureServices`.
- `App.xaml.cs:162-189`: Domain/Application-Services.
- `App.xaml.cs:191-210`: Infrastructure-/Singleton-Services.
- `App.xaml.cs:212-224`: Plugin-Infrastruktur.
- `App.xaml.cs:226-238`: ViewModels und Windows.

Fuer das Update-Feature muessen neue Services hier registriert werden. Naheliegende Lebensdauern:

- Release-Check/Update-Orchestrator: Singleton oder Scoped, je nach State.
- HTTP-Client: ueber `IHttpClientFactory` waere sauber; aktuell ist kein `Microsoft.Extensions.Http` referenziert.
- Download/Entpack/Skriptgenerator: stateless Services, Singleton moeglich.
- ViewModel-Integration im `MainWindowViewModel`: via Konstruktor-Injection.

## Dialoge

`IDialogService.BestaetigenDialog` ist bereits fuer Sicherheitsabfragen vorhanden und in DI als `WpfDialogService` registriert.

Relevante Stellen:

- `IDialogService.cs:7-10`: bestaetigende Warnabfrage.
- `WpfDialogService.cs:20-25`: WPF-MessageBox mit Yes/No und Warning.
- `App.xaml.cs:209`: DI-Registrierung.

Fuer Unit-Tests kann `IDialogService` gemockt werden.

## Testprojekte

Es gibt zwei zentrale Testprojekte:

- `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj`
- `src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj`

`Softwareschmiede.Tests` ist `net10.0-windows10.0.17763.0`, nutzt xUnit, FluentAssertions, Moq, FlaUI und bUnit. Es referenziert WPF-App, Core-Projekt und Plugins. Blazor-Komponenten-Tests werden explizit nicht kompiliert.

`Softwareschmiede.IntegrationTests` ist `net10.0`, nutzt xUnit, FluentAssertions, Moq, SQLite und referenziert Core plus LocalDirectoryPlugin.

Relevante Stellen:

- `Softwareschmiede.Tests.csproj:3-11`: WPF-Testtarget.
- `Softwareschmiede.Tests.csproj:13-32`: Testpakete.
- `Softwareschmiede.Tests.csproj:39-43`: Blazor-Komponenten ausgeschlossen.
- `Softwareschmiede.Tests.csproj:45-56`: ProjectReferences.
- `Softwareschmiede.IntegrationTests.csproj:3-10`: Integrationstesttarget.
- `Softwareschmiede.IntegrationTests.csproj:12-27`: Testpakete.

## Vorhandene relevante Tests

Relevante vorhandene Testbereiche:

- `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelTests.cs`: Navigation, aktive Aufgaben, Sidebar-Status.
- `src/Softwareschmiede.Tests/Application/Services/CliProcessManagerTests.cs`, `CliProcessManagerTests_AktiverLauf.cs`, `CliProcessManagerTests_LaufStatus.cs`: Prozess-/Laufstatus-Persistenz.
- `src/Softwareschmiede.Tests/Application/Services/AufgabeServiceTests_AktiverLauf.cs`: aktiver Lauf und `LaufStatus`.
- `src/Softwareschmiede.Tests/Infrastructure/Terminal/CliRuntimeStatusEvaluatorTests.cs`: Wartet-Erkennung.
- `src/Softwareschmiede.Tests/App/Converters/KiAusfuehrungsStatusConverterTests.cs`: UI-Status aus `LaufStatus`.
- `src/Softwareschmiede.Tests/E2E/*`: FlaUI-WPF-E2E-Tests.

## Test-Hilfsskript

`scripts/Run-AllTestsIndividually.ps1` baut Testprojekte einzeln und fuehrt Tests isoliert aus. Es ist fuer instabile/flaky Tests gedacht und schreibt Ergebnisse nach `test-results/<Zeitstempel>/`.

Relevante Stellen:

- `Run-AllTestsIndividually.ps1:1-25`: Zweck und Ablauf.
- `Run-AllTestsIndividually.ps1:71-86`: Build je Testprojekt.
- `Run-AllTestsIndividually.ps1:88-103`: Testprojekt-Ermittlung und Initial-Build.
- `Run-AllTestsIndividually.ps1:168-180`: einzelner Testlauf.

## Testluecken fuer Update-Feature

Neu benoetigt:

- Release-Client-Tests mit Fake-HTTP fuer neuere/gleiche/aeltere Version, Pre-Release-Filter, fehlendes `release.zip`, API-Fehler.
- Versionsvergleich-Tests inklusive `v`-Prefix, SemVer und invaliden Tags.
- Download-/Entpack-Service-Tests mit Temp-Verzeichnissen und korruptem ZIP.
- Skriptgenerator-Tests fuer Pfade mit Leerzeichen, App-PID, Ziel-/Temp-Verzeichnis, Exe-Neustart.
- `MainWindowViewModel`-Tests fuer sichtbaren Update-Button, Command, Sicherheitsabfrage bei aktiven nicht-wartenden Aufgaben.
- Optional E2E-/UI-Test, dass der Sidebar-Footer bei Update-Verfuegbarkeit erscheint.
