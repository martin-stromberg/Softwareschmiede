# Mocking und Test-Doubles

## Vorhandene Austauschpunkte

`KiAusfuehrungsService` besitzt bereits einen optionalen Konstruktorparameter:

```csharp
IPseudoConsoleProcessLauncher? launcher = null
```

Ohne Parameter wird `Win32PseudoConsoleProcessLauncher` verwendet. Fuer Tests kann stattdessen `SimulatedPseudoConsoleProcessLauncher` oder ein Mock injiziert werden.

Weitere vorhandene abstrahierte Abhaengigkeiten:

- `IPluginManager`
- `IGitPlugin`
- `IKiPlugin`
- `IArbeitsverzeichnisResolver`
- `IGitWorkspaceBrowserService`
- `ITextDiffService`
- `IDialogService`
- `TimeProvider`

## TaskDetailViewModelTestFactory

Datei: `src/Softwareschmiede.Tests/Helpers/TaskDetailViewModelTestFactory.cs`

Die Factory mockt viele UI-/Plugin-Abhaengigkeiten, erzeugt aber einen echten `KiAusfuehrungsService`:

```csharp
var kiService = new KiAusfuehrungsService(
    NullLogger<KiAusfuehrungsService>.Instance,
    NullLoggerFactory.Instance,
    scopeFactoryMock.Object);
```

Damit verwendet die Factory indirekt den Default-Launcher `Win32PseudoConsoleProcessLauncher`, falls Tests ueber das ViewModel den ConPTY-Startpfad ausloesen. Fuer regulaere ViewModel-Tests ist das ein zentraler Entkopplungspunkt.

## Geeignete Umbaupunkte

1. Factory-Default auf Test-Double umstellen.

   `TaskDetailViewModelTestFactory.Create` koennte einen optionalen `KiAusfuehrungsService` oder `IPseudoConsoleProcessLauncher` entgegennehmen. Der Default sollte OS-frei sein.

2. Prozessstart in ViewModel-Tests deterministisch machen.

   Tests, die nur Statuswechsel, Command-Enablement oder Protokolle pruefen, sollten den Service oder Launcher so faken, dass kein echter Prozess startet.

3. Clipboard aus Control-Tests entkoppeln.

   `TerminalControl` hat aktuell private Methoden fuer Clipboard-Zugriff. Ohne Abstraktion muessen diese Tests als OS-Schnittstellen-Tests markiert bleiben. Eine injizierbare Clipboard-Schnittstelle wuerde regulaere Tests fuer Tastatur-/Encoding-Logik erlauben.

4. PseudoConsoleSession in Control-Tests OS-frei erzeugen.

   Da es bereits `IPseudoConsoleHandle` und `NullPseudoConsoleHandle` gibt, koennen Tests, die keine echte ConPTY brauchen, auf No-Op-Handles umgestellt werden, sofern der Konstruktor das zulaesst.

## Vorhandene Fakes/Simulationen

- `SimulatedPseudoConsoleProcessLauncher`
- `NullPseudoConsoleHandle`
- zahlreiche Moq-basierte Test-Doubles in ViewModel- und Service-Tests
- In-Memory-/Temp-DB-Helfer wie `TestDbContextFactory`

## Offene technische Entscheidung

Fuer `OsInterfaceFact`/`OsInterfaceTheory` ist zu klaeren, ob direkte `[Trait]`-Dekoration ausreicht oder ob ein zentrales Attribut mit xUnit Trait Discoverer eingefuehrt wird. Direkte Traits sind einfacher und filterbar; ein Attribut reduziert Wiederholung, ist aber fehleranfaelliger, wenn der Trait-Discoverer nicht korrekt registriert ist.
