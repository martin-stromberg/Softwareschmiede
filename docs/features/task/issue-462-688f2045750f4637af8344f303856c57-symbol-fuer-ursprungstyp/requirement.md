# Symbol für Auftragsart auf den Aufgabenkacheln (Issue #462)

## Fachliche Zusammenfassung

Die Typ-Markierung (Auftragsart-Badge) auf den Kacheln der aktiven Aufgabenliste soll neben dem bisherigen Klartext („Manuell", „Anweisung", …) ein aussagekräftiges Unicode-Symbol erhalten, damit der Ursprung einer Aufgabe auf einen Blick erkennbar ist — konsistent zu den bereits vorhandenen Plugin-Glyphen (`ScmPluginSymbol`, `AiPluginSymbol`) in derselben Symbolzeile. Das Symbol-Mapping wird — analog zum vorhandenen Label-Mapping `WorkItemListLayout.KindGroupName` — an einer zentralen, testbaren Stelle abgelegt. Aufgaben ohne bestimmbare Auftragsart (`WorkItemKind.Unspecified`) zeigen weiterhin kein Badge.

## Fachlicher Scope

- **Verarbeitungseinheit:** Die Auftragsart-Markierung (`WorkItem.Kind` vom Typ `WorkItemKind`) einer einzelnen aktiven Aufgabe auf ihrer Kachel im `ActiveTasksListControl` — eingebunden in der Menü-Seitenleiste (`MainWindow.xaml`) und im Dashboard (`DashboardView.xaml`), beide über die gemeinsame `ActiveWorkItemsList`.
- **Auslösende Ereignisse:** Aufbau bzw. Aktualisierung der aktiven Aufgabenliste (`MainWindowViewModel.MapActiveWorkItemPanelItem` beim Refresh der Liste); kein neues Nutzerereignis, keine Änderung an Anlage-/Klassifizierungspfaden.
- **Erwartete Wirkung:** Auf jeder Kachel mit bekannter Auftragsart zeigt das Badge Symbol + Klartext (z. B. „✋ Manuell"); Tooltip und UIA-Hilfetext („Auftragsart: …") bleiben aussagekräftig. Das Symbol-Mapping aller abgedeckten Auftragsarten ist festgelegt und dokumentiert.
- **Umgang mit fehlenden Objekten:** `WorkItemKind.Unspecified` (Bestandsdaten ohne klassifizierbaren Ursprung) → kein Symbol, Badge bleibt ausgeblendet (Status quo über `NullOrEmptyToVisibilityConverter` auf `KindLabel`). Unbekannte/zukünftige Enum-Werte → Fallback auf „kein Symbol" bzw. vorhandenes Label-Verhalten von `KindGroupName` (Fallback „Ohne Auftragsart" nur für die Gruppierung, nicht als Badge — zu verifizieren, dass kein neuer Badge-Fall entsteht).
- **Scope-Ausschlüsse:**
  - Keine Konfigurierbarkeit der Symbole (fest verdrahtetes Mapping wie `KindGroupName`).
  - Keine Symbole in den Gruppenköpfen des Listenmodus „Art" — Gruppennamen bleiben Klartext (`KindGroupName`), das Symbol ist eine eigene Property.
  - Keine Änderung an `WorkItemKind`, an der Klassifizierungs-/Typwechsel-Logik (Issue #451) oder an Persistenz/Migrationen.
  - Keine Änderung an den bestehenden Plugin-Symbolen, am Status-Glyph oder am To-dos-Button in der Symbolzeile.
  - Begrifflichkeit: Im UI und im Code bleibt die Bezeichnung „Auftragsart" — der Issue-Begriff „Ursprungstyp" wird nicht eingeführt.

## Betroffene Klassen und Komponenten

- `src/Softwareschmiede.App/ViewModels/WorkItemListLayout.cs` — neue zentrale Mapping-Methode, z. B. `KindSymbol(WorkItemKind)`, analog zum bestehenden `KindGroupName(WorkItemKind)` (Zeilen 23–32).
- `src/Softwareschmiede.App/ViewModels/ActiveWorkItemPanelItem.cs` — neue Property, z. B. `KindSymbol` (Zeilen 44–54, neben `Kind`/`KindGroupName`/`KindLabel`); `KindSortOrder` unverändert.
- `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs` — `MapActiveWorkItemPanelItem` (Zeilen 495–499) befüllt zusätzlich `KindSymbol` (`Unspecified` → `null`/leer).
- `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml` — Badge-Block (Zeilen 19–31): Symbol-`TextBlock` im Border vor dem `KindLabel`-TextBlock (Symbol + Text), Tooltip/`AutomationProperties.HelpText` und `AutomationId="ActiveWorkItemKind_{Title}"` bleiben erhalten; Sichtbarkeit weiter über `KindLabel`. Ein Eingriff deckt Menü- und Dashboard-Liste gleichzeitig ab.
- `src/Softwareschmiede/Domain/Enums/WorkItemKind.cs` — unverändert; Werte: `Manual`, `Instruction`, `PullRequest`, `Alert`, `IssuePreparation`, `Unspecified`.
- Tests:
  - `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelTests.cs` — Mapping-Tests für `KindSymbol` (je Wert, inkl. `Unspecified` → kein Symbol).
  - `src/Softwareschmiede.Tests/App/ViewModels/WorkItemsListsViewTests.cs` — ggf. Test des neuen Mappings in `WorkItemListLayout`.
  - Optional E2E: `src/Softwareschmiede.Tests/E2E/Views/MenuView.cs`/`DashboardView.cs` — es existiert noch kein Accessor für das Auftragsart-Badge (nur `GetActiveTaskScmPluginSymbol`/`GetActiveTaskAiPluginSymbol` als Vorbild). Hinweis aus der Vorarbeit (Issue #451, `test-results.md`): Badge-Sichtbarkeit wurde bewusst ohne FlaUI-E2E über ViewModel-Tests abgedeckt.
- Dokumentation: `docs/help/plugins/github-ereignisse/business-rules.md` (Abschnitt „Auftragsart der Aufgabe") und `docs/help/aufgaben/ablauf-technisch.md` (Symbolzeile der Kachel) um das Symbol-Mapping ergänzen.

## Implementierungsansatz

- Erweiterung von `WorkItemListLayout` um eine statische Symbol-Mapping-Funktion `WorkItemKind → string?` (Switch-Ausdruck wie `KindGroupName`): `Manual`, `Instruction`, `PullRequest`, `Alert`, `IssuePreparation` → je ein Glyph; `Unspecified`/unbekannt → `null`.
- `ActiveWorkItemPanelItem` erhält `KindSymbol` (`string?`, init-only); `MapActiveWorkItemPanelItem` setzt es aus dem zentralen Mapping.
- XAML: Im bestehenden Badge-`Border` wird das Symbol als zusätzlicher `TextBlock` (analog zu `ScmPluginSymbol`/`AiPluginSymbol`, FontSize ~10–13, `SecondaryTextBrush`) gerendert; Bindung mit Ausblendung bei leerem Symbol. Tooltip/HelpText weiterhin aus `KindLabel` („Auftragsart: {0}"), damit Screenreader den Klartext erhalten.
- Vorbild/Parallelität: `IPlugin.PluginSymbol`-Mechanismus (`docs/help/plugins/api.md`) zeigt das etablierte Muster für Unicode-Glyphen auf der Kachel.

## Konfiguration

Keine — die Symbole sind fest verdrahtet (analog `KindGroupName` und den Plugin-`PluginSymbol`-Überschreibungen). Keine Anwendungs-, Benutzer- oder Datensatz-Einstellung vorgesehen.

## Offene Fragen

1. **Abdeckung:** Symbole nur für `Manual`/`Instruction` (ursprünglicher Issue-Vorschlag) oder für alle sichtbaren Auftragsarten (`PullRequest`, `Alert`, `IssuePreparation`)? Empfehlung des Issue-Textes: alle abdecken, sonst wirkt das Badge-Schema uneinheitlich.
2. **Symbol vs. Text:** Ersetzt das Symbol den Klartext oder ergänzt es ihn („✋ Manuell")? Empfehlung: Symbol + Text — die Hand-Glyphen sind in Badge-Schriftgröße schwer unterscheidbar; der Klartext bleibt zudem für Tooltip/UIA erhalten.
3. **Symbolwahl:** ✋/🙋 (Issue-Vorschlag) vs. Alternativen mit besserer Unterscheidbarkeit in kleiner Darstellung (z. B. ✍ für „Manuell", 💬/📣 für „Anweisung"). Vorschläge für die übrigen Arten (offen): `PullRequest` 🔀, `Alert` ⚠️, `IssuePreparation` 🔍. Vor Umsetzung Varianten in realer Badge-Größe vergleichen und final festlegen/dokumentieren.
4. **Unspecified-Fallback bei Badge-Text:** Aktuell liefert `KindGroupName(Unspecified)` den Gruppennamen „Ohne Auftragsart", `KindLabel` bleibt aber leer — zu prüfen, dass das neue Symbol-Mapping keine Badge-Anzeige für `Unspecified` einführt.
