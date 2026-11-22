# Umsetzungsplan: Symbol für Auftragsart auf den Aufgabenkacheln (Issue #462)

## Übersicht

Das Auftragsart-Badge auf den Kacheln der aktiven Aufgabenliste (Seitenleiste und Dashboard) erhält vor dem Klartext ein Unicode-Symbol („✋ Manuell"). Das Symbol-Mapping wird wie das vorhandene Label-Mapping `WorkItemListLayout.KindGroupName` zentral und testbar abgelegt; `WorkItemKind.Unspecified` zeigt weiterhin kein Badge. Betroffen sind `WorkItemListLayout`, `ActiveWorkItemPanelItem`, `MainWindowViewModel.MapActiveWorkItemPanelItem` und der Badge-Block in `ActiveTasksListControl.xaml` sowie Tests und zwei `docs/help/`-Dateien.

## Scope-Klärung

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Die Auftragsart-Markierung (`WorkItem.Kind` vom Typ `WorkItemKind`) einer einzelnen aktiven Aufgabe auf ihrer Kachel im `ActiveTasksListControl` — geteilt von Menü-Seitenleiste (`MainWindow.xaml` Z. 171–174) und Dashboard (`DashboardView.xaml` Z. 95–98) über die gemeinsame `ActiveWorkItemsList`. |
| Auslösende Ereignisse | Aufbau/Aktualisierung der aktiven Aufgabenliste in `MainWindowViewModel.RefreshActiveWorkItemsAsync` → `MapActiveWorkItemPanelItem` (Z. 464–514). Kein neues Nutzerereignis; keine Änderung an Anlage-/Klassifizierungspfaden. |
| Erwartete Wirkung | Jede Kachel mit bekannter Auftragsart zeigt im Badge Symbol + Klartext (z. B. „✋ Manuell"); Tooltip und `AutomationProperties.HelpText` bleiben Klartext („Auftragsart: {0}"). Das Symbol-Mapping aller fünf sichtbaren Auftragsarten ist festgelegt und dokumentiert. |
| Umgang mit fehlenden Objekten | `WorkItemKind.Unspecified` (Bestandsdaten ohne klassifizierbaren Ursprung): `KindSymbol` = `null`, `KindLabel` bleibt leer → Badge bleibt ausgeblendet (Status quo über `NullOrEmptyToVisibilityConverter`). Unbekannte/zukünftige Enum-Werte: `KindSymbol` liefert `null` → kein Badge (der `KindGroupName`-Fallback „Ohne Auftragsart" bleibt strikt gruppen-seitig; Details unter Designentscheidungen). |

### Scope-Ausschlüsse

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | Keine Konfigurierbarkeit der Symbole (fest verdrahtetes Mapping in `WorkItemListLayout`) | Anforderung/Entscheidungsempfehlung des Issue: analog `KindGroupName` und den Plugin-`PluginSymbol`-Überschreibungen; kein Anwendungs-, Benutzer- oder Datensatz-Setting vorgesehen | bestätigt (2026-11-23, im Issue-Text entschieden: Akzeptanzkriterium „zentrales Mapping analog Label-Mapping") |
| 2 | Keine Symbole in den Gruppenköpfen des Listenmodus „Art" | Anforderung: `KindGroupName` bleibt Klartext (`HeaderTemplate` bindet `CollectionViewGroup.Name`); das Symbol ist eine eigene Property `KindSymbol`, nicht Teil des Labels | bestätigt (2026-11-23, im Issue-Text entschieden: Offener Punkt 4 „Gruppenköpfe bleiben Klartext") |
| 3 | Keine Änderung an `WorkItemKind`, an Klassifizierungs-/Typwechsel-Logik (Issue #451, `WorkItemService`/`ScmEventOrchestrator`) oder an Persistenz/Migrationen | Reine Anzeige-Erweiterung des bestehenden Badges | bestätigt (2026-11-23, Issue fordert ausschließlich Symbol auf dem Badge) |
| 4 | Keine Änderung an den Plugin-Symbolen (`ScmPluginSymbol`/`AiPluginSymbol`), am Status-Glyph oder am To-dos-Button in der Symbolzeile | Expliziter Scope der Anforderung | bestätigt (2026-11-23, Issue enthält keinerlei Änderungswunsch an diesen Glyphen) |
| 5 | Begrifflichkeit: Es bleibt bei „Auftragsart" — der Issue-Begriff „Ursprungstyp" wird weder im UI noch im Code eingeführt | Anforderung: Begriff ist seit #451 etabliert und dokumentiert | bestätigt (2026-11-23, im Issue-Text entschieden: Offener Punkt 5 „vorhandene Bezeichnung weiterverwenden") |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Symbolwahl (`WorkItemListLayout.KindSymbol`) | `Manual` → `✋` (U+270B), `Instruction` → `📣` (U+1F4E3), `PullRequest` → `🔀` (U+1F500), `Alert` → `⚠` (U+26A0), `IssuePreparation` → `🔍` (U+1F50D); `Unspecified`/unbekannt → `null`. Glyphen ohne Variation Selector, wie bei den bestehenden Plugin-Glyphen (`✈`, `◈`, `✳`). | Alle fünf Glyphen besitzen gegenseitig klar unterscheidbare Silhouetten — das ist der Zweck des Symbols (Ursprung auf einen Blick). `✋`/`🔀`/`⚠`/`🔍` sind die Issue-Empfehlungen und semantisch eindeutig (Hand, Merge-Pfeile, Warndreieck, Lupe = Analyse). Für `Instruction` wird `📣` statt des Issue-Primärvorschlags `🙋` gewählt: `🙋` ist ein zweites Hand-Glyph und in Badge-Größe (10–11 px) mit `✋` verwechselbar; `💬` (Sprechblase) ist rund/flächig und grenzt sich vom Badge-Rahmen schlechter ab. `📣` (Megafon = zugestellte Anweisung aus einem Trigger-Ereignis) passt semantisch und hat eine eigenständige Form. Die Darstellung wird bei der Implementierung in realer Badge-Größe sichtbar geprüft (Sichtprüfung der Abnahme-Screenshots im Test-Schritt). |
| Badge-Sichtbarkeitsbedingung (`MapActiveWorkItemPanelItem`) | `KindLabel` wird leer, sobald `WorkItemListLayout.KindSymbol(kind)` `null` liefert — statt des bisherigen Vergleichs `kind == WorkItemKind.Unspecified`. | `KindGroupName` liefert für unbekannte/zukünftige Enum-Werte den Gruppen-Fallback „Ohne Auftragsart"; bliebe die Bedingung nur `Unspecified`, zeigte ein zukünftiger Wert ein symbolloses Badge „Ohne Auftragsart" — die Anforderung legt den Fallback explizit nur für die Gruppierung fest. Mit der Symbol-Kopplung gilt strikt: Badge sichtbar ⟺ bekannte Auftragsart mit Symbol. Für alle sechs bestehenden Enum-Werte ist das Verhalten identisch. |
| Symbol-Transport | Eigene init-only Property `KindSymbol` (`string?`) auf `ActiveWorkItemPanelItem` und eigener `TextBlock` im Badge — kein Zusammenführen in `KindLabel`. | Anforderung (getrennte Symbol-Property): `KindGroupName` versorgt Gruppenköpfe und Badge-Text; ein Emoji in `KindLabel` würde in Tooltip/`HelpText` („Auftragsart: ✋ Manuell") und damit in der Screenreader-Ausgabe landen. Tooltip/HelpText bleiben bewusst Klartext. |
| E2E-Strategie | Der bestehende konsolidierte FlaUI-Test `WorkItemsList_CompactTileSymbolAndModeswitch_E2E` (`E2E_WorkItemsListModes.cs` Z. 23) wird um Badge-Assertions erweitert — keine neue Testmethode. | Projektregel: FlaUI-E2E-Tests sind auf das Minimum zu konsolidieren (Laufzeit, nicht Methodenzahl, ist der Engpass). Der Test erzeugt bereits zwei aktive Aufgaben und liest die Symbolzeile; Badge-Assertions inkl. „Art"-Gruppierung fügen sich direkt ein. |
| UIA-Erreichbarkeit des Badge-Texts | Zwei neue AutomationIds auf den `TextBlock`s im Badge: `ActiveWorkItemKindSymbol_{Title}` (Glyph) und `ActiveWorkItemKindLabel_{Title}` (Klartext). | Der Badge-`Border` erzeugt keinen `AutomationPeer` und ist für UIA unsichtbar (vgl. Kommentar in `ActiveTasksListControl.xaml` Z. 72–74) — die bestehende `AutomationId="ActiveWorkItemKind_{Title}"` am Border ist für FlaUI unerreichbar und bleibt unverändert erhalten; die E2E-Accessoren zielen daher auf die `TextBlock`s, die einen Peer besitzen. |

## Programmabläufe

### Befüllung der aktiven Aufgabenliste (Badge-Felder)

1. `MainWindowViewModel.RefreshActiveWorkItemsAsync` lädt aktive Aufgaben über `IActiveWorkItemsService.GetActiveWorkItemsAsync` und mappt jede `WorkItem` via `MapActiveWorkItemPanelItem` auf ein `ActiveWorkItemPanelItem` in der geteilten `ActiveWorkItemsList`.
2. `MapActiveWorkItemPanelItem` setzt `Kind = workItem.Kind`, `KindGroupName = WorkItemListLayout.KindGroupName(workItem.Kind)` (Gruppenname im Modus „Art"), `KindSymbol = WorkItemListLayout.KindSymbol(workItem.Kind)` und `KindLabel` — leer, wenn `KindSymbol` `null` liefert (`Unspecified`, unbekannte Werte), sonst `KindGroupName(kind)`.
3. `ActiveTasksListControl` erzeugt je Instanz eine eigene `ListCollectionView` und rendert je Item das `WorkItemsTileContentTemplate`.

Beteiligte Klassen/Komponenten: `MainWindowViewModel`, `WorkItemListLayout`, `ActiveWorkItemPanelItem`, `ActiveTasksListControl`, `DashboardViewModel` (geteilte Liste — deckt Dashboard ohne eigenen Eingriff ab).

### Badge-Rendering auf der Kachel

1. Der Badge-`Border` (`ActiveTasksListControl.xaml` Z. 19–31) bleibt erstes Element der Symbolzeile; Sichtbarkeit weiterhin über `Visibility="{Binding KindLabel, Converter={StaticResource NullOrEmptyToVisibilityConverter}}"`, Tooltip/`HelpText` weiterhin „Auftragsart: {0}" aus `KindLabel`, `AutomationId="ActiveWorkItemKind_{Title}"` unverändert.
2. Innerhalb des Borders ersetzt ein horizontales `StackPanel` den einzelnen `TextBlock`: erster `TextBlock` bindet `KindSymbol` (Glyph, `Margin` mit kleinem Rechtsabstand), zweiter `TextBlock` bindet unverändert `KindLabel` (`FontSize="10"`).
3. Da der Badge nur bei nicht-leerem `KindLabel` sichtbar ist und `KindLabel` genau dann gefüllt wird, wenn ein Symbol existiert, benötigt das Symbol-Element keine eigene Sichtbarkeitsbindung.
4. Gruppenköpfe im Modus „Art" binden weiterhin `CollectionViewGroup.Name` (= `KindGroupName`) als Klartext — unverändert.

Beteiligte Klassen/Komponenten: `ActiveTasksListControl.xaml` (`WorkItemsTileContentTemplate`), `NullOrEmptyToVisibilityConverter`.

## Neue Klassen

Keine.

## Änderungen an bestehenden Klassen

### `WorkItemListLayout` (statische Klasse, `src/Softwareschmiede.App/ViewModels/WorkItemListLayout.cs`)

- **Neue Methoden:** `KindSymbol(WorkItemKind)` — `public static string?`; Switch-Ausdruck analog `KindGroupName` (Z. 23–32), eingefügt direkt danach: `Manual` → `"✋"`, `Instruction` → `"📣"`, `IssuePreparation` → `"🔍"`, `PullRequest` → `"🔀"`, `Alert` → `"⚠"`, `_` → `null` (deckt `Unspecified` und unbekannte Werte ab). XML-Doc verweist auf die Badge-Verwendung und darauf, dass `null` „kein Symbol/kein Badge" bedeutet.

### `ActiveWorkItemPanelItem` (Datenmodellklasse, `src/Softwareschmiede.App/ViewModels/ActiveWorkItemPanelItem.cs`)

- **Neue Eigenschaften:** `KindSymbol` (`string?`, `init`-only) — Unicode-Glyph der Auftragsart für das Badge; `null` bei `Unspecified`/unbekannt. Einfügen direkt nach `KindLabel` (Z. 54), XML-Doc im Muster der Plugin-Glyph-Properties (Z. 24, 30). `KindSortOrder` (Z. 51) bleibt unverändert.

### `MainWindowViewModel` (`src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs`)

- **Geänderte Methoden:** `MapActiveWorkItemPanelItem` (Z. 464–514) — im Initialisierer (Z. 495–499) neu: `KindSymbol = WorkItemListLayout.KindSymbol(workItem.Kind)` und `KindLabel` von `workItem.Kind == WorkItemKind.Unspecified ? string.Empty : KindGroupName(kind)` auf „leer, wenn `KindSymbol` `null`, sonst `KindGroupName(kind)`" umgestellt (siehe Designentscheidung Badge-Bedingung).

### `ActiveTasksListControl.xaml` (`src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml`)

- **Geänderter Badge-Block (Z. 19–31):** Innerer `TextBlock` wird durch `StackPanel Orientation="Horizontal"` mit zwei `TextBlock`s ersetzt: Symbol (`Text="{Binding KindSymbol}"`, `FontSize="11"` — zwischen 10-px-Label und den 13-px-Zeilen-Glyphen, `Foreground={DynamicResource SecondaryTextBrush}`, `Margin="0,0,3,0"`, `VerticalAlignment="Center"`, `AutomationProperties.AutomationId="{Binding Title, StringFormat=ActiveWorkItemKindSymbol_{0}}"`) und unveränderter `KindLabel`-`TextBlock` plus zusätzlicher `AutomationProperties.AutomationId="{Binding Title, StringFormat=ActiveWorkItemKindLabel_{0}}"`. Alle Border-Attribute (`AutomationId`, `HelpText`, `ToolTip`, `Visibility`, Styling) bleiben unverändert.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine.

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **Geteilte Liste:** `ActiveWorkItemsList` wird zwischen `MainWindowViewModel` und `DashboardViewModel` geteilt — der XAML-Eingriff in `ActiveTasksListControl.xaml` wirkt auf Seitenleiste und Dashboard zugleich; kein separates Dashboard-Handling nötig.
- **Badge-Verhalten bei unbekannten Enum-Werten:** Durch die Symbol-Kopplung der Badge-Bedingung entfällt der (bisher hypothetische, da nicht erzeugbare) Fall „Badge mit „Ohne Auftragsart"" für unbekannte Werte; für alle sechs definierten Werte bleibt das Verhalten identisch. Dokumentiert als Designentscheidung.
- **Emoji-Darstellung:** WPF rendert die Glyphen über den Emoji-Fallback (Segoe UI Emoji) — dasselbe Muster wie die bestehenden Plugin-Glyphen (`🐙`, `🤖`, `🧪`) und Status-Glyphen; kein neues Rendering-Risiko.
- **Zusätzliches Element im Badge:** Der Badge wird um die Glyph-Breite breiter; bei `Padding="6,1"` und Symbol-`Margin` unkritisch — die Symbolzeile hat danach weiterhin Platz für die übrigen Glyphen; visuelle Sichtprüfung im Test-Schritt.
- **`dotnet format`:** Der pre-push-Hook spiegelt den CI-Formatcheck — nach den Code-/XAML-Änderungen `dotnet format Softwareschmiede.slnx --verify-no-changes` ausführen.

## Umsetzungsreihenfolge

1. **`WorkItemListLayout.KindSymbol` + Mapping-Tests**
   - Voraussetzungen: Keine (Bestandsdatei, xUnit/FluentAssertions vorhanden).
   - Beschreibung: `KindSymbol`-Switch in `WorkItemListLayout.cs` ergänzen (Mapping lt. Designentscheidung); Tests in `WorkItemsListsViewTests.cs` schreiben (je Wert exakter Glyph, `Unspecified`/undefinierter Wert → `null`, Vollständigkeits-/Distinctness-Test über `Enum.GetValues<WorkItemKind>()`).

2. **`ActiveWorkItemPanelItem.KindSymbol` + `MapActiveWorkItemPanelItem` + ViewModel-Tests**
   - Voraussetzungen: Schritt 1 (`KindSymbol` existiert).
   - Beschreibung: Property ergänzen; `MapActiveWorkItemPanelItem` befüllt `KindSymbol` und stellt `KindLabel` auf die Symbol-Kopplung um; Theory-Test in `MainWindowViewModelTests` (Kind direkt am getrackten Entity setzen + `SaveChangesAsync`, dann `StartAsync` → `RefreshActiveWorkItemsAsync` → Assert auf `Kind`/`KindSymbol`/`KindLabel`/`KindGroupName`).

3. **Badge-Block in `ActiveTasksListControl.xaml`**
   - Voraussetzungen: Schritt 2 (`KindSymbol`-Property existiert als Binding-Ziel).
   - Beschreibung: `StackPanel` mit Symbol- und Label-`TextBlock` inkl. neuer AutomationIds einbauen; Border-Attribute unverändert lassen.

4. **E2E: `MenuView`-Accessoren + Erweiterung von `WorkItemsList_CompactTileSymbolAndModeswitch_E2E`**
   - Voraussetzungen: Schritt 3 (AutomationIds existieren); Test-DB-Zugriff über `OpenTestDbContext` und `db.WorkItems` ist im Test vorhanden (Muster Z. 48–76).
   - Beschreibung: `GetActiveTaskKindSymbol`/`GetActiveTaskKindLabel` in `MenuView.cs` ergänzen; im E2E-Test im vorhandenen DB-Block `workItemWithoutRepo.Kind = WorkItemKind.Alert` setzen und Badge-Assertions (Glyph + Klartext je Kachel) sowie „Art"-Gruppierung mit Klartext-Gruppenköpfen ergänzen.

5. **Dokumentation**
   - Voraussetzungen: Schritte 1–3 (finale Property-/Glyph-Namen liegen fest).
   - Beschreibung: `docs/help/plugins/github-ereignisse/business-rules.md` (Abschnitt „Auftragsart der Aufgabe", Z. 97–99) um die Symboltabelle ergänzen; `docs/help/aufgaben/ablauf-technisch.md` an drei Stellen nachziehen (Symbolzeile Z. 840 um das Badge, Felderliste Z. 896–900 um `Kind`/`KindGroupName`/`KindSymbol`/`KindLabel`, Modus-Aufzählung Z. 845–864 um den „Art"-Modus — vorbestehende Doku-Lücke).

6. **Build, Format, Tests**
   - Voraussetzungen: Schritte 1–5.
   - Beschreibung: `dotnet build Softwareschmiede.slnx -c Debug`; `dotnet format Softwareschmiede.slnx --verify-no-changes`; `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"` (nie im Hintergrund, nie `--no-build`); OsInterface-Lane separat für die geänderte E2E-Methode.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `KindSymbol_ShouldReturnExpectedGlyph_ForEachVisibleKind` (Theory: `Manual`/`Instruction`/`PullRequest`/`Alert`/`IssuePreparation` → exakter Glyph) | `WorkItemsListsViewTests` (`src/Softwareschmiede.Tests/App/ViewModels/`) | Das Symbol-Mapping in `WorkItemListLayout.KindSymbol` liefert je sichtbarer Auftragsart den dokumentierten Glyph. |
| `KindSymbol_ShouldReturnNull_ForUnspecifiedAndUndefined` (Theory: `Unspecified`, `(WorkItemKind)999`) | `WorkItemsListsViewTests` | `null` für nicht darstellbare/unbekannte Werte — Grundlage der Badge-Ausblendung. |
| `KindSymbol_ShouldBeDefinedAndDistinct_ForAllVisibleKinds` | `WorkItemsListsViewTests` | Alle definierten `WorkItemKind`-Werte außer `Unspecified` liefern nicht-leere, paarweise verschiedene Glyphen (Muster: `PluginSymbolTests`, Z. 75) — Abdeckungssicherung bei neuen Enum-Werten. |
| `RefreshActiveWorkItemsAsync_ShouldMapKindFieldsPerKind` (Theory: je `WorkItemKind` → erwartetes `KindSymbol`, `KindLabel`, `KindGroupName`; inkl. `Unspecified` → leer/`null`) | `MainWindowViewModelTests` | `MapActiveWorkItemPanelItem` befüllt `Kind`, `KindSymbol`, `KindLabel`, `KindGroupName` korrekt; `Unspecified` ⇒ `KindSymbol` `null` + `KindLabel` leer (kein Badge). Kind wird direkt am Entity gesetzt + `SaveChangesAsync`, dann `StartAsync` (Setter `WorkItem.Kind` ist `public`, Z. 126). |

### Betroffene bestehende Tests

Keine — `ActiveWorkItemPanelItem` erhält nur eine neue init-only Property, `MapActiveWorkItemPanelItem` bleibt privat, XAML-Änderung ist template-intern; kein bestehender Test referenziert `KindLabel`/das Badge (Testlücke lt. Bestandsaufnahme).

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Badge zeigt Symbol + Klartext zweier verschiedener Auftragsarten auf realen Kacheln; „Art"-Gruppierung zeigt Klartext-Gruppenköpfe | Erweiterung des bestehenden `WorkItemsList_CompactTileSymbolAndModeswitch_E2E` (`src/Softwareschmiede.Tests/E2E/E2E_WorkItemsListModes.cs`) — keine neue Methode | „✋ Manuell" und „⚠ Alert" sichtbar; Gruppenköpfe „Manuell"/"Alert" ohne Symbol | Die Änderung ist eine sichtbare UI-Darstellung in einem bestehenden Benutzerfluss; nur FlaUI belegt das tatsächliche Rendering (Glyph im Badge, Layout der Symbolzeile). |
| Pflicht (Hilfsmittel) | Neue Accessoren `GetActiveTaskKindSymbol`/`GetActiveTaskKindLabel` | `MenuView` (`src/Softwareschmiede.Tests/E2E/Views/MenuView.cs`, Muster Z. 472–485) | Lesen von Glyph und Klartext aus dem Badge über die neuen AutomationIds | Der Badge-`Border` ist UIA-unsichtbar (kein `AutomationPeer`); die TextBlock-AutomationIds sind der einzige stabile E2E-Anker. |

Welche bestehenden E2E-Tests müssen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `WorkItemsList_CompactTileSymbolAndModeswitch_E2E` (`E2E_WorkItemsListModes.cs`) | Erweiterung um Badge-Assertions: im DB-Block (Z. 62–76) `workItemWithoutRepo.Kind = WorkItemKind.Alert` setzen; nach den Glyph-Assertions (Z. 85–88) `GetActiveTaskKindSymbol`/`GetActiveTaskKindLabel` für beide Aufgaben assertieren („✋"/„Manuell" und „⚠"/„Alert"); im Menü-Modusblock zusätzlich `SetWorkItemsListsMode("Kind")` + `WaitForWorkItemsGroup("Manuell")`/`("Alert")` (Klartext-Gruppenköpfe). |

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | Happy Path: manuelle Aufgabe zeigt Symbol-Badge | App-Start / Refresh der aktiven Aufgabenliste | Aktive Aufgabe mit `Kind = Manual` existiert (manuelle Anlage setzt `Manual` automatisch) | `RefreshActiveWorkItemsAsync` → `MapActiveWorkItemPanelItem` → Kachel-Rendering | Badge „✋ Manuell" (Glyph + Text) in Seitenleiste und Dashboard; Tooltip „Auftragsart: Manuell" | ViewModel-Test + E2E (`GetActiveTaskKindSymbol`/`GetActiveTaskKindLabel`) |
| 2 | Ereignis-Aufgabe zeigt ihr Symbol | Eingehendes SCM-Ereignis legt Aufgabe an | Aufgabe mit `Kind = Alert` (bzw. `Instruction`/`PullRequest`/`IssuePreparation`) in der Liste | wie Szenario 1 | Badge „⚠ Alert" (bzw. „📣 Anweisung", „🔀 Pull Request", „🔍 Issue-Aufbereitung") | Theory-Mapping-Test + E2E (Alert-Fall auf realer Kachel) |
| 3 | Bestandsdaten ohne Auftragsart | Refresh | Aufgabe mit `Kind = Unspecified` in der Liste | wie Szenario 1 | Kein Badge (Status quo); Kachel zeigt nur Plugin-/Status-Glyphen | ViewModel-Test (`KindSymbol` `null`, `KindLabel` leer) |
| 4 | Gruppierung im Modus „Art" bleibt Klartext | Nutzer wählt RadioButton „Art" | Liste enthält Aufgaben verschiedener Auftragsarten | `ListMode`-Wechsel → `WorkItemListLayout.Configure` → Gruppenköpfe via `KindGroupName` | Gruppenköpfe „Manuell", „Alert" usw. ohne Symbol | E2E (`WaitForWorkItemsGroup`) |
| 5 | Typwechsel Aufbereitung → Anweisung | Prompt-Zustellung wechselt `Kind` (Bestandslogik, unverändert) | Aufgabe mit `Kind = IssuePreparation` | 5-s-Refresh / `RunDataChanged` → Neumapping | Badge wechselt von „🔍 Issue-Aufbereitung" zu „📣 Anweisung" | Mapping-Tests (beide Werte); Wechsel-Logik selbst ist Scope-Ausschluss und bereits durch Integrationstests abgedeckt |

## Offene Punkte

Keine. Die vier offenen Fragen der Anforderung sind im Plan entschieden: (1) Abdeckung aller sichtbaren Auftragsarten inkl. Distinctness-Test, (2) Symbol ergänzt den Klartext, (3) Symbolwahl festgelegt und begründet (siehe Designentscheidungen; finale Darstellung wird in der Sichtprüfung des Test-Schritts verifiziert), (4) `Unspecified`/unbekannte Werte erzeugen über die Symbol-Kopplung garantiert keinen Badge-Fall.
