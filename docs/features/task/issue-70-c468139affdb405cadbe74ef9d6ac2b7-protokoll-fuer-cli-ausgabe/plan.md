# Umsetzungsplan: CLI-Ausgaben im Aufgabenprotokoll speichern

## Uebersicht

Die ConPTY-Ausgabe wird derzeit nur in `PseudoConsoleSession.ReadLoopAsync` gelesen, per ANSI-Parser in den `TerminalBuffer` geschrieben und ueber `BufferChanged` an die UI gemeldet. Das bestehende Aufgabenprotokoll besitzt mit `ProtokollTyp.CliOutput` und `ProtokollService.AddCliOutputAsync(Guid aufgabeId, string outputLine, ...)` bereits den fachlich passenden Persistenzpfad, ist aber nicht mit der ConPTY-Session verbunden.

Umgesetzt wird eine nicht blockierende Output-Senke an der `PseudoConsoleSession`, die gelesene Output-Bytes vor der ANSI-Anwendung kopiert, robust zu Textzeilen rekonstruiert und diese zeilenweise ueber `ProtokollService.AddCliOutputAsync` im bestehenden Aufgabenprotokoll der jeweiligen `aufgabeId` speichert. Die Anbindung erfolgt in `KiAusfuehrungsService.StartWithPseudoConsoleAsync`, weil dort `aufgabeId`, ConPTY-Session und `IServiceScopeFactory` zusammenkommen.

## Designentscheidungen

| Bereich | Gewaehlter Ansatz | Begruendung |
|---------|-------------------|-------------|
| Persistenzort | Output-Erfassung in `PseudoConsoleSession`, Persistenzanbindung aus `KiAusfuehrungsService` | Die Session liest die vollstaendige Terminalausgabe unabhaengig von der UI. `KiAusfuehrungsService` kennt die `aufgabeId` und darf ueber `IServiceScopeFactory` scoped Services erzeugen. |
| UI als Quelle | Keine Protokollierung in `TerminalControl` oder `TaskDetailViewModel` | Eine Aufgabe kann weiterlaufen, waehrend keine View gebunden ist. UI-basierte Protokollierung wuerde die Akzeptanzkriterien nicht verlaesslich erfuellen. |
| DB-Modell | Bestehendes Modell verwenden: `Protokolleintrag` mit `Typ = CliOutput`, Zuordnung ueber `AufgabeId` | Das Modell und `AddCliOutputAsync` existieren bereits. Es sind keine neuen Felder fuer Run-ID, Stream-Art oder Sequenznummer erforderlich. |
| Wiederholte Bearbeitung | Chronologisch an das bestehende Aufgabenprotokoll anhaengen | Das vorhandene Protokoll ist aufgabenbezogen und nicht laufbezogen. Neue Starts derselben Aufgabe werden ueber Zeitstempel nachvollziehbar fortgeschrieben. |
| Inhalt | Dekodierten Output-Text als Zeilen speichern, inklusive sichtbarer ANSI-/Control-Sequenzen im Stream; keine Redaction | Die Anforderung fordert saemtliche CLI-Ausgaben. Filterung oder Bereinigung koennte relevante Ausgabe entfernen und ist im Bestand nicht vorgesehen. |
| Zeilenaggregation | UTF-8-Decoder mit Zustand ueber Chunk-Grenzen; Split auf `\n`, `\r\n` und einzelne `\r`; Restzeile bei Session-Ende flushen | ConPTY liefert Bytes in beliebigen Chunks. `AddCliOutputAsync` ist zeilenorientiert. Einzelnes `\r` deckt Progress-/Prompt-Ueberschreibungen besser ab als nur `\n`. |
| Threading | Leseschleife blockiert nicht auf DB-I/O; Output-Senke kopiert Chunks schnell in eine interne Queue/Channel | DB-Latenz darf Terminalausgabe, Buffer-Aktualisierung und Prozess-Lifecycle nicht ausbremsen. |
| Scoped DI | Pro gespeicherter Zeile oder kleinem Persistenzvorgang per `_scopeFactory.CreateAsyncScope()` einen `ProtokollService` holen | `KiAusfuehrungsService` ist Singleton, `ProtokollService` scoped. Das folgt dem vorhandenen Muster in `PersistFehlgeschlagenAsync`. |
| Fehlerbehandlung | Persistenzfehler loggen, Terminalausgabe und Prozessende nicht abbrechen | Protokollierung ist wichtig, darf aber nicht die CLI-Session crashen oder Exited/Dispose blockieren. |

## Programmablaeufe

### ConPTY-Output lesen und an Senke melden

1. `KiAusfuehrungsService.StartWithPseudoConsoleAsync(...)` erzeugt vor oder direkt nach `_launcher.Start(...)` eine aufgabenbezogene Output-Senke fuer die `aufgabeId`.
2. Die `PseudoConsoleSession` erhaelt diese Senke optional im Konstruktor.
3. `ReadLoopAsync` liest wie bisher bis zu 4096 Bytes aus `OutputStream`.
4. Nach erfolgreichem Read und vor oder direkt neben der ANSI-Parser-Verarbeitung ruft die Session die Senke mit dem gelesenen Byte-Span auf.
5. Die Senke kopiert die Bytes sofort in ihre eigene Verarbeitung und kehrt ohne DB-Zugriff zurueck.
6. Der bestehende Pfad `MarkOutputActivity()`, `AnsiSequenceParser.Parse(...)`, `Buffer.Apply(...)` und `BufferChanged` bleibt unveraendert sichtbar fuer den Benutzer.
7. Beim Ende der Leseschleife oder Dispose wird die Senke idempotent abgeschlossen, damit eine unvollstaendige Restzeile noch gespeichert werden kann.

Beteiligte Komponenten: `PseudoConsoleSession`, neue Output-Senken-Schnittstelle, `KiAusfuehrungsService`

### Chunk-zu-Zeile-Verarbeitung

1. Die Senke haelt pro Session einen `Decoder` fuer UTF-8, damit Multibyte-Zeichen ueber Chunk-Grenzen korrekt dekodiert werden.
2. Jeder Chunk wird in Text umgewandelt und an einen `StringBuilder` fuer die aktuelle Zeile angehaengt.
3. Bei `\n` wird die aktuelle Zeile abgeschlossen; ein direkt vorangehendes `\r` aus `\r\n` wird nicht doppelt als eigene Zeile gespeichert.
4. Bei einzelnem `\r` wird die aktuelle Zeile ebenfalls abgeschlossen. Das protokolliert Fortschritts-/Statusausgaben, die Zeilen ueberschreiben, statt sie bis zum Prozessende zu verlieren.
5. Leere Zeilen werden gespeichert, wenn sie explizit aus Zeilentrennern entstehen; reine leere Restpuffer am Ende werden nicht kuenstlich erzeugt.
6. Beim Abschluss der Session wird eine vorhandene Restzeile ohne abschliessenden Zeilentrenner gespeichert.

Beteiligte Komponenten: neue Klasse `CliOutputLineAccumulator` oder gleichwertige interne Hilfsklasse

### Persistenz im Aufgabenprotokoll

1. Die zeilenweise Ausgabe wird in einen Hintergrund-Worker pro Session eingereiht.
2. Der Worker liest die Queue sequenziell, damit die Reihenfolge innerhalb einer Session erhalten bleibt.
3. Fuer jede Zeile wird ein Async-Scope erzeugt und `ProtokollService.AddCliOutputAsync(aufgabeId, line, ct)` aufgerufen.
4. Die vorhandene Rate-Limit-Erkennung in `AddCliOutputAsync` bleibt dadurch automatisch aktiv.
5. `ObjectDisposedException` beim App-Shutdown und allgemeine Persistenzfehler werden geloggt; die Worker-Schleife beendet oder ueberspringt den fehlerhaften Eintrag ohne Rueckwirkung auf die Terminalsession.

Beteiligte Komponenten: `KiAusfuehrungsService`, neue Klasse `CliOutputProtokollWriter`, `ProtokollService`

## Neue Klassen und Schnittstellen

| Name | Projekt / Namespace | Typ | Zweck |
|------|---------------------|-----|-------|
| `ITerminalOutputSink` | `Softwareschmiede.Infrastructure.Terminal` | Interface | Optionale, UI-unabhaengige Senke fuer gelesene Terminal-Output-Bytes. Methodisch klein halten, z. B. `OnOutputChunk(ReadOnlySpan<byte> bytes)` und `Complete()`. |
| `CliOutputLineAccumulator` | `Softwareschmiede.Application.Services` oder testbarer interner Helper | Klasse | Dekodiert UTF-8 chunkuebergreifend und liefert abgeschlossene Ausgabezeilen aus `\n`, `\r\n`, `\r` sowie Restzeilen beim Flush. |
| `CliOutputProtokollWriter` | `Softwareschmiede.Application.Services` | Klasse | Implementiert `ITerminalOutputSink`, verarbeitet Chunks nicht blockierend, schreibt Zeilen sequenziell ueber `ProtokollService.AddCliOutputAsync` in scoped DI-Kontexten. |

## Aenderungen an bestehenden Klassen

### `PseudoConsoleSession`

- Konstruktoren um optionalen Parameter `ITerminalOutputSink? outputSink = null` erweitern; bestehende Aufrufer bleiben durch Defaultwert kompatibel.
- Feld fuer die Senke speichern.
- In `ReadLoopAsync` nach jedem erfolgreichen Read `outputSink.OnOutputChunk(data.AsSpan(0, bytesRead))` aufrufen. Der Aufruf muss vor Wiederverwendung des Puffers erfolgen; die Senke kopiert intern.
- In einem `finally`-Block der Leseschleife `outputSink.Complete()` idempotent aufrufen.
- Bestehende Terminal-Buffer-Logik und `BufferChanged`-Reihenfolge unveraendert lassen.

### `KiAusfuehrungsService`

- In `StartWithPseudoConsoleAsync` fuer jede ConPTY-Session einen `CliOutputProtokollWriter` mit `aufgabeId`, `_scopeFactory` und Logger erzeugen und an die Session/den Launcher uebergeben.
- Falls der aktuelle `IPseudoConsoleProcessLauncher.Start(...)` die Session intern konstruiert, die Launcher-Schnittstelle so erweitern, dass eine optionale Output-Senke bis zur `PseudoConsoleSession` durchgereicht werden kann. Alle Launcher-Implementierungen und Tests entsprechend anpassen.
- Den Writer im `CliProcessHandle` referenzieren, damit `CancelAndDisposeConPtyResources` ihn idempotent abschliessen kann.
- Keine scoped `ProtokollService`-Instanz im Singleton speichern; ausschliesslich Scope-Erzeugung im Writer verwenden.

### `CliProcessHandle`

- Optionales Feld oder Property fuer den aufgabenbezogenen `CliOutputProtokollWriter` bzw. die Output-Senke ergaenzen.
- Im Aufraeumpfad nur idempotent abschliessen, nicht synchron auf vollstaendige DB-Persistenz warten.

### `IPseudoConsoleProcessLauncher` und Launcher-Implementierungen

- `Start(Guid aufgabeId, string effectiveWorkingDirectory, string pluginCommand, ITerminalOutputSink? outputSink = null)` oder gleichwertige Signatur verwenden.
- Echte und simulierte Launcher reichen die Senke in den `PseudoConsoleSession`-Konstruktor weiter.
- Test-Launcher mit `MemoryStream` bleiben kompatibel, wenn keine Senke uebergeben wird.

### `ProtokollService`

- Keine fachliche Signaturaenderung erforderlich.
- `AddCliOutputAsync` bleibt die zentrale Persistenzmethode, damit bestehende `CliOutput`- und Rate-Limit-Tests weiter gelten.
- Optional nur falls Tests/Performance es erzwingen: spaeter eine Batch-Methode ergaenzen. Nicht Teil dieses Plans, weil die Anforderung ausdruecklich bestehendes `AddCliOutputAsync` beruecksichtigen soll.

### `TaskDetailViewModel` und UI

- Keine Pflichtaenderung fuer Live-Refresh der Info-Protokollliste.
- Begruendung: Die Akzeptanzkriterien fordern Nachvollziehbarkeit nach Bearbeitung/Unterbrechung. Beim erneuten Laden ruft das ViewModel bereits `GetByAufgabeAsync` auf und erhaelt die persistierten `CliOutput`-Eintraege.
- Optionaler Live-Refresh wird nicht umgesetzt, um den Persistenzpfad nicht an UI-Lebenszyklen zu koppeln.

## Datenbankmigrationen

Keine. Die bestehende Tabelle `Protokolleintraege`, `AufgabeId`, `Typ = CliOutput`, `Inhalt` und `Zeitstempel` werden weiterverwendet.

## Konfigurationsaenderungen

Keine.

## Seiteneffekte und Risiken

- **Mehr DB-Schreibvorgaenge:** `AddCliOutputAsync` speichert pro Zeile mit `SaveChangesAsync`. Der nicht blockierende Writer schuetzt den Terminal-Lesepfad, kann aber bei sehr viel Output eine wachsende Queue erzeugen. Die Implementierung soll Queue-Groesse und Logging beobachten; Batching bleibt eine spaetere Optimierung, falls Tests oder reale Nutzung Bedarf zeigen.
- **ANSI-/Control-Sequenzen im Protokoll:** Da nahe am Rohstream persistiert wird, koennen Escape-Sequenzen und Fortschrittsausgaben enthalten sein. Das ist fuer "saemtliche CLI-Ausgaben" akzeptabel und verhindert Informationsverlust durch den `TerminalBuffer`.
- **Prozessende vs. Restzeilen:** Da `Dispose()` nicht auf die Leseschleife wartet, muss `Complete()` idempotent sein und auch aus dem Aufraeumpfad aufgerufen werden koennen. Tests muessen den Restzeilen-Flush explizit absichern.
- **Shutdown:** Wenn der DI-Container waehrend ausstehender Persistenz bereits disposed ist, wird wie bei `PersistFehlgeschlagenAsync` gewarnt statt geworfen.
- **Parallele Aufgaben:** Jeder Start bekommt eigene Senke und eigene `aufgabeId`. Die Tests muessen absichern, dass Ausgaben nicht zwischen Aufgaben vermischt werden.

## Umsetzungsreihenfolge

1. **Output-Senken-Schnittstelle einfuehren**
   - Voraussetzungen: keine.
   - Beschreibung: `ITerminalOutputSink` im Terminal-Infrastruktur-Namespace definieren und `PseudoConsoleSession` optional damit ausstatten.

2. **`PseudoConsoleSession` an Output-Senke anbinden**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: Chunks in `ReadLoopAsync` an die Senke melden, bestehende Buffer-Verarbeitung unveraendert lassen und `Complete()` im Abschluss der Leseschleife aufrufen.

3. **Zeilenaggregator implementieren**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: UTF-8-Stateful-Decoding, Split auf `\n`, `\r\n`, `\r`, Restzeilen-Flush und deterministische Tests gegen synthetische Chunks.

4. **`CliOutputProtokollWriter` implementieren**
   - Voraussetzungen: Schritte 1 und 3.
   - Beschreibung: `ITerminalOutputSink` implementieren, Chunks kopieren, Zeilen sequenziell in einer Hintergrundverarbeitung speichern, pro Persistenzvorgang Async-Scope erzeugen und Fehler loggen.

5. **Launcher- und Service-Anbindung ergaenzen**
   - Voraussetzungen: Schritt 4.
   - Beschreibung: `IPseudoConsoleProcessLauncher.Start(...)` um die optionale Senke erweitern, echte/simulierte Launcher anpassen, Writer in `KiAusfuehrungsService.StartWithPseudoConsoleAsync` erzeugen und im `CliProcessHandle` hinterlegen.

6. **Aufraeumpfad robust machen**
   - Voraussetzungen: Schritt 5.
   - Beschreibung: `CancelAndDisposeConPtyResources` und Service-Dispose rufen den Writer/Sink idempotent zum Abschluss auf, ohne synchron auf DB-I/O zu warten.

7. **Tests erweitern**
   - Voraussetzungen: Schritte 1-6.
   - Beschreibung: Unit- und Integrationstests fuer Aggregation, automatische Persistenz, parallele Aufgaben, fehlende UI-Bindung und Fehlerpfade ergaenzen.

8. **Build und relevante Tests ausfuehren**
   - Voraussetzungen: Schritte 1-7.
   - Beschreibung: Voller Build und fokussierte Testklassen fuer Terminal, Protokollservice und `KiAusfuehrungsService`. Bei ConPTY-abhaengigen Tests Projektregeln zur lokalen Ausfuehrung beachten.

## Tests

### Neue Tests

| Test | Testklasse | Was wird geprueft |
|------|------------|-------------------|
| `Chunks_MitMehrerenLfZeilen_LiefertZeilenInReihenfolge` | neue `CliOutputLineAccumulatorTests` | Ein Chunk mit mehreren `\n`-Zeilen wird in korrekter Reihenfolge ausgegeben. |
| `Chunks_MitGeteilterZeile_UeberChunkGrenze_LiefertEineZeile` | `CliOutputLineAccumulatorTests` | Teilstrings aus mehreren Chunks werden vor `AddCliOutputAsync` korrekt zusammengefuehrt. |
| `Chunks_MitCrLf_ZaehltNichtDoppelt` | `CliOutputLineAccumulatorTests` | Windows-Zeilenende `\r\n` erzeugt genau einen Protokolleintrag. |
| `Chunks_MitEinzelnemCr_FlushtProgressZeile` | `CliOutputLineAccumulatorTests` | Progress-/Statusausgaben mit einzelnem Carriage Return werden nicht bis zum Prozessende verloren. |
| `Chunks_MitUtf8MultibyteGrenze_DekodiertKorrekt` | `CliOutputLineAccumulatorTests` | UTF-8-Zeichen ueber Chunk-Grenzen bleiben korrekt erhalten. |
| `Flush_MitRestzeile_SpeichertUnvollstaendigeLetzteZeile` | `CliOutputLineAccumulatorTests` | Ausgabe ohne abschliessenden Zeilentrenner landet beim Session-Ende im Protokoll. |
| `ReadLoopAsync_MeldetOutputChunksAnSink_UndAktualisiertBufferWeiterhin` | `PseudoConsoleSessionTests` | Senke wird aufgerufen, `BufferChanged` bleibt nach Buffer-Update erhalten. |
| `StartWithPseudoConsoleAsync_PersistiertSessionOutputAlsCliOutput` | `KiAusfuehrungsServiceTests` oder ServiceIntegration | Simulierter ConPTY-Output fuehrt automatisch zu `CliOutput`-Eintraegen fuer dieselbe `aufgabeId`. |
| `StartWithPseudoConsoleAsync_ParalleleAufgaben_TrenntProtokolleNachAufgabeId` | `KiAusfuehrungsServiceTests` oder ServiceIntegration | Zwei Sessions speichern in getrennte Aufgabenprotokolle. |
| `CliOutputProtokollWriter_Persistenzfehler_BeeintraechtigtSessionNicht` | neue Writer-Tests | Exception aus Scope/`ProtokollService` wird geloggt und wirft nicht in den Lesepfad. |
| `StartWithPseudoConsoleAsync_OutputOhneTerminalControl_WirdPersistiert` | Service-/Integrationstest | Persistenz haengt nicht an UI-Bindung oder `TerminalControl`. |
| `AddCliOutputAsync_RateLimitMarkerAusConPtyOutput_ErzeugtRateLimitEintrag` | ServiceIntegration | Bestehende Rate-Limit-Erkennung bleibt ueber den neuen Pfad aktiv. |

### Betroffene bestehende Tests

| Testbereich | Erwartung |
|-------------|-----------|
| `PseudoConsoleSessionTests` | Konstruktoraufrufe ggf. um optionalen Parameter erweitern oder unveraendert lassen, wenn Defaultwert genutzt wird. Bestehende Dispose-/BufferChanged-Tests muessen gruen bleiben. |
| `KiAusfuehrungsServiceTests` | Test-Launcher und Mocks an neue `IPseudoConsoleProcessLauncher.Start`-Signatur anpassen. Bestehende Prozessende- und Dispose-Tests muessen weiterhin ohne blockierende Wartezeiten laufen. |
| `ProtocolLoggingServiceIntegrationTests` und `ProtokollServiceTests` | Keine Verhaltensaenderung an `AddCliOutputAsync`; bestehende Tests bleiben unveraendert relevant. |
| `TerminalControlTests` | Keine fachliche Aenderung erwartet, da UI nur rendert und nicht persistiert. |

### E2E-Tests

Ein neuer UI-E2E-Test ist fuer diese Anforderung nicht sinnvoll als primaerer Nachweis, weil die Kernlogik im service-/sessionnahen Hintergrundpfad liegt und ein echter ConPTY-Prozess in E2E schwer deterministisch Output, Timing und DB-Zustand kontrollierbar macht. Die Akzeptanzkriterien werden deterministisch durch Service-/Integrationstests mit simuliertem ConPTY-Output abgedeckt. Bestehende E2E-Tests dienen nur als Regression, dass die Terminalansicht weiter startet und sichtbar bleibt.

## Akzeptanzkriterien-Abdeckung

| Akzeptanzkriterium | Abdeckung im Plan |
|--------------------|-------------------|
| CLI-Ausgaben waehrend einer Aufgabe werden gespeichert | `PseudoConsoleSession` meldet Chunks an `CliOutputProtokollWriter`, der `AddCliOutputAsync(aufgabeId, ...)` aufruft. |
| Relevante Ausgaben ueber den gesamten Bearbeitungsverlauf | Erfassung sitzt in der Session-Leseschleife, nicht in der UI, und laeuft bis Stream-Ende/Dispose mit Restzeilen-Flush. |
| Nach Bearbeitung nachvollziehbar | Persistenz erfolgt im bestehenden Aufgabenprotokoll; `GetByAufgabeAsync` laedt chronologisch. |
| Mehrere Aufgaben getrennt | Writer wird pro Session mit konkreter `aufgabeId` erzeugt; parallele-Aufgaben-Test sichert Trennung ab. |

## Offene Punkte

Keine.
