# Umsetzungsplan: Tests automatisierbar machen

## Übersicht

Die E2E-Tests scheitern sporadisch an zwei Ursachen: (1) korrumpierte/gesperrte Build-Artefakte durch eine noch laufende `Softwareschmiede.App.exe` einer vorherigen Testinstanz (führt zu ".NET Desktop Runtime nicht gefunden") und (2) Startup-Exceptions der App (z. B. `XamlParseException`), die sich im Test nur als "MainWindow nicht gefunden"/Timeout äußern, ohne die tatsächliche Ursache preiszugeben. Umgesetzt werden daher: eine Log-Auslese-Diagnose in der Test-Infrastruktur (`WpfTestBase` + neuer reiner Log-Parser), ein robusterer Prozess-Teardown in `Dispose`, sowie eine Warn-Erweiterung des Build-Hooks. Betroffen sind ausschließlich die Test-Infrastruktur (`src/Softwareschmiede.Tests/E2E`) und die Claude-Hooks (`.claude/hooks`) — kein Produktivcode der App, keine Datenbank.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Log-Parsing-Logik | Neue reine Hilfsklasse `AppStartupLogInspector` (internal static) als **Value Object / reine Funktion**, die ein Log-Verzeichnis + Offset entgegennimmt und Fehlerdiagnosen zurückgibt; `WpfTestBase` ruft sie nur auf | Trennt die parse-Logik von der FlaUI-/Prozess-Steuerung und macht sie ohne laufende App unit-testbar. Die alternative Umsetzung direkt in `WpfTestBase` wäre nur über einen realen App-Crash testbar (nicht deterministisch) |
| Abgrenzung "Log-Zeilen dieses Testlaufs" | **Byte-Offset-Snapshot**: vor `LaunchApp` die Länge der aktuellen Tageslog-Datei merken, bei Fehler nur den danach angehängten Inhalt auswerten | Serilog schreibt im Dateiformat nur `[HH:mm:ss LVL]` ohne Datum; ein Zeitstempel-Filter wäre unzuverlässig. Der Offset liefert exakt die Zeilen des aktuellen Prozessstarts (innerhalb eines Testlaufs rollt die Tagesdatei nicht) |
| Log-Datei lesen trotz Serilog-Filehandle | Öffnen mit `FileShare.ReadWrite` | Serilog hält die Datei während des App-Laufs offen; ohne `ReadWrite`-Share schlägt das Lesen fehl |
| Build-Hook-Frequenz | Build **vor jedem** `dotnet test` beibehalten; Ursache (paralleles Schreiben) bleibt über den bestehenden Datei-Lock abgesichert | E2E-Tests benötigen ein aktuelles `Softwareschmiede.App.exe`; ein Entfall des Builds würde veraltete Binaries riskieren. Die Korruption entsteht nicht durch die Frequenz, sondern durch gleichzeitige Schreibzugriffe bzw. gesperrte Dateien |
| Umgang mit laufender `Softwareschmiede.App.exe` im Hook | **Erkennen und warnen, niemals beenden** | `CLAUDE.md` (Self-Hosting-Risiko) verbietet das Beenden von `Softwareschmiede.App.exe`. Der Hook darf nur diagnostizieren, damit ein MSB3027-Lock-Fehler nachvollziehbar wird |

## Programmabläufe

### Diagnose bei ausbleibendem MainWindow (Launch-Pfad)

1. `LaunchApp` ermittelt über `ResolveAppLogDirectory()` das Log-Verzeichnis der App (`<exe-Verzeichnis>/logs`).
2. Vor dem Start wird per `AppStartupLogInspector.Snapshot(logDirectory)` der aktuelle Byte-Offset der neuesten `softwareschmiede-*.log` gemerkt (0, falls keine existiert).
3. Die App wird gestartet und `WaitWhileMainHandleIsMissing(30s)` abgewartet.
4. Nach dem Warten prüft `LaunchApp`, ob der Prozess bereits beendet ist (`HasExited`) **oder** kein Hauptfenster-Handle vorhanden ist.
5. Ist das der Fall, wird `CheckAppStartupException()` aufgerufen: liest über den Snapshot-Offset die neu angehängten Log-Zeilen und filtert `[ERR]`/`[FTL]`-Zeilen sowie bekannte Signaturen ("MainWindow konnte nicht angezeigt werden.", "Fehler beim Starten der Anwendung.").
6. Wird eine Startup-Exception gefunden, wirft `LaunchApp` eine `InvalidOperationException` mit dem tatsächlichen Exception-Auszug statt einer nichtssagenden `TimeoutException`.
7. Wird nichts gefunden, bleibt das bisherige Verhalten (Weiterlauf / ggf. späterer Timeout in `WaitForElement`).

Beteiligte Klassen/Komponenten: `WpfTestBase`, `AppStartupLogInspector`

### App-Log-Auslese

1. `GetLatestAppLogContent()` ermittelt über `AppStartupLogInspector` die neueste `softwareschmiede-*.log` im Log-Verzeichnis (Sortierung nach `LastWriteTime`).
2. Die Datei wird mit `FileShare.ReadWrite` geöffnet und ab dem gemerkten Offset gelesen; der angehängte Text wird zurückgegeben (leer, falls keine Datei/kein neuer Inhalt).
3. `CheckAppStartupException()` filtert daraus die Fehlerzeilen und gibt einen kompakten Diagnosetext oder `null` zurück.

Beteiligte Klassen/Komponenten: `WpfTestBase`, `AppStartupLogInspector`

### Robuster Prozess-Teardown (Dispose)

1. `Dispose` ruft wie bisher `_application.Close()`.
2. Neu: Anschließend wird über die Prozess-ID der App auf den vollständigen Prozess-Exit gewartet (`Process.WaitForExit(<timeout>)`), **ohne** den Prozess zu beenden.
3. Bleibt der Prozess nach dem Timeout bestehen, wird das in Debug-Output vermerkt (kein Kill — Self-Hosting-Regel), damit ein nachfolgender Build-Lock nachvollziehbar bleibt.

Beteiligte Klassen/Komponenten: `WpfTestBase`

### Build-Hook: Warnung bei laufender App-Instanz

1. `build_before_test.py` erwirbt wie bisher den Datei-Lock (`dotnet_build_lock`).
2. Neu: Vor `dotnet build` wird geprüft, ob ein Prozess `Softwareschmiede.App.exe` läuft.
3. Läuft eine Instanz, gibt der Hook eine deutliche Warnung aus (mögliche MSB3027/MSB3026-Datei-Locks; Anwender soll ggf. eine unwichtige Testinstanz selbst schließen) — **beendet den Prozess nicht**.
4. `dotnet build` läuft anschließend wie gehabt; der Testlauf zeigt einen echten Build-Fehler selbst.

Beteiligte Klassen/Komponenten: `.claude/hooks/build_before_test.py`

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `AppStartupLogInspector` | Internal static Hilfsklasse (reine Funktionen) | Snapshot des Log-Offsets, Auslesen der neuesten App-Log-Datei ab Offset (mit `FileShare.ReadWrite`), Filtern von Startup-Fehlerzeilen (`[ERR]`/`[FTL]`/bekannte Signaturen) |
| `AppStartupLogInspectorTests` | Testklasse (xUnit) | Unit-Tests für `AppStartupLogInspector` gegen synthetische Log-Dateien |

## Änderungen an bestehenden Klassen

### `WpfTestBase` (abstrakte Test-Basisklasse)

- **Neue Felder:** `_appLogDirectory` (`string?`) — einmalig aufgelöstes Log-Verzeichnis der App; `_appLogOffset` (`long`) — Byte-Offset-Snapshot der Tageslog-Datei vor dem Start.
- **Neue Methoden:**
  - `ResolveAppLogDirectory()` (`private static string`) — leitet `<App-exe-Verzeichnis>/logs` aus dem in `ResolveAppExePath()` gefundenen Pfad ab.
  - `GetLatestAppLogContent()` (`protected string`) — gibt den seit dem Snapshot angehängten Inhalt der neuesten Log-Datei zurück (leer, wenn keiner).
  - `CheckAppStartupException()` (`protected string?`) — gibt einen Diagnosetext der Startup-Fehlerzeilen zurück oder `null`.
- **Geänderte Methoden:**
  - `LaunchApp(bool)` — merkt vor dem Start den Log-Offset; prüft nach `WaitWhileMainHandleIsMissing`, ob Prozess beendet/Hauptfenster fehlt, und wirft bei erkannter Startup-Exception eine `InvalidOperationException` mit dem Log-Auszug.
  - `Dispose()` — wartet nach `Close()` zusätzlich auf den vollständigen Prozess-Exit (ohne Kill).

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine.

## Konfigurationsänderungen

Keine (die Struktur von `.claude/settings.json` bleibt unverändert; geändert wird nur das Hook-Skript `build_before_test.py`).

## Seiteneffekte und Risiken

- **Bestehende E2E-Tests:** `LaunchApp` wirft künftig bei echten Startup-Exceptions früher und mit anderem Exception-Typ (`InvalidOperationException` statt implizitem Timeout). Für erfolgreiche Starts ist das Verhalten unverändert; nur der Fehlerpfad wird aussagekräftiger. Kein Bruch bei grünen Tests.
- **Log-Datei-Zugriff:** Lesen erfolgt read-only mit `FileShare.ReadWrite`; kein Eingriff in Serilogs Schreibzugriff. Fehlt das Log-Verzeichnis, liefert die Diagnose leer und wirft nicht.
- **Dispose-Wartezeit:** Zusätzliches Warten auf Prozess-Exit verlängert den Teardown geringfügig (begrenzt durch Timeout). Kein Kill — Host-Session-Risiko bleibt ausgeschlossen.
- **Build-Hook:** Die Prozess-Erkennung ist rein informativ; sie ändert das Build-Verhalten nicht und kann keine `Softwareschmiede.App.exe` beenden.

## Umsetzungsreihenfolge

1. **`AppStartupLogInspector` anlegen**
   - Voraussetzungen: Keine (nur BCL-`System.IO`).
   - Beschreibung: Reine Hilfsklasse mit Snapshot-, Read-ab-Offset- (`FileShare.ReadWrite`) und Fehlerzeilen-Filter-Funktionen sowie den bekannten Fehlersignaturen.

2. **Unit-Tests `AppStartupLogInspectorTests` schreiben**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: Tests gegen synthetische Log-Dateien im Temp-Verzeichnis (Fehler-Signatur vorhanden/abwesend, Offset-Abgrenzung, keine Datei vorhanden).

3. **`WpfTestBase` um Log-Diagnose erweitern**
   - Voraussetzungen: Schritt 1 (`AppStartupLogInspector`).
   - Beschreibung: Felder, `ResolveAppLogDirectory()`, `GetLatestAppLogContent()`, `CheckAppStartupException()` ergänzen; `LaunchApp` um Offset-Snapshot und Startup-Exception-Prüfung erweitern.

4. **`WpfTestBase.Dispose` um Prozess-Exit-Wartelogik erweitern**
   - Voraussetzungen: Keine.
   - Beschreibung: Nach `Close()` auf vollständigen Prozess-Exit warten (ohne Kill), Timeout in Debug-Output vermerken.

5. **Build-Hook `build_before_test.py` um App-Prozess-Warnung erweitern**
   - Voraussetzungen: Keine.
   - Beschreibung: Vor `dotnet build` laufende `Softwareschmiede.App.exe` erkennen und warnen; kein Kill.

6. **Vollständigen Build + Tests ausführen**
   - Voraussetzungen: Schritte 1–5.
   - Beschreibung: `dotnet build` (voll), dann Unit-Tests; E2E-Tests lokal zur Regressionsprüfung.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `CheckAppStartupException_ErkenntMainWindowFehler` | `AppStartupLogInspectorTests` | Log mit `[ERR] MainWindow konnte nicht angezeigt werden.` liefert den Fehlerauszug |
| `CheckAppStartupException_OhneFehler_LiefertNull` | `AppStartupLogInspectorTests` | Log ohne `[ERR]`/`[FTL]` liefert `null`/leer |
| `GetNewEntries_LiestNurInhaltNachOffset` | `AppStartupLogInspectorTests` | Nur nach dem Snapshot-Offset angehängte Zeilen werden ausgewertet (alte Zeilen ignoriert) |
| `ReadLatestLog_KeinVerzeichnis_LiefertLeer` | `AppStartupLogInspectorTests` | Fehlendes Log-Verzeichnis führt nicht zu Exception, sondern zu leerem Ergebnis |
| `ReadLatestLog_WaehltNeuesteDatei` | `AppStartupLogInspectorTests` | Bei mehreren `softwareschmiede-*.log` wird die zuletzt geänderte gewählt |

### Betroffene bestehende Tests

Keine (Signaturen bestehender Methoden bleiben stabil; grüne E2E-Tests verhalten sich unverändert).

### E2E-Tests (Pflicht)

Die Änderung betrifft ausschließlich Test-Infrastruktur und Diagnose — es entsteht **keine neue Endanwender-UI-Interaktion**, die per FlaUI-E2E abbildbar wäre. Der Happy Path (App startet, MainWindow erscheint) ist bereits durch die bestehenden E2E-Tests (`WpfE2EPlaceholderTests`, `ProjectDetailE2ETests`) abgedeckt und bleibt unverändert grün. Der neue Fehlerpfad (Startup-Exception → aussagekräftige Meldung) ist deterministisch nur über die Unit-Tests von `AppStartupLogInspector` prüfbar, da ein realer `XamlParseException`-Crash nicht zuverlässig erzwingbar ist.

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| App startet, MainWindow erscheint (Regression) | `WpfE2EPlaceholderTests` (bestehend) | Erfolgreicher Start bleibt unverändert grün |
| Startup-Fehler wird aus Log als Klartext gemeldet | `AppStartupLogInspectorTests` (Ersatz für nicht erzwingbaren E2E-Crash) | Diagnose statt "element not found" |

Betroffene bestehende E2E-Tests: Keine.

## Offene Punkte

| # | Offener Punkt | Empfohlener Vorschlag |
|---|---------------|----------------------|
| 1 | Build-Hook-Frequenz: Build vor *jedem* `dotnet test`? | Beibehalten — E2E braucht aktuelles Binary; Korruptionsursache ist der Lock-Konflikt, nicht die Frequenz (siehe Designentscheidung). |
| 2 | Aktives Prozessmanagement blockierender `Softwareschmiede.App.exe` im Hook? | Nur erkennen und warnen, nicht beenden (`CLAUDE.md` Self-Hosting-Regel). |
| 3 | Log-Zugriff/Archivierung durch `WpfTestBase` zulässig? | Read-only mit `FileShare.ReadWrite`; keine Archivierung nötig (Serilog-Retention 14 Tage genügt für Diagnose). |
| 4 | Retry-Strategie für flaky Fenster-Erkennung? | Vorerst kein zusätzliches Retry — die Diagnose unterscheidet nun "echter Crash" von "fehlendes Fenster"; erst bei belegter Rest-Flakiness ein kurzes Retry ergänzen. |
| 5 | Reproduzierbarkeit (lokal vs. CI/Hardware)? | Nach Umsetzung E2E mehrfach lokal laufen lassen; die neuen Log-Diagnosen liefern die Datenbasis zur endgültigen Ursachenklärung. |
