# Bestandsaufnahme: Verbesserung der Aufgabenliste im Menü — kompakte Kacheln mit Symbolen und umschaltbaren Anzeigemodi

Analysiert wurde der Bereich "Aktive Aufgaben" (Seitenleisten- und Dashboard-Kachelliste) im WPF-Frontend, der dazugehörige Plugin-Vertrag sowie die Datenquelle `IAktiveAufgabenService`, bezogen auf die Anforderung aus `requirement.md` (Issue #288).

## Zusammenfassung

- **Kachelliste existiert bereits als wiederverwendbares Control:** `ActiveTasksListControl` (`src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml`) wird in der Seitenleiste von `MainWindow.xaml` (mit `ShowNavigationButton=true`, Standard) und im `DashboardView` (mit `ShowNavigationButton=false`) mit derselben `AktiveAufgabenListe` (Typ `ObservableCollection<AktiveAufgabePanelItem>`) betrieben.
- **Vollflächig klickbare Kachel existiert bereits:** Das Template `AufgabenKachelVollflaechigKlickbarTemplate` (Border mit `MouseBinding LeftClick` → `NavigateCommand` + `Id`-Parameter) wird heute bereits im Dashboard genutzt; der separate "→"-Button (`AufgabeNavigieren:{Titel}`) existiert nur im Seitenleisten-Template.
- **Aktuelle Kachel besteht aus sechs Zeilen:** Titel, Projektname (`Aufgabe.Projekt?.Name`, nicht Repository-Name), "SCM: {Name}", "KI: {Name}", Status-Volltext, To-do-Button. Symbole für SCM/KI/Status existieren noch nicht; es gibt kein `PluginSymbol` o. ä. im Plugin-Vertrag.
- **Modusumschaltung existiert noch nicht:** Es gibt weder ein Modus-Enum noch Gruppierung/Buttons in der Titelleiste. Als Namenskonvention für UI-Enums im ViewModels-Ordner existiert `DateibrowserAnsichtsmodus`.
- **Statuslogik existiert nur als Volltext-Converter:** `KiAusfuehrungsStatusConverter` liefert Texte wie "▶ Läuft", "⏸ Wartet", "✓ Bereit", "⏳ Prompt in Wartestellung", "⏸ Pausiert (noch …)"; eine reine Symbol-Variante fehlt.
- **`StatusUebergangsAnimation` greift in der Kachel vermutlich nicht:** `OnStatusChanged` verlangt `DataContext is Aufgabe` (`StatusUebergangsAnimation.cs:44`); im Kachel-Template ist der `DataContext` ein `AktiveAufgabePanelItem`, sodass die Animation dort frühzeitig abbricht (nur in Ansichten wirksam, deren DataContext eine `Aufgabe` ist).
- **Plugin-Vertrag ist für binärkompatible Erweiterungen vorbereitet:** `IGitPlugin` und `IPlugin`-nahe Member nutzen bereits Default-Interface-Implementierungen; `GitPluginBase<TPlugin>`/`CliKiPluginBase` stellen `virtual`/`abstract`-Member bereit. Ein neues Symbol-Member kann als Default-Interface-Member oder `virtual`-Property ergänzt werden, ohne bereits kompilierte Plugin-DLLs zu brechen (Plugins werden per `AssemblyLoadContext.Default.LoadFromAssemblyPath` aus dem `plugins`-Ordner geladen, `PluginManager.cs:174`).
- **Alle benötigten Daten liegen bereits vor:** `Aufgabe` enthält `KiPluginPrefix`, `GitRepository` (mit `RepositoryName` + `PluginTyp`), `Status`, `AusfuehrungsStatus`, `LaufStatus`, `AktiveRunId`, `LastHeartbeatUtc`, `LetzterCliStartUtc`, `PausiertBisUtc`, `ErstellungsDatum`. `GetAktiveAufgabenAsync` inkludiert `Projekt` und `GitRepository` und sortiert absteigend nach `LetzterCliStartUtc ?? ErstellungsDatum` (max. 20). Offene To-do-Anzahlen liefert `TodoService.GetOpenTodoCountsAsync`.
- **UIA-Anker sind dokumentiert und E2E-genutzt:** `AufgabeNavigieren:{Titel}` (Pfeil-Button), `Aufgabe:{Titel}` (Kachel-Border — erzeugt aber keinen `AutomationPeer` und ist per UIA unsichtbar), `AufgabeStatus:{Titel}` (HelpText = Status-Volltext), `AufgabeKachel:{Titel}` (HelpText "Aktiv:{bool};Pausiert:{bool}"), `AktiveAufgabeKiPlugin_{Titel}` (AutomationId der "KI: "-Zeile), `OffeneTodos:{Titel}` (To-do-Button).
- **Offene Lücken ggü. der Anforderung:** kein Plugin-Symbol im Vertrag, kein Repository-Name im Panel-Item (nur `ProjektName`), kein Sortierzeitpunkt/Gruppenschlüssel im Panel-Item, kein Modus-Enum, keine Gruppenanzeige, keine Modus-Buttons in der Titelleiste, kein UIA-ankerbares Kachel-Element für die vollflächig klickbare Variante.

## Test-Ausgangszustand

Vollständiger Build `dotnet build Softwareschmiede.slnx -c Debug`: **Exit 0, 0 Warnungen, 0 Fehler** (~12 s).

Vier Test-Lanes ausgeführt (jeweils mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`):

| Suite | Filter | Ergebnis |
|-------|--------|----------|
| Softwareschmiede.Tests | `Category!=OsInterface` | 2022 bestanden, **0 fehlgeschlagen**, 1 übersprungen, Exit 0 |
| Softwareschmiede.Tests | `Category=OsInterface` | 66 bestanden, **0 fehlgeschlagen**, 5 übersprungen (ConPTY-Sandbox-Skips), Exit 0 |
| Softwareschmiede.IntegrationTests | `Category!=OsInterface` | 194 bestanden, **0 fehlgeschlagen**, 0 übersprungen, Exit 0 |
| Softwareschmiede.IntegrationTests | `Category=OsInterface` | 9 bestanden, **0 fehlgeschlagen**, 0 übersprungen, Exit 0 |

**Keine vorab existierenden Testfehler nachgewiesen.** Details, Befehle, Zeiten und Nachweise: [inventory/tests.md](inventory/tests.md), Rohlogs und TRX-Reports unter [inventory/test-results/](inventory/test-results/).

## Details

- [UI / XAML-Struktur](inventory/ui.md) — `ActiveTasksListControl`-Templates, `MainWindow`-Seitenleiste, `DashboardView`
- [Datenmodell](inventory/models.md) — `AktiveAufgabePanelItem`, `Aufgabe`, `GitRepository`, `AppEinstellung`
- [Logik](inventory/logic.md) — `MainWindowViewModel`, `DashboardViewModel`, Converter, Behavior, `AufgabeService`, `PluginManager`, Hilfsdienste
- [Enums](inventory/enums.md) — `AufgabeStatus`, `AufgabeAusfuehrungsStatus`, `AufgabeLaufStatus`, `PluginType`, `DateibrowserAnsichtsmodus`
- [Interfaces und Plugin-Basisklassen](inventory/interfaces.md) — `IPlugin`, `IGitPlugin`, `IKiPlugin`, `IIdePlugin`, `IPluginManager`, `IAktiveAufgabenService`, `GitPluginBase<TPlugin>`, `CliKiPluginBase`
- [Plugin-Implementierungen](inventory/plugins.md) — SCM- und KI-Plugins mit Namen/Prefix
- [Tests](inventory/tests.md) — Test-Ausgangszustand, relevante Testklassen und E2E-Hilfen
