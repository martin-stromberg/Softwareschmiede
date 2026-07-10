# Anforderung: Archivierte Aufgaben

## Zusammenfassung
In der Projektdetailansicht sollen Aufgaben nach ihrem Abschlussstatus getrennt dargestellt werden. Nicht beendete Aufgaben sollen direkt sichtbar bleiben, waehrend beendete Aufgaben in einem standardmaessig zugeklappten Register angezeigt werden. Dadurch bleibt der Fokus auf den noch offenen Aufgaben, ohne abgeschlossene Aufgaben aus der Projektdetailansicht zu entfernen.

## Ausloeser und Akteure
- **Ausloeser:** Ein Benutzer oeffnet die Detailansicht eines Projekts.
- **Akteure:** Benutzer, die Aufgaben eines Projekts in der Projektdetailansicht einsehen.

## Beschreibung
Die Projektdetailansicht soll die Aufgaben eines Projekts in zwei getrennten Bereichen darstellen:

1. Nicht beendete Aufgaben werden in einem eigenen sichtbaren Bereich angezeigt.
2. Beendete Aufgaben werden in einem separaten Register angezeigt.
3. Das Register fuer beendete Aufgaben ist beim Oeffnen der Projektdetailansicht standardmaessig zugeklappt.
4. Benutzer koennen das Register fuer beendete Aufgaben aufklappen, um die abgeschlossenen Aufgaben einzusehen.
5. Die Zuordnung einer Aufgabe zu einem Bereich richtet sich nach ihrem Abschlussstatus.

## Eingaben und Ausgaben
- **Eingaben:** Projekt mit zugeordneten Aufgaben und deren Abschlussstatus.
- **Ausgaben/Ergebnisse:** Projektdetailansicht mit getrennten Bereichen fuer nicht beendete und beendete Aufgaben; beendete Aufgaben befinden sich in einem initial zugeklappten Register.

## Fehlerbehandlung
Wenn ein Projekt keine Aufgaben enthaelt, soll die Projektdetailansicht weiterhin korrekt angezeigt werden. Wenn nur nicht beendete oder nur beendete Aufgaben vorhanden sind, soll die jeweilige Liste korrekt dargestellt werden, ohne die Trennung der Bereiche zu brechen.

## Abgrenzung
Nicht Teil dieser Anforderung sind Aenderungen am Datenmodell, an der Definition des Abschlussstatus, an der Bearbeitung von Aufgaben oder an der Erstellung und Loeschung von Aufgaben. Es geht ausschliesslich um die Darstellung der Aufgaben in der Projektdetailansicht.

## Akzeptanzkriterien
- [ ] In der Projektdetailansicht werden nicht beendete und beendete Aufgaben getrennt voneinander dargestellt.
- [ ] Nicht beendete Aufgaben sind beim Oeffnen der Projektdetailansicht direkt sichtbar.
- [ ] Beendete Aufgaben werden in einem separaten Register dargestellt.
- [ ] Das Register fuer beendete Aufgaben ist beim Oeffnen der Projektdetailansicht standardmaessig zugeklappt.
- [ ] Das Register fuer beendete Aufgaben kann aufgeklappt werden, um die beendeten Aufgaben anzuzeigen.
- [ ] Aufgaben werden anhand ihres Abschlussstatus dem korrekten Bereich zugeordnet.
- [ ] Die Darstellung funktioniert auch, wenn keine Aufgaben, nur beendete Aufgaben oder nur nicht beendete Aufgaben vorhanden sind.

## Offene Punkte
Keine.
