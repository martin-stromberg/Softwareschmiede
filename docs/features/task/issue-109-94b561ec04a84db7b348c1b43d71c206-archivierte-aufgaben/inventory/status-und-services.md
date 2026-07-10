# Statusmodell und Service-Schnittstellen

## Dateien

- `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`
- `src/Softwareschmiede/Domain/Enums/AufgabeStatus.cs`
- `src/Softwareschmiede/Domain/Enums/AufgabeStatusExtensions.cs`
- `src/Softwareschmiede/Domain/Enums/AufgabenFilterTyp.cs`
- `src/Softwareschmiede/Application/Services/AufgabeService.cs`

## Aufgabe und Status

`Aufgabe` enthaelt den Persistenzstatus in `Status` sowie ein optionales `AbschlussDatum`. Der Status-Enum enthaelt:

- `Neu`
- `Gestartet`
- `Wartend`
- `Beendet`
- `Archiviert`

Fuer diese Anforderung ist `Beendet` der fachliche Abschlussstatus. `Archiviert` ist ein separater Status fuer nicht mehr aktive, ausgeblendete Aufgaben.

## Status-Hilfen

`AufgabeStatusExtensions` definiert aktuell nur `AktivOderWartendStatus` und `IstAktivOderWartend()`. Diese Hilfen betreffen `Gestartet` und `Wartend`, nicht die fuer diese Anforderung benoetigte Einteilung in "beendet" und "nicht beendet".

Eine neue zentrale Hilfsmethode wie `IstBeendet()` oder `IstNichtBeendet()` koennte die Zuordnung testbar machen. Alternativ kann die Trennung lokal im ViewModel erfolgen, wenn der Statusbegriff nicht breiter wiederverwendet werden soll.

## AufgabeService

Relevante Methoden:

- `GetByProjektAsync(Guid projektId, ...)`: liefert alle Aufgaben eines Projekts mit `Status != Archiviert`, absteigend nach `ErstellungsDatum`.
- `GetArchiviertByProjektAsync(Guid projektId, ...)`: liefert nur archivierte Aufgaben.
- `AbschliessenAsync(Guid id, ...)`: setzt `Status = Beendet`, setzt `AbschlussDatum`, leert Branch/Klonpfad.
- `ArchivierenAsync(Guid id, ...)`: erlaubt Archivierung nur aus `Beendet` heraus und setzt `Status = Archiviert`.
- `GetAktiveAufgabenAsync(...)`: liefert nur `Gestartet` und `Wartend` fuer Seitenleiste/Dashboard.

Die Anforderung benoetigt voraussichtlich keine Service-Aenderung, weil `GetByProjektAsync()` beendete Aufgaben bereits liefert. Wenn die Planung archivierte Aufgaben ausdruecklich nicht anzeigen will, ist das bestehende Verhalten passend.

## Bestehende Filtertypen

`AufgabenFilterTyp` enthaelt `Alle`, `Aktiv`, `Archiviert`. Diese Filtertypen sind fuer die neue UI-Trennung nicht ideal benannt:

- `Aktiv` bedeutet im ViewModel aktuell `Status != Archiviert`, umfasst also auch `Beendet`.
- `Archiviert` kann mit der aktuellen Ladequelle in der Projektdetailansicht nicht greifen.

Die Planung sollte entscheiden, ob der Filter erhalten, umbenannt, entfernt oder von der neuen Trennung getrennt behandelt wird.
