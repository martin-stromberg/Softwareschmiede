# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### TaskDetailViewModel.cs (TaskDetailViewModel)

- **Doppelter Code / Ineffizienz** — `OeffneIdeAsync` (Zeile 1806–1830) und `OeffneIdeAuswahlAsync` (Zeile 1839–1863) rufen jeweils zuerst `AktualisiereVerfuegbareEinstiegspunkteAsync(effectiveWorkdir, ct)` auf (Zeile 1817 bzw. 1850), die intern `_pluginSelectionService.ResolveIdePluginAsync(...)` und `plugin.FindEntryPointsAsync(...)` ausführt (Zeile 1897–1898), und rufen unmittelbar danach `_ideOeffnenService.OpenRepositoryInIdeAsync(...)` auf (Zeile 1819 bzw. 1852). `IdeOeffnenService.OpenRepositoryInIdeAsync` (unverändert, `src/Softwareschmiede/Application/Services/IdeOeffnenService.cs` Zeile 60–62) führt exakt dieselben zwei Schritte (`ResolveIdePluginAsync` + `FindEntryPointsAsync`) noch einmal aus. Bei jedem Klick auf Haupt- oder Dropdown-Button wird die Plugin-Auflösung und die (potenziell dateisystembasierte) Einstiegspunkt-Ermittlung also doppelt durchgeführt. Die Fehlerbehandlung ist dadurch zusätzlich inkonsistent: Der erste (redundante) Aufruf verschluckt Fehler in `AktualisiereVerfuegbareEinstiegspunkteAsync` und loggt nur eine Warnung (Zeile 1906–1911), während derselbe Fehler beim zweiten (im Service verborgenen) Aufruf ungefangen bis zum äußeren Catch durchschlägt und als `FehlerMeldung` angezeigt wird.

  Empfehlung: Plugin-Auflösung und Einstiegspunkt-Ermittlung nur einmal pro Klick ausführen — z. B. `AktualisiereVerfuegbareEinstiegspunkteAsync` so umbauen, dass sie das aufgelöste Plugin und die gefundenen Einstiegspunkte zurückgibt, und anschließend direkt `plugin.OpenEntryPointAsync(...)` (ggf. mit Dialog-Callback) statt erneut `IdeOeffnenService.OpenRepositoryInIdeAsync(...)` aufrufen. Alternativ `IdeOeffnenService` um eine Methode erweitern, die bereits ermittelte Einstiegspunkte entgegennimmt.

- **Doppelter Code** — `OeffneIdeAsync` (Zeile 1806–1830) und `OeffneIdeAuswahlAsync` (Zeile 1839–1863) sind bis auf den übergebenen Callback-Parameter und den Log-/Fehlertext nahezu identisch (Pfad-Guard, `ErmittleEffektivesArbeitsverzeichnisAsync`-Aufruf, `AktualisiereVerfuegbareEinstiegspunkteAsync`-Aufruf, Try/Catch-Struktur mit identischem `FehlerMeldung`-Text `"IDE konnte nicht geöffnet werden: {ex.Message}"`).

  Empfehlung: Gemeinsame private Hilfsmethode extrahieren, die den optionalen `waehleEntryPointAsync`-Callback als Parameter entgegennimmt, und beide öffentlichen Methoden darauf reduzieren.

- **Namenskonvention** — Die private Methode `waehleEntryPointAsync` (Zeile 1874) ist als einzige Methode der Klasse in camelCase benannt; alle übrigen privaten Methoden folgen durchgängig PascalCase (`LadenAsync`, `OeffneIdeAsync`, `OeffneIdeAuswahlAsync`, `AktualisiereVerfuegbareEinstiegspunkteAsync` usw.).

  Empfehlung: In `WaehleEntryPointAsync` umbenennen, inklusive der drei XML-Doc-Referenzen `<see cref="waehleEntryPointAsync"/>` (Zeile 422, 432, 1834) und der Verwendung als Delegate-Argument (Zeile 1852).

- **Speculative Generality / totes Datum** — Die öffentliche Property `VerfuegbareEinstiegspunkte` (Zeile 434–438) wird laut Doc-Kommentar für "Debugging/Logging" bereitgestellt, ist aber weder an ein XAML-Element gebunden noch wird sie an irgendeiner produktiven Stelle gelesen (Grep über `src/Softwareschmiede.App` findet außer Getter/Setter nur die Zuweisungen in `AktualisiereVerfuegbareEinstiegspunkteAsync` selbst). Einziger Konsument ist ein einzelner Unit-Test.

  Empfehlung: Entweder tatsächlich für Diagnose nutzen (z. B. strukturiertes Logging beim Aktualisieren) oder die Property entfernen, bis ein konkreter Verwendungszweck entsteht.

### RibbonSplitButton.xaml (RibbonSplitButton)

- **Doppelter Code** — Das komplette `Button.Style`/`ControlTemplate` (Hintergrund, Border, Hover-/Pressed-/Disabled-Trigger) des Haupt-Buttons (Zeile 16–47) ist praktisch identisch mit dem Style in `RibbonLargeButton.xaml` (Zeile 16–47) und wird innerhalb derselben Datei ein weiteres Mal für den Dropdown-Button wiederholt (Zeile 72–103) — einziger Unterschied jeweils der `CornerRadius`-Wert (`4`, `4,0,0,4`, `0,4,4,0`).

  Empfehlung: Gemeinsamen Style (z. B. `Style x:Key="RibbonButtonStyle"` in einer ResourceDictionary, mit `CornerRadius` als austauschbarem Parameter/Attached Property) extrahieren und von `RibbonLargeButton` sowie beiden Buttons in `RibbonSplitButton` referenzieren, statt den Style dreifach zu duplizieren.

### E2E_TaskDetailView_IdeAuswahl.cs (End2EndTest)

- **Testqualität — Zustands-Leck durch fehlenden Reset** — `IdeAuswahl_KeineEinstiegspunkteUndDropdownAbbruch_E2E` deaktiviert in Phase 1 (Zeile 43) das IDE-Plugin `Softwareschmiede.VisualStudioCode` global über die Einstellungen und speichert dies (Zeile 45–47), aktiviert es aber am Ende des Tests nicht wieder. Laut `MainTest.cs` (`RunConPtyTests`) teilen sich alle `..._E2E(mainWindow)`-Methoden denselben App-Lifecycle und damit denselben persistierten Einstellungszustand; das Plugin bleibt also für alle nach dieser Methode aufgerufenen Tests in derselben Run-Methode deaktiviert. Das etablierte Gegenstück im selben Feature, `E2E_IdePluginSettings.cs` (Zeile 76–81), deaktiviert das gleiche Plugin ebenfalls testweise, aktiviert es aber explizit wieder ("Visual Studio Code für Phase 2 wieder aktivieren [...] damit Phase 2 mit beiden Plugins aktiv startet") — genau um dieses Zustands-Leck zu vermeiden. Aktuell funktioniert die Reihenfolge in `MainTest.cs` nur zufällig, weil kein nachfolgender Test in der Liste (Zeile 49 ff.) von einem aktivierten Visual-Studio-Code-Plugin abhängt; das ist aber eine unsichtbare, nicht dokumentierte Abhängigkeit von der Aufrufreihenfolge.

  Empfehlung: Am Ende von `IdeAuswahl_KeineEinstiegspunkteUndDropdownAbbruch_E2E` das Plugin wieder aktivieren und speichern, analog zu `E2E_IdePluginSettings.cs` Zeile 76–81 (z. B. `AktiviereIdePlugin(mainWindow, "Softwareschmiede.VisualStudioCode")` gefolgt von Speichern-Klick), damit der Test unabhängig von seiner Position innerhalb von `RunConPtyTests` keinen globalen Zustand hinterlässt.

## Geprüfte Dateien

- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
- `src/Softwareschmiede.App/Views/TaskDetailView.xaml`
- `src/Softwareschmiede.App/Controls/RibbonSplitButton.xaml`
- `src/Softwareschmiede.App/Controls/RibbonSplitButton.xaml.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTestsBase.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_IdeAuswahl.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_VerzeichnisAktionen.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_TaskDetailView_IdeAuswahl.cs`
- `src/Softwareschmiede.Tests/E2E/MainTest.cs`
