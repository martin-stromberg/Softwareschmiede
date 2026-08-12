# Pseudokonsole und Eingabestream

## Relevante Dateien

- `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs`
- `src/Softwareschmiede/Infrastructure/Terminal/Win32PseudoConsoleProcessLauncher.cs`
- `src/Softwareschmiede/Infrastructure/Terminal/IPseudoConsoleProcessLauncher.cs`
- `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`
- `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsole.cs`
- `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleProcessStarter.cs`

## Session-Modell

`PseudoConsoleSession` kapselt die native Pseudokonsole, den Prozess, den schreibbaren `InputStream`, den lesbaren `OutputStream`, Terminal-Buffer, Runtime-Status und Leseschleife. Die Leseschleife startet im Konstruktor und verarbeitet Output unabhaengig vom gebundenen UI-Control.

`Win32PseudoConsoleProcessLauncher` erstellt die native ConPTY-Instanz mit 220x50 Zellen, startet `cmd.exe`, baut `FileStream`-Wrapper fuer Input und Output und erzeugt daraus die `PseudoConsoleSession`.

Der Input-Stream wird als `FileStream` ueber `pseudoConsole.InputWritePipe` angelegt:

- `FileAccess.Write`
- `bufferSize: 1`
- `isAsync: false`

Trotz `isAsync: false` ruft der Clipboard-Pfad `WriteAsync` auf diesem Stream auf. Das ist funktional erlaubt, kann aber je nach Stream-Implementierung synchron laufen und sollte nicht als echte asynchrone Pipe-Flusskontrolle interpretiert werden.

## Gemeinsame Schreibpfade

Mehrere Stellen schreiben in denselben Session-Input:

- `TerminalControl.WriteToInputStream` fuer Tasten und Textinput, synchron.
- `TerminalControl.WriteToInputStreamAsync` fuer Clipboard-Paste, asynchron.
- `PseudoConsoleSession.WritePromptAsync` fuer Promptversand, asynchron mit `FlushAsync`.
- `KiAusfuehrungsService.SendCommandDelayedAsync` fuer den initialen Plugin-Befehl an `cmd.exe`, asynchron mit `FlushAsync`.

Eine gemeinsame Queue, ein `SemaphoreSlim` pro Session oder eine andere Serialisierung fuer diese Writes wurde nicht gefunden.

## Start- und Lebenszyklus

`KiAusfuehrungsService.StartWithPseudoConsoleAsync` startet die ConPTY-Sitzung ueber `IPseudoConsoleProcessLauncher`. Danach wird ein verzogerter initialer Plugin-Befehl an `cmd.exe` gesendet. Fuer sehr kurzlebige Prozesse existiert Schutzlogik ueber `SendCts` und `CancelAndDisposeConPtyResourcesAsync`.

Beim Prozessende wird die Session gedraint und disposed. `PseudoConsoleSession.Dispose` schliesst unter anderem `OutputStream`, `InputStream`, die Pseudokonsole und den Prozess. Ein Paste, der waehrenddessen schreibt, endet in einer geloggten Exception; ob bereits ein Teil der Bytes beim Kindprozess angekommen ist, wird nicht diagnostiziert.

## Auffaellige Punkte

- `PseudoConsoleSession` bietet fuer laengere Eingaben nur `WritePromptAsync`, aber keinen generischen `WriteInputAsync`-/`PasteAsync`-Pfad.
- `WritePromptAsync` normalisiert Zeilenumbrueche, schreibt, flusht und markiert Eingabeaktivitaet. Clipboard-Paste dupliziert einen Teil dieses Verhaltens im UI-Control, aber ohne Flush und ohne zentrale Serialisierung.
- `SendCommandDelayedAsync` verwendet `command + "\r\n"`, waehrend Tastatur, Clipboard und `WritePromptAsync` auf einzelnes `\r` setzen. Das betrifft zwar den Startbefehl und nicht den Nutzer-Paste, zeigt aber uneinheitliche Input-Konventionen im gemeinsamen Stream.
- Wenn Chunking eingefuehrt wird, sollte es in der Session-Schicht liegen, weil dort Prozess-/Dispose-Zustand, Logging und Serialisierung gebuendelt werden koennen.

## Relevanz fuer die Anforderung

Die nicht deterministische Natur des Fehlers passt zu konkurrierenden oder unvollstaendig beobachteten Stream-Writes. Eine robuste Korrektur sollte auf Session-Ebene sicherstellen, dass lange Clipboard-Inhalte vollstaendig, sequentiell, optional in Chunks und mit abschliessendem Flush geschrieben werden.
