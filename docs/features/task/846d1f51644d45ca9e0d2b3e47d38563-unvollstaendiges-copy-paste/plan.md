# Umsetzungsplan - Vollstaendiges Copy & Paste in der Pseudokonsole

## Zielbild

Laengere mehrzeilige Clipboard-Inhalte werden als stabile Momentaufnahme in die aktive Pseudokonsolen-Sitzung geschrieben und kommen dort vollstaendig, in richtiger Reihenfolge und mit erhaltener Zeilenstruktur an. Der Paste-Pfad verliert keine Stacktrace-Zeilen, Sonderzeichen, Pfade, generischen Typnamen oder Klammern mehr.

Die Korrektur liegt im gemeinsamen Terminal-/Pseudokonsolen-Eingabepfad und wirkt damit fuer Claude sowie fuer andere CLI-Plugins, die dieselbe `PseudoConsoleSession` nutzen. Normale Tastatureingaben bleiben funktional unveraendert.

## Entscheidungen

- Die robuste Schreiblogik wird in `PseudoConsoleSession` gebuendelt, nicht privat im `TerminalControl`, weil dort Session-Lebenszyklus, Input-Stream, Flush und Serialisierung zusammenlaufen.
- Clipboard-Paste nutzt kuenftig eine zentrale asynchrone Methode fuer laengere Eingaben, z. B. `WriteClipboardTextAsync` oder `WriteInputAsync`.
- Der Clipboard-Text wird vor dem Schreiben im UI-Handler gelesen und die Zielsession wird vor dem asynchronen Write in einer lokalen Variable snapshotet.
- Schreibvorgaenge auf den Session-Input werden pro Session serialisiert, damit Paste, Promptversand und andere laengere Writes nicht konkurrierend in denselben Stream schreiben.
- Lange Paste-Inhalte werden kontrolliert in Chunks geschrieben. Jeder Chunk wird awaited; am Ende wird geflusht.
- Vollstaendige Clipboard-Inhalte werden nicht geloggt. Diagnose beschraenkt sich auf Laengen, Chunk-Indizes und Fehlerkontext.

## Umsetzungsschritte

### 1. Zentrale Input-Schreibmethode in PseudoConsoleSession einfuehren

1. `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs` um eine private `SemaphoreSlim` fuer Input-Writes erweitern, z. B. `_inputWriteLock`.
2. Eine zentrale Methode fuer bereits kodierte Eingabebytes ergaenzen, z. B.:
   - `Task WriteInputAsync(ReadOnlyMemory<byte> bytes, CancellationToken ct = default)`
   - sequentieller Zugriff ueber `_inputWriteLock.WaitAsync(ct)`;
   - Abbruch, wenn die Session bereits disposed ist;
   - Schreiben in stabiler Reihenfolge;
   - `InputStream.FlushAsync(ct)` nach erfolgreichem Write;
   - `MarkInputActivity()` nur nach erfolgreichem Schreiben.
3. Fuer lange Eingaben eine Chunk-Groesse als Konstante definieren, z. B. `4096` oder `8192` Bytes.
4. In `WriteInputAsync` grosse Bytefolgen ueber Slices von `ReadOnlyMemory<byte>` schreiben:
   - jeder Chunk wird mit `await InputStream.WriteAsync(chunk, ct).ConfigureAwait(false)` abgeschlossen;
   - der naechste Chunk startet erst danach;
   - die Konkatenation aller Chunks entspricht exakt der Eingabe.
5. Fehler mit Laenge, Chunk-Index und Chunk-Anzahl loggen, ohne Inhalt des Clipboard-Texts zu protokollieren.
6. `_inputWriteLock` in `Dispose()` freigeben, nachdem konkurrierende Dispose-Aufrufe weiterhin durch `_disposed` geschuetzt bleiben.

### 2. Promptversand auf denselben Schreibpfad umstellen

1. `PseudoConsoleSession.WritePromptAsync` behaelt die fachliche Normalisierung:
   - `NormalizeToCarriageReturn(prompt).TrimEnd('\r') + "\r"`
   - UTF-8-Kodierung.
2. Danach ruft `WritePromptAsync` die neue zentrale `WriteInputAsync`-Methode auf.
3. Damit erhalten Promptversand und Clipboard-Paste dieselbe Serialisierung, denselben Flush und dieselbe Aktivitaetsmarkierung.
4. Bestehende Tests zu `WritePromptAsync` muessen unveraendert gruen bleiben.

### 3. TerminalControl-Paste auf stabile Zielsession umstellen

1. `src/Softwareschmiede.App/Controls/TerminalControl.cs` im `Ctrl+V`-Pfad die aktuelle Session in eine lokale Variable uebernehmen.
2. `ReadClipboardAndInsertAsync` so anpassen, dass die Zielsession als Parameter uebergeben wird, z. B. `ReadClipboardAndInsertAsync(PseudoConsoleSession session)`.
3. Nach dem Clipboard-Lesen nicht erneut ueber `Session` auf die moeglicherweise gewechselte aktive Session zugreifen.
4. Clipboard-Text weiterhin ueber `KeyToVt100Encoder.EncodeClipboardText(text)` kodieren, damit die vorhandene CR-Normalisierung erhalten bleibt.
5. Statt `Session.InputStream.WriteAsync(...)` die neue zentrale Session-Methode aufrufen.
6. Fehler weiterhin im Control loggen, damit UI-Fehler nicht propagieren. Falls die Session-Methode selbst loggt und rethrowt, faengt das Control die Exception und schreibt die bestehende Warnung.
7. Die private `WriteToInputStreamAsync`-Methode entfernen oder nur noch als duennen Wrapper auf die Session-Methode behalten, falls bestehende Tests/Struktur davon profitieren.

### 4. Normale Tastatureingaben abgrenzen

1. Pruefen, ob `TerminalControl.WriteToInputStream` fuer einzelne Tastendruecke synchron bleiben soll.
2. Falls synchrone Tastatureingaben unveraendert bleiben, im Plan-Review akzeptieren, dass nur laengere asynchrone Eingaben serialisiert werden.
3. Falls konkurrierende Tastatureingaben waehrend eines laufenden Paste-Vorgangs ebenfalls relevant sind, `WriteToInputStream` entweder:
   - auf eine nicht blockierende fire-and-forget-Variante der zentralen Methode umstellen; oder
   - mit einem kurzen synchronen Lock-Pfad gegen denselben Session-Lock absichern.
4. Die bevorzugte Umsetzung ist, Tastatureingaben funktional unveraendert zu lassen und mindestens Paste/Prompt/Startbefehle zu serialisieren, weil die Anforderung den Paste-Verlust betrifft und normale Tastatur-Latenz nicht verschlechtert werden darf.

### 5. Optionalen Startbefehlspfad pruefen

1. `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs` schreibt den initialen Plugin-Befehl direkt auf `session.InputStream`.
2. Pruefen, ob dieser Pfad auf `PseudoConsoleSession.WriteInputAsync` umgestellt werden kann, ohne die bestehende `command + "\r\n"`-Semantik unbeabsichtigt zu aendern.
3. Wenn keine sichere Umstellung moeglich ist, diesen Pfad unveraendert lassen und im Code-Review als bewusst ausgeklammert dokumentieren.
4. Wenn umgestellt wird, die Bytes vorab mit der bisherigen CRLF-Endung erzeugen und nur die Schreib-/Flush-/Serialisierungslogik wiederverwenden.

### 6. Unit-Tests fuer zentrale Session-Schreiblogik ergaenzen

1. Neue Tests in `src/Softwareschmiede.Tests/Infrastructure/Terminal/PseudoConsoleSessionTests_WritePromptAsync.cs` oder einer neuen Datei `PseudoConsoleSessionTests_WriteInputAsync.cs`.
2. Test fuer langen mehrzeiligen Stacktrace-aehnlichen Text:
   - Text mit vielen Zeilen, Pfaden, Klammern, Backticks, generischen Typnamen und Umlauten erzeugen;
   - ueber denselben Encoder wie Clipboard-Paste kodieren oder erwartete normalisierte Bytes bauen;
   - `WriteInputAsync` auf eine kontrollierte Memory-/Recording-Stream-Session ausfuehren;
   - exakt erwartete Bytes vergleichen.
3. Test fuer Chunk-Reihenfolge:
   - Eingabe groesser als die definierte Chunk-Groesse;
   - Recording-Stream erfasst jeden `WriteAsync`-Aufruf;
   - Anzahl der Chunks und Konkatenation pruefen.
4. Test fuer Flush:
   - Teststream zaehlt `FlushAsync`;
   - nach erfolgreichem `WriteInputAsync` muss genau ein abschliessender Flush erfolgt sein.
5. Test fuer Serialisierung:
   - ein blockierender Recording-Stream haelt den ersten Write kontrolliert an;
   - zwei parallele `WriteInputAsync`-Aufrufe starten;
   - pruefen, dass die zweite Eingabe erst nach Abschluss der ersten geschrieben wird und die Bytes nicht ineinander verschachtelt sind.
6. Test fuer Fehlerpfad:
   - Stream wirft beim Write oder Flush;
   - Methode propagiert oder loggt gemaess Implementierungsentscheidung konsistent;
   - `MarkInputActivity` wird bei Fehler nicht faelschlich gesetzt.

### 7. TerminalControl-Tests anpassen und erweitern

1. `src/Softwareschmiede.Tests/App/Controls/TerminalControlTests.ClipboardPaste.cs` an die neue `ReadClipboardAndInsertAsync(PseudoConsoleSession)`-Signatur anpassen.
2. Bestehende Tests fuer `Ctrl+V`, leere Zwischenablage, Fehlerlogging und Aktivitaetsmarkierung erhalten.
3. Neuen Test fuer Zielsession-Stabilitaet ergaenzen:
   - Clipboard-Text setzen;
   - Paste fuer Session A starten;
   - `control.Session` vor Abschluss auf Session B wechseln;
   - pruefen, dass Bytes in Session A und nicht in Session B landen.
4. Neuen Test fuer langen mehrzeiligen Clipboard-Inhalt ueber `TerminalControl` ergaenzen:
   - Stacktrace-aehnlicher Text;
   - `Ctrl+V` oder Reflection-Aufruf;
   - erwartete vollstaendige normalisierte Bytes im Input-Stream.
5. Falls der Test auf echte Windows-Clipboard-API zugreift, bestehende `OsInterfaceFact`- und STA-Helfer weiterverwenden.

### 8. Diagnose und Logging begrenzen

1. Bestehende Warnlogs bei Clipboard- und Input-Fehlern beibehalten.
2. Bei erfolgreichen Pastedurchlaeufen hoechstens Debug-Level mit Zeichen-/Byte-Laenge und Chunk-Anzahl verwenden.
3. Niemals den Clipboard-Inhalt selbst loggen.
4. Fehlerlogs sollen genug Kontext enthalten, um partielle Writes zu erkennen:
   - Gesamtbytes;
   - Chunk-Index;
   - Chunk-Anzahl;
   - ob Write oder Flush fehlgeschlagen ist.

## Akzeptanzkriterien

- Ein langer mehrzeiliger Clipboard-Text wird vollstaendig in den Input-Stream der Pseudokonsole geschrieben.
- Zeilenumbrueche werden weiterhin wie bisher auf einzelne `\r` normalisiert.
- Sonderzeichen, Umlaute, Windows-Pfade, Backticks, generische Typnamen und Klammern bleiben erhalten.
- Bei internem Chunking bleibt die Reihenfolge aller Chunks erhalten.
- Jeder Chunk wird abgeschlossen, bevor der naechste Chunk geschrieben wird.
- Nach erfolgreichem Paste erfolgt ein Flush.
- Parallele laengere Writes auf dieselbe Session koennen sich nicht gegenseitig ueberholen oder verschachteln.
- Ein laufender Paste schreibt weiter in die beim Start snapshotete Session, auch wenn das UI-Control inzwischen eine andere Session anzeigt.
- Normale Tastatureingaben in der Pseudokonsole bleiben nutzbar und werden nicht spuerbar verlangsamt.
- Die Korrektur wirkt fuer Claude und andere CLI-Plugins, die ueber die gemeinsame `PseudoConsoleSession` laufen.

## Risiken und Gegenmassnahmen

- Risiko: Eine zu kleine Chunk-Groesse erzeugt viele Writes und kann Paste spuerbar verlangsamen.
  Gegenmassnahme: moderate Chunk-Groesse waehlen und nur fuer grosse Eingaben slicen.
- Risiko: Eine zu grosse Chunk-Groesse laesst das urspruengliche Pipe-/Throughput-Problem bestehen.
  Gegenmassnahme: Tests mit Eingaben deutlich oberhalb der Chunk-Groesse und manueller Claude-Paste-Reproduktion.
- Risiko: Serialisierung kann Promptversand und Paste in seltenen Faellen hintereinander warten lassen.
  Gegenmassnahme: Lock pro Session begrenzen und nur um den eigentlichen Input-Write halten.
- Risiko: Dispose waehrend eines laufenden Paste-Vorgangs fuehrt weiterhin zu einer Exception nach partiellem Write.
  Gegenmassnahme: Fehler sichtbar loggen, Session-Disposed-Zustand vor Start pruefen und Tests fuer Fehlerpfade ergaenzen.
- Risiko: Direkte `InputStream`-Nutzung an anderen Stellen umgeht die neue Serialisierung.
  Gegenmassnahme: alle Treffer von `InputStream.Write`/`WriteAsync` pruefen und entweder umstellen oder bewusst dokumentieren.
- Risiko: WPF-Clipboard-Tests bleiben wegen der systemweiten Zwischenablage timinganfaellig.
  Gegenmassnahme: zentrale Logik hauptsaechlich ohne echte Clipboard-API testen; Clipboard-Tests nur fuer UI-Verkabelung verwenden.

## Validierung

- `dotnet test`
- Gezielte Testlaeufe:
  - `dotnet test --filter PseudoConsoleSessionTests_WriteInputAsync`
  - `dotnet test --filter TerminalControlTests`
- Manuelle Pruefung in der Anwendung:
  - Claude-CLI ueber die Pseudokonsole starten;
  - langen mehrzeiligen .NET-Stacktrace einfuegen;
  - pruefen, dass Anfang, Mitte und Ende vollstaendig sichtbar bzw. bei der CLI angekommen sind;
  - denselben Paste nach Session-Wechsel/Zurueckwechsel wiederholen;
  - kurze normale Tastatureingaben und Enter pruefen.

## Offene Punkte

Keine.
