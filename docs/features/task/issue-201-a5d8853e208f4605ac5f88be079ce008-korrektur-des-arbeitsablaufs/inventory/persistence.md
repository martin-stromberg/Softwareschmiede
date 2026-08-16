# Persistenz und Migrationen

## EF-Modell

[`SoftwareschmiededDbContext`](../../../../../src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContext.cs) mappt `Aufgabe.Status` und `Aufgabe.LaufStatus` als Strings. Heartbeat, letzter CLI-Start, Abschlussdatum und geplante Prompt-Zeitpunkte werden als nullable Zeitwerte persistiert. `AktiveRunId` ist ein normales nullable Feld der Aufgabe.

## Bestehende Migrationen

Die Migration [`AddAufgabeLaufStatus`](../../../../../src/Softwareschmiede/Migrations/20260710115042_202607100001_AddAufgabeLaufStatus.cs) fuegt die nullable Textspalte `LaufStatus` zur Tabelle `Aufgaben` hinzu. Sie bildet nur den UI-/Terminal-Substatus ab und kann den geforderten Ausfuehrungslebenszyklus nicht abdecken.

Vorherige Migrationen aendern den Gesamtstatus (`UpdateAufgabeStatusEnum`, `SimplifyAufgabeStatusEnum`). Bei einer Erweiterung ist zu beachten, dass bestehende Datensaetze keinen expliziten Ausfuehrungsstatus besitzen. Die Default-/Backfill-Regel fuer `Neu`, `Gestartet`, `Wartend` und `Beendet` muss im Plan festgelegt werden.

## Persistenzrisiken

- `AktiveRunId == null` ist derzeit mehrdeutig.
- Ein Prozess kann beendet sein, waehrend `Aufgabe.Status == Gestartet` bleibt.
- Ein Programmneustart kann keinen in-memory Prozess wiederherstellen; persistiert werden derzeit nur Metadaten, keine Session.
- Abschluss leert `LokalerKlonPfad`; ein erneuter Start einer insgesamt beendeten Aufgabe darf daher nicht versehentlich einen neuen Klon anlegen.
