# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### WpfTestBase.cs (WpfTestBase)

- **Doppelter Code** — `SetupProjectMitNeuerAufgabe` (Zeilen 549–553) enthält denselben Inline-FlaUI-Block, der jetzt in der neuen `protected`-Methode `NeueAufgabeAnlegen` (Zeilen 667–673) gekapselt ist: `AufgabeNeu`-Button suchen + klicken und anschließend auf `EditTitel` warten. Der Kern der Anforderung (Zentralisierung dieses Inline-Codes) wurde für diese Basisklassen-Methode nicht umgesetzt.

  Empfehlung: In `SetupProjectMitNeuerAufgabe` die drei Zeilen (549–552) durch `NeueAufgabeAnlegen(mainWindow);` ersetzen und danach weiterhin `return mainWindow;`.

- **Verhalten/Kommentar-Mismatch (irreführende Synchronisation)** — `ProjektNamenAendernUndSpeichern` (Zeilen 748–757) klickt `Speichern` und wartet danach mit `WaitForElement(... "Speichern", Short)` (Zeile 756) auf ein „Wiedererscheinen" des Buttons. Im UpdateAsync-Pfad bleibt die Detailansicht jedoch geöffnet, der `Speichern`-Button verschwindet nicht — die Wartezeile findet ihn folglich sofort und synchronisiert nicht auf den Abschluss des Speichervorgangs. Die XML-Doku („wartet … auf das Wiedererscheinen von 'Speichern' (Ladevorgang abgeschlossen)") beschreibt damit ein Verhalten, das die Implementierung nicht liefert; nachfolgende Text-Asserts (siehe `ProjectDetailE2ETests`, Zeilen 92–93) können vor Abschluss des UpdateAsync laufen (Race).

  Empfehlung: Auf ein Element warten, das den Abschluss des Speicherns tatsächlich signalisiert (z. B. kurzzeitiges Verschwinden/Deaktivieren des Buttons via `WaitUntilGone` gefolgt von `WaitForElement`, oder ein dediziertes Statuselement), oder die XML-Doku an das reale Verhalten anpassen und die fehlende Synchronisation dokumentieren.

- **Inkonsistente Helper-API / fehlende Vorbedingungsvalidierung** — `ErsteOffeneAufgabeOeffnen(AutomationElement[] items)` (Zeilen 723–726) verlangt vom Aufrufer, die Items vorher selbst über `OffeneAufgabenItems` zu holen, während das analoge `AufgabeAusListeOeffnen(mainWindow, titel)` (Zeilen 732–740) das Element selbst ermittelt. Die zwei fachlich verwandten Öffnen-Helfer arbeiten damit auf unterschiedlichen Abstraktionsebenen. Zudem greift die Methode ohne Prüfung auf `items[0]` zu; bei leerem Array entsteht eine kontextlose `IndexOutOfRangeException` statt einer aussagekräftigen Meldung (die dokumentierte Vorbedingung wird nicht fail-fast erzwungen).

  Empfehlung: Entweder eine `mainWindow`-basierte Überladung anbieten, die intern `OffeneAufgabenItems` aufruft (konsistent zu `AufgabeAusListeOeffnen` und zum Plan), oder zumindest am Methodenanfang `if (items.Length == 0) throw new InvalidOperationException(...)` ergänzen.

### E2E_PluginSelectionDialog.cs (E2E_PluginSelectionDialog)

- **Doppelter Code** — Die „Phase OK" (Zeilen 56–64) implementiert exakt den Ablauf der bereits vorhandenen Basis-Hilfsmethode `StartenUndPluginWaehlen` (WpfTestBase.cs:578–589) inline nach: `Starten` klicken → `WaitForWindow("KI-Plugin auswählen")` → `PluginAuswahl`-ComboBox → `SelectComboBoxItemByClick("Softwareschmiede.KiSimulator")` → `OK` klicken. Der Plan nennt `StartenUndPluginWaehlen` ausdrücklich als vorhandenen Helfer.

  Empfehlung: Zeilen 56–64 durch `StartenUndPluginWaehlen(mainWindow, "Softwareschmiede.KiSimulator");` ersetzen. (Die „Phase Abbrechen" muss inline bleiben, da sie den Dialog per `Abbrechen` schließt.)

### E2E_TaskWechselUeberMenue.cs (E2E_TaskWechselUeberMenue)

- **Irreführende Kommentare (Copy-Paste-Fehler)** — Im Block „zu Aufgabe A wechseln" (Zeilen 94–108) beschreiben zwei Kommentare noch Aufgabe B, obwohl der Code auf Aufgabe A navigiert und auf `pidA`/`TitelA` prüft: Zeile 99 „// Die eingebettete CLI muss jetzt tatsächlich zu Aufgabe B gehören (nicht mehr zu Aufgabe A)." (Assert prüft `Assert.Equal(pidA, pidNachWechsel)`), und Zeile 104 „// … Das Info-Panel zeigt den Titel von Aufgabe B." (geprüft wird `titelAImInfoPanel` / `TitelA`). Die Kommentare widersprechen dem tatsächlichen Prüfziel.

  Empfehlung: Kommentare in Zeile 99 und 104 auf Aufgabe A korrigieren (analog zum B-Block in Zeilen 83/88).

### Mehrere Testklassen (Testqualität)

- **Immer-wahre Assertions** — `WaitForElement` (WpfTestBase.cs:211) gibt einen non-nullable `AutomationElement` zurück und wirft bei Nichtfinden eine Exception; es kann nie `null` liefern. Die Muster `Assert.NotNull(WaitForElement(...))` bzw. `Assert.NotNull(<Ergebnis von WaitForElement>)` können daher niemals fehlschlagen und erzeugen eine falsche Abdeckungsaussage. Betroffene neue/geänderte Stellen u. a.: `E2E_CreateNewTaskNavigation.cs` Zeilen 37, 41; `E2E_TaskDetailNavigation.cs` Zeilen 38, 47; `E2E_PluginSelectionDialog.cs` Zeilen 49, 68; `E2E_PluginProjectDefault_NextTask.cs` Zeile 68; `E2E_AutoStartCli.cs` Zeilen 47, 58; `ProjectDetailE2ETests.cs` Zeilen 48, 83, 88, 243, 251, 262.

  Empfehlung: Diese redundanten `Assert.NotNull`-Zeilen entfernen (das erfolgreiche `WaitForElement` ist bereits die Zusicherung, dass das Element erschienen ist) oder durch eine fachlich aussagekräftige Prüfung ersetzen (z. B. Text-/Zustandsvergleich wie in `E2E_TaskDetailNavigation.cs:33`).

### ProjectDetailE2ETests.cs (ProjectDetailE2ETests)

- **Fehlende Wiederverwendung** — Der async-Test `Projektdetailansicht_TrenntOffeneUndBeendeteAufgaben_E2E` ermittelt die offenen Aufgaben inline (Zeilen 154–157: `WaitForElement("OffeneAufgabenListe")` + `FindAllChildren(ListItem)`), obwohl dafür jetzt der neue Basishelfer `OffeneAufgabenItems(mainWindow)` (WpfTestBase.cs:710–714) existiert, der genau dieselbe Logik kapselt.

  Empfehlung: Zeilen 154–157 auf `var offeneItems = OffeneAufgabenItems(mainWindow);` umstellen (die separate `Assert.NotNull`-Zeile auf die Liste entfällt gemäß Befund „Immer-wahre Assertions").

## Geprüfte Dateien

- `src/Softwareschmiede.Tests/E2E/WpfTestBase.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_CreateNewTaskNavigation.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_TaskDetailNavigation.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_PluginSelectionDialog.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_TaskWechselUeberMenue.cs`
- `src/Softwareschmiede.Tests/E2E/ProjectDetailE2ETests.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_PluginProjectDefault_NextTask.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_AutoStartCli.cs`
