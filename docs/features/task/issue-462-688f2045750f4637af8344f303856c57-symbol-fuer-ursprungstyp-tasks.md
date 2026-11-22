# Tasks: Symbol für Auftragsart auf den Aufgabenkacheln (Issue #462)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | `WorkItemListLayout.KindSymbol(WorkItemKind)` als statische Switch-Mapping-Methode anlegen (`Manual`→`✋`, `Instruction`→`📣`, `IssuePreparation`→`🔍`, `PullRequest`→`🔀`, `Alert`→`⚠`, `_`→`null`) — `src/Softwareschmiede.App/ViewModels/WorkItemListLayout.cs` nach `KindGroupName` (Z. 23–32) | Offen | — |
| 2 | Datenmodell | `ActiveWorkItemPanelItem.KindSymbol` (`string?`, init-only) ergänzen — `src/Softwareschmiede.App/ViewModels/ActiveWorkItemPanelItem.cs` nach `KindLabel` (Z. 54) | Offen | — |
| 3 | Logik | `MainWindowViewModel.MapActiveWorkItemPanelItem` (Z. 495–499) erweitern: `KindSymbol` aus `WorkItemListLayout.KindSymbol` befüllen; `KindLabel` auf Symbol-Kopplung umstellen (leer, wenn `KindSymbol` `null` — statt bisher nur `Unspecified`-Vergleich) | Offen | — |
| 4 | UI | Badge-Block in `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml` (Z. 19–31) umbauen: horizontales `StackPanel` mit Symbol-`TextBlock` (`KindSymbol`, `FontSize="11"`, `AutomationId=ActiveWorkItemKindSymbol_{Title}`) vor dem `KindLabel`-`TextBlock` (zusätzlich `AutomationId=ActiveWorkItemKindLabel_{Title}`); Border-Attribute unverändert | Offen | — |
| 5 | Tests | `WorkItemsListsViewTests`: Theory `KindSymbol_ShouldReturnExpectedGlyph_ForEachVisibleKind` (fünf sichtbare Arten → exakter Glyph) | Offen | — |
| 6 | Tests | `WorkItemsListsViewTests`: Theory `KindSymbol_ShouldReturnNull_ForUnspecifiedAndUndefined` (`Unspecified`, `(WorkItemKind)999` → `null`) | Offen | — |
| 7 | Tests | `WorkItemsListsViewTests`: `KindSymbol_ShouldBeDefinedAndDistinct_ForAllVisibleKinds` (alle definierten Werte außer `Unspecified`: nicht-leer + paarweise verschieden) | Offen | — |
| 8 | Tests | `MainWindowViewModelTests`: Theory `RefreshActiveWorkItemsAsync_ShouldMapKindFieldsPerKind` (Kind am Entity setzen + `SaveChangesAsync` + `StartAsync`; Assert `Kind`/`KindSymbol`/`KindLabel`/`KindGroupName`, inkl. `Unspecified` → `null`/leer) | Offen | — |
| 9 | E2E-Tests | `MenuView`: Accessoren `GetActiveTaskKindSymbol` und `GetActiveTaskKindLabel` ergänzen (AutomationIds `ActiveWorkItemKindSymbol_{Title}`/`ActiveWorkItemKindLabel_{Title}` im Scope `ActiveWorkItemsListMenu`, Muster Z. 472–485) | Offen | — |
| 10 | E2E-Tests | `E2E_WorkItemsListModes.cs` `WorkItemsList_CompactTileSymbolAndModeswitch_E2E` erweitern: `workItemWithoutRepo.Kind = WorkItemKind.Alert` im DB-Block; Badge-Assertions („✋"/„Manuell", „⚠"/„Alert"); `SetWorkItemsListsMode("Kind")` + `WaitForWorkItemsGroup("Manuell")`/`("Alert")` | Offen | — |
| 11 | Dokumentation | `docs/help/plugins/github-ereignisse/business-rules.md` Abschnitt „Auftragsart der Aufgabe" (Z. 97–99) um Symboltabelle ergänzen | Offen | — |
| 12 | Dokumentation | `docs/help/aufgaben/ablauf-technisch.md` nachziehen: Symbolzeile (Z. 840) um Auftragsart-Badge, Felderliste von `MapActiveWorkItemPanelItem` (Z. 896–900) um `Kind`/`KindGroupName`/`KindSymbol`/`KindLabel`, Modus-Aufzählung (Z. 845–864) um „Art"-Modus (Doku-Lücke) | Offen | — |
