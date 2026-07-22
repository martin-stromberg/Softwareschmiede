# KiAusfuehrungsService und Prozess-Lifecycle

## Rolle im CLI-Start

`KiAusfuehrungsService` ist ein Singleton und verwaltet laufende CLI-Prozesse pro Aufgabe in `_handles` (`src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs:17`, `:19`). Der klassische Startpfad `StartCliAsync` existiert weiterhin, der fuer die UI relevante Terminalpfad nutzt aber `StartWithPseudoConsoleAsync`.

`StartWithPseudoConsoleAsync`:

- verhindert parallelen Doppelstart fuer dieselbe Aufgabe (`KiAusfuehrungsService.cs:191`)
- ermittelt das effektive Arbeitsverzeichnis (`:204`)
- fragt das KI-Plugin nach `ProcessStartInfo` (`:207`)
- baut daraus den Plugin-Befehl fuer die interaktive `cmd.exe` (`:208`, `:576`)
- startet ConPTY ueber `_launcher.Start(aufgabeId, effectiveWorkdir, pluginCommand)` (`:210`)
- speichert Session und Prozess im `CliProcessHandle` (`:219`, `:226`)
- feuert `CliProcessStatusChanged(..., Gestartet)` (`:241`)
- sendet den Plugin-Befehl nach kurzer Verzoegerung in die Session (`:243`, `:246`, `:526`)

## DI- und Scope-Aspekt

`KiAusfuehrungsService` ist Singleton, `ProtokollService` ist Scoped (`src/Softwareschmiede.App/App.xaml.cs:171`, `:218`). Fuer Protokollpersistenz aus einer lang laufenden Session darf daher kein scoped `ProtokollService` direkt im Singleton gehalten werden. Es gibt bereits ein Muster: `PersistFehlgeschlagenAsync` erzeugt bei Prozessfehlern einen Async-Scope ueber `_scopeFactory` und holt darin `ProtokollService` (`KiAusfuehrungsService.cs:352`, `:364`, `:374`).

Dieses Muster ist ein naheliegender Anknuepfungspunkt fuer CLI-Output-Persistenz, sofern die Persistenz im oder neben dem `KiAusfuehrungsService` angesiedelt wird.

## Prozessende und Ressourcen

Beim Prozessende entfernt `HandleProcessExited` das Handle, ermittelt Exit-Code, ruft optional ConPTY-Aufraeumlogik auf und feuert Statusereignisse (`KiAusfuehrungsService.cs:404`, `:410`, `:417`, `:419`, `:444`). Fuer ConPTY ruft `CancelAndDisposeConPtyResources` unter anderem `handle.PseudoConsoleSession?.Dispose()` auf (`:556`, `:567`).

Eine Output-Protokollierung muss mit dem Dispose-Pfad kompatibel bleiben:

- keine blockierenden Waits im Exited-Handler,
- keine Verwendung bereits disposeter Streams/Scopes,
- ausstehende Persistenzaufgaben muessen Fehler loggen statt Prozessende zu blockieren oder die App zu crashen.

## Start ueber EntwicklungsprozessService

`EntwicklungsprozessService.ProzessStartenUndCliStartenAsync` startet zuerst den Entwicklungsprozess und ruft danach `KiAusfuehrungsService.StartWithPseudoConsoleAsync` mit `aufgabeId`, Plugin und Repository-Startkonfiguration auf (`src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs:120`, `:134`, `:161`). Das ist der normale Aufgabenstart aus der Detailansicht.

