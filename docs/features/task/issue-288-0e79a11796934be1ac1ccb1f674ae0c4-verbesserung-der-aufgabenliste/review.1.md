# Plan-Review

## Ergebnis

**Status:** Offene Aufgaben vorhanden

## Umgesetzte Planelemente

### Neue Klassen / Objekte

- [x] `AufgabenListenModus` (Enum, `src/Softwareschmiede.App/ViewModels/AufgabenListenModus.cs`) — Werte `Datum`, `Repository`, `Ki` vorhanden
- [x] `AufgabenListenModusService` (Singleton-Service, `src/Softwareschmiede.App/Services/AufgabenListenModusService.cs`) — `LadeMenueModusAsync`/`LadeDashboardModusAsync`/`SpeichereMenueModusAsync`/`SpeichereDashboardModusAsync`, Scope-Factory-Muster nach `DarkModeService`, `Enum.TryParse` + `Enum.IsDefined` mit `Datum`-Fallback — vorhanden
- [x] `KiAusfuehrungsStatusSymbolConverter` (`src/Softwareschmiede.App/Converters/AppConverters.cs`, registriert in `App.xaml`) — liefert nur Glyphen `⏸`/`⏳`/`▶`/`✓`, `ConvertBack` wirft `NotSupportedException`
- [x] `KiAusfuehrungsZustand` (interner Enum) — `Pausiert`, `PromptInWartestellung`, `Laeuft`, `Wartet`, `Bereit` vorhanden
- [x] `KiAusfuehrungsStatusKlassifikation` (interne statische Klasse) — extrahierte Statuslogik; `KiAusfuehrungsStatusConverter` delegiert, Volltext-Ausgabe unverändert (inkl. Pausiert-Countdown-Formatierung)
- [x] `AufgabenListenAnsicht` (statische Klasse, `src/Softwareschmiede.App/Controls/AufgabenListenAnsicht.cs`) — `Konfiguriere(ListCollectionView, AufgabenListenModus)`, `ErstelleSortierDeskriptoren`, `ErstelleGruppenDeskriptor`, Konstanten `NichtAusgefuehrt`/`OhneRepository`; Tie-Breaker `Titel`/`Id` nachgezogen (in Plan als Prüfpunkt genannt)

### Plugin-Vertrag

- [x] `IPlugin.PluginSymbol` (`src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IPlugin.cs:35`) — Default-Interface-Member `"◻"`, binärkompatibel
- [x] `GitPluginBase<TPlugin>.PluginSymbol` — `virtual` mit Fallback `"◻"` (Zeile 22)
- [x] `CliKiPluginBase.PluginSymbol` — `virtual` mit Fallback `"◻"` (Zeile 22)
- [x] `PluginSymbol`-Overrides in allen acht Repo-Plugins: `BitbucketPlugin` (🪣), `GitHubPlugin` (🐙), `LocalDirectoryPlugin` (📁), `ClaudeCliPlugin` (✳), `CodexPlugin` (◈), `DevinPlugin` (🤖), `GitHubCopilotPlugin` (✈), `KiSimulatorPlugin` (🧪)

### Persistenz / Konfiguration

- [x] `AppEinstellungService.AktiveAufgabenListenModusKey` (`"aktiveaufgaben.listenmodus"`) und `AktiveAufgabenListenModusDashboardKey` (`"aktiveaufgaben.listenmodus.dashboard"`) — vorhanden
- [x] `App.xaml.cs`: `services.AddSingleton<AufgabenListenModusService>()` + Auflösung in der `MainWindowOptionaleDienste`-Factory — vorhanden
- [x] `MainWindowOptionaleDienste`-Record um `AufgabenListenModusService? AufgabenListenModusService = null` erweitert — vorhanden

### ViewModels

- [x] `AktiveAufgabePanelItem` — neue Properties `RepositoryName`, `ScmPluginSymbol`, `KiPluginSymbol`, `RepositoryGruppenName`, `RepositoryGruppeIstFallback`, `KiGruppenName`, `KiGruppeIstFallback`, `SortierZeitpunkt`; `ProjektName` entfernt (keine Referenzen mehr)
- [x] `MainWindowViewModel.AufgabenListenModus` — Property mit Default `Datum`, Setter persistiert via `SpeichereMenueModusAsync` + `SafeFireAndForget`, setzt `_aufgabenListenModusGeaendert`
- [x] `MainWindowViewModel.GespeichertenAufgabenListenModusLadenAsync` — setzt geladenen Modus über `_dispatcherInvoke` ohne Rückschreiben, Race-Guard + `_disposed`-Check, No-op ohne Service; Konstruktor-Anstoß per `SafeFireAndForget`
- [x] `MainWindowViewModel.ResolvePlugin` — Nachfolger von `ResolvePluginName`, Rückgabe `(Name, Symbol)`, Fallback-Glyph `◻` bei nicht auflösbarem Plugin oder leerem/`null` `PluginSymbol`
- [x] `MainWindowViewModel.MapAktiveAufgabePanelItem` — befüllt alle neuen Felder inkl. Gruppen-Fallbacks (`KiGruppenName` = PluginName / roher Prefix / "Nicht ausgeführt"; `RepositoryGruppenName` = RepositoryName / "Ohne Repository") und `SortierZeitpunkt = LetzterCliStartUtc ?? ErstellungsDatum`
- [x] `DashboardViewModel.AufgabenListenModus` — eigener Modus-Zustand mit `SpeichereDashboardModusAsync`-Persistenz + `_aufgabenListenModusGeaendert`-Flag
- [x] `DashboardViewModel.GespeichertenAufgabenListenModusLadenAsync` — Dashboard-Schlüssel, ohne Rückschreiben, Race-Guard; aus `Initialize` per `SafeFireAndForget` angestoßen; optionaler Konstruktorparameter `AufgabenListenModusService?`

### UI

- [x] `ActiveTasksListControl.xaml` — `AufgabenKachelInhaltTemplate` dreizeilig (Titel, `RepositoryName`, Symbolzeile: SCM-Glyph `AktiveAufgabeScmPlugin_{Titel}` + `SCM: {Name}`-ToolTip/HelpText + `NullOrEmptyToVisibilityConverter`, KI-Glyph `AktiveAufgabeKiPlugin_{Titel}` analog, Status-Anker `AufgabeStatus:{Titel}` mit Volltext-HelpText + separater Glyph-`TextBlock` mit `KiAusfuehrungsStatusSymbolConverter` und `StatusUebergangsAnimation.Status` an den Symbol-Converter, `OffeneTodos:{Titel}`-Button, `AufgabeKachel:{Titel}`-Marker)
- [x] Kachel-Template `AufgabenKachelTemplate` als vollflächiger `Button` (`AufgabeNavigieren:{Titel}`, `Cursor="Hand"`, `Command`/`CommandParameter`, `ControlTemplate` mit bisherigem `Border` inkl. `IsAktiv`/`IstPausiert`-Triggern); `AufgabenKachelMitNavigationButtonTemplate`, `AufgabenKachelVollflaechigKlickbarTemplate`, `MouseBinding` und `ShowNavigationButton`-Trigger entfernt
- [x] `ItemsControl.GroupStyle` mit `HeaderTemplate` (`AutomationProperties.Name="AufgabenGruppe:{Name}"`)
- [x] `ActiveTasksListControl.xaml.cs` — `ShowNavigationButton`-DP entfernt; `ListenModus`-DP (Default `Datum`, Callback wendet `AufgabenListenAnsicht` an); instanz-eigene `ListCollectionView` über `ItemsSource` (kein `GetDefaultView`); `OffeneTodosButton_Click` mit `e.Handled = true` erhalten
- [x] `MainWindow.xaml` — drei `RadioButton`s `AufgabenModus:Datum`/`Repository`/`KI` via `EnumToBoolConverter`, `Visibility` an `IsNavigationExpanded`; `ListenModus`-Binding + `AutomationId="AktiveAufgabenListeMenue"` am Control
- [x] `DashboardView.xaml` — drei `RadioButton`s `DashboardAufgabenModus:Datum`/`Repository`/`KI` an `DashboardViewModel.AufgabenListenModus`; `ShowNavigationButton="False"` entfernt; `ListenModus`-Binding + `AutomationId="AktiveAufgabenListeDashboard"`
- [x] `StatusUebergangsAnimation.OnStatusChanged` — akzeptiert `AktiveAufgabePanelItem` (`item.Id` als Schlüssel) neben `Aufgabe`
- [x] `App.xaml` — `KiAusfuehrungsStatusSymbolConverter` als globale Resource registriert

### Unit-/Integrationstests

- [x] `KiAusfuehrungsStatusSymbolConverterTests` (`src/Softwareschmiede.Tests/App/Converters/`) — 7 Methoden: alle Zustände → Glyph, ungültige Eingabe → `string.Empty`, `ConvertBack` → `NotSupportedException`
- [x] `AufgabenListenAnsichtTests` (`src/Softwareschmiede.Tests/App/Controls/`) — 5 Methoden: Deskriptoren je Modus, Fallback-Gruppe zuletzt, gruppeninterne `SortierZeitpunkt`-Sortierung
- [x] `AufgabenListenModusServiceTests` (`src/Softwareschmiede.Tests/ServiceIntegration/`) — 6 Methoden: Roundtrip über getrennte Scopes je Schlüssel, fehlender/ungültiger/leerer Wert → `Datum`, Schlüssel-Unabhängigkeit
- [x] `PluginSymbolTests` (`src/Softwareschmiede.Tests/`) — alle acht Repo-Plugins ≠ Interface-Default; Minimal-`IPlugin` → Interface-Default
- [x] `MainWindowViewModelTests` — neue Mapping-Tests (`ShouldMapRepositoryName` inkl. Projektfallback, `ShouldMapPluginSymbols` inkl. `null`-Toleranz/Fallback-Glyph, `ShouldMapKiGruppenName_ForAlleFaelle`, `ShouldMapRepositoryGruppenName_ForAlleFaelle`, `ShouldMapSortierZeitpunkt`) und Modus-Tests (Default/Setzbarkeit/Restore/Persistenz/kein Write-Back)
- [x] `DashboardViewModelTests` — Modus-Tests (Default, Setzbarkeit/`PropertyChanged`, Restore, Persistenz); `CreateSut` um optionalen Service-Parameter erweitert

### E2E

- [x] `MenuView` — `GetActiveTaskKiPluginName` liest `HelpText`; neue Helfer `SetzeAufgabenListenModus`, `GetAufgabenGruppenNamen`/`WaitForAufgabenGruppe` (gescoped unter `AktiveAufgabenListeMenue`), `GetActiveTaskKiPluginSymbol`, `GetActiveTaskScmPluginSymbol`, `GetActiveTaskStatusGlyph`, `KlickeOffeneTodosButton`, `EnsureNavigationExpanded`; `NavigateToTask` und `WaitForTaskStatus` auf Scoping unter `AktiveAufgabenListeMenue` umgestellt
- [x] `DashboardView` (E2E-Hilfsklasse) — `SetzeAufgabenListenModus` (`DashboardAufgabenModus:{modus}`), `GetAufgabenGruppenNamen`/`WaitForAufgabenGruppe` gescoped unter `AktiveAufgabenListeDashboard`
- [x] `E2E_AufgabenListeModi.cs` — `AufgabenListe_KompakteKachelSymboleUndModuswechsel_E2E` deckt alle sechs Pflicht-Szenarien ab (Symbolzeile, To-do-Button-ohne-Navigation via `OpenTodosDialogView` + `TaskDetailView.IsVisible == false`, Kachelklick-Navigation, Seitenleisten-Modi inkl. Fallback-Gruppen, Dashboard-Modus inkl. DB-Assertion beider Schlüssel + Schlüssel-Unabhängigkeit, Wiederherstellung nach `RestartAppPreservingDatabase` für beide Ansichten, Rücksetzen auf `Datum`); in `MainTest.cs` `General_RepositoryUndAufgaben_E2E` eingehängt
- [x] `E2E_AufgabePausieren` — Kommentar zum entfallenen "→"-Button/`ShowNavigationButton` aktualisiert

## Offene Aufgaben

- [ ] `Verifikation: vollständiger Build + beide Test-Lanes` (Tasks-Datei #36) — nicht verifizierbar umgesetzt: Es existieren Build-Artefakte (`Softwareschmiede.App.dll` 23:54, `Softwareschmiede.Tests.dll` 00:32) und E2E-Lauf-Spuren (`.e2e-tmp/` mit Test-DB bis 01:07, App-Log `softwareschmiede-20261019.log` mit regulärem Shutdown), die auf ausgeführte Builds/Tests hindeuten. Es gibt jedoch **keine dokumentierten Testergebnisse** (kein `.trx`, kein `test-results.md`, keine Logs) nach dem Stand der letzten Codeänderung (00:32), mit denen sich das erfolgreiche Durchlaufen beider Lanes (`Category!=OsInterface` und `Category=OsInterface`) nachweisen ließe.

## Hinweise

- **Dokumentierte Planabweichung (bewusst, funktional äquivalent):** Der Plan sah vor, dass `GetActiveTaskStatusGlyph` den Glyph über das **TextPattern** des `AufgabeStatus:{Titel}`-Elements liest. Da `TextBlock` kein TextPattern unterstützt, wurde der Status auf zwei Elemente aufgeteilt: ein leerer Anker-`TextBlock` mit `AutomationProperties.Name="AufgabeStatus:{Titel}"` + Volltext-`HelpText` (E2E-Vertrag `WaitForTaskStatus` unverändert) und ein separater Glyph-`TextBlock` mit `AutomationId="AktiveAufgabeStatusGlyph_{Titel}"`, dessen UIA-`Name` der Glyph ist (`MenuView.GetActiveTaskStatusGlyph` liest diesen). Der sichtbare Vertrag und alle Anker sind erhalten.
- **Sortier-Tie-Breaker:** Die im Plan als Prüfpunkt genannten Quell-Tie-Breaker (`ThenBy(Titel)`/`ThenBy(Id)`) wurden als zusätzliche `SortDescriptions` in `AufgabenListenAnsicht` nachgezogen — umgesetzt.
- **Nicht angeforderte Änderungen im Working Tree:** untracked Datei `nul` (163 Byte, Artefakt eines falsch maskierten `> nul`-Redirects) und Verzeichnis `.e2e-tmp/` mit E2E-Testdaten — beide sind Build-/Laufzeit-Artefakte und sollten vor dem Commit entfernt bzw. ignoriert werden.
- Vor der finalen Verifikation ggf. prüfen, ob eine laufende `Softwareschmiede.App.exe` den Build sperrt (Self-Hosting-Risiko laut CLAUDE.md — Prozess niemals eigenständig beenden).
