# Technische Anforderung: Korrektur der Aufgabenliste im Programmmenü

## Ausgangslage

Im linken Programmmenü wird eine Liste von Aufgaben angezeigt. Die aktuelle Darstellung ist für Anwender unzureichend, weil die aktuell geöffnete Aufgabe nicht eindeutig hervorgehoben wird, die Sortierung der Aufgaben instabil wirkt und relevante Plugin-Informationen in den Aufgabenpanels fehlen.

## Ziel

Die Aufgabenliste im linken Programmmenü soll so korrigiert werden, dass sie die aktuell angezeigte Aufgabe visuell markiert, Aufgaben anhand ihres letzten echten CLI-Starts stabil und nachvollziehbar sortiert und in jedem Aufgabenpanel die Namen des SCI-Plugins sowie des KI-Plugins anzeigt.

## Funktionale Anforderungen

### 1. Hervorhebung der aktuell angezeigten Aufgabe

- Wenn im Inhaltsbereich aktuell eine Aufgabe angezeigt wird, muss das zugehörige Aufgabenpanel im linken Programmmenü hervorgehoben werden.
- Die Hervorhebung muss eindeutig der Aufgabe entsprechen, deren Inhalt aktuell im Haupt- bzw. Inhaltsbereich sichtbar ist.
- Ist keine Aufgabe im Inhaltsbereich aktiv, darf keine falsche Aufgabe als aktiv markiert werden.

### 2. Speicherung des Zeitstempels "Letzter Start"

- Immer wenn die CLI einer Aufgabe neu gestartet wird, muss an dieser Aufgabe ein aktueller Zeitstempel als "Letzter Start" gespeichert werden.
- Ein Neustart liegt nur vor, wenn die CLI tatsächlich neu gestartet wird.
- Wird eine Aufgabe lediglich aus dem Hintergrund wieder in den Vordergrund geholt, darf der Zeitstempel "Letzter Start" nicht aktualisiert werden.
- Der gespeicherte Zeitstempel muss dauerhaft genug verfügbar sein, um die Aufgabenliste im Programmmenü danach sortieren zu können.

### 3. Sortierung der Aufgabenliste

- Die Aufgaben im linken Programmmenü müssen absteigend nach dem Zeitstempel "Letzter Start" sortiert werden.
- Aufgaben mit dem neuesten "Letzter Start"-Zeitstempel müssen zuerst angezeigt werden.
- Die Sortierung darf sich nicht ohne relevanten neuen CLI-Start ständig verändern.
- Für Aufgaben ohne vorhandenen "Letzter Start"-Zeitstempel muss ein deterministisches Fallback-Verhalten verwendet werden, damit die Liste stabil bleibt.

### 4. Anzeige des SCI-Plugins

- Im Aufgabenpanel muss der Name des SCI-Plugins der jeweiligen Aufgabe angezeigt werden.
- Die Anzeige muss pro Aufgabe erfolgen und darf nicht global oder statisch für alle Aufgaben identisch gesetzt werden, sofern die Aufgaben unterschiedliche SCI-Plugins verwenden.

### 5. Anzeige des KI-Plugins

- Im Aufgabenpanel muss der Name des KI-Plugins der jeweiligen Aufgabe angezeigt werden.
- Die Anzeige muss pro Aufgabe erfolgen und darf nicht global oder statisch für alle Aufgaben identisch gesetzt werden, sofern die Aufgaben unterschiedliche KI-Plugins verwenden.

## Nicht-funktionale Anforderungen

- Die Aufgabenliste muss für Anwender stabil und nachvollziehbar bleiben.
- Die Sortierung darf nicht durch bloßes Anzeigen, Aktualisieren oder Reaktivieren einer Hintergrundaufgabe verändert werden.
- Bestehende Aufgaben ohne neue Metadaten müssen weiterhin angezeigt werden.
- Die UI-Änderungen müssen sich in das bestehende Layout und Design des linken Programmmenüs einfügen.

## Abgrenzungen

- Es ist keine Änderung der fachlichen Aufgabeninhalte gefordert.
- Es ist keine Änderung daran gefordert, wie Aufgaben gestartet oder aus dem Hintergrund hervorgeholt werden, außer soweit dies zur korrekten Erfassung von "Letzter Start" notwendig ist.
- Es ist keine neue manuelle Sortierfunktion gefordert.

## Akzeptanzkriterien

- Ist eine Aufgabe im Inhaltsbereich geöffnet, ist genau das zugehörige Aufgabenpanel im linken Programmmenü als aktiv hervorgehoben.
- Beim echten Neustart der CLI einer Aufgabe wird deren "Letzter Start"-Zeitstempel auf den aktuellen Zeitpunkt gesetzt.
- Beim Hervorholen einer Hintergrundaufgabe bleibt deren bisheriger "Letzter Start"-Zeitstempel unverändert.
- Die Aufgabenliste ist absteigend nach "Letzter Start" sortiert.
- Die Reihenfolge der Aufgaben bleibt stabil, solange keine CLI einer Aufgabe neu gestartet wird.
- Jedes Aufgabenpanel zeigt den Namen des zugehörigen SCI-Plugins an.
- Jedes Aufgabenpanel zeigt den Namen des zugehörigen KI-Plugins an.
- Aufgaben ohne vorhandenen "Letzter Start"-Zeitstempel werden weiterhin angezeigt und verursachen keine instabile Sortierung.
