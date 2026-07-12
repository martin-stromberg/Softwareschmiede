# Bestandsaufnahme: ConPTY-E2E-Tests von echtem Kindprozess entkoppeln

Analysiert wurden der ConPTY-Startpfad (`KiAusfuehrungsService`, `PseudoConsole*`), das bestehende Muster für Testmodus-Verzweigung (`PluginManager`), die DI-Registrierung (`App.xaml.cs`), das `IKiPlugin`-Interface samt `KiSimulatorPlugin`, sowie die 14 betroffenen E2E-Tests und die bestehende Unit-Test-Abdeckung von `KiAusfuehrungsService`, bezogen auf die Anforderung, den ConPTY-Prozessstart für Testzwecke austauschbar zu machen.

## Zusammenfassung

- Der ConPTY-Prozessstart ist aktuell **nicht** hinter einem Interface/Seam versteckt: `KiAusfuehrungsService.StartPseudoConsoleProcess` ruft die Win32-Mechanik (`PseudoConsole.Create`, `PseudoConsoleProcessStarter.Start`) direkt und `private` auf. Kein Austauschpunkt vorhanden.
- Es existiert bereits ein **etabliertes, projektweit genutztes Muster** für genau diese Art von Testmodus-Verzweigung: `PluginManager.IsTestMode()` liest `SOFTWARESCHMIEDE_TEST_DB_PATH` (dieselbe Variable, die `WpfTestBase.LaunchApp` für jeden E2E-Testlauf setzt) und filtert darauf basierend Verhalten. Dasselbe Muster kann für den neuen Launcher-Seam wiederverwendet werden.
- `PseudoConsoleSession` ist an eine konkrete `PseudoConsole`-Instanz gekoppelt (Konstruktor-Pflichtparameter, `Resize()`/`Dispose()` rufen direkt `_pseudoConsole.*` auf) — für ein Test-Double ohne echtes ConPTY-Handle besteht hier eine Kopplungsstelle, die berücksichtigt werden muss.
- `IKiPlugin.StartCliAsync` liefert ausschließlich die zu startende Kommandozeile (`ProcessStartInfo`) und ist vom ConPTY-Mechanismus unabhängig — `KiSimulatorPlugin` muss für die neue Teststrategie nicht verändert werden.
- Alle 14 betroffenen E2E-Tests teilen ein gemeinsames Kernkriterium: Warten auf das Erscheinen des `"CliStoppen"`-Buttons (15s-Timeout) nach `StartenUndPluginWaehlen`. Keiner der 14 Tests prüft den tatsächlichen Terminal-Ausgabetext — nur UI-Zustand (Buttons, Status-Label, Fehlerbanner-Abwesenheit). Das senkt die Anforderungen an die Realitätsnähe eines Test-Doubles erheblich.
- Einzelne Tests benötigen zusätzliches Prozessverhalten: sauberes Reagieren auf `StopCliAsync` (`E2E_ConPtyProcessEnd`), mehrfacher Start/Stop nacheinander bzw. parallel (`E2E_AutoStartCli`, `E2E_PluginWechsel`, `E2E_TaskWechselUeberMenue`, `E2E_PluginProjectDefault_NextTask`), fehlerfreies `Resize`/Tastatureingabe ohne sichtbare Reaktion (`E2E_ConPtyResize`, `E2E_ConPtyKeyboardInput`).
- 19 bestehende Aufrufstellen von `new KiAusfuehrungsService(...)` in 13 Testdateien — eine Konstruktoränderung an `KiAusfuehrungsService` hat potenziell breite Auswirkung; die vier Unit-Tests, die bereits `StartWithPseudoConsoleAsync` nutzen, sind vom Sofort-Sterben-Problem selbst nicht betroffen (sie erwarten ohnehin ein schnelles/erzwungenes Prozessende).
- Der bestehende Skip-Mechanismus (`ConPtyEnvironmentProbe`/`SkipWennConPtyNichtVerfuegbar`, gesteuert über `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS`) bleibt als Fallback bestehen, wird für die 14 Tests nach Umsetzung der neuen Teststrategie aber voraussichtlich nicht mehr benötigt.

## Details

- [Logik](inventory/logic.md)
- [Interfaces](inventory/interfaces.md)
- [Tests](inventory/tests.md)
