# Aufgaben & KI-Entwicklungsprozess — Ablauf für Anwender

## Voraussetzungen

- Ein Projekt mit mindestens einem aktiven Git-Repository ist vorhanden.
- Ein KI-Plugin (z.B. Claude CLI) und ein SCM-Plugin (z.B. GitHub) sind konfiguriert.
- Die Softwareschmiede läuft als WPF-Desktopanwendung unter Windows 11.

## Schritt-für-Schritt-Anleitung

### 0. Schneller Zugriff auf aktive Aufgaben über die Seitenleiste

In der linken Navigationsseitenleiste findest du eine Sektion „Aktive Aufgaben", die bis zu 20 derzeit laufende Aufgaben (Status: Gestartet oder Wartend) anzeigt. Jede Aufgabe wird als gerahmte Kachel mit Titel und aktuellem KI-Ausführungsstatus dargestellt:

- `▶ Läuft` — Die KI arbeitet gerade an der Aufgabe
- `⏸ Wartet` — Die Aufgabe hat ein Rate-Limit erreicht und wartet auf Wiederaufnahme
- `✓ Bereit` — Keine aktive Ausführung erkannt

**Aufgabe öffnen:** Klicke auf den Navigations-Button (→) auf einer Aufgabenkachel, um direkt zur Aufgabendetailansicht zu navigieren. Die Seitenleisten-Sektion wird verborgen wenn du das Dashboard aufrufst — dort siehst du stattdessen die vollständige Liste.

> **Hinweis:** Die Anzeige ist auf 20 Aufgaben begrenzt und wird regelmäßig aktualisiert.

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

1. Die Aufgabendetailansicht öffnet sich sofort in der **Info-Ansicht** mit bearbeitbaren Stammdaten.
2. Gib einen **Titel** ein (Pflichtfeld).
3. Optional: Füge eine **Anforderungsbeschreibung** hinzu.
4. Klicke im Ribbon (Gruppe „Aufgabe") auf **Speichern**.

Die Aufgabe wird in der Datenbank gespeichert, bleibt aber im Status **Neu**.
Die Titelleiste des Fensters zeigt nach dem Laden den Namen der geöffneten Aufgabe.

> **Hinweis:** Der Titel ist erforderlich, um speichern zu können. Der „Speichern"-Button ist ausgegraut, wenn das Feld leer ist.

Nach dem Speichern können Sie bleiben, um die Aufgabe weiterzubearbeiten, oder mit **Zurück** zur Projektdetailansicht navigieren. Die neue Aufgabe erscheint sofort in der Aufgabenliste.

### 2. Aufgabe bearbeiten (optional)

Befindest du dich im Status **Neu** oder **Gestartet**, kannst du die **Info-Ansicht** öffnen und Titel oder Anforderungsbeschreibung ändern. Klicke auf **Speichern** im Ribbon, um die Änderungen zu übernehmen.

### 2a. Issue aus der Aufgabe anlegen (optional)

Wenn der Aufgabe noch kein Issue zugeordnet ist und der Repository-Provider die Issue-Anlage unterstützt, erscheint im Ribbon die Gruppe **Issue** mit dem Button **Issue anlegen**:

1. Öffne die **Info-Ansicht** der Aufgabe und klicke auf **Issue anlegen**.
2. Prüfe oder bearbeite den vorausgefüllten **Titel** und die **Beschreibung**. Die Anforderungsbeschreibung der Aufgabe wird als Ausgangstext übernommen; ein leerer Text ist zulässig.
3. Falls Templates verfügbar sind, wähle ein **Template** aus. Die Beschreibung enthält danach den Template-Inhalt, eine Trennlinie und `Originalanforderung:` mit der ursprünglichen Anforderungsbeschreibung. Der zusammengesetzte Text kann vollständig geändert werden.
4. Optional: Wähle einen angebotenen KI-Provider und klicke auf **Ausfüllen**, um das ausgewählte Template anhand der Originalanforderung ausfüllen zu lassen. Prüfe und bearbeite das Ergebnis vor dem Absenden.
5. Klicke auf **Anlegen**. Mit **Abbrechen** wird der Dialog ohne Provider- oder Speichervorgang geschlossen.

Nach erfolgreicher Anlage wird die Issue-Referenz in der Aufgabe angezeigt; die Anlage eines weiteren Issues ist für diese Aufgabe nicht mehr verfügbar. Wenn der Provider keine Templates unterstützt oder keine Templates findet, kannst du direkt ohne Template fortfahren. Fehler werden im Dialog angezeigt und die bisherigen Eingaben bleiben zur Korrektur erhalten.

### 3. Aufgabe starten (Repository einrichten)

Im Status **Neu**, klicke im Ribbon (Gruppe „Aufgabe") auf **Starten**:

- Während der Repository-Vorbereitung zeigt die Fußzeile `Bereit Repository vor...`.
- Die Anwendung klont das Repository in das konfigurierte Arbeitsverzeichnis.
- Ein `task/`-Branch wird angelegt.
- Eine lokale `issue.md`-Datei wird automatisch erstellt, die die Aufgabebeschreibung enthält (Titel, ID, Branch-Name, Erstellungsdatum, Anforderung).
- Die `.gitignore`-Datei wird automatisch angepasst, um `issue.md` von der Versionskontrolle auszuschließen.
- Der Status wechselt auf **Gestartet**.
- Die **CLI-Ansicht** wird angezeigt.

> **Hinweis:** Das Arbeitsverzeichnis muss in den Einstellungen konfiguriert sein.
> 
> Die `issue.md`-Datei ist eine lokale Datei und wird nicht committet. Sie dient als Referenzmaterial für die KI und den Entwickler während der Aufgabenbearbeitung.
>
> Aufgaben, die aus einem Git-Plugin ohne Issue-Bezug erstellt wurden, können ebenfalls gestartet werden. In diesem Fall wird der Aufgabenbranch ohne Issue-Nummer angelegt.

### 4. CLI starten

In der CLI-Ansicht (Status **Gestartet** oder höher):

1. Im Dropdown (Gruppe „CLI") das gewünschte **KI-Plugin** auswählen (z.B. „Claude CLI").
2. Optional: **CLI-Parameter** eingeben.
3. Klicke auf **CLI starten**.

Das CLI-Fenster erscheint eingebettet in die Aufgabenansicht. Die Aufgabe bleibt im Status **Gestartet** oder **Wartend**, während die laufende CLI über den Laufstatus und den Namen in der Fußzeile erkennbar ist. Die KI ist nun interaktiv bedienbar.

### 5. Zwischen Info-, CLI- und Diff-Ansicht wechseln

In der Aufgabendetailansicht befindet sich oberhalb des Inhalts eine Ansichtsleiste:

- **Info:** Zeigt Aufgabenstammdaten, Beschreibung, optionale Issue-Referenz und Protokollinformationen. Diese Ansicht ist auch bei gestarteten, wartenden und beendeten Aufgaben erreichbar.
- **CLI:** Zeigt das Terminalfenster des KI-Tools, wenn für die Aufgabe eine CLI-Ausführung verfügbar ist.
- **Diff:** Zeigt die Änderungen nach Abschluss der Aufgabe, wenn eine Diff-Ansicht verfügbar ist.

Du kannst während einer laufenden CLI-Ausführung zur Info-Ansicht wechseln, ohne den CLI-Prozess zu unterbrechen. Während eine CLI läuft, zeigt die Fußzeile den Namen der aktiven CLI. Nach Stop, Fehler oder Ende wird dieser Name entfernt.

### 6. Mit der KI arbeiten

Das eingebettete CLI-Fenster verhält sich wie ein natives Terminalfenster. Prompts können direkt eingegeben werden. Das Protokoll der Sitzung wird laufend in der Aufgabe gespeichert und ist über die Info-Ansicht einsehbar.

Im Ribbon der Aufgabendetailansicht steht in der Gruppe **CLI** zusätzlich die Auswahl **Promptvorlage** zur Verfügung. Wenn eine CLI läuft, können Sie dort eine in den Einstellungen gepflegte Vorlage auswählen. Die Anwendung ersetzt vor dem Versand die Platzhalter `%ProjectName%`, `%TaskName%` und `%RepositoryUrl%` und sendet den fertigen Prompt sofort mit Zeilenende an die laufende CLI.

> **Hinweis:** Die Auswahl sendet sofort. Falls dem Projekt kein Repository zugewiesen ist, wird `%RepositoryUrl%` durch einen leeren Wert ersetzt.

### 6a. Zeitgesteuerter Prompt-Versand (optional)

Statt einen Prompt sofort zu versenden, kannst du ihn auch für eine später Uhrzeit planen:

1. Im Ribbon (Gruppe „CLI") findest du zwei Eingabefelder: **Zielzeit** (Stunde und Minute).
2. Gib die Zielzeit ein, zu der der Prompt versendet werden soll (z.B. 16 und 30 für 16:30 Uhr).
3. Wähle eine Promptvorlage aus dem Dropdown **Promptvorlage**.
4. Klicke auf **Zeitgesteuert senden** (⏰-Button).
5. Der Status „Prompt in Wartestellung" mit der Zielzeit (z.B. „16:30") wird angezeigt.
6. Die Eingabefelder werden geleert, die Vorlagenauswahl wird zurückgesetzt.
7. Bei Erreichen der Zielzeit wird der Prompt automatisch an die CLI versendet; die Status-Anzeige verschwindet.

> **Hinweis:** Die eingegebene Uhrzeit wird als lokale Zeit deines Computers interpretiert. Liegt die Zielzeit in der Vergangenheit, wird der Prompt sofort versendet. Falls die CLI bis zur Zielzeit beendet wird, kann der Prompt nicht mehr versendet werden — es gibt aber keinen visuellen Fehler.
>
> Wenn du einen neuen Prompt zur gleichen oder anderen Uhrzeit planst, wird der vorherige Plan ersetzt.

### 7. CLI beenden

Beendet sich das CLI-Programm selbst, aktualisiert die Ansicht automatisch. Alternativ kannst du im Ribbon (Gruppe „CLI") auf **Stoppen** klicken (graceful shutdown: 5 s Wartezeit, dann Kill).

### 8. Aufgabe abschließen

Im Status **Gestartet** oder **Wartend**, klicke im Ribbon (Gruppe „Aufgabe") auf **Beenden**. Der Status wechselt auf **Beendet**. Die **Diff-Ansicht** wird angezeigt und zeigt die Änderungen im Repository. Die **Info-Ansicht** bleibt weiterhin auswählbar.

### 9. Pull Request erstellen

Wenn die Aufgabe einen Branch hat und ein verknüpftes Git-Repository mit Pull-Request-Unterstützung vorhanden ist, erscheint im Ribbon die Gruppe **Pull Request**. Klicke dort auf **PR erstellen**, um aus dem Aufgabenbranch einen Pull Request zu erstellen. Die Anwendung erstellt die PR-Beschreibung aus den Commits des Aufgabenbranches und pusht den Branch vorher automatisch zum Remote. Bei Aufgaben mit verknüpfter Issue-Nummer ergänzt sie den Pull-Request-Body automatisch um `Closes #<Issue>`. Enthält der Branch keine Commits gegenüber dem Zielbranch, kann GitHub keinen Pull Request erstellen.

### 10. Aufgabe löschen

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
