# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### src/Softwareschmiede/Application/Services/ProjektleiterAgentService.cs (ProjektleiterAgentService)

- **Inkonsistente Nutzung des neuen `GitArbeitsbereich`-Wrappers innerhalb derselben Methode** — `SteuereUnteragentAsync` liest in Zeile 87 (`_cliRunner.RunAsync("git", ["branch", unteragent.Branch], ...)`), Zeile 90 (Fehlermeldung) und Zeile 112 (Log-Statement) weiterhin `unteragent.Branch` direkt, während Zeile 96–97 für denselben logischen Wert (und im selben Methodenkörper, nur wenige Zeilen später) `unteragent.GitArbeitsbereich.BranchName` / `.ClonePfad` verwendet. `ValidiereUnteragent` (Zeilen 193 und 203) validiert ebenfalls weiterhin über `unteragent.Branch` / `unteragent.ClonePfad` statt über den Wrapper. Das Value Object wurde also nur an einem von fünf Zugriffspunkten auf dieselben Daten in derselben Datei eingesetzt — wirkt wie ein unvollständiger Refactor, nicht wie eine bewusste Design-Entscheidung, und begünstigt Verwirrung/Doppelpflege, falls künftig einmal an nur einer der beiden Zugriffsarten etwas geändert wird.

  Empfehlung: In dieser Datei einheitlich auf einen Zugriffsstil festlegen — entweder durchgängig `unteragent.GitArbeitsbereich.BranchName`/`.ClonePfad` verwenden (Zeilen 87, 90, 112, 193, 203 anpassen) oder die Umstellung in Zeilen 96–97 zurücknehmen und dort ebenfalls `unteragent.Branch`/`unteragent.ClonePfad` direkt lesen, bis die ganze Datei konsequent migriert wird.

### src/Softwareschmiede/Domain/Entities/Aufgabe.cs (Aufgabe)

- **Getter von `GitArbeitsbereich` verschleiert Teilzustände statt sie sichtbar zu machen** — Die Null-Prüfung im Getter (`BranchName is null && LokalerKlonPfad is null ? null : new GitArbeitsbereich(...)`) liefert nur dann `null`, wenn *beide* Felder unbesetzt sind. Ist nur eines der beiden Felder `null` (das andere gesetzt), wird trotzdem ein nicht-`null`-`GitArbeitsbereich` zurückgegeben, bei dem das fehlende Feld still zu `string.Empty` wird — die eigentliche Divergenz (nur Branch oder nur Klonpfad gesetzt) geht dabei verloren. Dieser Teilzustand ist in der Praxis nicht hypothetisch: `GitOrchestrationServiceTests.cs:168-169` setzt genau diesen Zustand (`LokalerKlonPfad` gesetzt, `BranchName = null`) für dieselbe Entität. Der Doc-Kommentar „Null, solange kein Branch/Klon-Pfad gesetzt ist" ist mehrdeutig und deckt dieses Verhalten nicht eindeutig ab. Aktuell wird der Getter in Produktionscode nirgends gelesen (nur der Setter, in `AufgabeService.cs`), das Risiko ist also latent, aber real, sobald ein künftiger Aufrufer `aufgabe.GitArbeitsbereich` liest und `null` als Signal für „kein vollständiger Arbeitsbereich" erwartet.

  Empfehlung: Getter-Bedingung auf `BranchName is null || LokalerKlonPfad is null` ändern, sodass jeder unvollständige Zustand weiterhin `null` liefert und nicht in eine scheinbar gültige VO-Instanz mit leeren Strings umgewandelt wird (analog zur Absicht des Doc-Kommentars).

### Testabdeckung der neuen `[NotMapped]`-Wrapper-Properties

- **Kein einziger Unit-Test prüft Getter/Setter der drei neuen Wrapper-Properties direkt** — Weder `Aufgabe.GitArbeitsbereich` noch `UnteragentSpezifikation.GitArbeitsbereich` noch `AutonomAufgabeKonfiguration.RessourcenLimits` werden in `src/Softwareschmiede.Tests` direkt auf einer Entity-Instanz gelesen oder gesetzt (Suche nach `.GitArbeitsbereich` bzw. `.RessourcenLimits` auf Entity-Objekten liefert keine Treffer). Getestet wird nur `AutonomAufgabeInitialisierungsAnfrage.RessourcenLimits` — das ist aber ein gewöhnliches Record-Property ohne eigene Getter/Setter-Logik, kein Wrapper um flach gemappte Felder. Damit ist die für diesen PR zentrale Eigenschaft — dass Wrapper-Getter/-Setter tatsächlich synchron mit den zugrunde liegenden EF-Feldern bleiben (keine versteckte Divergenz) — durch keinen Test abgesichert; ein Fehler wie der oben beschriebene Getter-Bug in `Aufgabe.cs` wäre durch die bestehende Testsuite nicht aufgefallen.

  Empfehlung: Für jede der drei Entities einen kleinen Roundtrip-Test ergänzen: VO setzen → prüfen, dass die flachen Felder den erwarteten Wert haben; flache Felder setzen → prüfen, dass der VO-Getter den erwarteten Wert (bzw. bei `Aufgabe.GitArbeitsbereich` `null` im Teilzustand) liefert.

## Geprüfte Dateien

- `src/Softwareschmiede.App/ViewModels/AutonomAufgabeInitialisierungsDialogViewModel.cs`
- `src/Softwareschmiede.Tests/Application/Services/AutonomAufgabenInitialisierungsServiceTests.cs`
- `src/Softwareschmiede/Application/Services/AufgabeService.cs`
- `src/Softwareschmiede/Application/Services/AutonomAufgabenInitialisierungsService.cs`
- `src/Softwareschmiede/Application/Services/ProjektleiterAgentService.cs`
- `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`
- `src/Softwareschmiede/Domain/Entities/AutonomAufgabeKonfiguration.cs`
- `src/Softwareschmiede/Domain/Entities/UnteragentSpezifikation.cs`
- `src/Softwareschmiede/Domain/ValueObjects/AutonomAufgabeInitialisierungsAnfrage.cs`
- `src/Softwareschmiede/Domain/ValueObjects/GitArbeitsbereich.cs` (neu)
- `src/Softwareschmiede/Domain/ValueObjects/RessourcenLimits.cs` (neu)

## Ergänzende Prüfpunkte (aus Auftrag)

- **`[NotMapped]`-Wrapper Getter/Setter korrekt?** Für `AutonomAufgabeKonfiguration.RessourcenLimits` und `UnteragentSpezifikation.GitArbeitsbereich` ja, vollständig korrekt (Setter schreibt beide Felder, Getter liest beide Felder, keine Divergenz möglich). Für `Aufgabe.GitArbeitsbereich` liegt der oben beschriebene Getter-Befund vor (Teilzustand wird verschluckt statt als `null` propagiert); Setter ist korrekt.
- **Keine Migration/Model-Snapshot-Änderung ausgelöst?** Bestätigt — `git status` zeigt keinerlei Änderungen unter `src/Softwareschmiede/Migrations/`, und `SoftwareschmiededDbContext.cs` konfiguriert weiterhin ausschließlich die flachen Spalten (`Branch`, `ClonePfad`, `TokenBudget` etc.), nicht die neuen `[NotMapped]`-Properties. Reiner `[NotMapped]`-Ansatz wie erwartet ohne Schemaauswirkung.
- **Ausschluss von `AutonomAufgabeKonfiguration` beim `GitArbeitsbereich`-VO nachvollziehbar?** Ja — `AutonomAufgabeKonfiguration.ArbeitsverzeichnisPfad` bleibt ein eigenständiges, nicht in ein `GitArbeitsbereich` verpacktes String-Feld; die Entity hat keinen `Branch`/`ClonePfad`-Feldpaar, sondern nur das Wurzel-Arbeitsverzeichnis, das fachlich klar kein Klon-Pfad ist. Kein Befund.
- **Reduktion von `AutonomAufgabeInitialisierungsAnfrage` von 9 auf 7 Parameter korrekt, bricht nichts?** Ja — es gibt im gesamten Repository nur einen einzigen Produktionscode-Aufrufer des Konstruktors (`AutonomAufgabeInitialisierungsDialogViewModel.cs:385`), der korrekt auf `RessourcenLimits: new RessourcenLimits(...)` umgestellt wurde. Alle Testaufrufer (`AutonomAufgabenInitialisierungsServiceTests.cs`) wurden ebenfalls konsistent angepasst, inklusive der beiden `with`-Ausdrücke, die jetzt korrekt verschachtelt über `basisAnfrage.RessourcenLimits with { ... }` einzelne Werte überschreiben. Kein Befund.
