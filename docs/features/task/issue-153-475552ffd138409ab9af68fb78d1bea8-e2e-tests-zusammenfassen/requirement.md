# Übersetzte Anforderung: E2E-Test-Struktur zur Reduktion von Testlaufzeiten optimieren

## Fachliche Zusammenfassung

Die bestehende E2E-Test-Struktur besteht aus isolierten, einzelnen Testmethoden für jedes Szenario (z. B. separate Tests für Aufgabenerstellung, Aufgabenbearbeitung, Aufgabenlöschung). Dies führt zu unnötig langen Gesamtlaufzeiten und Prozess-Interrupts, die die Entwicklungsarbeit stören. Die Anforderung ist, logisch zusammenhängende E2E-Szenarien in Testmethoden zusammenzufassen, ohne dabei auf Wartbarkeit und Lesbarkeit zu verzichten. Dazu werden UI-Interaktionen in wiederverwendbare Hilfsmethoden ausgelagert (Page-Object- oder Test-Helper-Pattern), sodass die Testmethoden selbst nur noch eine Sammlung von strukturierten Methodenaufrufen darstellen — etwa: `CreateTask()` → `EditTaskTitle()` → `DeleteTask()` in einem einzigen Testdurchgang statt drei separaten.

## Betroffene Klassen und Komponenten

- **Erweiterte Klasse:** `WpfTestBase` (bestehend, `src/Softwareschmiede.Tests/E2E/WpfTestBase.cs`) — erhält neue geschützte Hilfsmethoden für häufig wiederholte UI-Interaktionen (z. B. `CreateNewTask()`, `EditTaskTitle()`, `DeleteTask()`, `SelectPluginInDialog()`, `StartTaskExecution()`, etc.)
- **Neue Testhelfer-Klassen (optional):** Je nach Umfang können spezialisierte Helfer-Klassen entstehen (z. B. `TaskDetailPageHelper`, `ProjectDetailPageHelper`), die komplexe UI-Navigationsmuster und -Interaktionen kapseln. Diese können als innere Klassen in `WpfTestBase` oder als separate Dateien organisiert sein.
- **Betroffene E2E-Test-Klassen:** Alle Testklassen unter `src/Softwareschmiede.Tests/E2E/E2E_*.cs` (mindestens `E2E_CreateNewTaskNavigation.cs`, `E2E_AufgabeStarten.cs`, `E2E_TaskDetailNavigation.cs` und weitere aufgabenbezogene Tests) — werden refaktoriert, um Hilfsmethoden statt inliner UI-Code zu nutzen; Tests werden konsolidiert (mehrere Szenarien pro Testmethode).
- **Testinfrastruktur (bestehend, unverändert):** `FlaUI` (UI-Automatisierung), `WaitForElement()`, Keyboard/Mouse-Events — werden durch Hilfsshichten abstrahiert, bleiben aber funktional unverändert.

## Implementierungsansatz

1. **Hilfsmethoden in `WpfTestBase` oder dedizierten Helfern:**
   - Strukturierte Methoden für häufige Aktionen wie `NavigateToProjectDetail(string projectName)`, `CreateNewTask(string taskTitle)`, `EditTaskTitle(string newTitle)`, `ClickButtonByName(string buttonName)`, `WaitForAndClickElement(...)`.
   - Jede Hilfsmethode encapsuliert die einzelnen FlaUI-Aufrufe (`WaitForElement`, `Click()`, `Keyboard.Type()`, etc.) und gibt nur das Ergebnis oder die neu verfügbare AutomationElement zurück, falls nötig.
   - Methodennamen folgen fachlicher Semantik („was tut der Anwender?"), nicht technischer Semantik („welche UI-Elemente werden angeclickt?").

2. **Test-Konsolidierung:**
   - Statt drei Tests (Anlage, Bearbeitung, Löschung) ein durchgehender Test: `TaskCrudOperations_AnlageBearbeitungLoeschung_E2E()` startet die App einmal, führt alle drei Szenarien sequenziell aus und prüft die Zustandsübergänge.
   - Jede Testmethode verbleibt eine `IDisposable`/`[Fact]`-gekennzeichnete Methode; `WpfTestBase.LaunchApp()` wird am Anfang aufgerufen, `Dispose()` beim Ende durch das Test-Framework.
   - Fehlerbehandlung und Assertions bleiben granular (z. B. Assert nach jeder Aktion), sodass Fehlerquelle erkennbar bleibt.

3. **Wiederverwendbarkeit:**
   - Hilfsmethoden sind `protected` (für Erbung durch Subklassen) oder `internal` (falls separate Helfer-Klassen existieren).
   - Jede Hilfsmethode sollte idempotent oder klar in ihren Voraussetzungen sein (z. B. „erwartet, dass die Aufgabenliste sichtbar ist").
   - Dokumentation (XML-Kommentare) beschreibt Aufrufkontext und Voraussetzungen.

4. **Keine Architekturveränderung:**
   - `FlaUI`, `Automation`, `WaitForElement()` bleiben die Basis; Hilfsmethoden sind nur eine „Facade"-Schicht.
   - Keine Änderungen an Produktivcode oder Datenmodellen.
   - Alle 14 E2E-Testklassen in `E2E_*.cs` können parallel (unter `[Collection("E2E")]` Isolation) weiterlaufen, ohne gegenseitige Abhängigkeiten.

## Konfiguration

Keine neue Konfiguration erforderlich. Das Refactoring ist rein infrastruktureller Natur und ändert das Testverhalten nicht — weder in Erfolgs- noch in Fehlerfällen.

## Offene Fragen

- **Konsolidierungsumfang:** Welche Tests sollen konkret zusammengefasst werden? (z. B. alle Aufgaben-CRUD-Tests in `E2E_CreateNewTaskNavigation`, `E2E_TaskDetailNavigation` zusammenziehen?) Oder nur jene, die zeitlich lange brauchen?
- **Hilfsmethoden-Granularität:** Sollen Hilfsmethoden sehr feinkörnig sein (z. B. `ClickElement(buttonName)`) oder bereits Workflows kapseln (z. B. `CreateTaskAndNavigateToDetails(title)`, das Klick + Tastatureingabe + Speicherung zusammenfasst)?
- **Test-Helfer-Organisation:** Reicht die Erweiterung von `WpfTestBase` oder sollten separate Helfer-Klassen wie `TaskPageHelper` entstehen, um Komplexität zu reduzieren?
- **Fehlerbehandlung in Helfern:** Wie aggressiv sollen Hilfsmethoden bei Timeouts oder Elementen-nicht-gefunden reagieren? (z. B. Exception werfen oder ein Fehler-Rückgabeobjekt liefern?)
- **Bestehende Test-Methoden:** Sollen bestehende Einzel-Tests vollständig gelöscht oder als Rauch-Tests erhalten bleiben (z. B. nur Anlage prüfen, Bearbeitung + Löschung kombiniert)?
