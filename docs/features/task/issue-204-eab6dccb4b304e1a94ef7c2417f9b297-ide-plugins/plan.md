# Umsetzungsplan: Split-Button-Muster für IDE-Öffnen-Funktion

## Übersicht

Das IDE-Öffnen-Feature in der TaskDetailView wird um ein Split-Button-Muster erweitert. Der Haupt-Button öffnet direkt den ersten (priorisierten) Einstiegspunkt, während ein zusätzlicher Dropdown-Button nur bei mehreren verfügbaren Einstiegspunkten sichtbar wird und eine Auswahlliste anzeigt. Diese Änderung betrifft die WPF-UI-Schicht und erweitert das bestehende IDE-Plugin-System ohne Änderungen an Domain- oder Application-Logik.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| **Split-Button-Komponente** | Neue dedizierte `RibbonSplitButton.xaml`-Komponente statt Erweiterung von `RibbonLargeButton` | Saubere Trennung der Verantwortlichkeiten; `RibbonLargeButton` bleibt unverändert und kann für andere Zwecke wiederverwendet werden. Split-Button-Logik (Dropdown-Sichtbarkeit, zwei separate Befehle) unterscheidet sich grundlegend vom Single-Button-Verhalten. |
| **Einstiegspunkte-Ermittlung** | Hybrid: einmalige Berechnung von `KannIdeAuswaehlen` am Ende von `LadenAsync` (ohne `OpenEntryPointAsync`) **zusätzlich** zur on-demand-Ermittlung bei jedem Haupt-/Dropdown-Button-Klick | Der Dropdown-Button muss bereits beim ersten Anzeigen der View korrekt sichtbar/unsichtbar sein (`TaskDetailViewModel` ist `Transient` registriert, jede neu geöffnete View startet sonst mit `KannIdeAuswaehlen == false`). Die zusätzliche Ermittlung bei jedem Öffnen-Versuch bleibt bestehen, da sich Einstiegspunkte zwischen Laden und Klick ändern können und der eigentliche Öffnen-Vorgang ohnehin eine frische Ermittlung benötigt. Der Overhead der zusätzlichen Ermittlung beim Laden ist bei bereits geladenen Aufgaben minimal. |
| **Dialog-Anzeige** | Wiederverwendung von `ShowSolutionSelectionDialogAsync` (mit Pfad-Strings) statt neue Methode mit vollständigen `IdeEntryPoint`-Objekten | Minimale Änderungen an bestehenden Interfaces; `IdeEntryPoint.DisplayName` wird zur Anzeige genutzt. Eine neue `ShowIdeSelectionDialogAsync` mit Plugin-Informationen bleibt als optionale zukünftige Erweiterung. |
| **Fallback-Logik Haupt-Button** | Haupt-Button verwendet weiterhin Fallback-Verhalten via `PluginSelectionService.ResolveIdePluginAsync` (Single-Plugin-Auflösung, kein Callback) | Konsistent mit aktuellem Verhalten; kein Breaking Change. Falls primäres Plugin kompatibel ist und Einstiegspunkte hat, wird der erste geöffnet. |
| **Dialog-Inhalt (Iteration 2 — löst Offene Frage 1)** | Einstiegspunkte ALLER kompatiblen (Explicit- oder Fallback-kompatiblen), aktivierten IDE-Plugins aggregiert anzeigen — nicht mehr nur des einen priorisierten Plugins | Explizite Anwenderentscheidung (Originalzitat): „es soll bei dieser auswahl nicht nur um die entrypoints innerhalb des einen ide-plugins gehen. sondern um alle kompatiblen ide-plugins. so soll es möglich sein, dass sowohl Visual Studio als explizites Plugin auch Visual Studio Code als Fallback aufgerufen werden kann." Löst die bisher offene Frage 1 aus `requirement.md` explizit zugunsten der „umfassenderen" Option. Ersetzt die ursprüngliche Designentscheidung „Nur Einstiegspunkte des priorisierten Plugins". |
| **Aggregations-Ort** | Neue Methode `PluginSelectionService.ResolveAlleKompatiblenIdePluginsAsync(string repositoryPath, CancellationToken ct = default)` statt Aggregationslogik direkt im `TaskDetailViewModel` über `IPluginManager` | Wiederverwendung der bestehenden Priorisierungs-/Aktivierungslogik (`PluginActivationService.GetEnabledIdePluginsAsync`, `ApplyIdePluginOrder`/`IdePluginOrderResolver`, Default-Plugin-Fallback über `IPluginManager.GetDefaultIdePlugin`), die bereits in `ResolveIdePluginAsync` liegt. Konsistenz zwischen Einzel- und Mehrfachauflösung ist wichtiger als Kapselung im ViewModel. |
| **Sortierung der aggregierten Liste** | Explicit-kompatible Plugins zuerst (in konfigurierter `plugins.ide.order`-Reihenfolge), danach Fallback-kompatible Plugins (ebenfalls in konfigurierter Reihenfolge) | Konsistent mit der Priorisierungslogik von `ResolveIdePluginAsync`. Dadurch ist das erste Element der aggregierten Liste (Plugin und dessen erster Einstiegspunkt) inhaltlich identisch mit dem, was der Haupt-Button über `ResolveIdePluginAsync` öffnen würde — Haupt- und Dropdown-Button bleiben konzeptionell konsistent. |
| **Callback-Rückgabetyp `WaehleEntryPointAsync`** | Ändern von `Task<IdeEntryPoint?>` auf `Task<(IIdePlugin Plugin, IdeEntryPoint EntryPoint)?>`; der Callback-Parametertyp in `OeffneIdeInternAsync` wird entsprechend auf `Func<IReadOnlyList<(IIdePlugin Plugin, IdeEntryPoint EntryPoint)>, CancellationToken, Task<(IIdePlugin Plugin, IdeEntryPoint EntryPoint)?>>?` geändert | Da Einstiegspunkte jetzt von unterschiedlichen Plugins stammen können, muss der gewählte Eintrag über das zu ihm gehörende Plugin geöffnet werden (`gewaehlt.Value.Plugin.OpenEntryPointAsync(gewaehlt.Value.EntryPoint, ct)`), nicht mehr zwingend über das eine zuvor über `ResolveIdePluginAsync` aufgelöste Plugin. |
| **Dialog-Anzeige-Format** | Weiterhin `IDialogService.ShowSolutionSelectionDialogAsync(IReadOnlyList<string>, CancellationToken)` nutzen (Interface/Implementierung unverändert), aber mit neu zusammengesetzten, plugin-qualifizierten Anzeige-Strings über eine neue private Hilfsmethode `TaskDetailViewModel.FormatiereAnzeigeWert(IIdePlugin plugin, IdeEntryPoint entryPoint)` | Kein neues Dialog-Interface nötig, minimiert Änderungsumfang (entspricht der ursprünglichen Designentscheidung „Wiederverwendung von `ShowSolutionSelectionDialogAsync`"). `FormatiereAnzeigeWert` liefert `"{PluginName}: {DisplayName ?? Path.GetFileName(Path)}"`, außer die ermittelte Einstiegspunkt-Bezeichnung ist bereits identisch mit `PluginName` (Fall `VisualStudioCodeIdePlugin`, dessen einziger Einstiegspunkt `DisplayName == "Visual Studio Code"` liefert, was bereits `PluginName` entspricht) — dann wird nur `PluginName` angezeigt. Beispiel-Ergebnisse: „Visual Studio: MyProject.sln" bzw. „Visual Studio Code". Die Auswahl wird intern über den Listenindex (nicht über Stringgleichheit) auf das zugehörige `(Plugin, EntryPoint)`-Tupel zurückgeführt, um Mehrdeutigkeiten bei zufällig identischen Anzeige-Strings zu vermeiden. |
| **Haupt-Button-Pfad bleibt eigenständig** | `OeffneIdeInternAsync` behält für den Haupt-Button (`waehleEntryPointAsync == null`) den bestehenden Single-Plugin-Pfad über `ErmittleIdeEntryPointsAsync`/`PluginSelectionService.ResolveIdePluginAsync` bei; nur die zusätzliche `KannIdeAuswaehlen`-Neuberechnung nutzt zusätzlich die neue aggregierte Ermittlung | Erfüllt die explizite Vorgabe „Haupt-Button-Verhalten bleibt unverändert" (kein Breaking Change für den Haupt-Klick-Pfad selbst). Nimmt dafür in Kauf, dass beim Haupt-Klick zwei Ermittlungen (Single-Plugin + Aggregiert) statt einer stattfinden — siehe „Seiteneffekte und Risiken". |

## Programmabläufe

### Haupt-Button-Klick (Bestehendes Verhalten, inhaltlich unverändert; `KannIdeAuswaehlen`-Nebenberechnung jetzt aggregiert)

1. Benutzer klickt auf den Haupt-Button des Split-Buttons
2. `OeffneIdeCommand` wird ausgelöst → ruft `OeffneIdeAsync` auf → ruft `OeffneIdeInternAsync(waehleEntryPointAsync: null, ct)` auf
3. `OeffneIdeInternAsync` ermittelt (Single-Plugin-Pfad, wie bisher) über die bestehende private Methode `ErmittleIdeEntryPointsAsync(lokalerKlonPfad, ct)`: effektives Arbeitsverzeichnis → `PluginSelectionService.ResolveIdePluginAsync(effectiveWorkdir, ct)` (genau EIN Plugin) → `FindEntryPointsAsync` auf diesem einen Plugin
4. **Neu:** Zusätzlich (unabhängig vom Öffnen-Ergebnis) wird über die neue private Methode `ErmittleAggregierteIdeEinstiegspunkteAsync(lokalerKlonPfad, ct)` (siehe Dropdown-Ablauf) die aggregierte Gesamtanzahl über ALLE kompatiblen Plugins ermittelt und `KannIdeAuswaehlen = BerechneKannIdeAuswaehlen(eintraege.Count)` gesetzt — nicht mehr nur aus der Anzahl des einen in Schritt 3 aufgelösten Plugins
5. **0 Einstiegspunkte (Single-Plugin, Schritt 3):** Fehler wird geworfen und in `FehlerMeldung` angezeigt (bestehend)
6. **1 Einstiegspunkt:** Wird direkt via `plugin.OpenEntryPointAsync` geöffnet
7. **≥2 Einstiegspunkte:** Erster Einstiegspunkt wird direkt via `plugin.OpenEntryPointAsync` geöffnet (Fallback, bestehend)

Beteiligte Klassen/Komponenten: `RibbonSplitButton`, `TaskDetailViewModel` (`OeffneIdeAsync`, `OeffneIdeInternAsync`, `ErmittleIdeEntryPointsAsync`, `ErmittleAggregierteIdeEinstiegspunkteAsync`), `PluginSelectionService`, IDE-Plugin (`IIdePlugin`)

### Dropdown-Button-Klick (Erweitert: Aggregation über alle kompatiblen IDE-Plugins)

1. Benutzer klickt auf den Dropdown-Teil des Split-Buttons
2. `OeffneIdeAuswahlCommand` wird ausgelöst → ruft `OeffneIdeAuswahlAsync` auf → ruft `OeffneIdeInternAsync(waehleEntryPointAsync: WaehleEntryPointAsync, ct)` auf
3. `OeffneIdeInternAsync` ruft die neue private Methode `ErmittleAggregierteIdeEinstiegspunkteAsync(lokalerKlonPfad, ct)` auf:
   - Ermittelt das effektive Arbeitsverzeichnis über `ErmittleEffektivesArbeitsverzeichnisAsync`
   - Ruft die neue Methode `PluginSelectionService.ResolveAlleKompatiblenIdePluginsAsync(effectiveWorkdir, ct)` auf, um ALLE aktivierten, zum Repository `Explicit`- oder `Fallback`-kompatiblen IDE-Plugins zu ermitteln (sortiert: erst alle `Explicit`-Plugins, dann alle `Fallback`-Plugins, jeweils in der konfigurierten `plugins.ide.order`-Reihenfolge; siehe „Änderungen an bestehenden Klassen")
   - Ruft für **jedes** zurückgegebene Plugin `FindEntryPointsAsync(effectiveWorkdir, ct)` auf und führt die Ergebnisse zu einer Liste von `(IIdePlugin Plugin, IdeEntryPoint EntryPoint)`-Tupeln zusammen (Plugin-Reihenfolge aus dem vorigen Schritt sowie Einstiegspunkt-Reihenfolge je Plugin bleiben erhalten)
   - Liefert `(EffectiveWorkdir, Eintraege)` zurück
4. `KannIdeAuswaehlen` wird aus der Gesamtanzahl der aggregierten Tupel neu berechnet: `KannIdeAuswaehlen = BerechneKannIdeAuswaehlen(eintraege.Count)`
5. **0 Einstiegspunkte insgesamt:** `FileNotFoundException` wird geworfen und in `FehlerMeldung` angezeigt (wie bisher, jetzt bezogen auf die aggregierte Gesamtanzahl)
6. **1 Einstiegspunkt insgesamt:** Callback `WaehleEntryPointAsync` wird **nicht** aufgerufen; der einzige Eintrag wird direkt über sein zugehöriges Plugin geöffnet: `eintraege[0].Plugin.OpenEntryPointAsync(eintraege[0].EntryPoint, ct)` (Optimierung, wie bisher — jetzt bezogen auf die Gesamtanzahl über alle Plugins, nicht mehr nur ein Plugin)
7. **≥2 Einstiegspunkte insgesamt:**
   - Callback `WaehleEntryPointAsync(eintraege, ct)` wird mit der vollständigen aggregierten Liste aufgerufen
   - Callback baut je Eintrag über die neue Hilfsmethode `FormatiereAnzeigeWert(Plugin, EntryPoint)` einen plugin-qualifizierten Anzeige-String (z. B. „Visual Studio: MyProject.sln", „Visual Studio Code")
   - `IDialogService.ShowSolutionSelectionDialogAsync(anzeigeWerte, ct)` wird mit der Liste der Anzeige-Strings aufgerufen (Interface/Implementierung unverändert)
   - Dialog zeigt Auswahl an; Benutzer wählt einen Anzeige-String oder bricht ab
   - Callback ermittelt den Listenindex (`anzeigeWerte.IndexOf(gewaehlterWert)`) des gewählten Anzeige-Strings und liefert das an gleicher Position stehende `(Plugin, EntryPoint)`-Tupel aus `eintraege` zurück (oder `null` bei Abbruch bzw. falls der Index nicht gefunden wird)
   - Falls Auswahl: `OeffneIdeInternAsync` öffnet über das zurückgegebene Plugin: `gewaehlt.Value.Plugin.OpenEntryPointAsync(gewaehlt.Value.EntryPoint, ct)` — **nicht mehr zwingend** über das eine, zuvor für den Haupt-Button über `ResolveIdePluginAsync` aufgelöste Plugin
   - Falls Abbruch: nichts wird geöffnet

Beteiligte Klassen/Komponenten: `RibbonSplitButton`, `TaskDetailViewModel` (`OeffneIdeAuswahlAsync`, `OeffneIdeInternAsync`, `ErmittleAggregierteIdeEinstiegspunkteAsync`, `WaehleEntryPointAsync`, `FormatiereAnzeigeWert`), `IDialogService`, `PluginSelectionService` (`ResolveAlleKompatiblenIdePluginsAsync`), IDE-Plugins (`IIdePlugin`, z. B. `VisualStudioIdePlugin`, `VisualStudioCodeIdePlugin`)

### Sichtbarkeitskontrolle des Dropdown-Buttons (Erweitert: aggregierte Gesamtanzahl statt Single-Plugin-Anzahl)

Hybrides Verhalten bleibt bestehen: `KannIdeAuswaehlen` wird sowohl einmalig beim Laden der Aufgabe als auch erneut bei jedem Öffnen-Versuch berechnet, damit der Dropdown-Button bereits beim ersten Anzeigen der View korrekt sichtbar/unsichtbar ist und trotzdem bei jedem Öffnen-Versuch den aktuellen Stand widerspiegelt. **Neu ist, dass beide Berechnungen jetzt auf der aggregierten Gesamtanzahl über alle kompatiblen, aktivierten IDE-Plugins basieren (`ErmittleAggregierteIdeEinstiegspunkteAsync`), nicht mehr nur auf den Einstiegspunkten des einen über `ResolveIdePluginAsync` aufgelösten Plugins.**

**a) Einmalige Berechnung beim Laden (`LadenAsync` → `AktualisiereKannIdeAuswaehlenAsync`):**

1. `LadenAsync` wird beim Initialisieren der View oder beim Wechsel der Aufgabe aufgerufen (Setter von `AufgabeId`) und ruft am Ende `AktualisiereKannIdeAuswaehlenAsync(ct)` auf
2. `AktualisiereKannIdeAuswaehlenAsync` ruft **neu** `ErmittleAggregierteIdeEinstiegspunkteAsync(lokalerKlonPfad, ct)` auf (statt bisher `ErmittleIdeEntryPointsAsync`):
   - Arbeitsverzeichnis wird über `ErmittleEffektivesArbeitsverzeichnisAsync` ermittelt
   - ALLE aktivierten, kompatiblen IDE-Plugins werden über `PluginSelectionService.ResolveAlleKompatiblenIdePluginsAsync` aufgelöst
   - `FindEntryPointsAsync` wird auf **jedem** dieser Plugins aufgerufen und zu einer Gesamtliste aggregiert — **ohne** anschließenden Aufruf von `OpenEntryPointAsync`
   - Falls Fehler, keine kompatiblen Plugins oder kein Arbeitsverzeichnis: `KannIdeAuswaehlen = false` (wird **nicht** als `FehlerMeldung` angezeigt, da das Laden der Aufgabe selbst erfolgreich war)
   - **< 2 Einstiegspunkte (aggregiert über alle Plugins):** `KannIdeAuswaehlen = false` → Dropdown-Button unsichtbar
   - **≥ 2 Einstiegspunkte (aggregiert über alle Plugins):** `KannIdeAuswaehlen = true` → Dropdown-Button sichtbar. Dies gilt jetzt **auch dann**, wenn jedes einzelne kompatible Plugin für sich genommen nur einen Einstiegspunkt liefert (z. B. 1× Visual Studio `.sln` + 1× Visual Studio Code Fallback = 2 aggregierte Einstiegspunkte → Dropdown sichtbar)
3. Binding in `RibbonSplitButton` reagiert auf Eigenschaftsänderung und passt Sichtbarkeit an

**b) Erneute Berechnung bei jedem Öffnen-Versuch (Haupt- oder Dropdown-Button-Klick):**

Bei jedem Klick auf Haupt- oder Dropdown-Button wird `KannIdeAuswaehlen` als Nebeneffekt von `OeffneIdeInternAsync` erneut berechnet — **ebenfalls aus der aggregierten Ermittlung über `ErmittleAggregierteIdeEinstiegspunkteAsync`**, unabhängig davon, ob der eigentliche Öffnen-Vorgang (beim Haupt-Button weiterhin) über den Single-Plugin-Pfad läuft (siehe Abschnitte „Haupt-Button-Klick" und „Dropdown-Button-Klick"). Diese erneute Berechnung ersetzt den beim Laden ermittelten Wert.

Beteiligte Klassen/Komponenten: `TaskDetailViewModel` (`AktualisiereKannIdeAuswaehlenAsync`, `ErmittleAggregierteIdeEinstiegspunkteAsync`, `BerechneKannIdeAuswaehlen`), `PluginSelectionService` (`ResolveAlleKompatiblenIdePluginsAsync`), IDE-Plugins, `RibbonSplitButton` (XAML-Binding)

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `RibbonSplitButton` | WPF UserControl (XAML + Code-Behind) | Ribbon-Button mit zwei Teilen: Haupt-Button und Dropdown-Button mit Pfeil; steuert Sichtbarkeit des Dropdown basierend auf einer Boolean-Property |

## Änderungen an bestehenden Klassen

> **Hinweis zum Stand:** Das Split-Button-Grundfeature (Haupt-/Dropdown-Button, `KannIdeAuswaehlen`, `OeffneIdeAuswahlCommand`, `WaehleEntryPointAsync` als Single-Plugin-Callback) ist bereits implementiert (siehe Commits `f5a44f0`, `63f3d9e`). Es gibt **keine** separate `IdeOeffnenService`-Klasse — die Ermittlungs-/Öffnen-Logik liegt direkt in `TaskDetailViewModel` (`OeffneIdeInternAsync`, `ErmittleIdeEntryPointsAsync`) und nutzt `PluginSelectionService` sowie `IIdePlugin` direkt. Die folgenden Abschnitte beschreiben ausschließlich die **Erweiterung** dieses bestehenden Stands um die Multi-Plugin-Aggregation.

### `PluginSelectionService` (Application-Service-Klasse)

- **Neue Methode:** `ResolveAlleKompatiblenIdePluginsAsync(string repositoryPath, CancellationToken ct = default)` → `Task<IReadOnlyList<IIdePlugin>>`
  - Analog zu `ResolveIdePluginAsync`, aber liefert statt eines einzelnen Plugins **alle** kompatiblen:
    1. `ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath)`
    2. `enabledPlugins = await _pluginActivationService.GetEnabledIdePluginsAsync(ct)`; ist die Liste leer, wird eine einelementige Liste `[_pluginManager.GetDefaultIdePlugin()]` zurückgegeben (Konsistenz mit dem No-Plugin-Fallback von `ResolveIdePluginAsync`)
    3. `orderSetting` wird wie in `ResolveIdePluginAsync` über `_appEinstellungService?.GetSettingAsync(AppEinstellungService.IdePluginOrderKey, ct)` gelesen, `orderedPlugins = ApplyIdePluginOrder(enabledPlugins, orderSetting)` (bestehende private Methode wird wiederverwendet, keine Änderung an ihr nötig)
    4. Für jedes Plugin in `orderedPlugins` wird `await plugin.CheckCompatibilityAsync(repositoryPath, ct)` aufgerufen; Ergebnis `Explicit` → in eine Liste `explicitPlugins` einsortieren, `Fallback` → in eine Liste `fallbackPlugins` einsortieren, `Incompatible` → verwerfen
    5. Sind sowohl `explicitPlugins` als auch `fallbackPlugins` leer (kein aktiviertes Plugin kompatibel), wird — analog zu `ResolveIdePluginAsync`s `fallbackPlugin ?? _pluginManager.GetDefaultIdePlugin()` — eine einelementige Liste `[_pluginManager.GetDefaultIdePlugin()]` zurückgegeben
    6. Sonst: Rückgabe `explicitPlugins.Concat(fallbackPlugins).ToList()` (Explicit-Plugins zuerst, dann Fallback-Plugins, jeweils in der durch `orderedPlugins` vorgegebenen Reihenfolge)
  - Nutzt ausschließlich bereits vorhandene private Hilfen (`ApplyIdePluginOrder`) und Felder (`_pluginActivationService`, `_appEinstellungService`, `_pluginManager`) — keine neuen Abhängigkeiten, kein neuer Konstruktor-Parameter.
  - `ResolveIdePluginAsync` selbst bleibt **unverändert** (weiterhin genutzt vom Haupt-Button-Pfad).

### `TaskDetailViewModel` (ViewModel-Klasse)

Bereits vorhandene Elemente (Grundfeature, unverändert): `KannIdeAuswaehlen`-Property, `OeffneIdeAuswahlCommand`, `OeffneIdeAsync`, `OeffneIdeAuswahlAsync`, `ErmittleEffektivesArbeitsverzeichnisAsync`.

- **Neue private Methode:** `ErmittleAggregierteIdeEinstiegspunkteAsync(string lokalerKlonPfad, CancellationToken ct)` → `Task<(string EffectiveWorkdir, IReadOnlyList<(IIdePlugin Plugin, IdeEntryPoint EntryPoint)> Eintraege)>`
  - `effectiveWorkdir = await ErmittleEffektivesArbeitsverzeichnisAsync(lokalerKlonPfad, ct)`
  - `plugins = await _pluginSelectionService.ResolveAlleKompatiblenIdePluginsAsync(effectiveWorkdir, ct)`
  - Für jedes Plugin in `plugins` (in dieser Reihenfolge): `entryPoints = await plugin.FindEntryPointsAsync(effectiveWorkdir, ct)`, anschließend jeden `entryPoint` als `(plugin, entryPoint)`-Tupel an eine Ergebnisliste anhängen
  - Rückgabe `(effectiveWorkdir, eintraege)`
  - Pendant zur bestehenden `ErmittleIdeEntryPointsAsync` (Single-Plugin), die **unverändert** bestehen bleibt und weiterhin vom Haupt-Button-Pfad genutzt wird.

- **Neue private Hilfsmethode:** `static string FormatiereAnzeigeWert(IIdePlugin plugin, IdeEntryPoint entryPoint)`
  - `var bezeichnung = entryPoint.DisplayName ?? Path.GetFileName(entryPoint.Path);`
  - `return string.Equals(bezeichnung, plugin.PluginName, StringComparison.Ordinal) ? plugin.PluginName : $"{plugin.PluginName}: {bezeichnung}";`
  - Ergebnis z. B. „Visual Studio: MyProject.sln" (VS-Einstiegspunkte haben `DisplayName == null`, `Path` ist der volle `.sln`-Pfad → Dateiname wird verwendet) bzw. „Visual Studio Code" (VS-Code-Einstiegspunkt hat bereits `DisplayName == "Visual Studio Code" == PluginName` → kein Doppel-Label).

- **Geänderte Methode:** `BerechneKannIdeAuswaehlen`
  - Signatur ändert sich von `private static bool BerechneKannIdeAuswaehlen(IReadOnlyList<IdeEntryPoint> entryPoints)` auf `private static bool BerechneKannIdeAuswaehlen(int entryPointCount) => entryPointCount >= 2;`
  - Grund: Der Aufrufer übergibt jetzt in den meisten Fällen `eintraege.Count` aus der aggregierten Tupel-Liste (`IReadOnlyList<(IIdePlugin, IdeEntryPoint)>`), nicht mehr eine reine `IdeEntryPoint`-Liste; ein `int`-Parameter deckt beide Aufrufstellen (aggregiert und Single-Plugin, z. B. für zukünftige Wiederverwendung) einheitlich ab.

- **Geänderte Methode:** `AktualisiereKannIdeAuswaehlenAsync(CancellationToken ct)`
  - Ruft jetzt `ErmittleAggregierteIdeEinstiegspunkteAsync(lokalerKlonPfad, ct)` auf (statt bisher `ErmittleIdeEntryPointsAsync`) und setzt `KannIdeAuswaehlen = BerechneKannIdeAuswaehlen(eintraege.Count)`. Fehlerbehandlung (Catch-Block setzt `KannIdeAuswaehlen = false`, kein `FehlerMeldung`) bleibt unverändert.

- **Geänderte Methode:** `WaehleEntryPointAsync`
  - Signatur ändert sich von `private async Task<IdeEntryPoint?> WaehleEntryPointAsync(IReadOnlyList<IdeEntryPoint> entryPoints, CancellationToken ct)` auf `private async Task<(IIdePlugin Plugin, IdeEntryPoint EntryPoint)?> WaehleEntryPointAsync(IReadOnlyList<(IIdePlugin Plugin, IdeEntryPoint EntryPoint)> eintraege, CancellationToken ct)`
  - Neue Implementierung:
    ```
    var anzeigeWerte = eintraege.Select(e => FormatiereAnzeigeWert(e.Plugin, e.EntryPoint)).ToList();
    var gewaehlterWert = await _dialogService.ShowSolutionSelectionDialogAsync(anzeigeWerte, ct);
    if (gewaehlterWert is null) return null;
    var index = anzeigeWerte.IndexOf(gewaehlterWert);
    return index >= 0 ? eintraege[index] : null;
    ```
  - Matching erfolgt jetzt über den Listenindex statt über Stringgleichheit auf `IdeEntryPoint`, da mehrere Einträge (aus unterschiedlichen Plugins) theoretisch denselben Anzeige-String liefern könnten.

- **Geänderte Methode:** `OeffneIdeInternAsync`
  - Parametertyp ändert sich von `Func<IReadOnlyList<IdeEntryPoint>, CancellationToken, Task<IdeEntryPoint?>>? waehleEntryPointAsync` auf `Func<IReadOnlyList<(IIdePlugin Plugin, IdeEntryPoint EntryPoint)>, CancellationToken, Task<(IIdePlugin Plugin, IdeEntryPoint EntryPoint)?>>? waehleEntryPointAsync`
  - Verzweigt jetzt explizit nach `waehleEntryPointAsync`:
    - **`null` (Haupt-Button):** bestehender Single-Plugin-Pfad über `ErmittleIdeEntryPointsAsync` bleibt erhalten (öffnet `entryPoints[0]` über das eine aufgelöste `plugin`), **zusätzlich** wird `ErmittleAggregierteIdeEinstiegspunkteAsync` aufgerufen, um `KannIdeAuswaehlen` aggregiert zu aktualisieren (siehe „Seiteneffekte und Risiken" zum Mehraufwand)
    - **nicht `null` (Dropdown-Button):** nutzt ausschließlich `ErmittleAggregierteIdeEinstiegspunkteAsync`; bei 0 Einträgen `FileNotFoundException`, bei 1 Eintrag direktes Öffnen über `eintraege[0].Plugin`, bei ≥2 Aufruf von `waehleEntryPointAsync(eintraege, ct)` und Öffnen über das zurückgegebene `(Plugin, EntryPoint)`-Tupel
  - `KannIdeAuswaehlen` wird in beiden Zweigen aus der aggregierten Anzahl gesetzt (siehe „Sichtbarkeitskontrolle des Dropdown-Buttons").

- **Unverändert:** `OeffneIdeAsync`, `OeffneIdeAuswahlAsync` (rufen weiterhin nur `OeffneIdeInternAsync` mit `null` bzw. `WaehleEntryPointAsync` auf), `ErmittleIdeEntryPointsAsync`, `ErmittleEffektivesArbeitsverzeichnisAsync`, `OeffneIdeCommand`, `OeffneIdeAuswahlCommand`, `KannIdeAuswaehlen`-Property-Deklaration.

### `TaskDetailView.xaml` (WPF View)

- **Ersetzung eines UI-Elements:** 
  - Der bestehende `<controls:RibbonLargeButton>` (Zeile ~180–183) wird durch eine neue `<controls:RibbonSplitButton>`-Komponente ersetzt
  - Bindungen: Haupt-Button bindet `OeffneIdeCommand`, Dropdown-Button bindet `OeffneIdeAuswahlCommand`; Dropdown-Sichtbarkeit bindet `KannIdeAuswaehlen`
  - Icon/Text: Icon "🛠", Text "IDE öffnen", AutomationName "IdeOeffnen" (unverändert)

### `RibbonLargeButton.xaml` (WPF UserControl)

- **Keine Änderungen** — Komponente bleibt unverändert und wird weiterhin für einzelne Buttons genutzt

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine.

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **TaskDetailView-Abhängige Tests:** E2E-Tests, die die Struktur oder das Verhalten des IDE-Öffnen-Buttons prüfen, müssen auf die neue `RibbonSplitButton`-Komponente angepasst werden (z. B. das Lokalisieren und Klicken des Dropdown-Buttons, Prüfung der Dropdown-Sichtbarkeit basierend auf Einstiegspunkt-Anzahl).
- **`OeffneIdeAsync` Callback-Verhalten:** Der bestehende Inline-Callback in `OeffneIdeAsync` (der `ShowSolutionSelectionDialogAsync` aufruft) wird in die neue Methode `waehleEntryPointAsync` ausgelagert. Falls andere Code-Stellen `OeffneIdeAsync` direkt aufrufen und der Callback-Aufruf erwarten, könnte dies ein Breaking Change sein — ist aber unwahrscheinlich, da die Methode `private` ist.
- **Asynchrone Ermittlung von `KannIdeAuswaehlen`:** Property `KannIdeAuswaehlen` kann sich nach dem Laden der View asynchron ändern (Einstiegspunkte werden erst nach Plugin-Auflösung ermittelt). Dies könnte kurzzeitig zu inkonsistenter UI führen (Dropdown-Button erscheint später). Dies ist akzeptabel und entspricht Bestandteilen wie `KannIdeOeffnen`, die ebenfalls vom Arbeitsverzeichnis abhängen.
- **Betroffene bestehende Tests:** 
  - `TaskDetailViewModelTests` — Evtl. müssen Tests für `OeffneIdeCommand` angepasst werden, wenn sie Mock-Argumente von `OpenRepositoryInIdeAsync` prüfen (Callback-Signatur bleibt identisch, also kein Bruch, aber neue Tests sind erforderlich).
  - E2E-Tests für TaskDetailView — Tests, die auf den IDE-Button klicken, müssen aktualisiert werden, um den neuen Split-Button zu handhaben.

**Zusätzliche Risiken der Erweiterung (Multi-Plugin-Aggregation):**

- **Performance bei vielen aktivierten IDE-Plugins:** `ResolveAlleKompatiblenIdePluginsAsync` ruft `CheckCompatibilityAsync` für **jedes** aktivierte IDE-Plugin auf (statt nur bis zum ersten Treffer wie `ResolveIdePluginAsync`), und `ErmittleAggregierteIdeEinstiegspunkteAsync` ruft anschließend `FindEntryPointsAsync` auf **jedem** als kompatibel zurückgegebenen Plugin auf. Bei aktuell zwei IDE-Plugins (VS, VS Code) ist der Mehraufwand vernachlässigbar; bei künftig mehr IDE-Plugins könnte sich das Laden der Aufgabe bzw. der Öffnen-Versuch spürbar verzögern, insbesondere wenn einzelne Plugins Dateisystem- oder Prozess-Zugriffe in `CheckCompatibilityAsync`/`FindEntryPointsAsync` durchführen. Akzeptiert, da aktuelle Plugin-Anzahl gering ist; bei Bedarf könnte künftig eine parallele Ausführung der Compatibility-Checks (`Task.WhenAll`) nachgerüstet werden.
- **Doppelte Ermittlung beim Haupt-Button-Klick:** Da der Haupt-Button-Pfad laut Designentscheidung unverändert über `ErmittleIdeEntryPointsAsync`/`ResolveIdePluginAsync` läuft, aber `KannIdeAuswaehlen` jetzt zusätzlich aggregiert berechnet wird, finden bei jedem Haupt-Button-Klick **zwei** unabhängige Ermittlungsdurchläufe statt (Single-Plugin für das Öffnen + aggregiert für `KannIdeAuswaehlen`), die sich teilweise überschneiden (z. B. wird das vom Single-Plugin-Pfad gewählte Plugin i. d. R. auch im aggregierten Durchlauf erneut geprüft). Bewusst in Kauf genommen zugunsten von Einfachheit/Nachvollziehbarkeit und um das explizit geforderte unveränderte Haupt-Button-Verhalten nicht mit der neuen Aggregationslogik zu vermischen.
- **Breaking-Change-Risiko `WaehleEntryPointAsync`-Signatur:** Die Callback-Signatur ändert sich von `Func<IReadOnlyList<IdeEntryPoint>, CancellationToken, Task<IdeEntryPoint?>>` auf ein Tupel-basiertes Pendant. Da `WaehleEntryPointAsync` und der Callback-Parameter von `OeffneIdeInternAsync` `private` sind, betrifft dies keine externen Aufrufer — wohl aber alle bestehenden Unit-Tests, die den Dialog-Mock mit rohen Pfad-/DisplayName-Strings füttern (siehe „Betroffene bestehende Tests").
- **Geändertes Anzeigeformat im Auswahl-Dialog:** Einträge, die zuvor als roher `IdeEntryPoint.Path` (z. B. voller `.sln`-Pfad) angezeigt wurden, erscheinen jetzt als `"{PluginName}: {Dateiname}"`. Dies ist eine bewusste, vom Anwender indirekt geforderte UX-Änderung (Plugin muss erkennbar sein), kann aber bestehende, auf den alten Anzeige-Strings basierende Screenshots/Dokumentation/E2E-Selektoren invalidieren.
- **Immer vorhandener VS-Code-Fallback-Eintrag:** Da `VisualStudioCodeIdePlugin.CheckCompatibilityAsync` für **jedes** Repository `Fallback` zurückliefert (unabhängig davon, ob Visual Studio Code tatsächlich lokal verfügbar ist) und standardmäßig aktiviert ist, enthält die aggregierte Liste ab jetzt in der Praxis für praktisch jedes Repository mindestens einen zusätzlichen VS-Code-Eintrag, sobald das Plugin aktiviert ist — auch wenn VS Code lokal nicht installiert/auffindbar ist (der Fehler tritt dann erst beim tatsächlichen `OpenEntryPointAsync`-Aufruf auf, wie bereits bisher). Dadurch wird `KannIdeAuswaehlen` in vielen bisher „1 Einstiegspunkt"-Szenarien jetzt `true` (Dropdown erscheint), was die zentrale, vom Anwender gewünschte Verhaltensänderung ist, aber explizit in den betroffenen Bestandstests berücksichtigt werden muss.
- **Mehrdeutige Anzeige-Strings bei Namensgleichheit:** Liefern zwei unterschiedliche Plugins (oder zwei Einstiegspunkte desselben Plugins) zufällig denselben formatierten Anzeige-String, wählt `WaehleEntryPointAsync` über `IndexOf` immer den **ersten** Treffer. Dieses Risiko bestand strukturell bereits vor der Erweiterung (Stringgleichheit auf `DisplayName`/`Path`) und wird durch die Aggregation nicht wesentlich verschärft, da unterschiedliche Plugins durch das Namenspräfix in der Praxis eindeutig unterscheidbar bleiben.

## Umsetzungsreihenfolge

1. **`RibbonSplitButton.xaml` (Komponente) anlegen**
   - Voraussetzungen: Keine (WPF-Grundlagen sind im Projekt vorhanden)
   - Beschreibung: Neue UserControl mit zwei Button-Bereichen (Haupt-Button + Dropdown-Button mit Pfeil). Haupt-Button nutzt DependencyProperties für Icon/Text/Command (ähnlich `RibbonLargeButton`). Dropdown-Button ist unsichtbar wenn Binding `KannIdeAuswaehlen == false`. Styling folgt bestehenden Ribbon-Buttons.

2. **`RibbonSplitButton.xaml.cs` (Code-Behind) implementieren**
   - Voraussetzungen: `RibbonSplitButton.xaml` angelegt
   - Beschreibung: DependencyProperties `ButtonIcon`, `ButtonText`, `AutomationName`, `ButtonCommand`, `DropdownCommand`, `CanShowDropdown` definieren. Event-Handler für Klicks auf Haupt- und Dropdown-Button. Styling (Hover, Pressed, Disabled) analog zu `RibbonLargeButton`.

3. **`TaskDetailViewModel` erweitern — Neue Property `KannIdeAuswaehlen`**
   - Voraussetzungen: `TaskDetailViewModel` existiert (bereits im Repo)
   - Beschreibung: Property `KannIdeAuswaehlen` hinzufügen (initialisiert mit `false`). Wird bei jedem Aufruf von `OeffneIdeAsync` oder `OeffneIdeAuswahlAsync` basierend auf Einstiegspunkt-Anzahl aktualisiert. PropertyChanged-Event wird gefeuert wenn sich die Anzahl ändert.

4. **`TaskDetailViewModel` erweitern — Neue Property `VerfuegbareEinstiegspunkte` (optional)**
   - Voraussetzungen: `TaskDetailViewModel` existiert
   - Beschreibung: Property `VerfuegbareEinstiegspunkte` hinzufügen (vom Typ `IReadOnlyList<IdeEntryPoint>`). Wird bei jedem Aufruf von `OeffneIdeAsync` oder `OeffneIdeAuswahlAsync` mit den ermittelten Einstiegspunkten aktualisiert. Dient Debugging und Logging.

5. **`TaskDetailViewModel` erweitern — Neues Kommando `OeffneIdeAuswahlCommand`**
   - Voraussetzungen: `TaskDetailViewModel` erweitert (Properties), `IdeOeffnenService` existiert
   - Beschreibung: `OeffneIdeAuswahlCommand` (`AsyncRelayCommand`) anlegen, das `OeffneIdeAuswahlAsync` aufruft. CanExecute prüft `KannIdeOeffnen == true` und dass keine laufende Ermittlung stattfindet.

6. **`TaskDetailViewModel` erweitern — Methode `OeffneIdeAuswahlAsync` implementieren**
   - Voraussetzungen: `TaskDetailViewModel` erweitert (Kommando), `waehleEntryPointAsync` Callback-Methode existiert (Schritt 7)
   - Beschreibung: Methode `OeffneIdeAuswahlAsync` implementieren — analog zu `OeffneIdeAsync`, aber mit `waehleEntryPointAsync` Callback an `OpenRepositoryInIdeAsync` übergeben. Fehlerbehandlung identisch.

7. **`TaskDetailViewModel` erweitern — Callback-Methode `waehleEntryPointAsync` implementieren**
   - Voraussetzungen: `TaskDetailViewModel` erweitert, `IDialogService` vorhanden (bereits im Repo)
   - Beschreibung: Private Methode `waehleEntryPointAsync` implementieren, die von `IdeOeffnenService` als Callback aufgerufen wird. Extrahiert Pfade aus `IdeEntryPoint`-Liste (nutzt `DisplayName` falls vorhanden), ruft `ShowSolutionSelectionDialogAsync` mit Pfad-Strings auf, findet das zugehörige `IdeEntryPoint`-Objekt, gibt es zurück (oder `null`).

8. **`TaskDetailView.xaml` anpassen — IDE-Button ersetzen**
   - Voraussetzungen: `RibbonSplitButton` Komponente implementiert, `TaskDetailViewModel` erweitert (alle Kommandos und Properties)
   - Beschreibung: Bestehenden `<controls:RibbonLargeButton>` (Zeile ~180–183) durch `<controls:RibbonSplitButton>` ersetzen. Haupt-Button Binding: `ButtonCommand="{Binding OeffneIdeCommand}"`. Dropdown-Button Binding: `DropdownCommand="{Binding OeffneIdeAuswahlCommand}"`, `CanShowDropdown="{Binding KannIdeAuswaehlen}"`. Icon/Text/AutomationName unverändert.

9. **Unit-Tests schreiben — `TaskDetailViewModel` Kommandos und Properties**
   - Voraussetzungen: `TaskDetailViewModel` erweitert (alle Methoden implementiert), `TaskDetailViewModelTestsBase` existiert
   - Beschreibung: Tests für `OeffneIdeAuswahlCommand` (ausführbar, ruft `OeffneIdeAuswahlAsync` auf), Tests für `KannIdeAuswaehlen` (berechnet korrekt basierend auf Einstiegspunkt-Anzahl), Tests für `waehleEntryPointAsync`-Callback (zeigt Dialog bei mehreren Einstiegspunkten, findet korrektes `IdeEntryPoint`-Objekt).

10. **Unit-Tests anpassen — Bestehende `TaskDetailViewModel`-Tests**
    - Voraussetzungen: `TaskDetailViewModel` erweitert, neue Tests geschrieben (Schritt 9)
    - Beschreibung: Tests für `OeffneIdeAsync` überprüfen, ob neue Callback-Logik beeinträchtigt wird. Falls Tests Mocks der `OpenRepositoryInIdeAsync`-Signatur verwenden, müssen sie ggf. angepasst werden (Callback bleibt aber im Verhalten gleich).

11. **E2E-Test schreiben — Haupt-Button öffnet direkt (bestehend, neu zu verfizieren)**
    - Voraussetzungen: Komponenten implementiert, View angepasst, Unit-Tests grün
    - Beschreibung: E2E-Test, der mit einer Aufgabe mit 1 Einstiegspunkt prüft: Klick auf Haupt-Button öffnet die IDE direkt. Dropdown-Button sollte unsichtbar sein.

12. **E2E-Test schreiben — Dropdown-Button sichtbar bei mehreren Einstiegspunkten**
    - Voraussetzungen: Komponenten implementiert (alle), Unit-Tests grün
    - Beschreibung: E2E-Test, der mit einer Aufgabe mit mehreren Einstiegspunkten prüft: Haupt-Button ist sichtbar, Dropdown-Button ist sichtbar, `KannIdeAuswaehlen == true`. Klick auf Haupt-Button öffnet den ersten Einstiegspunkt direkt.

13. **E2E-Test schreiben — Dropdown-Button zeigt Dialog und öffnet gewählten Einstiegspunkt**
    - Voraussetzungen: Komponenten implementiert (alle), Unit-Tests grün, E2E-Infrastruktur für Dialog-Handling vorhanden
    - Beschreibung: E2E-Test, der mit einer Aufgabe mit mehreren Einstiegspunkten prüft: Klick auf Dropdown-Button zeigt Auswahldialog mit allen Einstiegspunkten. Benutzer wählt einen aus → IDE öffnet den gewählten Einstiegspunkt. Alternativ: Abbruch-Klick → Nichts wird geöffnet.

14. **E2E-Test anpassen — Bestehende IDE-öffnen-Tests**
    - Voraussetzungen: Alle neuen Tests geschrieben (Schritte 11–13), Komponenten und ViewModel angepasst
    - Beschreibung: Falls E2E-Tests existieren, die das IDE-öffnen testen (z. B. über den alten `RibbonLargeButton`), müssen sie auf den neuen `RibbonSplitButton` angepasst werden (Selector/Automation-IDs können sich ändern).

### Umsetzungsreihenfolge (Erweiterung: Multi-Plugin-Aggregation)

Baut auf dem oben beschriebenen, bereits implementierten Split-Button-Grundfeature (Schritte 1–14) auf. Reihenfolge fortlaufend nummeriert:

15. **`PluginSelectionService.ResolveAlleKompatiblenIdePluginsAsync` implementieren**
    - Voraussetzungen: `PluginSelectionService.ResolveIdePluginAsync`, `ApplyIdePluginOrder`/`IdePluginOrderResolver.Apply`, `PluginActivationService.GetEnabledIdePluginsAsync`, `IPluginManager.GetDefaultIdePlugin` (alle bereits im Repo vorhanden)
    - Beschreibung: Neue öffentliche Methode gemäß Spezifikation im Abschnitt „Änderungen an bestehenden Klassen" implementieren (Explicit-Plugins zuerst, dann Fallback-Plugins, jeweils in konfigurierter Reihenfolge; Default-Plugin-Fallback bei keinem aktivierten/kompatiblen Plugin).

16. **`PluginSelectionServiceTests_IdePlugin` um Tests für `ResolveAlleKompatiblenIdePluginsAsync` ergänzen**
    - Voraussetzungen: Schritt 15
    - Beschreibung: Neue Testfälle analog zum bestehenden Testmuster dieser Klasse (`CreateIdePlugin`, `CreatePluginManager`, `CreateSut`) hinzufügen (siehe Tabelle im Abschnitt „Tests").

17. **`TaskDetailViewModel.ErmittleAggregierteIdeEinstiegspunkteAsync` implementieren**
    - Voraussetzungen: Schritt 15
    - Beschreibung: Neue private Methode gemäß Spezifikation implementieren; ruft `ResolveAlleKompatiblenIdePluginsAsync` und anschließend `FindEntryPointsAsync` je Plugin auf und aggregiert zu `(IIdePlugin, IdeEntryPoint)`-Tupeln.

18. **`TaskDetailViewModel.BerechneKannIdeAuswaehlen` Signatur ändern**
    - Voraussetzungen: Keine (reine Signaturänderung an bestehender Methode)
    - Beschreibung: Parameter von `IReadOnlyList<IdeEntryPoint>` auf `int entryPointCount` ändern; alle bestehenden Aufrufer (aktuell in `AktualisiereKannIdeAuswaehlenAsync` und `OeffneIdeInternAsync`) auf `.Count`-Übergabe der jeweils passenden Liste anpassen.

19. **`TaskDetailViewModel.FormatiereAnzeigeWert` (neue private statische Hilfsmethode) implementieren**
    - Voraussetzungen: Keine
    - Beschreibung: Gemäß Spezifikation im Abschnitt „Änderungen an bestehenden Klassen" implementieren (`using System.IO;` für `Path.GetFileName` ist in der Datei bereits vorhanden).

20. **`TaskDetailViewModel.WaehleEntryPointAsync` auf Tupel-Signatur umstellen**
    - Voraussetzungen: Schritt 17, 19
    - Beschreibung: Rückgabetyp und Parametertyp gemäß Spezifikation ändern; Dialog-Anzeige-Strings über `FormatiereAnzeigeWert` erzeugen; Auswahl über Listenindex statt Stringgleichheit zurückführen.

21. **`TaskDetailViewModel.OeffneIdeInternAsync` Callback-Parametertyp und Dropdown-Zweig umstellen**
    - Voraussetzungen: Schritt 17, 18, 20
    - Beschreibung: Callback-Parametertyp gemäß Spezifikation ändern. Haupt-Button-Zweig (`waehleEntryPointAsync == null`) bleibt inhaltlich beim bestehenden Single-Plugin-Pfad (`ErmittleIdeEntryPointsAsync`), berechnet aber zusätzlich `KannIdeAuswaehlen` über `ErmittleAggregierteIdeEinstiegspunkteAsync`. Dropdown-Zweig nutzt ausschließlich die aggregierte Ermittlung und öffnet über das vom Callback zurückgegebene Plugin.

22. **`TaskDetailViewModel.AktualisiereKannIdeAuswaehlenAsync` auf aggregierte Ermittlung umstellen**
    - Voraussetzungen: Schritt 17, 18
    - Beschreibung: Ruft jetzt `ErmittleAggregierteIdeEinstiegspunkteAsync` statt `ErmittleIdeEntryPointsAsync` auf; restliche Fehlerbehandlung unverändert.

23. **Unit-Tests anpassen — bestehende, durch die Aggregation in ihrer Aussage verändernde Tests**
    - Voraussetzungen: Schritte 15–22 vollständig implementiert
    - Beschreibung: Die in der Tabelle „Betroffene bestehende Tests" gelisteten Tests in `TaskDetailViewModelTests_IdeAuswahl.cs` anpassen (Setup mit isoliertem Single-Plugin dort, wo „genau 1 Einstiegspunkt insgesamt" gemeint ist; Dialog-Mock-Rückgabewerte und Verify-Listen auf das neue `"{PluginName}: {…}"`-Anzeigeformat umstellen).

24. **Neue Unit-Tests schreiben — Multi-Plugin-Aggregation**
    - Voraussetzungen: Schritte 15–22
    - Beschreibung: Neue Testfälle gemäß Tabelle „Neue Tests" in `TaskDetailViewModelTests_IdeAuswahl.cs` ergänzen (Dropdown aggregiert über 2 Plugins, Öffnen über das tatsächlich gewählte Plugin statt des von `ResolveIdePluginAsync` aufgelösten, Sortierung Explicit-vor-Fallback, `KannIdeAuswaehlen` bei je 1 Einstiegspunkt pro Plugin aber ≥2 Plugins).

25. **Ggf. E2E-Test-Szenario „mehrere kompatible IDE-Plugins gleichzeitig aktiv" ergänzen**
    - Voraussetzungen: Schritte 15–24 abgeschlossen und grün, bestehende E2E-Infrastruktur zur Aktivierung mehrerer IDE-Plugins in Tests vorhanden (Settings-UI bzw. direktes Setzen des `plugins.enabled.*`-Settings)
    - Beschreibung: E2E-Test, der mit aktivierten VS- und VS-Code-Plugins und vorhandener `.sln`-Datei prüft, dass der Dropdown-Dialog sowohl den VS-Eintrag als auch „Visual Studio Code" anzeigt.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `OeffneIdeAuswahlCommand_ExecuteAsync_RuftOeffneIdeAuswahlAsyncAuf` | `TaskDetailViewModelTests_IdeAuswahl` | Kommando ist verfügbar und ruft `OeffneIdeAuswahlAsync` auf |
| `OeffneIdeAuswahlCommand_CanExecute_WhenKannIdeOeffnenFalse_ReturnsFalse` | `TaskDetailViewModelTests_IdeAuswahl` | Kommando kann nicht ausgeführt werden wenn `KannIdeOeffnen == false` |
| `KannIdeAuswaehlen_WhenOneEntryPoint_ReturnsFalse` | `TaskDetailViewModelTests_IdeAuswahl` | Property ist `false` bei 1 Einstiegspunkt |
| `KannIdeAuswaehlen_WhenMultipleEntryPoints_ReturnsTrue` | `TaskDetailViewModelTests_IdeAuswahl` | Property ist `true` bei ≥2 Einstiegspunkten |
| `KannIdeAuswaehlen_WhenNoEntryPoints_ReturnsFalse` | `TaskDetailViewModelTests_IdeAuswahl` | Property ist `false` bei 0 Einstiegspunkten / Fehler |
| `WaehleEntryPointAsync_WithMultipleEntryPoints_ShowsDialogAndReturnsSelected` | `TaskDetailViewModelTests_IdeAuswahl` | Callback zeigt Dialog und gibt gewählten `IdeEntryPoint` zurück |
| `WaehleEntryPointAsync_WithDialogAbort_ReturnsNull` | `TaskDetailViewModelTests_IdeAuswahl` | Callback gibt `null` zurück wenn Benutzer abbricht |
| `WaehleEntryPointAsync_UsesDisplayNameInDialog` | `TaskDetailViewModelTests_IdeAuswahl` | Callback nutzt `IdeEntryPoint.DisplayName` falls vorhanden für Dialog-Anzeige |
| `OeffneIdeAuswahlAsync_WithNoEntryPoints_ShowsError` | `TaskDetailViewModelTests_IdeAuswahl` | Fehlerbehandlung identisch zu `OeffneIdeAsync` |
| `VerfuegbareEinstiegspunkte_UpdatedAfterOeffneIde` | `TaskDetailViewModelTests_IdeAuswahl` | Property wird mit ermittelten Einstiegspunkten aktualisiert (optional, für Debugging) |
| (ggf. Hilfsmethode) `ErzeugeEntryPointMitDisplayName` | `TaskDetailViewModelTestsBase` | Erstellt Test-`IdeEntryPoint`-Objekte mit `DisplayName` für Tests |

**Neue Tests — Erweiterung Multi-Plugin-Aggregation:**

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `ResolveAlleKompatiblenIdePluginsAsync_ShouldReturnExplicitAndFallbackPlugins_WhenBothCompatible` | `PluginSelectionServiceTests_IdePlugin` | Ein Explicit- und ein Fallback-kompatibles Plugin sind beide in der Rückgabeliste enthalten (nicht nur das Explicit-Plugin wie bei `ResolveIdePluginAsync`) |
| `ResolveAlleKompatiblenIdePluginsAsync_ShouldOrderExplicitPluginsBeforeFallbackPlugins` | `PluginSelectionServiceTests_IdePlugin` | Bei gemischter Kompatibilität stehen alle Explicit-Plugins vor allen Fallback-Plugins in der Rückgabeliste, unabhängig von der Entdeckungsreihenfolge |
| `ResolveAlleKompatiblenIdePluginsAsync_ShouldRespectPluginOrder_FromSetting_WithinEachGroup` | `PluginSelectionServiceTests_IdePlugin` | Innerhalb der Explicit- bzw. Fallback-Gruppe wird die `plugins.ide.order`-Reihenfolge eingehalten |
| `ResolveAlleKompatiblenIdePluginsAsync_ShouldExcludeIncompatiblePlugins` | `PluginSelectionServiceTests_IdePlugin` | Ein `Incompatible`-Plugin taucht nicht in der Rückgabeliste auf, auch wenn es aktiviert ist |
| `ResolveAlleKompatiblenIdePluginsAsync_ShouldReturnDefaultPlugin_WhenNoPluginActive` | `PluginSelectionServiceTests_IdePlugin` | Keine aktivierten Plugins → einelementige Liste mit `IPluginManager.GetDefaultIdePlugin()` (Konsistenz mit `ResolveIdePluginAsync`) |
| `ResolveAlleKompatiblenIdePluginsAsync_ShouldReturnDefaultPlugin_WhenNoPluginCompatible` | `PluginSelectionServiceTests_IdePlugin` | Alle aktivierten Plugins `Incompatible` → einelementige Liste mit `GetDefaultIdePlugin()` |
| `WaehleEntryPointAsync_WithEntryPointsFromTwoPlugins_ShowsBothInDialog` | `TaskDetailViewModelTests_IdeAuswahl` | Bei aktiviertem VS (mit `.sln`) und VS Code (Fallback) enthält die an `ShowSolutionSelectionDialogAsync` übergebene Liste sowohl den formatierten VS-Eintrag als auch „Visual Studio Code" |
| `WaehleEntryPointAsync_SelectingEntryFromFallbackPlugin_OpensViaThatPlugin_NotViaResolvedPlugin` | `TaskDetailViewModelTests_IdeAuswahl` | Wählt der Anwender den VS-Code-Eintrag aus dem Dialog (obwohl VS als `Explicit`-Plugin von `ResolveIdePluginAsync` bevorzugt würde), wird `OpenEntryPointAsync` auf dem VS-Code-Plugin aufgerufen, nicht auf VS |
| `WaehleEntryPointAsync_OrdersExplicitPluginEntriesBeforeFallbackPluginEntries` | `TaskDetailViewModelTests_IdeAuswahl` | Die an den Dialog übergebene Liste ordnet Einträge kompatibler Explicit-Plugins vor Einträgen von Fallback-Plugins |
| `FormatiereAnzeigeWert_ForVisualStudioEntryPoint_UsesPluginNamePrefixAndFileName` | `TaskDetailViewModelTests_IdeAuswahl` | Für einen VS-Einstiegspunkt (`DisplayName == null`) liefert die Formatierung „Visual Studio: {Dateiname}" |
| `FormatiereAnzeigeWert_ForVisualStudioCodeEntryPoint_UsesPluginNameOnly` | `TaskDetailViewModelTests_IdeAuswahl` | Für den VS-Code-Einstiegspunkt (`DisplayName == PluginName`) liefert die Formatierung nur „Visual Studio Code" ohne Doppelung |
| `KannIdeAuswaehlen_WhenEachCompatiblePluginHasExactlyOneEntryPoint_ButMultiplePluginsCompatible_ReturnsTrue` | `TaskDetailViewModelTests_IdeAuswahl` | Regressionstest für die zentrale Anforderung: VS liefert genau 1 `.sln`, VS Code liefert genau 1 Fallback-Eintrag → aggregiert 2 Einstiegspunkte → `KannIdeAuswaehlen == true`, obwohl kein einzelnes Plugin für sich genommen mehrere Einstiegspunkte hat |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `TaskDetailViewModelTests.cs` — Tests für `OeffneIdeCommand` | Evtl. müssen Mock-Setups angepasst werden wenn Tests die Callback-Signatur prüfen, aber Verhalten bleibt gleich (kein Breaking Change erwartet) |
| E2E-Tests in `E2E_TaskDetailView*.cs` oder ähnlich | Müssen aktualisiert werden um `RibbonSplitButton` zu handhaben, da UI-Struktur sich ändert (Old: 1 Button, New: 2 Buttons) |
| `TaskDetailViewModelTests_IdeAuswahl.KannIdeAuswaehlen_WhenOneEntryPoint_ReturnsFalse` | Nutzt aktuell `CreateSut()` ohne `idePlugins`-Override, wodurch standardmäßig VS **und** VS Code aktiv sind. Mit Aggregation liefert VS Code für jedes Repository einen zusätzlichen Fallback-Eintrag, wodurch die Gesamtanzahl bei 1 `.sln`-Datei auf 2 steigt und `KannIdeAuswaehlen` **true** statt `false` würde. **Anpassung:** `CreateSut(idePlugins: [visualStudioPlugin])` (nur VS, kein VS Code) übergeben, um weiterhin „genau 1 Einstiegspunkt insgesamt" zu testen. |
| `TaskDetailViewModelTests_IdeAuswahl.KannIdeAuswaehlen_NachLadenAsync_WhenOneEntryPoint_ReturnsFalse` | Gleicher Grund wie oben (Berechnung erfolgt jetzt in `AktualisiereKannIdeAuswaehlenAsync` über die aggregierte Ermittlung). **Anpassung:** ebenfalls `idePlugins`-Override auf ein einzelnes Plugin. |
| `TaskDetailViewModelTests_IdeAuswahl.OeffneIdeAuswahlCommand_ExecuteAsync_RuftOeffneIdeAuswahlAsyncAuf` | Dialog-Mock (`ShowSolutionSelectionDialogAsync`) gibt aktuell den rohen Solution-Pfad (`zweiteSolution`) zurück und die Verify-Assertion prüft auf rohe Pfade in der Anzeige-Liste. Nach der Umstellung auf `FormatiereAnzeigeWert` enthält die Liste stattdessen `"Visual Studio: Zweite.sln"` (und zusätzlich einen VS-Code-Eintrag, da Standard-Setup beide Plugins aktiviert). **Anpassung:** Mock-Rückgabewert und Verify-Prädikat auf das neue Anzeigeformat umstellen (oder `idePlugins`-Override auf nur VS, falls der zusätzliche VS-Code-Eintrag den Test nicht betreffen soll). |
| `TaskDetailViewModelTests_IdeAuswahl.WaehleEntryPointAsync_WithMultipleEntryPoints_ShowsDialogAndReturnsSelected` | Gleicher Grund wie oben — Verify prüft aktuell `liste.Contains(ersteSolution) && liste.Contains(zweiteSolution)` (rohe Pfade). **Anpassung:** auf `"Visual Studio: Erste.sln"` / `"Visual Studio: Zweite.sln"` umstellen; ggf. `idePlugins`-Override, falls der zusätzliche VS-Code-Eintrag ausgeblendet werden soll. |
| `TaskDetailViewModelTests_IdeAuswahl.WaehleEntryPointAsync_UsesDisplayNameInDialog` | Nutzt einen `idePluginMock` mit `PluginName == "Test-IDE"` und Einstiegspunkten mit `DisplayName` „Erste Solution"/„Zweite Solution". Der Dialog-Mock gibt aktuell `zweiterEntryPoint.DisplayName` (roh, ohne Plugin-Präfix) zurück; nach `FormatiereAnzeigeWert` lautet der tatsächliche Anzeige-String aber `"Test-IDE: Zweite Solution"`. **Anpassung:** Mock-Rückgabewert und Verify-Listen-Prädikat auf `"Test-IDE: Erste Solution"` / `"Test-IDE: Zweite Solution"` umstellen. |
| `TaskDetailViewModelTests_IdeAuswahl.KannIdeAuswaehlen_WhenOpenEntryPointFailsWithMultipleEntryPoints_BleibtTrue` | Nutzt bereits `idePlugins`-Override auf einen einzelnen Mock (kein VS/VS Code) und ruft `OeffneIdeCommand` (Haupt-Button) auf. Verhalten bleibt inhaltlich unverändert, da Haupt-Button-Pfad unverändert bleibt und die aggregierte Ermittlung bei nur einem aktivierten Plugin dieselbe Anzahl liefert wie bisher — **keine Änderung erwartet, nur zur Verifikation nach der Umstellung erneut ausführen.** |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| 1 Einstiegspunkt: Haupt-Button öffnet direkt, Dropdown unsichtbar | `E2E_TaskDetailView_IdeAuswahl.cs` (neue Klasse) | „Bei 1 Einstiegspunkt ist der Haupt-Button sichtbar und öffnet direkt, der Dropdown-Button ist unsichtbar" |
| ≥2 Einstiegspunkte: Haupt-Button öffnet ersten direkt | `E2E_TaskDetailView_IdeAuswahl.cs` | „Bei mehreren Einstiegspunkten öffnet der Haupt-Button den ersten direkt" |
| ≥2 Einstiegspunkte: Dropdown-Button ist sichtbar und zeigt Dialog | `E2E_TaskDetailView_IdeAuswahl.cs` | „Bei mehreren Einstiegspunkten ist der Dropdown-Button sichtbar und zeigt einen Auswahldialog" |
| Dropdown-Dialog: Benutzer wählt Einstiegspunkt → IDE öffnet ihn | `E2E_TaskDetailView_IdeAuswahl.cs` | „Benutzer kann einen Einstiegspunkt aus dem Dropdown-Dialog wählen und die IDE öffnet ihn" |
| Dropdown-Dialog: Benutzer bricht ab → Nichts wird geöffnet | `E2E_TaskDetailView_IdeAuswahl.cs` | „Benutzer kann den Dialog abbrechen und es wird nichts geöffnet" |
| 0 Einstiegspunkte: Fehler wird angezeigt (bestehend, zu verifizieren) | `E2E_TaskDetailView_IdeAuswahl.cs` | „Bei 0 Einstiegspunkten wird eine Fehlermeldung angezeigt (Haupt- und Dropdown-Button sollten deaktiviert sein)" |
| **Neu:** Mehrere kompatible IDE-Plugins gleichzeitig aktiv (VS explizit + VS Code Fallback) → Dropdown zeigt beide Einträge; Auswahl des VS-Code-Eintrags öffnet VS Code, nicht VS | `E2E_TaskDetailView_IdeAuswahl.cs` | „Der Anwender kann im Dropdown gezielt zwischen einem explizit kompatiblen IDE-Plugin (Visual Studio) und einem Fallback-Plugin (Visual Studio Code) wählen" — deckt direkt die Anwenderentscheidung zu Offener Frage 1 ab |

**Bestehende E2E-Tests, die betroffen sind:**
- Falls E2E-Tests den IDE-öffnen-Button automatisieren (z. B. `E2E_TaskDetailView*.cs`, `E2E_*IdeOeffnen*.cs`), müssen sie aktualisiert werden um:
  - Den neuen `RibbonSplitButton` zu lokalisieren (statt `RibbonLargeButton`)
  - Ggf. die korrekte Schaltfläche (Haupt vs. Dropdown) zu wählen basierend auf Szenario
  - Dropdown-Sichtbarkeit zu prüfen basierend auf Einstiegspunkt-Anzahl
- E2E-Tests, die die exakte Anzahl oder den exakten Anzeige-Text der Dropdown-Auswahlliste prüfen, müssen auf das neue plugin-qualifizierte Anzeigeformat (`"{PluginName}: {…}"`) sowie auf die durch Aggregation potenziell zusätzlich vorhandenen Einträge (z. B. immer vorhandener VS-Code-Fallback-Eintrag, sofern aktiviert) angepasst werden.

## Offene Punkte

Keine. Die in der Anforderung genannten offenen Punkte (1–5) werden wie folgt adressiert:

1. **Dialog-Inhalt bei mehreren IDEs — GEKLÄRT in dieser Planungsrevision (vorherige Antwort revidiert):** Ursprünglich wurde entschieden, im Dropdown nur die Einstiegspunkte des einen priorisierten/aufgelösten Plugins (via `PluginSelectionService.ResolveIdePluginAsync`) anzuzeigen. **Der Anwender hat sich nachträglich explizit für die umfassendere Alternative entschieden** (Originalzitat): „es soll bei dieser auswahl nicht nur um die entrypoints innerhalb des einen ide-plugins gehen. sondern um alle kompatiblen ide-plugins. so soll es möglich sein, dass sowohl Visual Studio als explizites Plugin auch Visual Studio Code als Fallback aufgerufen werden kann." Der Dropdown zeigt daher jetzt die aggregierten Einstiegspunkte **aller** aktivierten, Explicit- oder Fallback-kompatiblen IDE-Plugins, plugin-qualifiziert und nach Priorität sortiert (siehe Designentscheidungen „Dialog-Inhalt (Iteration 2)" sowie neue Methode `PluginSelectionService.ResolveAlleKompatiblenIdePluginsAsync`). Der Haupt-Button bleibt davon unberührt und nutzt weiterhin ausschließlich `ResolveIdePluginAsync` (siehe Punkt 2).

2. **Haupt-Button Fallback-Logik:** Haupt-Button nutzt **kein** Callback und fallen auf den ersten Einstiegspunkt zurück (bestehend). Dies bleibt unverändert.

3. **Datei-Dialog vs. Struktur-Dialog:** Wiederverwendung von `ShowSolutionSelectionDialogAsync` mit Pfad-Strings (flach). Eine zukünftige hierarchische Variante kann als separate Methode `ShowIdeSelectionDialogAsync` hinzugefügt werden.

4. **Tastatur-Navigation:** Folgt bestehenden WPF-Ribbon-Mustern (Tab-Navigation zwischen Buttons, Enter zum Aktivieren). Kein spezielles `Alt+I`-Muster erforderlich, da Ribbon ohnehin über Tab navigierbar ist.

5. **Async-Ermittlung der Einstiegspunkte:** On-demand beim Dropdown-Klick (Schritt 7 in der Umsetzungsreihenfolge). `KannIdeAuswaehlen` wird asynchron berechnet und kann sich nach View-Load ändern — akzeptable UX-Verzögerung.
