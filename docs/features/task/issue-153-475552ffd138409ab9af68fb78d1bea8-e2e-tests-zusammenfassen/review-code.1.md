# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### E2E_TaskWechselUeberMenue.cs (E2E_TaskWechselUeberMenue)

- **Toter Code** — Zeile 2: `using System.Runtime.ConstrainedExecution;` wird nirgends verwendet (kein Typ aus diesem Namespace – `ReliabilityContract`, `CriticalFinalizerObject` o. Ä. – kommt in der Datei vor; verifiziert per Suche: einzige Fundstelle ist die `using`-Zeile selbst). Der Branch räumt in genau dieser Datei die `using`-Direktiven auf (entfernt `FlaUI.Core.Definitions` und `FlaUI.Core.Input`, die nach der Refaktorierung nicht mehr gebraucht werden), lässt diese tote Direktive aber stehen.

  Empfehlung: Die Zeile `using System.Runtime.ConstrainedExecution;` ersatzlos entfernen.

- **Doppelter Code** — Zeilen 64–66 und 74–76: Beide Inline-Blöcke
  ```
  var zurueckNachX = WaitForElement(mainWindow, cf => cf.ByName("Zurück"), Short);
  zurueckNachX.AsButton().Click();
  WaitForElement(mainWindow, cf => cf.ByName("ProjektName"), Medium);
  ```
  sind Anweisung für Anweisung identisch mit der in diesem Branch neu eingeführten Hilfsmethode `WpfTestBase.AufgabeDetailZurueck` (WpfTestBase.cs, Zeilen 703–709). Die Klasse wurde ansonsten konsequent auf die neuen Basishelfer umgestellt (`ErstelleUndStarteAufgabe`, `AufgabeAusListeOeffnen`), diese zwei Rücknavigationen jedoch nicht – dadurch bleibt genau das Inline-Muster bestehen, dessen Zentralisierung Kern der Anforderung ist.

  Empfehlung: Beide Blöcke durch `AufgabeDetailZurueck(mainWindow);` ersetzen.

### ProjectDetailE2ETests.cs (ProjectDetailE2ETests)

- **Doppelter Code** — Zeilen 43–47, 55–57 und 63–65 in `ProjektNavigation_NeuanlageAbbrechenUndOeffnenUndSchliessen_E2E`: Das Muster „`Zurück`-Button holen + klicken, danach `WaitUntilGone(mainWindow, cf => cf.ByName("Speichern"), Short)`" wiederholt sich dreimal innerhalb derselben Methode (die Projektdetail-Rücknavigation wartet auf das Verschwinden von „Speichern", nicht auf „ProjektName", ist daher nicht durch `AufgabeDetailZurueck` abgedeckt).

  Empfehlung: Das Muster in einen kleinen (klasseninternen oder in `WpfTestBase` liegenden) Helfer `ZurueckZurProjektuebersicht(mainWindow)` auslagern und an den drei Stellen aufrufen.

### WpfTestBase.cs (WpfTestBase) / Aufrufstellen in E2E_TaskDetailNavigation.cs, E2E_AutoStartCli.cs

- **Effizienz / redundante UI-Abfrage** (niedrige Priorität) — `ErsteOffeneAufgabeOeffnen` (WpfTestBase.cs Zeilen 722–726) ruft intern erneut `OffeneAufgabenItems(mainWindow)` auf. An den Aufrufstellen (E2E_TaskDetailNavigation.cs Zeilen 41–43, E2E_AutoStartCli.cs Zeilen 52–54) wird `OffeneAufgabenItems` unmittelbar davor bereits für die `Assert.True(items.Length >= 1, …)`-Prüfung aufgerufen, sodass die (jeweils reale UI-Automation-Abfrage `FindAllChildren`) doppelt gegen dieselbe Liste läuft. Neben dem Mehraufwand entsteht eine kleine Race-Möglichkeit (die Liste könnte sich zwischen beiden Abfragen ändern).

  Empfehlung: Entweder `ErsteOffeneAufgabeOeffnen` die bereits ermittelten `items` als Parameter übergeben lassen, oder das Assert direkt auf dem Rückgabewert einer einzelnen `OffeneAufgabenItems`-Abfrage durchführen und danach `items[0].DoubleClick()` verwenden.

## Geprüfte Dateien

- `src/Softwareschmiede.Tests/E2E/WpfTestBase.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_CreateNewTaskNavigation.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_TaskDetailNavigation.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_PluginSelectionDialog.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_TaskWechselUeberMenue.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_PluginProjectDefault_NextTask.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_AutoStartCli.cs`
- `src/Softwareschmiede.Tests/E2E/ProjectDetailE2ETests.cs`
