# Implementierungsrelevante Risiken und offene Entscheidungen

## Architekturentscheidung: Wo wird persistiert?

Naheliegende Varianten:

1. `PseudoConsoleSession` erhaelt eine optionale Output-Senke, z. B. Callback/Interface, die pro dekodierter Zeile oder Chunk aufgerufen wird.
2. `KiAusfuehrungsService` abonniert ein neues Output-Event der Session und persistiert ueber `IServiceScopeFactory`.
3. `TerminalControl` oder `TaskDetailViewModel` persistiert Ausgabe bei `BufferChanged`.

Variante 3 erfuellt die Anforderung nicht zuverlaessig, weil die Session unabhaengig von der UI weiterlaufen kann. Variante 1/2 halten die Persistenz im Ausgabepfad und koennen auch nicht gebundene Sessions erfassen.

## Scoped Service im Singleton

`KiAusfuehrungsService` ist Singleton, `ProtokollService` scoped. Eine Implementierung darf keinen scoped Service dauerhaft im Singleton oder in einer langlebigen Session halten. Das vorhandene Muster in `PersistFehlgeschlagenAsync` nutzt `_scopeFactory.CreateAsyncScope()` pro Persistenzvorgang und ist dafuer relevant.

## Performance und Transaktionszahl

`AddCliOutputAsync` ruft pro Zeile `SaveChangesAsync` auf. Bei CLI-TUIs oder Build/Test-Ausgaben kann das viele Schreibvorgaenge erzeugen. Risiken:

- UI/CLI-Lesepfad darf nicht auf DB-Latenz warten.
- Backpressure darf den Terminalstream nicht blockieren.
- Bei Prozessende duerfen noch gepufferte Zeilen nicht verloren gehen.

Moegliche Planpunkte: asynchrone Queue pro Session, begrenzte Pufferung, Batch-Speicherung oder bewusst einfache direkte Persistenz mit Fehlerlogging, falls Ausgabemengen akzeptabel sind.

## Textrekonstruktion

ConPTY liefert Terminal-Bytes, nicht fachliche Log-Zeilen. ANSI-Sequenzen, Cursorbewegungen, Progressbars und TUI-Ausgaben koennen Rohtext schwer lesbar machen. `TerminalBuffer` ist fuer Historie ungeeignet, der rohe dekodierte Stream enthaelt aber Escape-Sequenzen. Es muss entschieden werden, ob das Protokoll:

- rohe Ausgabe inkl. ANSI speichert,
- ANSI bereinigt,
- oder eine vereinfachte Textausgabe aus Parser-Events erzeugt.

Die bestehende Methode `AddCliOutputAsync` erwartet `outputLine`; sie definiert nicht, ob ANSI enthalten sein darf.

## Zeilen- und Chunk-Grenzen

`OutputStream.ReadAsync` liest bis zu 4096 Bytes pro Chunk. Eine CLI-Zeile kann ueber mehrere Chunks verteilt sein, mehrere Zeilen koennen in einem Chunk liegen. Fuer `AddCliOutputAsync` muss ein Aggregator Chunk-Grenzen korrekt behandeln und am Ende eine Restzeile flushen.

## Aufgaben- und Run-Zuordnung

Das bestehende Protokoll trennt nur per `AufgabeId`. Die Requirement-Datei fragt offen nach Fortschreiben oder Neuerzeugen bei wiederholter Bearbeitung. Aktuell existiert kein Run-Feld in `Protokolleintrag`; ein wiederholter Start wuerde bei gleicher Aufgabe chronologisch an dasselbe Protokoll angehaengt. Das passt zur vorhandenen Datenstruktur, muss aber fachlich bewusst akzeptiert oder modellseitig erweitert werden.

## Datenschutz/Sicherheit

Es gibt keine bestehende Filterung fuer sensible CLI-Ausgabe. Da die Anforderung "saemtliche Ausgaben" fordert, waere ungefilterte Persistenz naheliegend, aber die Requirement-Datei nennt Datenschutz/Sicherheit als offene Frage. Ein Plan sollte zumindest dokumentieren, dass keine Redaction existiert, oder explizite Filterregeln vorsehen.

