# Enums — Symbol für Auftragsart auf den Aufgabenkacheln (Issue #462)

## `WorkItemKind`
Datei: `src/Softwareschmiede/Domain/Enums/WorkItemKind.cs`

Fachliche Auftragsart einer Aufgabe (Issue #451) — benennt ihren Ursprung. Reine Anzeige-Klassifikation, getrennt von `WorkItem.Purpose` (Routing/Isolation). Darf sich im Lebenszyklus ändern (Aufbereitung → Anweisung nach Prompt-Zustellung, `ScmEventOrchestrator` Z. 804).

| Wert | Wertigkeit | Bedeutung |
|------|-----------|-----------|
| `Unspecified` | 0 | Nicht bestimmt — Bestandsdaten ohne klassifizierbaren Ursprung (Z. 11). `KindLabel` bleibt leer → kein Badge; `KindGroupName`-Fallback „Ohne Auftragsart" nur für die Gruppierung |
| `Manual` | 1 | Manuell angelegte Aufgabe, einschließlich manueller Anlage aus einem Issue (Z. 14). Label: „Manuell". Zugewiesen in `WorkItemService` Z. 219, 248 |
| `Instruction` | 2 | Aus SCM-Trigger-Ereignis (Issue-/Kommentar-Trigger) angelegte Anweisungsaufgabe; Zieltyp der Issue-Aufbereitung nach Zustellung (Z. 19). Label: „Anweisung". Zugewiesen in `WorkItemService` Z. 321, `ScmEventOrchestrator` Z. 804 |
| `PullRequest` | 3 | Aus einem Pull-Request-Ereignis angelegte Aufgabe (Z. 22). Label: „Pull Request". `WorkItemService` Z. 412 |
| `Alert` | 4 | Aus einem SCM-Alert angelegte Aufgabe (Z. 25). Label: „Alert". `WorkItemService` Z. 542 |
| `IssuePreparation` | 5 | Automatische Issue-Aufbereitung (Analyse-Prompt); wechselt nach erfolgreicher Zustellung zu `Instruction` (Z. 29). Label: „Issue-Aufbereitung". `WorkItemService` Z. 321 |

Die Enum-Labels stammen aus `WorkItemListLayout.KindGroupName` (siehe [Logik](logic.md)); eine Symbol-Tabelle existiert noch nicht.

## `WorkItemListMode`
Datei: `src/Softwareschmiede.App/ViewModels/WorkItemListMode.cs`

Anzeigemodus der aktiven Aufgabenliste (Seitenleiste und Dashboard, je eigene `RadioButton`-Gruppe und eigener Persistenzschlüssel).

| Wert | Bedeutung |
|------|-----------|
| `Date` | Flache Liste, sortiert nach letzter Ausführung (Z. 7); RadioButton „Datum" (`WorkItemsMode:Date` / `DashboardWorkItemsMode:Date`) |
| `Repository` | Gruppiert nach `RepositoryGroupName` (Z. 10); RadioButton „Repository" |
| `Ai` | Gruppiert nach `AiGroupName` (Z. 13); RadioButton „KI" (`WorkItemsMode:AI`) |
| `Kind` | Gruppiert nach `KindGroupName` (Z. 16); RadioButton „Art" (`WorkItemsMode:Kind` / `DashboardWorkItemsMode:Kind`), `MainWindow.xaml` Z. 157–162, `DashboardView.xaml` Z. 87–91 |

Persistenz: Enum-Name via `WorkItemListModeService` (`AppSettingService.ActiveWorkItemsListModeKey` / `…DashboardKey`); `Enum.TryParse` + `Enum.IsDefined`, Fallback `Date`.
