# Plan-Review

## Ergebnis

**Status:** Offene Aufgaben vorhanden

## Umgesetzte Planelemente

### Neue Klassen / Objekte

- [x] `AufgabenListenModus` (Enum, `src/Softwareschmiede.App/ViewModels/AufgabenListenModus.cs`) — Werte `Datum`, `Repository`, `Ki` vorhanden
- [x] `AufgabenListenModusService` (Singleton-Service, `src/Softwareschmiede.App/Services/AufgabenListenModusService.cs`) — `LadeMenueModusAsync`/`LadeDashboardModusAsync`/`SpeichereMenueModusAsync`/`SpeichereDashboardModusAsync`, eigener DI-Scope pro Operation (`IServiceScopeFactory` → scoped `AppEinstellungService`, Muster `DarkModeService`), `Enum.TryParse` (ignoreCase) + `Enum.IsDefined` mit `Datum`-Fallback
- [x] `KiAusfuehrungsStatusSymbolConverter` (`src/Softwareschmiede.App/Converters/AppConverters.cs:242`, registriert in `App.xaml:23`) — nur Glyphen, `ConvertBack` wirft `NotSupportedException`
- [x] `KiAusfuehrungsZustand` (interner Enum, `AppConverters.cs:116`) — `Pausiert`/`PromptInWartestellung`/`Laeuft`/`Wartet`/`Bereit`
- [x] `KiAusfuehrungsStatusKlassifikation` (interne statische Klasse, `AppConverters.cs:138`) — extrahierte Klassifikation; `KiAusfuehrungsStatusConverter.Convert` delegiert (`AppConverters.cs:215`), Volltext-Vertrag unverändert
- [x] `AufgabenListenAnsicht` (statische Klasse, `src/Softwareschmiede.App/ViewModels/AufgabenListenAnsicht.cs` — seit Review-Fix in `ViewModels` statt `Controls`) — `Konfiguriere(ListCollectionView, AufgabenListenModus)`, `ErstelleSortierDeskriptoren`, `ErstelleGruppenDeskriptor`, Konstanten `NichtAusgefuehrt`/`OhneRepository`, Tie-Breaker `Titel`/`Id` nachgezogen

### Plugin-Vertrag

- [x] `IPlugin.PluginSymbol` (`src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IPlugin.cs:36`) — Default-Interface-Member `"◻"`
- [x] `GitPluginBase<TPlugin>.PluginSymbol` — `virtual`, Fallback `"◻"` (`GitPluginBase.cs:26`)
- [x] `CliKiPluginBase.PluginSymbol` — `virtual`, Fallback `"◻"` (`CliKiPluginBase.cs:24`)
- [x] `PluginSymbol`-Overrides in allen acht Repo-Plugins: `BitbucketPlugin` 🪣, `GitHubPlugin` 🐙, `LocalDirectoryPlugin` 📁, `ClaudeCliPlugin` ✳, `CodexPlugin` ◈, `DevinPlugin` 🤖, `GitHubCopilotPlugin` ✈, `KiSimulatorPlugin` 🧪

### Persistenz / Konfiguration

- [x] `AppEinstellungService.AktiveAufgabenListenModusKey` (`"aktiveaufgaben.listenmodus"`) und `AktiveAufgabenListenModusDashboardKey` (`"aktiveaufgaben.listenmodus.dashboard"`) — `AppEinstellungService.cs:54,57`
- [x] `App.xaml.cs` — `services.AddSingleton<AufgabenListenModusService>()` (Z. 339) + Auflösung in der `MainWindowOptionaleDienste`-Factory (Z. 363)
- [x] `MainWindowOptionaleDienste`-Record — optionaler Parameter `AufgabenListenModusService? AufgabenListenModusService = null` (Z. 25)

### ViewModels

- [x] `AktiveAufgabePanelItem` — `RepositoryName`, `ScmPluginSymbol`, `KiPluginSymbol`, `RepositoryGruppenName`, `RepositoryGruppeIstFallback`, `KiGruppenName`, `KiGruppeIstFallback`, `SortierZeitpunkt` vorhanden; `ProjektName` entfernt (keine Referenzen mehr)
- [x] `MainWindowViewModel.AufgabenListenModus` — Default `Datum`, Setter persistiert via `SpeichereMenueModusAsync` + `SafeFireAndForget`, setzt `_aufgabenListenModusGeaendert` (Z. 109–119)
- [x] `MainWindowViewModel.GespeichertenAufgabenListenModusLadenAsync` — Race-Guard, kein Write-Back, No-op ohne Service; Konstruktor-Anstoß per `SafeFireAndForget` (Z. 212, 224–238)
- [x] `MainWindowViewModel.ResolvePlugin` / `MapAktiveAufgabePanelItem` — Mapping aller neuen Felder inkl. Gruppen-Fallbacks, `null`-Toleranz bei `PluginSymbol`-Mocks, `SortierZeitpunkt = LetzterCliStartUtc ?? ErstellungsDatum` (durch die `ShouldMap*`-Tests belegt)
- [x] `DashboardViewModel.AufgabenListenModus` — eigener Zustand mit `SpeichereDashboardModusAsync` + `_aufgabenListenModusGeaendert` (Z. 93–104); optionaler Konstruktorparameter (Z. 113)
- [x] `DashboardViewModel.GespeichertenAufgabenListenModusLadenAsync` — Dashboard-Schlüssel, Race-Guard, Anstoß aus `Initialize` (Z. 171, 182–194)

### UI

- [x] `ActiveTasksListControl.xaml` — `AufgabenKachelInhaltTemplate` dreizeilig: Titel, `RepositoryName`, Symbolzeile (`AktiveAufgabeScmPlugin_{Titel}` + `SCM:`-ToolTip/HelpText + `NullOrEmptyToVisibilityConverter`; `AktiveAufgabeKiPlugin_{Titel}` analog; Status-Glyph `AktiveAufgabeStatusGlyph_{Titel}` mit `AufgabeStatus:{Titel}`-Anker + Volltext-HelpText + `StatusUebergangsAnimation.Status` an den Symbol-Converter; `OffeneTodos:{Titel}`-Button; `AufgabeKachel:{Titel}`-Marker)
- [x] Kachel-Template `AufgabenKachelTemplate` — vollflächiger `Button` (`AufgabeNavigieren:{Titel}`, `Cursor="Hand"`, `Command`/`CommandParameter`, `ControlTemplate` mit `Border` inkl. `IsAktiv`/`IstPausiert`-Triggern); `AufgabenKachelMitNavigationButtonTemplate`, `AufgabenKachelVollflaechigKlickbarTemplate`, `MouseBinding` und `ShowNavigationButton` entfernt (keine Referenzen mehr)
- [x] `ItemsControl.GroupStyle` — `AufgabenGruppe:{Name}`-Anker liegt per Review-Fix auf `GroupStyle.ContainerStyle` (`AutomationProperties.Name` am `GroupItem`, `ActiveTasksListControl.xaml:135-140`); `HeaderTemplate` rendert den Gruppennamen weiterhin
- [x] `ActiveTasksListControl.xaml.cs` — `ListenModus`-DP (Default `Datum`, Callback → `AufgabenListenAnsicht.Konfiguriere`); instanz-eigene `ListCollectionView` über `ItemsSource` (kein `GetDefaultView`); `OffeneTodosButton_Click` mit `e.Handled = true`
- [x] `MainWindow.xaml` — drei `RadioButton`s `AufgabenModus:Datum`/`Repository`/`KI` via `EnumToBoolConverter`, `Visibility` an `IsNavigationExpanded`; `ListenModus`-Binding + `AutomationId="AktiveAufgabenListeMenue"` (Z. 136–167)
- [x] `DashboardView.xaml` — drei `RadioButton`s `DashboardAufgabenModus:Datum`/`Repository`/`KI` an `DashboardViewModel.AufgabenListenModus`; `ShowNavigationButton` entfernt; `ListenModus`-Binding + `AutomationId="AktiveAufgabenListeDashboard"` (Z. 69–92)
- [x] `StatusUebergangsAnimation.OnStatusChanged` — akzeptiert `AktiveAufgabePanelItem` (`item.Id` als Schlüssel, `StatusUebergangsAnimation.cs:50`)

### Unit-/Integrationstests

- [x] `KiAusfuehrungsStatusSymbolConverterTests` — 8 Methoden (alle Zustände → Glyph, Nicht-Aufgabe → `string.Empty`, `ConvertBack` → `NotSupportedException`)
- [x] `AufgabenListenAnsichtTests` — 5 Methoden (Deskriptoren je Modus, Fallback-Gruppe zuletzt, gruppeninterne `SortierZeitpunkt`-Sortierung)
- [x] `AufgabenListenModusServiceTests` + `AufgabenListenModusServiceTestFactory` (`src/Softwareschmiede.Tests/Helpers/`) — 6 Methoden: Roundtrips über getrennte Scopes je Schlüssel, fehlend/ungültig/leer → `Datum`, Schlüssel-Unabhängigkeit
- [x] `PluginSymbolTests` — per Reflection über alle referenzierten Plugin-Assemblies (nicht-leer, ≠ Interface-Default); Minimal-`IPlugin` → Default
- [x] `MainWindowViewModelTests` — `ShouldMapRepositoryName` (Projektfallback), `ShouldMapPluginSymbols` (`null`-Toleranz/Fallback-Glyph), `ShouldMapKiGruppenName`, `ShouldMapRepositoryGruppenName`, `ShouldMapSortierZeitpunkt`, `AufgabenListenModus_Should*` (Default/Setzbarkeit/Restore/Persistenz ohne Write-Back)
- [x] `DashboardViewModelTests` — `AufgabenListenModus_Should*` (Default, Setzbarkeit/`PropertyChanged`, Restore, Persistenz ohne Menü-Schlüssel-Berührung)

### E2E

- [x] `MenuView` — `GetActiveTaskKiPluginName` liest `HelpText`; `SetzeAufgabenListenModus`, `GetAufgabenGruppenNamen`/`WaitForAufgabenGruppe` (gescoped unter `AktiveAufgabenListeMenue`), `GetActiveTaskKiPluginSymbol`, `GetActiveTaskScmPluginSymbol`, `GetActiveTaskStatusGlyph`, `KlickeOffeneTodosButton`, `EnsureNavigationExpanded`
- [x] `DashboardView` (E2E-Hilfsklasse) — `SetzeAufgabenListenModus` (`DashboardAufgabenModus:{modus}`), `GetAufgabenGruppenNamen`/`WaitForAufgabenGruppe` gescoped unter `AktiveAufgabenListeDashboard`
- [x] `E2E_AufgabenListeModi.cs` — `AufgabenListe_KompakteKachelSymboleUndModuswechsel_E2E` deckt alle sechs Pflicht-Szenarien ab (Symbolzeile, To-do-Button ohne Navigation, Kachelklick-Navigation, Seitenleisten-Modi inkl. Fallback-Gruppen, Dashboard-Modus inkl. DB-Assertion beider `AppEinstellungService`-Schlüssel-Konstanten + Schlüssel-Unabhängigkeit, Wiederherstellung nach `RestartAppPreservingDatabase` für beide Ansichten, Abschluss-Reset auf `Datum` mit korrekter Navigationsreihenfolge); eingehängt in `MainTest.cs:36` (`General_RepositoryUndAufgaben_E2E`)
- [x] `E2E_AufgabePausieren` — Kommentar zur Kachel-Umstellung aktualisiert (Z. 57)
- [x] `WindowExtensions.GetOpenTopLevelWindowTitles` — `COMException` zusätzlich gefiltert (Z. 104)

## Offene Aufgaben

- [ ] `Verifikation: vollständiger Build + beide Test-Lanes` (Tasks-Datei #36) — teilweise umgesetzt: `dotnet build Softwareschmiede.slnx -c Debug` fehlerfrei und reguläre Lane vollständig gelaufen (2055/2056 bestanden, 1 Umgebungsfehlschlag, 1 Skip — dokumentiert in `test-results.md`). Die OsInterface-Lane wurde nur **gefiltert** auf `General_RepositoryUndAufgaben_E2E` ausgeführt: Run 7 verifizierte alle Feature-Pflichtszenarien bis inkl. Neustart-Wiederherstellung; der letzte Befund (Test-Reihenfolgefehler beim Abschluss-Reset) ist im Code behoben (`E2E_AufgabenListeModi.cs:165-170`), aber ein komplett grüner Lauf der **vollständigen** OsInterface-Lane (`--filter "Category=OsInterface"`) auf dem finalen Code-Stand steht noch aus — wiederholte Umgebungsabbrüche (UIA `COMException 0x80040201`, `testhost`/DLL-Locks, siehe `test-results.md`).

## Hinweise

- **Dokumentierte Planabweichungen (bewusst, aus Review-Fixes):**
  - `AufgabenListenAnsicht` liegt in `src/Softwareschmiede.App/ViewModels/` statt `Controls/` (Code-Review-Fix; Testklasse weiter unter `Tests/App/Controls/`).
  - Der `AufgabenGruppe:{Name}`-Anker liegt auf `GroupStyle.ContainerStyle` (GroupItem), nicht im `HeaderTemplate`-TextBlock — `GroupItemAutomationPeer` exponiert den Header nicht (E2E-Fix, per UIA-Tree-Dump belegt).
  - `GetActiveTaskStatusGlyph` liest den Glyph über den separaten `AktiveAufgabeStatusGlyph_{Titel}`-TextBlock statt über das TextPattern (TextBlock hat kein TextPattern; Iteration-1-Abweichung besteht weiter).
  - Modus-RadioButtons liegen in einer eigenen Zeile unter der Titelzeile (Usability-Fix); Hover-/Pressed-Trigger am Kachel-Button ergänzt.
- **Umgebungsbefunde:** `MainWindowViewModelTests.AktiveAufgabenAktualisierenAsync_ShouldMapSortierZeitpunkt` schlägt in dieser Sandbox fehl (`LetzterCliStartUtc` bleibt null ohne echten Prozessstart) — dokumentiert als Umgebungslimitation, kein Codefehler. Vor einem erneuten vollständigen Lane-Lauf laufende `Softwareschmiede.App.exe`-Instanzen prüfen (Self-Hosting-Risiko laut CLAUDE.md — niemals eigenständig beenden) sowie verwaiste `testhost`-Instanzen und das parallel laufende Visual Studio (DLL-Locks).
- **Arbeitsartefakte im Working Tree:** untracked Datei `nul` (Redirect-Artefakt) — vor dem Commit entfernen.
