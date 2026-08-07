# Laufstatus und Runtime-Status

## Domain-Modell

`Aufgabe` enthaelt neben dem groben `AufgabeStatus` drei Felder fuer aktive CLI-Laeufe:

- `AktiveRunId`: gesetzt, solange ein aktiver Lauf bekannt ist
- `LastHeartbeatUtc`: letzter Heartbeat des aktiven Laufs
- `LaufStatus`: optionaler Substatus der aktiven CLI-Ausfuehrung

`AufgabeLaufStatus` kennt zwei Werte:

- `Laeuft`
- `WartetAufEingabe`

Der Enum ist bewusst von `CliRuntimeStatus` getrennt, damit die Domain-Schicht nicht von Infrastructure abhaengt.

`AufgabeLaufAktivitaet.IstAktiv(...)` wertet nur `AktiveRunId` und einen frischen Heartbeat aus. `LaufStatus` entscheidet danach nur, ob die Anzeige "Laeuft" oder "Wartet" lautet.

## Persistenz im AufgabeService

`AufgabeService.AktivenLaufSetzenAsync` setzt:

- `AktiveRunId`
- `LastHeartbeatUtc`
- `LetzterCliStartUtc`
- `LaufStatus = AufgabeLaufStatus.Laeuft`

`AktivenLaufBeendenAsync` entfernt:

- `AktiveRunId`
- `LaufStatus`

`AktualisiereLaufStatusAsync` setzt `LaufStatus` nur, solange `AktiveRunId` noch gesetzt ist. Spaete Events nach Prozessende werden dadurch ignoriert.

`GetAktiveAufgabenAsync` liest aktive/wartende Aufgaben mit `AsNoTracking`, inkludiert `Projekt` und `GitRepository`, sortiert nach `LetzterCliStartUtc` und gibt maximal 20 Aufgaben zurueck. `LaufStatus`, `AktiveRunId` und `LastHeartbeatUtc` sind normale `Aufgabe`-Properties und stehen im Ergebnis fuer das Mapping zur Verfuegung.

## Runtime-Status der CLI

`PseudoConsoleSession` haelt lokal `RuntimeStatus` mit den Werten:

- `Laeuft`
- `WartetAufEingabe`
- `Inaktiv`

`CliRuntimeStatusEvaluator` bestimmt den Status aus Prozesszustand, letzter Output-Aktivitaet, letzter Eingabe-Aktivitaet und einem Timeout. Frische Ausgabe oder Eingabe ergibt `Laeuft`; fehlende Aktivitaet bei laufendem Prozess ergibt `WartetAufEingabe`.

`CliProcessManager` ist die Bruecke zur Persistenz:

- Bei `CliProcessStatus.Gestartet` startet er den Heartbeat, persistiert den aktiven Lauf und abonniert `PseudoConsoleSession.RuntimeStatusChanged`.
- Runtime-Wechsel `Laeuft`/`WartetAufEingabe` werden in `AufgabeLaufStatus` uebersetzt und ueber `AufgabeService.AktualisiereLaufStatusAsync` persistiert.
- Bei `Gestoppt` oder `Fehler` stoppt er den Heartbeat, meldet die Runtime-Subscription ab und beendet den aktiven Lauf in der Aufgabe.

## Detailansicht und Fusszeile

`TaskDetailViewModel` setzt `IsCliRunning` direkt ueber `KiAusfuehrungsService.IsRunning(aufgabeId)`. Beim Laden der Aufgabe wird die aktuelle `PseudoConsoleSession` geholt und an `AttachCliStatusSession` uebergeben.

`AttachCliStatusSession` abonniert `RuntimeStatusChanged` der lokalen Session. `UpdateCliStatusText` bildet die Fusszeilentexte:

- `CliRuntimeStatus.Laeuft` -> `CLI-Status: Ausführung läuft`
- `CliRuntimeStatus.WartetAufEingabe` -> `CLI-Status: Wartet auf Eingabe`
- `CliRuntimeStatus.Inaktiv` -> `CLI inaktiv`

Die Fusszeile muss nicht auf Persistenz oder den Menue-Refresh warten. Daher kann sie korrekt "Ausführung läuft" zeigen, waehrend die Menuekachel noch ein altes `AktiveAufgabePanelItem` mit "Bereit" anzeigt.

## Relevanz fuer die Anforderung

Die fachliche Quelle ist bereits vorhanden:

- Detail/Fusszeile: lokale `PseudoConsoleSession.RuntimeStatus`
- Menue: persistierte `Aufgabe`-Laufdaten plus `KiAusfuehrungsStatusConverter`

Die Differenz entsteht wahrscheinlich an der Synchronisationsgrenze zwischen lokaler Runtime-Session, persistiertem `Aufgabe.LaufStatus` und der bereits gerenderten `AktiveAufgabenListe`.
