# Übersetzte Anforderung: ConPTY-E2E-Tests von echtem Kindprozess entkoppeln

## Fachliche Zusammenfassung

14 E2E-Tests, die einen CLI-Prozess über ConPTY starten (`WpfTestBase.StartenUndPluginWaehlen` → `KiAusfuehrungsService.StartWithPseudoConsoleAsync`), scheitern reproduzierbar unter jeder `dotnet test`/`vstest.console.exe`-Ausführung, weil der gestartete ConPTY-Kindprozess (`cmd.exe`) und sein interner `conhost.exe`-Host binnen ~35ms sauber beendet werden (`ExitCode: 0`), bevor das eigentliche Plugin-Kommando ausgeführt wird. Per Procmon-Trace ist belegt, dass dies kein Bug im eigenen ConPTY-Code ist, sondern eine ungeklärte, tiefliegende Eigenart des Betriebssystems, die ausschließlich außerhalb von Visual Studio auftritt. Diese OS-Ursache wird nicht weiterverfolgt.

Stattdessen wird `KiAusfuehrungsService` um einen Austauschpunkt (Seam) für den eigentlichen ConPTY-Prozessstart erweitert. Produktiv bleibt der echte Win32-ConPTY-Start (`PseudoConsoleProcessStarter`) unverändert. Für die 14 betroffenen E2E-Tests wird ein Test-Double eingesetzt, das einen simulierten, langlebigen Kindprozess mit realistischem, über den bestehenden `PseudoConsoleSession`/`TerminalBuffer`-Mechanismus laufenden Terminal-Output liefert — ohne echten ConPTY-Aufruf. Die Tests prüfen weiterhin reales, beobachtbares Verhalten (Status-Übergänge `Gestartet`/`Gestoppt`/`Fehler`, sichtbare Terminal-Ausgabe, `IsCliRunning`, „Stoppen"-Button), nur der darunterliegende Betriebssystem-Mechanismus wird für Testzwecke ausgetauscht.

## Betroffene Klassen und Komponenten

- **Neues Interface:** `IPseudoConsoleProcessLauncher` (o. ä., Name gemäß bestehender Namenskonvention `Pseudo Console*`) — kapselt exakt den Teil von `KiAusfuehrungsService.StartPseudoConsoleProcess`, der einen echten Win32-Prozess samt `PseudoConsole`/`PseudoConsoleSession` erzeugt.
- **Neue Klasse (Produktivcode):** `Win32PseudoConsoleProcessLauncher` — Standardimplementierung, kapselt die bisherige Logik aus `KiAusfuehrungsService.StartPseudoConsoleProcess` (inkl. `PseudoConsole.Create`, `PseudoConsoleProcessStarter.Start`, `CreatePseudoConsoleSession`) unverändert.
- **Neue Klasse (Testcode):** Ein Fake-Launcher (Testinfrastruktur, z. B. unter `src/Softwareschmiede.Tests/E2E/` oder einer neuen `TestSupport`-Ecke), der einen simulierten Kindprozess ohne echtes ConPTY erzeugt, aber `Process`, `PseudoConsoleSession` (bzw. eine kompatible Abstraktion) und Terminal-Output liefert, die von `KiAusfuehrungsService` und `TerminalControl` ununterscheidbar von einer echten Sitzung verarbeitet werden.
- **Geänderte Klasse:** `KiAusfuehrungsService` — erhält den Launcher als Konstruktor-Abhängigkeit (DI), `StartPseudoConsoleProcess` delegiert an den Launcher statt die Win32-Logik selbst auszuführen.
- **Geänderte Registrierung:** `App.xaml.cs` (DI-Setup) — registriert `Win32PseudoConsoleProcessLauncher` als Implementierung von `IPseudoConsoleProcessLauncher`.
- **Geänderte Testinfrastruktur:** `WpfTestBase` — Mechanismus, wie die 14 betroffenen E2E-Tests den Fake-Launcher statt des echten aktivieren (vermutlich über eine Testkonfiguration/DI-Override im Testhost-Startup der `Softwareschmiede.App`, analog zum bereits bestehenden Testmodus, der nur `LocalDirectoryPlugin` als SCM-Plugin zulässt).
- **Betroffene Tests (bestehend, anzupassen):** `KiAusfuehrungsServiceTests.cs` (nutzt intern denselben Startpfad, ggf. für die neue Abstraktion anzupassen), die 14 in der Anforderung genannten E2E-Tests.
- **Nicht betroffen:** `PseudoConsole.cs`, `PseudoConsoleNativeMethods.cs`, `PseudoConsoleProcessStarter.cs`, `PseudoConsoleSession.cs` — bleiben als Produktivmechanismus unverändert; `Softwareschmiede.Plugin.KiSimulator` bleibt unverändert (liefert nur das zu startende Kommando, nicht den ConPTY-Mechanismus).

## Implementierungsansatz

- Erweiterungspunkt nach dem Strategy-Pattern: `KiAusfuehrungsService.StartPseudoConsoleProcess` wird zu einem dünnen Aufruf an ein injiziertes `IPseudoConsoleProcessLauncher`.
- Der bestehende Rückgabetyp `(Process Process, PseudoConsoleSession Session, IntPtr NativeProcessHandle)` bleibt die Schnittstelle zwischen Launcher und `KiAusfuehrungsService`, damit sich an `CliProcessHandle`, `HandleProcessExited`, `TryGetExitCode` etc. nichts ändert.
- Der Fake-Launcher muss einen echten (aber harmlosen) `Process` liefern, damit `Process.HasExited`/`Exited`-Event/`GetExitCodeProcess`-Fallback weiterhin funktionieren (Vorschlag: ein echter, lange laufender, aber trivialer Hilfsprozess statt eines simulierten Objekts — vermeidet Sonderfälle in `KiAusfuehrungsService` für „kein echter Prozess").
- `PseudoConsoleSession` selbst bleibt unverändert nutzbar, sofern sie mit Streams konstruiert werden kann, die nicht zwingend von einer echten `PseudoConsole` stammen (zu prüfen: aktuell nimmt der interne Konstruktor `PseudoConsole` entgegen — ggf. Erweiterung um eine Überladung/Abstraktion für „kein echtes PseudoConsole-Handle zum Schließen", damit `PseudoConsoleSession` nicht zwingend an einen echten `PseudoConsole`-Handle gekoppelt ist).
- Wie der Fake-Launcher realistischen Terminal-Output erzeugt, ist eine Designentscheidung für die Planungsphase (z. B. ein Hintergrund-Thread, der zeitversetzt Text in den `OutputStream` schreibt, gesteuert durch das über `IKiPlugin`/`KiSimulator` gelieferte Kommando oder eine feste Testsequenz).
- Aktivierung des Fake-Launchers für E2E-Tests: Analog zum bestehenden Testmodus-Muster (siehe `WpfTestBase`-Kommentar „Im Test-Modus steht ausschließlich das LocalDirectoryPlugin als SCM-Plugin zur Verfügung") vermutlich über eine Umgebungsvariable oder einen Test-Startparameter, den `App.xaml.cs` bei der DI-Registrierung auswertet.

## Konfiguration

Kein produktiv sichtbares/konfigurierbares Verhalten für Endanwender. Die Umschaltung zwischen echtem und simuliertem Launcher ist ausschließlich Testinfrastruktur (vergleichbar mit dem bestehenden Test-Modus-Schalter für SCM-Plugins) und nicht Teil der Anwendungseinstellungen.

## Offene Fragen

- Exakter Mechanismus, über den `Softwareschmiede.App` im E2E-Testkontext erkennt, dass der Fake-Launcher statt des echten verwendet werden soll (Umgebungsvariable analog zu bestehenden Testschaltern vs. Test-spezifischer Startparameter vs. Test-spezifisches DI-Overlay).
- Ob `PseudoConsoleSession` für den Fake-Fall unverändert wiederverwendet werden kann oder eine kleine Erweiterung (z. B. optionaler `PseudoConsole`-Parameter) nötig ist.
- Wie realistisch/deterministisch der simulierte Terminal-Output sein muss, damit alle 14 betroffenen Tests (unterschiedliche Szenarien: Start, Stopp, Resize, Tastatureingabe, Prozessende, Plugin-Wechsel) ihre jeweiligen Erwartungen ohne Anpassung der Testassertions weiter erfüllen.
- Ob einzelne der 14 Tests (z. B. `E2E_ConPtyResize`, `E2E_ConPtyKeyboardInput`) zusätzliche Fähigkeiten vom Fake-Launcher verlangen (Resize-Reaktion, Eingabe-Echo), die über reine Ausgabe-Simulation hinausgehen.
