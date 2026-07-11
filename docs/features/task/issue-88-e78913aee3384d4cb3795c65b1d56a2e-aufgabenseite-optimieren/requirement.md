# Anforderung: Aufgabenseite optimieren

## Metadaten

- Aufgaben-ID: `e78913ae-e338-4d4c-b379-5c65b1d56a2e`
- Branch: `task/issue-88-e78913aee3384d4cb3795c65b1d56a2e-aufgabenseite-optimieren`
- Erstellt: 2026-07-11

## Ziel

Die Aufgabendetailansicht soll mehr Kontext zur aktuell angezeigten Aufgabe und zur laufenden CLI-Ausführung anzeigen. Außerdem soll die Info-Ansicht konsistent über die Ansichtsleiste erreichbar sein und Aufgaben ohne Issue-Bezug aus einem Git-Plugin müssen wieder ausführbar sein.

## Funktionale Anforderungen

### Programmtitel

- Wenn die Aufgabendetailansicht angezeigt wird, muss der Name der Aufgabe in der Titelleiste des Programms angezeigt werden.
- Die Anzeige muss sich auf die aktuell geöffnete Aufgabe beziehen.

### Fußzeile

- Wenn für die Aufgabe eine CLI ausgeführt wird, muss der Name der ausgeführten CLI in der Fußzeile angezeigt werden.
- Wenn keine CLI ausgeführt wird, darf kein falscher oder veralteter CLI-Name angezeigt werden.

### Info-Ansicht

- Die Leisten zur Auswahl der Ansichten müssen um einen Button für `Info` ergänzt werden.
- Der `Info`-Button muss zur Ansicht mit den Aufgabenstamminformationen wechseln.
- Die Info-Ansicht muss auch dann aufrufbar sein, wenn die Aufgabe gestartet ist.
- Die Info-Ansicht muss auch dann aufrufbar sein, wenn die Aufgabe beendet ist.

### Ausführung von Aufgaben ohne Issue-Bezug

- Aufgaben, die ohne Bezug zu einem Issue aus einem Git-Plugin erstellt wurden, müssen wieder ausführbar sein.
- Die Ausführung darf nicht voraussetzen, dass zu einer Aufgabe ein Issue-Bezug vorhanden ist.

## Akzeptanzkriterien

- Bei geöffneter Aufgabendetailansicht enthält die Programmtitelleiste den Namen der geöffneten Aufgabe.
- Während einer CLI-Ausführung zeigt die Fußzeile den Namen der ausgeführten CLI.
- Ohne laufende CLI-Ausführung zeigt die Fußzeile keinen veralteten CLI-Namen.
- In jeder Ansichts-Auswahlleiste der Aufgabendetailansicht ist ein `Info`-Button vorhanden.
- Der `Info`-Button öffnet die Aufgabenstamminformationen.
- Die Info-Ansicht ist bei nicht gestarteten, gestarteten und beendeten Aufgaben erreichbar.
- Eine Aufgabe ohne Issue-Bezug, die aus einem Git-Plugin erstellt wurde, kann gestartet und ausgeführt werden.

## Nicht-Ziele

- Es wird keine neue Aufgabenart eingeführt.
- Es wird keine fachliche Änderung an den Aufgabenstammdaten gefordert.
- Es wird keine Änderung am Verhalten issue-bezogener Aufgaben gefordert, außer soweit sie durch die neue Info-Navigation oder Kontextanzeige betroffen sind.

## Offene Punkte

- Keine.
