# Aufgaben & KI-Entwicklungsprozess — Ablauf für Anwender

## Voraussetzungen

- Ein Projekt mit mindestens einem aktiven Git-Repository ist vorhanden.
- Ein KI-Plugin (z.B. Claude CLI) und ein SCM-Plugin (z.B. GitHub) sind konfiguriert.
- Die Softwareschmiede läuft als WPF-Desktopanwendung unter Windows 11.

## Schritt-für-Schritt-Anleitung

### 0. Navigation zwischen Projekt und Aufgabe

Die Aufgabendetailansicht ist vollständig vom Projekt getrennt dargestellt:

**Aufgabe öffnen:**
1. Öffne ein Projekt über die Projektliste.
2. In der Projektdetailansicht siehst du die Aufgabenliste.
3. Doppelklick auf eine Aufgabe öffnet die Aufgabendetailansicht als vollständige View.
4. Die Projektdetailansicht wird ausgeblendet.

**Zurück zur Projektansicht:**
1. Klicke im Ribbon der Aufgabendetailansicht (Gruppe „Navigation") auf **Zurück**.
2. Du kehst zur Projektdetailansicht zurück.
3. Alle deine Änderungen an der Aufgabe bleiben erhalten.

> **Hinweis:** Die Navigation erfolgt fensterumfassend — du siehst entweder die Projektdetailansicht oder die Aufgabendetailansicht, nicht beide gleichzeitig.

### 1. Neue Aufgabe anlegen

Navigiere über die Seitenleiste zu **Projekte**, öffne das gewünschte Projekt. Im Ribbon der Projektdetailansicht (Gruppe „Aufgaben") klickst du auf **Neue Aufgabe**:

1. Die Aufgabendetailansicht öffnet sich sofort mit dem **Edit-Panel**.
2. Gib einen **Titel** ein (Pflichtfeld).
3. Optional: Füge eine **Anforderungsbeschreibung** hinzu.
4. Klicke im Ribbon (Gruppe „Aufgabe") auf **Speichern**.

Die Aufgabe wird in der Datenbank gespeichert, bleibt aber im Status **Neu**.

> **Hinweis:** Der Titel ist erforderlich, um speichern zu können. Der „Speichern"-Button ist ausgegraut, wenn das Feld leer ist.

Nach dem Speichern können Sie bleiben, um die Aufgabe weiterzubearbeiten, oder mit **Zurück** zur Projektdetailansicht navigieren. Die neue Aufgabe erscheint sofort in der Aufgabenliste.

### 2. Aufgabe bearbeiten (optional)

Befindest du dich im Status **Neu** oder **Gestartet**, kannst du das Edit-Panel erneut öffnen und Titel oder Anforderungsbeschreibung ändern. Klicke auf **Speichern** im Ribbon, um die Änderungen zu übernehmen.

### 3. Aufgabe starten (Repository einrichten)

Im Status **Neu**, klicke im Ribbon (Gruppe „Aufgabe") auf **Starten**:

- Die Anwendung klont das Repository in das konfigurierte Arbeitsverzeichnis.
- Ein `task/`-Branch wird angelegt.
- Eine lokale `issue.md`-Datei wird automatisch erstellt, die die Aufgabebeschreibung enthält (Titel, ID, Branch-Name, Erstellungsdatum, Anforderung).
- Die `.gitignore`-Datei wird automatisch angepasst, um `issue.md` von der Versionskontrolle auszuschließen.
- Der Status wechselt auf **Gestartet**.
- Das **CLI-Panel** wird angezeigt.

> **Hinweis:** Das Arbeitsverzeichnis muss in den Einstellungen konfiguriert sein.
> 
> Die `issue.md`-Datei ist eine lokale Datei und wird nicht committet. Sie dient als Referenzmaterial für die KI und den Entwickler während der Aufgabenbearbeitung.

### 4. CLI starten

Im CLI-Panel (Status **Gestartet** oder höher):

1. Im Dropdown (Gruppe „CLI") das gewünschte **KI-Plugin** auswählen (z.B. „Claude CLI").
2. Optional: **CLI-Parameter** eingeben.
3. Klicke auf **CLI starten**.

Das CLI-Fenster erscheint eingebettet in die Aufgabenansicht. Der Status wechselt auf **In Arbeit**. Die KI ist nun interaktiv bedienbar.

### 5. Zwischen CLI-Fenster und Info-Ansicht umschalten

Im CLI-Panel findest du ein Toggle-Button „Info"/"CLI" in der Leiste über dem Fenster:

- **CLI:** Zeigt das Terminalfenster des KI-Tools.
- **Info:** Zeigt Aufgabeeigenschaften (Titel, Status, Beschreibung) und das Protokoll aller bisherigen Einträge.

Du kannst jederzeit zwischen beiden Ansichten wechseln, ohne den CLI-Prozess zu unterbrechen.

### 6. Mit der KI arbeiten

Das eingebettete CLI-Fenster verhält sich wie ein natives Terminalfenster. Prompts können direkt eingegeben werden. Das Protokoll der Sitzung wird laufend in der Aufgabe gespeichert und ist über die Info-Ansicht einsehbar.

### 7. CLI beenden

Beendet sich das CLI-Programm selbst, aktualisiert die Ansicht automatisch. Alternativ kannst du im Ribbon (Gruppe „CLI") auf **Stoppen** klicken (graceful shutdown: 5 s Wartezeit, dann Kill).

### 8. Aufgabe abschließen

Im Status **Gestartet**, **In Arbeit** oder **Wartend**, klicke im Ribbon (Gruppe „Aufgabe") auf **Beenden**. Der Status wechselt auf **Beendet**. Das **Diff-Panel** wird angezeigt, das die Änderungen im Repository zeigt.

### 9. Aufgabe löschen

Im Status **Neu** oder **Gestartet** (nicht **Beendet** oder **Archiviert**), klicke im Ribbon (Gruppe „Aufgabe") auf **Löschen**:

1. Ein Bestätigungsdialog fragt: „Aufgabe '{Titel}' wirklich löschen? Diese Aktion kann nicht rückgängig gemacht werden."
2. Bestätige mit **Löschen** oder breche mit **Abbrechen** ab.
3. Bei Bestätigung wird die Aufgabe gelöscht und du wirst zur Projektdetailansicht zurücknavigiert.

## Ergebnis

Die Aufgabe ist mit Status **Beendet** abgelegt. Alle Protokolleinträge bleiben erhalten.

## Sonderfälle

- **Rate-Limit (Status Wartend):** Das CLI gibt einen Rate-Limit-Marker aus; Status wechselt auf „Wartend". Ein Prompt-Vorschlag wird gespeichert. Über „Wiederherstellen" (Recovery-Banner auf dem Dashboard) kann die Aufgabe auf „Gestartet" zurückgesetzt und das CLI erneut gestartet werden.
- **Aufgabe wiederherstellen:** Erscheint auf dem Dashboard das Banner „X Aufgabe(n) benötigen Wiederherstellung", kann durch Klick auf **Wiederherstellen** der Status zurückgesetzt werden. Voraussetzung: kein aktiver CLI-Prozess und Heartbeat älter als 5 Minuten.

## Barrierefreiheit

Die Seitenleiste kann mit dem Hamburger-Button (☰) ein- und ausgeklappt werden. Navigationselemente sind per Tastatur erreichbar. Das eingebettete CLI-Fenster verhält sich wie eine native Windows-Anwendung.
