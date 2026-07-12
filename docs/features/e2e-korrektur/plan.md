# Umsetzungsplan: ConPTY-E2E-Tests von echtem Kindprozess entkoppeln

## Übersicht

`KiAusfuehrungsService` erhält einen Austauschpunkt (`IPseudoConsoleProcessLauncher`) für den eigentlichen ConPTY-Prozessstart. Produktiv bleibt der bestehende Win32-Mechanismus (`Win32PseudoConsoleProcessLauncher`, unveränderte Logik). Im E2E-Testmodus (erkannt über dieselbe Umgebungsvariable, die `PluginManager.IsTestMode()` bereits nutzt) wird stattdessen `SimulatedPseudoConsoleProcessLauncher` registriert, der einen echten, aber gewöhnlich (nicht über ConPTY) gestarteten `cmd.exe`-Prozess liefert. Dafür wird `PseudoConsoleSession` von der konkreten `PseudoConsole`-Klasse auf ein neues schlankes Interface (`IPseudoConsoleHandle`) entkoppelt. Betroffen sind ausschließlich `Softwareschmiede`- und `Softwareschmiede.App`-Produktivcode sowie die DI-Registrierung — kein neues Nutzerverhalten, keine UI-Änderung.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Austauschpunkt für ConPTY-Prozessstart | Strategy-Pattern über neues Interface `IPseudoConsoleProcessLauncher`, injiziert in `KiAusfuehrungsService` | Kapselt exakt den heute privaten Abschnitt `StartPseudoConsoleProcess`; Rückgabeform `(Process, PseudoConsoleSession, IntPtr)` bleibt unverändert, dadurch minimale Änderung am Rest von `KiAusfuehrungsService` |
| Aktivierung des Test-Doubles | Bedingte DI-Registrierung in `App.xaml.cs` anhand derselben Umgebungsvariable (`SOFTWARESCHMIEDE_TEST_DB_PATH`), die `PluginManager.IsTestMode()` bereits verwendet | Bereits etabliertes, projektweites Muster für genau diese Art Testmodus-Verzweigung; keine neue Umgebungsvariable, keine Änderung an `WpfTestBase` nötig |
| Konstruktor-Kompatibilität zu 19 bestehenden Aufrufstellen von `new KiAusfuehrungsService(...)` | Neuer Parameter `IPseudoConsoleProcessLauncher? launcher = null`, Fallback auf `new Win32PseudoConsoleProcessLauncher()` im Konstruktorkörper | Exaktes Vorbild bereits im Repo: `PluginManager(IServiceProvider, ILogger<PluginManager>, string? pluginDirectory = null)` verwendet denselben optionalen-Parameter-mit-Fallback-Ansatz für Testbarkeit |
| Entkopplung von `PseudoConsoleSession` und `PseudoConsole` | Neues schlankes Interface `IPseudoConsoleHandle` (`Resize`, `IDisposable`); `PseudoConsole` implementiert es zusätzlich, `PseudoConsoleSession`-Konstruktor nimmt `IPseudoConsoleHandle` statt `PseudoConsole` entgegen | Rein additive Typänderung (jede vorhandene `PseudoConsole`-Instanz erfüllt weiterhin die Signatur) — bestehende Aufrufstellen (`KiAusfuehrungsService.CreatePseudoConsoleSession`, `PseudoConsoleSessionTests.CreateSession`) bleiben unverändert kompilierbar |
| Simulierter Kindprozess | `SimulatedPseudoConsoleProcessLauncher` startet einen echten `cmd.exe`-Prozess bare (kein `/c`) über gewöhnlichen `Process.Start` mit `RedirectStandardInput/Output/Error = true` statt über ConPTY/`CreateProcess`+`STARTUPINFOEX`; `process.StandardInput.BaseStream`/`StandardOutput.BaseStream` werden direkt als `PseudoConsoleSession`-Streams verwendet, `IPseudoConsoleHandle` wird durch eine No-Op-Instanz (`NullPseudoConsoleHandle`) ersetzt | Der spätere verzögerte Kommando-Versand (`KiAusfuehrungsService.SendCommandDelayedAsync`) bleibt dadurch unverändert wirksam: Das über `IKiPlugin` gelieferte Kommando wird wie im Produktivpfad als Zeile in ein bereits laufendes, interaktives `cmd.exe` geschrieben — nur der darunterliegende OS-Mechanismus (gewöhnliche anonyme Pipes statt ConPTY) ist ausgetauscht, wodurch der belegte Sofort-Sterben-Effekt umgangen wird, ohne die Choreografie in `KiAusfuehrungsService` zu ändern |
| Platzierung des Test-Doubles | `SimulatedPseudoConsoleProcessLauncher` liegt im Produktivprojekt `src/Softwareschmiede/Infrastructure/Terminal/` (nicht in `Softwareschmiede.Tests`), Aktivierung ausschließlich über DI-Bedingung | `Softwareschmiede.App.exe` wird von `WpfTestBase` als externer Prozess gestartet und hat keinen Zugriff auf das Testprojekt; exaktes Vorbild bereits im Repo: `Softwareschmiede.Plugin.KiSimulator` ist ebenfalls ein „nur für Tests/Demo gedachtes" Artefakt, das reguär mitgebaut und nur über `PluginManager`s Testmodus-Filter zur Laufzeit gesteuert wird |

## Programmabläufe

### Produktivstart eines ConPTY-Prozesses (unverändert im Ergebnis)

1. `KiAusfuehrungsService.StartWithPseudoConsoleAsync` ermittelt Arbeitsverzeichnis und Plugin-Kommando wie bisher.
2. Statt der bisherigen privaten Methode ruft es `_launcher.Start(aufgabeId, effectiveWorkingDirectory, pluginCommand)` auf.
3. `Win32PseudoConsoleProcessLauncher.Start` führt die bisherige, unveränderte Logik aus (`PseudoConsole.Create`, `PseudoConsoleProcessStarter.Start`, `Process.GetProcessById`, `new PseudoConsoleSession(pseudoConsole, ...)`).
4. Rückgabe `(Process, PseudoConsoleSession, IntPtr NativeProcessHandle)` an `StartWithPseudoConsoleAsync`, restlicher Ablauf (Handle-Registrierung, verzögerter Kommando-Versand über `SendCommandDelayedAsync`, Events) bleibt unverändert.

Beteiligte Klassen/Komponenten: `KiAusfuehrungsService`, `IPseudoConsoleProcessLauncher`, `Win32PseudoConsoleProcessLauncher`, `PseudoConsole`, `PseudoConsoleProcessStarter`, `PseudoConsoleSession`.

### Simulierter Start im E2E-Testmodus

1. `App.xaml.cs.ConfigureServices` registriert beim Start von `Softwareschmiede.App.exe` `IPseudoConsoleProcessLauncher` als `SimulatedPseudoConsoleProcessLauncher`, wenn `SOFTWARESCHMIEDE_TEST_DB_PATH` gesetzt ist (von `WpfTestBase.LaunchApp` für jeden E2E-Testlauf gesetzt), sonst als `Win32PseudoConsoleProcessLauncher`.
2. `KiAusfuehrungsService.StartWithPseudoConsoleAsync` ruft wie im Produktivpfad `_launcher.Start(...)` auf — unwissend davon, welche Implementierung aktiv ist.
3. `SimulatedPseudoConsoleProcessLauncher.Start` startet `cmd.exe` bare (kein `/c`-Argument) über `Process.Start` mit `RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = effectiveWorkingDirectory`.
4. Es konstruiert `new PseudoConsoleSession(NullPseudoConsoleHandle.Instance, process, process.StandardInput.BaseStream, process.StandardOutput.BaseStream, logger)` und liefert `(process, session, IntPtr.Zero)` zurück.
5. `KiAusfuehrungsService.SendCommandDelayedAsync` schreibt nach 300ms wie gewohnt den Plugin-Befehl (z. B. das `KiSimulatorPlugin`-Kommando) in `session.InputStream` — das laufende, interaktive `cmd.exe` empfängt die Zeile über die reguläre STDIN-Pipe und führt sie aus.
6. Prozessende (manuell über `StopCliAsync` oder durch natürliches Prozessende) läuft über denselben `Process.Exited`-Handler/`HandleProcessExited`-Pfad wie im Produktivfall; da `NativeProcessHandle = IntPtr.Zero`, wird der Exit-Code regulär über `Process.ExitCode` ermittelt (kein `GetExitCodeProcess`-Sonderfall).

Beteiligte Klassen/Komponenten: `App.xaml.cs`, `KiAusfuehrungsService`, `IPseudoConsoleProcessLauncher`, `SimulatedPseudoConsoleProcessLauncher`, `NullPseudoConsoleHandle`, `PseudoConsoleSession`.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `IPseudoConsoleProcessLauncher` | Interface | Austauschpunkt für den eigentlichen ConPTY-Prozessstart; eine Methode `Start(Guid aufgabeId, string effectiveWorkingDirectory, string pluginCommand)` mit Rückgabe `(Process Process, PseudoConsoleSession Session, IntPtr NativeProcessHandle)` |
| `Win32PseudoConsoleProcessLauncher` | Klasse | Produktivimplementierung; enthält die aus `KiAusfuehrungsService.StartPseudoConsoleProcess` verschobene, unveränderte Win32-ConPTY-Logik |
| `SimulatedPseudoConsoleProcessLauncher` | Klasse | Test-Double; startet einen echten `cmd.exe`-Prozess über gewöhnlichen `Process.Start` (kein ConPTY) mit umgeleiteten Standard-Streams |
| `IPseudoConsoleHandle` | Interface | Entkoppelt `PseudoConsoleSession` von der konkreten `PseudoConsole`-Klasse; Mitglieder `bool Resize(short cols, short rows)` und `IDisposable` |
| `NullPseudoConsoleHandle` | Klasse | No-Op-Implementierung von `IPseudoConsoleHandle` für den simulierten Pfad (`Resize` liefert `true`, `Dispose()` ist leer); als Singleton (`Instance`) bereitgestellt |

## Änderungen an bestehenden Klassen

### `KiAusfuehrungsService` (Klasse)

- **Neue Konstruktor-Parameter:** `IPseudoConsoleProcessLauncher? launcher = null` — bei `null` wird intern `new Win32PseudoConsoleProcessLauncher()` verwendet (Fallback für die 19 bestehenden, unveränderten Aufrufstellen); explizit gesetzt durch die DI-Registrierung in `App.xaml.cs`.
- **Geänderte Methode:** `StartPseudoConsoleProcess` — der bisherige Methodenkörper entfällt zugunsten eines direkten Aufrufs von `_launcher.Start(aufgabeId, effectiveWorkingDirectory, pluginCommand)` innerhalb von `StartWithPseudoConsoleAsync` (die private Hilfsmethode kann entfallen oder als dünner Wrapper erhalten bleiben).

### `PseudoConsole` (Klasse)

- Implementiert zusätzlich `IPseudoConsoleHandle` (`: IDisposable` → `: IPseudoConsoleHandle`); keine Verhaltensänderung, `Resize`/`Dispose` erfüllen die Signatur bereits.

### `PseudoConsoleSession` (Klasse)

- **Geänderte Methoden:** Beide Konstruktoren (öffentlicher Kurz-Konstruktor und interner Vollständig-Konstruktor) — Parametertyp `PseudoConsole pseudoConsole` wird zu `IPseudoConsoleHandle pseudoConsole`; Feld `_pseudoConsole` entsprechend `IPseudoConsoleHandle`. Nutzung (`_pseudoConsole.Resize(...)`, `_pseudoConsole.Dispose()`) bleibt unverändert, da beide Mitglieder bereits im neuen Interface enthalten sind.

### `App.xaml.cs` (Startklasse, `ConfigureServices`)

- **Geänderte Methode:** `ConfigureServices` — die Zeile `services.AddSingleton<KiAusfuehrungsService>();` wird um eine vorgeschaltete bedingte Registrierung von `IPseudoConsoleProcessLauncher` ergänzt (Testmodus-Erkennung analog `PluginManager.IsTestMode()`); `KiAusfuehrungsService` wird weiterhin als Singleton registriert, DI löst den neuen Konstruktorparameter automatisch auf.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine.

## Konfigurationsänderungen

Keine neuen Konfigurationseinträge — die bestehende Umgebungsvariable `SOFTWARESCHMIEDE_TEST_DB_PATH` (bereits vorhanden, bereits von `WpfTestBase` und `PluginManager` genutzt) wird für einen zusätzlichen Zweck (Launcher-Auswahl) mitverwendet.

## Seiteneffekte und Risiken

- **Terminal-Ausgabeformat im Testmodus:** Ein über gewöhnliche STDIN/STDOUT-Umleitung gestartetes `cmd.exe` emittiert keine VT100/ANSI-Sequenzen (die reale ConPTY übersetzt Cursor-Steuerung etc. in ANSI-Escape-Sequenzen für `AnsiSequenceParser`/`TerminalBuffer`). Simulierte Terminalausgabe sieht daher optisch einfacher aus als eine echte ConPTY-Sitzung. Unkritisch, da laut Bestandsaufnahme keiner der 14 betroffenen Tests den gerenderten Ausgabetext prüft — betrifft nur die visuelle Erscheinung bei manueller Beobachtung.
- **`StopCliAsync`-Latenz:** `CloseMainWindow()` liefert für Konsolenprozesse ohne Hauptfenster bereits heute im Produktivpfad `false` (ConPTY-`cmd.exe` hat ebenfalls kein HWND) — der 5-Sekunden-Wartepfad bis zum `Kill(entireProcessTree: true)`-Fallback ist folglich bereits bestehendes Verhalten, keine neue Regression durch den simulierten Pfad.
- **`PseudoConsoleSessionTests.CreateSession`:** Verwendet weiterhin eine echte `PseudoConsole`-Instanz (`PseudoConsole.Create(1, 1)`) — bleibt durch die additive Typänderung unverändert kompilierbar, ist aber selbst weiterhin auf funktionierendes ConPTY angewiesen. Betrifft nicht die 14 E2E-Tests, sollte aber bei der Umsetzung nicht stillschweigend als „mitbehoben" missverstanden werden.
- **`Softwareschmiede.Plugin.KiSimulator`-Kommando:** `ping -n 31 127.0.0.1 > nul` läuft ca. 31 Sekunden — bei mehrfachem Start/Stop in einem Test (`E2E_AutoStartCli`, `E2E_PluginWechsel`, `E2E_TaskWechselUeberMenue`, `E2E_PluginProjectDefault_NextTask`) muss der jeweils vorherige simulierte Prozess zuverlässig beendet werden, bevor der nächste startet — bestehendes Verhalten von `StopCliAsync`/`HandleProcessExited`, keine neue Anforderung.

## Umsetzungsreihenfolge

1. **`IPseudoConsoleHandle`-Interface anlegen**
   - Voraussetzungen: Keine.
   - Beschreibung: Neues Interface in `src/Softwareschmiede/Infrastructure/Terminal/` mit `bool Resize(short cols, short rows)`, erbt `IDisposable`.

2. **`NullPseudoConsoleHandle` anlegen**
   - Voraussetzungen: `IPseudoConsoleHandle` (Schritt 1).
   - Beschreibung: No-Op-Implementierung, `Resize` liefert `true`, `Dispose()` leer; statisches `Instance`-Feld.

3. **`PseudoConsole` um `IPseudoConsoleHandle` erweitern**
   - Voraussetzungen: `IPseudoConsoleHandle` (Schritt 1).
   - Beschreibung: Klassendeklaration um `: IPseudoConsoleHandle` ergänzen; keine Methodenänderung.

4. **`PseudoConsoleSession`-Konstruktoren auf `IPseudoConsoleHandle` umstellen**
   - Voraussetzungen: `IPseudoConsoleHandle` (Schritt 1), `PseudoConsole` implementiert es (Schritt 3).
   - Beschreibung: Parametertyp beider Konstruktoren und Feldtyp `_pseudoConsole` ändern.

5. **`IPseudoConsoleProcessLauncher`-Interface anlegen**
   - Voraussetzungen: Keine (kann parallel zu Schritt 1–4 erfolgen).
   - Beschreibung: Methode `Start(Guid aufgabeId, string effectiveWorkingDirectory, string pluginCommand) : (Process Process, PseudoConsoleSession Session, IntPtr NativeProcessHandle)`.

6. **`Win32PseudoConsoleProcessLauncher` anlegen**
   - Voraussetzungen: `IPseudoConsoleProcessLauncher` (Schritt 5), `PseudoConsoleSession` akzeptiert `IPseudoConsoleHandle` (Schritt 4).
   - Beschreibung: Bisherigen Inhalt von `KiAusfuehrungsService.StartPseudoConsoleProcess` unverändert hierher verschieben.

7. **`SimulatedPseudoConsoleProcessLauncher` anlegen**
   - Voraussetzungen: `IPseudoConsoleProcessLauncher` (Schritt 5), `NullPseudoConsoleHandle` (Schritt 2), `PseudoConsoleSession` akzeptiert `IPseudoConsoleHandle` (Schritt 4).
   - Beschreibung: `Process.Start` mit umgeleiteten Standard-Streams, `PseudoConsoleSession` mit `NullPseudoConsoleHandle.Instance` und den Prozess-Streams konstruieren.

8. **`KiAusfuehrungsService` auf den Launcher umstellen**
   - Voraussetzungen: `IPseudoConsoleProcessLauncher` (Schritt 5), `Win32PseudoConsoleProcessLauncher` (Schritt 6).
   - Beschreibung: Konstruktor um optionalen `IPseudoConsoleProcessLauncher? launcher = null`-Parameter mit Fallback erweitern; `StartPseudoConsoleProcess`-Aufruf durch `_launcher.Start(...)` ersetzen.

9. **DI-Registrierung in `App.xaml.cs` anpassen**
   - Voraussetzungen: `Win32PseudoConsoleProcessLauncher` (Schritt 6), `SimulatedPseudoConsoleProcessLauncher` (Schritt 7), angepasster `KiAusfuehrungsService`-Konstruktor (Schritt 8).
   - Beschreibung: Bedingte Registrierung von `IPseudoConsoleProcessLauncher` vor `services.AddSingleton<KiAusfuehrungsService>()`, Bedingung analog `PluginManager.IsTestMode()`.

10. **Unit-Tests für `SimulatedPseudoConsoleProcessLauncher` schreiben**
    - Voraussetzungen: `SimulatedPseudoConsoleProcessLauncher` (Schritt 7).
    - Beschreibung: Siehe Abschnitt Tests.

11. **Verifikation der 14 betroffenen E2E-Tests unter `dotnet test`**
    - Voraussetzungen: Schritt 9 abgeschlossen.
    - Beschreibung: Alle 14 in der Anforderung genannten Tests ohne `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS` ausführen und grünen Zustand bestätigen.

12. **`SkipWennConPtyNichtVerfuegbar()`-Aufrufe aus den 14 Testdateien entfernen**
    - Voraussetzungen: Erfolgreiche Verifikation (Schritt 11).
    - Beschreibung: Aufräumen der jetzt überflüssigen Skip-Aufrufe in den 14 betroffenen Testdateien; `ConPtyEnvironmentProbe`/`SkipWennConPtyNichtVerfuegbar` selbst bleiben als Mechanismus erhalten (ggf. noch für andere, hier nicht betroffene Szenarien relevant).

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `Start_LiefertLaufendenProzessUndSession` | Neue Testklasse `SimulatedPseudoConsoleProcessLauncherTests` (`src/Softwareschmiede.Tests/Infrastructure/Terminal/`) | `Start(...)` liefert einen `Process`, der nicht sofort beendet ist (`!Process.HasExited`), sowie eine funktionsfähige `PseudoConsoleSession` |
| `Start_GesendetesKommandoWirdAusgefuehrt` | `SimulatedPseudoConsoleProcessLauncherTests` | Ein über `session.InputStream` gesendetes Kommando (z. B. `echo MARKER`) erscheint innerhalb der bestehenden `PseudoConsoleSession`-Leseschleife im `Buffer`/löst `BufferChanged` aus |
| `Start_ProzessBeendetSichAufKillEntireProcessTree` | `SimulatedPseudoConsoleProcessLauncherTests` | Nach `Process.Kill(entireProcessTree: true)` wird `Process.Exited` ausgelöst und `HasExited` liefert `true` — bestätigt Kompatibilität mit `KiAusfuehrungsService.StopCliAsync` |
| `StartWithPseudoConsoleAsync_MitInjiziertemFakeLauncher_ErreichtGestartet` | `KiAusfuehrungsServiceTests.cs` (Erweiterung) | `KiAusfuehrungsService`, konstruiert mit explizitem `IPseudoConsoleProcessLauncher`-Mock oder mit echtem `SimulatedPseudoConsoleProcessLauncher`, durchläuft `CliProcessStatusChanged` bis `Gestartet`, ohne echtes ConPTY zu benötigen |

### Betroffene bestehende Tests

Keine — durch den optionalen Konstruktorparameter mit Fallback (`launcher = null`) bleiben alle 19 bestehenden Aufrufstellen von `new KiAusfuehrungsService(...)` unverändert kompilier- und lauffähig; die additive Typänderung an `PseudoConsoleSession` lässt `PseudoConsoleSessionTests.CreateSession` unverändert kompilieren.

### E2E-Tests (Pflicht)

Es wird kein neues Nutzerverhalten eingeführt — die „Akzeptanzkriterien" dieser Anforderung sind die 14 bereits bestehenden E2E-Tests, die nach Umsetzung zuverlässig unter `dotnet test` grün laufen müssen. Kein neuer E2E-Test erforderlich.

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| CLI startet und „Stoppen"-Button erscheint | `E2E_WorkingDirectory`, `E2E_TaskExecutionCommandLineParameters`, `E2E_PluginSelectionDialog`, `E2E_PluginProjectDefault`, `E2E_ConPtyTerminalStart`, `E2E_AufgabeStarten`, `E2E_ArbeitsstatusAktualisierung` | Läuft zuverlässig unter `dotnet test` (bisher: Timeout wegen Sofort-Sterben) |
| Fenstergröße ändern während laufendem CLI | `E2E_ConPtyResize` | `PseudoConsoleSession.Resize` (über `NullPseudoConsoleHandle`) wirft keinen Fehler |
| Tastatureingabe während laufendem CLI | `E2E_ConPtyKeyboardInput` | Eingabe über `session.InputStream` löst keinen Fehlerbanner aus |
| Manuelles Stoppen | `E2E_ConPtyProcessEnd` | `StopCliAsync` beendet den simulierten Prozess zuverlässig, `CliStoppen` verschwindet |
| Automatischer Neustart nach Stop+Navigation | `E2E_AutoStartCli` | Mehrfacher Start/Stop desselben simulierten Prozesstyps funktioniert |
| Pluginwechsel während laufendem CLI | `E2E_PluginWechsel` | Stop des alten, Start eines neuen simulierten Prozesses in Folge |
| Aufgabenwechsel über Menü mit zwei parallelen CLI-Läufen | `E2E_TaskWechselUeberMenue` | Zwei gleichzeitige simulierte Prozesse unterschiedlicher Aufgaben |
| Zweite Aufgabe nach erster starten | `E2E_PluginProjectDefault_NextTask` | Mehrere simulierte Prozesse nacheinander in derselben App-Instanz |

Bestehende E2E-Tests, die angepasst werden müssen:

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| Alle 14 in der Anforderung genannten Testdateien | Entfernen des jetzt überflüssigen `SkipWennConPtyNichtVerfuegbar()`-Aufrufs (siehe Umsetzungsschritt 12) — keine strukturelle Änderung, nur Aufräumen |

## Offene Punkte

| # | Offener Punkt | Empfohlener Vorschlag |
|---|---------------|----------------------|
| 1 | Ob `Process.StandardInput.BaseStream`/`StandardOutput.BaseStream` sich in allen von `PseudoConsoleSession` genutzten Aspekten (insbesondere synchrones vs. asynchrones Lesen in `ReadLoopAsync`, Verhalten bei `Dispose()`/Stream-Schließung) identisch zu den bisherigen `FileStream`-Wrappern um rohe Pipe-Handles verhält. | Während der Umsetzung (Schritt 7/10) gezielt mit den neuen Unit-Tests verifizieren; funktional gleichwertig erwartet, da beide letztlich anonyme Pipes kapseln, aber vor Abschluss von Schritt 11 nicht als gesichert annehmen. |
| 2 | Ob die vier bestehenden Unit-Tests, die bereits `StartWithPseudoConsoleAsync` mit bare `cmd.exe` nutzen (`ConPtyProcessExited_SubscriberThrows_LogsAndDoesNotCrash`, `KiAusfuehrungsService_HandleProcessExited_DisposesSession`, `KiAusfuehrungsService_Dispose_CancelsAllSessions`, `StartWithPseudoConsoleAsync_ProzessEndetVorVerzoegertemSenden_KeineWarnungWegenGeschlossenemStream`) weiterhin bewusst den echten `Win32PseudoConsoleProcessLauncher` (Default-Fallback) verwenden sollen, oder ob sie ebenfalls auf den simulierten Launcher umgestellt werden sollten. | Unverändert lassen (echter Launcher als Default) — sie sind laut Bestandsaufnahme vom Sofort-Sterben-Problem nicht betroffen, da sie ohnehin ein schnelles/erzwungenes Prozessende erwarten; eine Umstellung wäre eine über die Anforderung hinausgehende Änderung. |
