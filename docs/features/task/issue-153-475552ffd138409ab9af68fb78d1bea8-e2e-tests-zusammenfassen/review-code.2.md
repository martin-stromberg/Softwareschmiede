# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### WpfTestBase.cs (WpfTestBase)

- **Toter Code / Speculative Generality** — Die Instanz-Überladung `ErsteOffeneAufgabeOeffnen(AutomationElement mainWindow)` (Zeile 722–726) wird nirgends aufgerufen. Alle vier Aufrufstellen (`E2E_AutoStartCli.cs:54`, `E2E_TaskDetailNavigation.cs:43` sowie die Doku-Verweise) benutzen ausschließlich die statische Array-Überladung `ErsteOffeneAufgabeOeffnen(AutomationElement[] items)`, weil sie die Item-Liste ohnehin vorher für ein `Assert.True(items.Length …)` benötigen. Die Instanz-Variante bleibt damit ungenutzt.

  Empfehlung: Die ungenutzte Überladung `ErsteOffeneAufgabeOeffnen(AutomationElement mainWindow)` entfernen. Sollte sie als bequeme "Öffnen ohne vorherige Item-Abfrage"-Variante beabsichtigt sein, stattdessen an mindestens einer bestehenden Aufrufstelle tatsächlich verwenden (dort, wo das `items`-Array nur zum Öffnen und nicht für ein eigenes Assert dient), damit sie nicht als toter Code verbleibt.

- **Doppelter Code / Fehlende Kapselung** — Das Idiom "Feld fokussieren, Inhalt per Strg+A markieren und neu tippen" ist identisch in `AufgabeTitelSetzen` (Zeile 681–684) und `ProjektNamenAendernUndSpeichern` (Zeile 762–765) ausgeschrieben (`Click()` + `Keyboard.TypeSimultaneously(CONTROL, KEY_A)` + `Keyboard.Type(...)`).

  Empfehlung: Das wiederholte Muster in eine private Hilfsmethode auslagern, z. B. `private static void FeldInhaltErsetzen(AutomationElement box, string text)`, und aus beiden Methoden aufrufen. Reduziert die Duplikation und hält die Tastenkombination an einer Stelle wartbar.

### ProjectDetailE2ETests.cs (ProjectDetailE2ETests)

- **Fehlerbehandlung / Fragilität (niedrig)** — In `ProjektBearbeiten_NamenAendernSpeichernZurueckUndErneutBearbeiten_E2E` wird die Referenz `nameBox` in Zeile 87 ermittelt, danach durchläuft `ProjektNamenAendernUndSpeichern` (Zeile 91) intern einen UpdateAsync-/LadenAsync-Zyklus (eigene, frische `ProjektName`-Abfrage). Das anschließende `nameBox.AsTextBox().Text` in Zeile 92 liest über die *vor* dem Speicher-/Reload-Zyklus gehaltene Referenz. Das funktioniert nur, weil WPF dasselbe TextBox-Control beibehält; es ist aber kein bewusst frisch nachgeladener Zustand und macht die Aussage fragiler als nötig.

  Empfehlung: Vor dem `Assert.Equal` das `ProjektName`-Element erneut per `WaitForElement(mainWindow, cf => cf.ByName("ProjektName"), Short)` abfragen und auf dieser frischen Referenz `AsTextBox().Text` prüfen, statt die alte `nameBox`-Referenz wiederzuverwenden.

## Geprüfte Dateien

- `src/Softwareschmiede.Tests/E2E/WpfTestBase.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_AutoStartCli.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_CreateNewTaskNavigation.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_PluginProjectDefault_NextTask.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_PluginSelectionDialog.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_TaskDetailNavigation.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_TaskWechselUeberMenue.cs`
- `src/Softwareschmiede.Tests/E2E/ProjectDetailE2ETests.cs`
