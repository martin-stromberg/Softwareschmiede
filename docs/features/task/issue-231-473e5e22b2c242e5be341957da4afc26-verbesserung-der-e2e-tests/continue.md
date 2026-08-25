# Offene Aufgaben

Erstellt am: 2026-08-25
Abbruchgrund: Maximale Iterationsanzahl erreicht (3 von 3)

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine (Plan-Review-Status: „Vollständig umgesetzt").

## Code-Review-Befunde

- [ ] **Namenskonventionen** (`E2E_ViewPattern.cs`): Alle 9 neuen `_E2E`-Szenariomethoden sind englisch benannt, während alle übrigen `_E2E`-Methoden in derselben `RunGeneralTests`/`RunConPtyTests`-Sequenz dem deutschen Muster `Handlung_ErwartetesErgebnis_E2E` folgen. Umbenennen gemäß Empfehlung in `review-code.md`.
- [ ] **Doppelter Code** (`ProjectListView`/`ProjectDetailView`/`TaskDetailView` vs. `WpfTestBase`): Mehrere UI-Interaktionssequenzen (`CreateProject`/`OpenProject`, `DeleteProject`/`DeleteCurrentProject`, `DeleteTask`/`DeleteCurrentTask`, `GoBack`/`AufgabeDetailZurueck`, `GetTaskElements`/`OffeneAufgabenItems`) existieren doppelt in `WpfTestBase` und der View-Pattern-Schicht und werden im selben Testlauf parallel genutzt. Empfehlung: `WpfTestBase`-Methoden so umbauen, dass sie intern auf die entsprechenden `View`-Klassen delegieren (Komposition), statt die Klick-Sequenz zweimal zu pflegen.
- [ ] **Dokumentationskonvention** (`ElementWaitHelper.cs`): `<returns>`-Tags an den statischen Feldern `Short`/`Medium` entfernen (nur `<summary>` verwenden, analog zu `WpfTestBase.Short`/`Medium`/`Long`).

## Fehlgeschlagene Tests

- [ ] `Softwareschmiede.Tests.E2E.End2EndTest.RunGeneralTests` — **kritischer Funktionsfehler, nicht nur Codequalität.** Schlägt reproduzierbar (2x unabhängig bestätigt) im Teilszenario `RunViewPatternHappyPath_E2E` fehl: `WindowExtensions.CurrentView()` erkennt nach `Menu.NavigateToProjects()` fälschlich weiterhin `TaskDetailView` statt der tatsächlich sichtbaren `ProjectListView`.
  Root-Cause-Hypothese (plausibel, nicht abschließend verifiziert): Nach dem Verlassen einer zuvor geöffneten, fensterumfassenden `TaskDetailView` verbleiben deren Marker-Elemente ("EditTitel"/"Zurück") im FlaUI-Automation-Baum, ohne dass `IsOffscreen` (genutzt in `BaseWindowView`/`ElementWaitHelper` zur Sichtbarkeitsfilterung) dies erkennt — vermutlich, weil die Elemente durch reine Z-Order-Überdeckung statt durch Clipping/Entfernen aus dem Baum verdeckt werden.
  **Empfehlung für die nächste Iteration:** Gezielt reproduzieren (App manuell/interaktiv starten, `RunGeneralTests` bis zu diesem Punkt laufen lassen, danach den Automation-Baum inspizieren, um zu bestätigen, ob "EditTitel" tatsächlich noch vorhanden und laut UIA "on screen" ist). Mögliche Fixrichtungen: (a) `IsOnScreen`/`ElementExists` um eine zusätzliche Prüfung ergänzen, die die tatsächliche Bounding-Rectangle-Überschneidung mit dem Fenster-Client-Bereich statt nur `IsOffscreen` berücksichtigt, oder (b) in `CurrentView()` die Prüfreihenfolge/Marker robuster gegen verwaiste Elemente machen (z. B. zusätzliches, eindeutigeres Ausschlusskriterium für `TaskDetailView`). **Dieser Fund entstand während der Bearbeitung der Iteration-3-Befunde und liegt außerhalb von deren zugewiesenem Scope — er wurde bewusst nicht spekulativ behoben, da eine Korrektur ohne verlässliche lokale Reproduktion (siehe unten) nicht sicher verifizierbar war.**

## Hinweis zur Testumgebung (kein Code-Befund, aber relevant für die nächste Iteration)

`%APPDATA%\AutonomAufgaben` enthält inzwischen über 340 verwaiste Verzeichnisse aus wiederholten
Sandbox-Testläufen (Altlast, nicht durch diesen Branch verursacht, wächst mit jedem weiteren Lauf).
Dies verlangsamt/blockiert `AutonomAufgabeInitialisierung_..._E2E` (Timeout nach 30s) und verhindert
in dieser Sandbox eine zuverlässige eigene Reproduktion des oben genannten `CurrentView()`-Bugs, da
`RunGeneralTests` oft schon vorher abbricht. Der Bug selbst betrifft aber nachweislich (2x reproduziert
vom Iteration-3-Agenten) einen anderen, unveränderten Teil der Testkette. Vor der nächsten Iteration
sollte der Anwender entscheiden, ob `%APPDATA%\AutonomAufgaben` bereinigt werden darf (nicht ohne
Rückfrage gelöscht, da dies laut CLAUDE.md potenziell produktive Daten der Self-Hosting-Instanz
enthalten könnte).
