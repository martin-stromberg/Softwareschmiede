# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### PluginSelectionService.cs (PluginSelectionService)

- **Doppelter Code** — `ResolveAlleKompatiblenIdePluginsAsync` (Zeilen 167–203) dupliziert nahezu wörtlich den Setup-Teil von `ResolveIdePluginAsync` (Zeilen 124–137): Validierung von `repositoryPath`, Laden von `GetEnabledIdePluginsAsync`, Leer-Check mit Rückgabe des Default-Plugins, Laden von `orderSetting` über `_appEinstellungService` und Aufruf von `ApplyIdePluginOrder`. Nur die anschließende Sammlung (erstes Explicit-Treffer-`return` vs. vollständige Explicit/Fallback-Listen) unterscheidet sich.

  Empfehlung: Den gemeinsamen Teil in eine private Hilfsmethode extrahieren, z. B. `private async Task<IReadOnlyList<IIdePlugin>> GetOrderedEnabledIdePluginsAsync(string repositoryPath, CancellationToken ct)`, die `ArgumentException.ThrowIfNullOrWhiteSpace`, das Laden/Sortieren übernimmt und bei leerer `enabledPlugins`-Liste eine leere Liste zurückgibt (Aufrufer entscheiden dann selbst, ob `GetDefaultIdePlugin()` als Ersatz verwendet wird). Beide öffentlichen Methoden rufen anschließend nur noch diese Hilfsmethode plus ihre jeweilige Auswahl-/Aggregationslogik auf.

### TaskDetailViewModel.cs (TaskDetailViewModel)

- **Doppelte/ineffiziente Ermittlung im Haupt-Button-Zweig von `OeffneIdeInternAsync`** — Zeilen 1927–1941: Für den Haupt-Button wird zuerst `ErmittleIdeEntryPointsAsync` aufgerufen (löst effektives Arbeitsverzeichnis, ein IDE-Plugin via `ResolveIdePluginAsync` und dessen `FindEntryPointsAsync` auf), anschließend zusätzlich `ErmittleAggregierteIdeEinstiegspunkteAsync` (löst das Arbeitsverzeichnis ein zweites Mal auf, prüft `CheckCompatibilityAsync` für **alle** aktivierten Plugins erneut — inkl. des bereits durch den ersten Aufruf aufgelösten Plugins — und ruft für jedes kompatible Plugin erneut `FindEntryPointsAsync` auf, darunter wieder für das bereits im ersten Schritt behandelte Plugin). Bei jedem Klick auf den Haupt-Button wird damit für das primäre Plugin die Kompatibilitätsprüfung und Einstiegspunkt-Ermittlung doppelt ausgeführt, und `ErmittleEffektivesArbeitsverzeichnisAsync` läuft zweimal statt einmal. Bei Plugins mit dateisystem-basierter Ermittlung (z. B. `.sln`-Suche, VS-Code-Locator) verdoppelt das unnötig die I/O-Last pro Öffnen-Versuch.

  Empfehlung: `effectiveWorkdir` einmal ermitteln und an beide Hilfsmethoden als Parameter übergeben, statt es intern erneut aufzulösen. Zusätzlich `ErmittleAggregierteIdeEinstiegspunkteAsync` so erweitern (oder eine Variante anbieten), dass sie ein bereits bekanntes `(Plugin, EntryPoints)`-Paar wiederverwenden kann, statt es für das im Haupt-Pfad bereits aufgelöste Plugin erneut über `CheckCompatibilityAsync`/`FindEntryPointsAsync` zu ermitteln — z. B. indem die Aggregation intern die Liste der kompatiblen Plugins berechnet, das im Haupt-Pfad bereits bekannte Plugin daraus per Prefix-Vergleich ausschließt und dessen bereits vorliegende `entryPoints` direkt in die aggregierte Liste einfügt.

- **Fehlerbehandlung: fehlende Isolierung einzelner Plugin-Fehler in der Aggregationsschleife** — `ErmittleAggregierteIdeEinstiegspunkteAsync` (Zeilen 1792–1805): Die `foreach`-Schleife ruft `plugin.FindEntryPointsAsync(...)` für jedes kompatible Plugin ungeschützt auf. Wirft ein einzelnes Plugin (z. B. weil ein externes Tool/Locator fehlschlägt) eine Exception, bricht die gesamte Aggregation ab — auch die Einstiegspunkte aller anderen, fehlerfrei funktionierenden Plugins gehen verloren, und `AktualisiereKannIdeAuswaehlenAsync` setzt `KannIdeAuswaehlen` komplett auf `false` bzw. `OeffneIdeInternAsync` zeigt eine `FehlerMeldung`, obwohl z. B. Visual Studio Code weiterhin hätte geöffnet werden können. Vor dieser Iteration war das unkritisch, da nur ein einziges Plugin aufgelöst wurde; mit der Aggregation über mehrere heterogene Plugin-Implementierungen vergrößert sich der Blast-Radius eines einzelnen fehlerhaften Plugins auf alle anderen.

  Empfehlung: In der Schleife jedes `plugin.FindEntryPointsAsync(...)` einzeln mit `try/catch` (außer `OperationCanceledException`, die weiterhin propagieren soll) absichern, den Fehler pro Plugin loggen (`_logger.LogWarning`) und mit den übrigen Plugins fortfahren, statt die gesamte Aggregation abzubrechen.

### TaskDetailViewModelTests_IdeAuswahl.cs (TaskDetailViewModelTests_IdeAuswahl)

- **Doppelter Code (bekannt, in dieser Iteration nicht behoben)** — `WaehleEntryPointAsync_UsesDisplayNameInDialog` (Zeilen 254–264) und `KannIdeAuswaehlen_WhenOpenEntryPointFailsWithMultipleEntryPoints_BleibtTrue` (Zeilen 299–309) bauen weiterhin denselben `Mock<IIdePlugin>` mit acht identischen Setup-Zeilen auf (`PluginName`, `PluginPrefix`, `PluginType`, `GetSettingGroups()`, `CheckCompatibilityAsync`, `FindEntryPointsAsync`) und unterscheiden sich nur im letzten Setup für `OpenEntryPointAsync` (`Returns(Task.CompletedTask)` vs. `ThrowsAsync(...)`). Dieser Duplikat-Block war bereits in `continue.md` (Zeile 15/46) als bekannter, geringfügiger Befund vermerkt, mit der expliziten Empfehlung, ihn "zusammen mit der als nächstes anstehenden ... Anforderungsänderung" (= genau diese Multi-Plugin-Aggregations-Iteration) zu beheben, da sie die Datei ohnehin grundlegend anfasst. In dieser Iteration wurden an beiden Blöcken nur die erwarteten Anzeige-/Rückgabewerte angepasst (Präfix `Test-IDE:` ergänzt), der duplizierte Aufbau selbst blieb unangetastet.

  Empfehlung: Private Hilfsmethode `CreateTestIdePluginMock(IReadOnlyList<IdeEntryPoint> entryPoints, string pluginName = "Test-IDE", string pluginPrefix = "Softwareschmiede.TestIde")` in der Testklasse ergänzen, die den gemeinsamen Setup-Teil kapselt und den fertig konfigurierten `Mock<IIdePlugin>` zurückgibt; das jeweils individuelle `OpenEntryPointAsync`-Setup bleibt Sache des einzelnen Testfalls.

### PluginSelectionServiceTests_IdePlugin.cs (PluginSelectionServiceTests_IdePlugin)

- **Namenskonventionen/Dokumentation** — Der Klassen-Doc-Kommentar (Zeile 12) lautet weiterhin `Tests für <see cref="PluginSelectionService.ResolveIdePluginAsync"/> (IDE-Plugin-Auflösung).`, obwohl die Klasse seit dieser Iteration zusätzlich 7 Tests für `ResolveAlleKompatiblenIdePluginsAsync` enthält (Zeilen 112–197).

  Empfehlung: Doc-Kommentar erweitern, z. B. `Tests für <see cref="PluginSelectionService.ResolveIdePluginAsync"/> und <see cref="PluginSelectionService.ResolveAlleKompatiblenIdePluginsAsync"/> (IDE-Plugin-Auflösung).`

## Geprüfte Dateien

- `src/Softwareschmiede/Application/Services/PluginSelectionService.cs`
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
- `src/Softwareschmiede.Tests/Application/Services/PluginSelectionServiceTests_IdePlugin.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_IdeAuswahl.cs`
