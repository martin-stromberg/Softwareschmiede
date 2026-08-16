# Domain-Entity und Statusmodell

## Aufgabe

[`Aufgabe`](../../../../../src/Softwareschmiede/Domain/Entities/Aufgabe.cs) speichert `Status`, Branch, lokalen Klonpfad, `AktiveRunId`, Heartbeat, letzten CLI-Start und `LaufStatus`. `AktiveRunId` wird als Kennzeichen eines aktiven Laufs behandelt; beim Laufende wird sie entfernt.

## Gesamtstatus

[`AufgabeStatus`](../../../../../src/Softwareschmiede/Domain/Enums/AufgabeStatus.cs) enthaelt `Neu`, `Gestartet`, `Wartend`, `Beendet` und `Archiviert`. `Gestartet` und `Wartend` werden ueber [`AufgabeStatusExtensions`](../../../../../src/Softwareschmiede/Domain/Enums/AufgabeStatusExtensions.cs) als aktiv oder wartend zusammengefasst. Diese Stati steuern Listen, CLI-Panel, Recovery und die erlaubten Uebergaenge.

Die validierten Uebergaenge in [`AufgabeService`](../../../../../src/Softwareschmiede/Application/Services/AufgabeService.cs) erlauben `Neu -> Gestartet`, `Gestartet <-> Wartend` und `Gestartet/Wartend -> Beendet`. `Beendet` und `Archiviert` sind terminal. `StartenAsync` setzt den Gesamtstatus direkt auf `Gestartet`, ohne die Uebergangsvalidierung zu verwenden.

## Vorhandener Laufstatus und Luecke

[`AufgabeLaufStatus`](../../../../../src/Softwareschmiede/Domain/Enums/AufgabeLaufStatus.cs) hat nur `Laeuft` und `WartetAufEingabe`. Laut Dokumentation ist er absichtlich ein beobachtender Substatus waehrend `AktiveRunId` gesetzt, nicht der Lebenszyklus einer KI-Ausfuehrung.

`AktivenLaufSetzenAsync` setzt Run-ID, Heartbeat, Startzeit und `LaufStatus = Laeuft`. `AktivenLaufBeendenAsync` setzt Run-ID und `LaufStatus` auf `null`. Damit sind `nicht gestartet` und `beendet` nicht persistiert unterscheidbar; ein fehlender aktiver Lauf kann beides bedeuten. Diese Unterscheidung ist fuer die Anforderung der zentrale Modellierungsbedarf.

## Konsequenzen

- Gesamtstatus `Beendet` muss unabhaengig vom KI-Ausfuehrungsstatus bleiben.
- Ein Stoppen oder natuerliches Prozessende darf nicht automatisch den Gesamtstatus setzen.
- Die Sperre gegen Neustart einer insgesamt beendeten Aufgabe muss auf `AufgabeStatus.Beendet` beruhen, nicht auf dem KI-Ausfuehrungsstatus.
