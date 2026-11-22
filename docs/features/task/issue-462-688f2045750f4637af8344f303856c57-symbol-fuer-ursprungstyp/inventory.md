# Bestandsaufnahme: Symbol für Auftragsart auf den Aufgabenkacheln (Issue #462)

Bestandsaufnahme der aktiven Aufgabenliste (Kachel-Rendering, Auftragsart-Mapping, Plugin-Symbol-Vorbilder, Tests und Hilfsdoku) bezogen auf die Anforderung, dem Auftragsart-Badge ein Unicode-Symbol voranzustellen ([requirement.md](requirement.md)).

## Zusammenfassung

- **Badge existiert bereits** als Klartext-Badge: `Border` mit `TextBlock KindLabel` in `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml` (Z. 19–31) — Sichtbarkeit über `NullOrEmptyToVisibilityConverter` auf `KindLabel`, Tooltip/HelpText „Auftragsart: {0}", `AutomationId="ActiveWorkItemKind_{Title}"`. Es gibt **noch kein Symbol-Element** im Badge.
- **Zentrales Label-Mapping vorhanden:** `WorkItemListLayout.KindGroupName(WorkItemKind)` (`src/Softwareschmiede.App/ViewModels/WorkItemListLayout.cs` Z. 23–32) mappt alle sechs `WorkItemKind`-Werte (`Manual`, `Instruction`, `IssuePreparation`, `PullRequest`, `Alert`, `_` → `WithoutKind`). Ein `KindSymbol`-Mapping existiert noch nicht — `KindGroupName` ist der vorgesehene strukturelle Vorbild-Anchor.
- **Mapping-Seite `ActiveWorkItemPanelItem`** (`src/Softwareschmiede.App/ViewModels/ActiveWorkItemPanelItem.cs` Z. 44–54): `Kind`, `KindGroupName`, `KindSortOrder` (abgeleitet, `Unspecified` → ans Ende), `KindLabel` — keine `KindSymbol`-Property. Die Plugin-Glyph-Properties `ScmPluginSymbol`/`AiPluginSymbol` (Z. 24, 30) sind das unmittelbare Muster.
- **`MapActiveWorkItemPanelItem`** (`MainWindowViewModel.cs` Z. 464–514) befüllt `KindLabel` mit `Unspecified → string.Empty`, sonst `KindGroupName(kind)` — wichtig: `KindGroupName` liefert für unbekannte Enum-Werte „Ohne Auftragsart" als Gruppen-Fallback; die Badge-Ausblendung für `Unspecified` beruht allein auf dem `Unspecified`-Vergleich im Mapping, nicht auf `KindGroupName` selbst.
- **Gemeinsame Liste:** `MainWindowViewModel.ActiveWorkItemsList` wird mit `DashboardViewModel` geteilt — ein Eingriff in `ActiveTasksListControl.xaml` deckt Seitenleiste (`ActiveWorkItemsListMenu`) und Dashboard (`ActiveWorkItemsListDashboard`) ab.
- **Vorbilder:** `IPlugin.PluginSymbol` (Default-Interface-Member → `FallbackSymbol = "◻"`, alle acht Repo-Plugins überschreiben) und `ResolvePlugin` in `MainWindowViewModel` (Z. 532–546); die beiden Glyph-`TextBlock`s (FontSize 13, `SecondaryTextBrush`, eigene AutomationIds) zeigen das UI-Muster für ein Symbol-Element.
- **Datenherkunft:** `WorkItem.Kind` wird an fünf Stellen in `WorkItemService` gesetzt (`Manual`/`Instruction`/`IssuePreparation`/`PullRequest`/`Alert`) und in `ScmEventOrchestrator` Z. 804 nach Prompt-Zustellung auf `Instruction` gewechselt — laut Scope unverändert.
- **Tests:** Es gibt **keinen** bestehenden Test für `KindLabel`/das Badge-Mapping und keinen E2E-Accessor für `ActiveWorkItemKind_{Title}` (Vorbilder: `MenuView.GetActiveTaskScmPluginSymbol`/`GetActiveTaskAiPluginSymbol`); `WorkItemsListsViewTests` prüft nur die Deskriptoren des „Art"-Modus; `WorkItemServiceTests` assertiert `Kind` je Anlagepfad. Badge-Sichtbarkeit wurde in der Vorarbeit (#451, Schritt 4) bewusst ohne FlaUI-E2E über ViewModel-/Deskriptor-Tests abgedeckt.
- **Doku:** `docs/help/plugins/github-ereignisse/business-rules.md` beschreibt die Auftragsart fachlich (ohne Symbole); `docs/help/aufgaben/ablauf-technisch.md` dokumentiert die Symbolzeile, erwähnt aber **bereits heute weder das Auftragsart-Badge noch den „Art"-Modus** — die Stelle ist unabhängig vom neuen Symbol nachzuziehen.

**Test-Ausgangszustand:** Build `dotnet build Softwareschmiede.slnx -c Debug` erfolgreich (0 Warnungen, 0 Fehler). Alle vier Lanes grün: regulär 2782 bestanden / 0 fehlgeschlagen / 3 übersprungen (plattformbedingt); regulär-Integration 406/406; OsInterface 70 bestanden / 0 fehlgeschlagen / 5 übersprungen (`SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`-Skips); OsInterface-Integration 19/19. Keine nachgewiesenen bestehenden Testfehler — Nachweis in [inventory/tests.md](inventory/tests.md) mit TRX-Reports unter `inventory/test-results/`.

## Details

- [Datenmodell](inventory/models.md) — `ActiveWorkItemPanelItem` (alle Kachel-Properties), `WorkItem.Kind`
- [Enums](inventory/enums.md) — `WorkItemKind` (6 Werte + Labels), `WorkItemListMode` (4 Modi)
- [Logik](inventory/logic.md) — `WorkItemListLayout`, `MainWindowViewModel` (Mapping/Refresh/`ResolvePlugin`), `ActiveTasksListControl` (Code-Behind), `NullOrEmptyToVisibilityConverter`, `WorkItem.Kind`-Herkunft, `DashboardViewModel`, Modus-Persistenz
- [Interfaces](inventory/interfaces.md) — `IPlugin` (`PluginSymbol`/`FallbackSymbol`), `IPluginManager`, `IActiveWorkItemsService`, Converter-Verträge
- [UI / XAML](inventory/ui.md) — Badge-Block und Symbolzeile in `ActiveTasksListControl.xaml`, Kachel-/Gruppen-Templates, Verwendungsstellen in `MainWindow.xaml`/`DashboardView.xaml`
- [Dokumentations-Ist-Stand](inventory/docs-status.md) — relevante `docs/help/`-Abschnitte und vorhandene Doc-Lücken
- [Tests](inventory/tests.md) — Test-Ausgangszustand (4 Lanes + TRX-Nachweise), relevante Testklassen und Hilfsmethoden
