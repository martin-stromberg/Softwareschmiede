← [Zurück zur Übersicht](index.md)

# GitHub-Ereignisüberwachung — Ablauf für Anwender

## Voraussetzungen

- Das GitHub-Repository ist mit einer Aufgabe verknüpft; die Aufgabe enthält eine Issue- oder Pull-Request-Referenz.
- In **Einstellungen → Plugins → GitHub → Authentifizierung** ist ein gültiger **Personal Access Token** gespeichert, der auf das Repository zugreifen darf.
- Der Text enthält das Trigger-Keyword (Standard `$Softwareschmiede`, einstellbar unter **Einstellungen → Allgemein → SCM-Ereignisse**) und stammt vom Repository-Owner. Bei einer bearbeiteten Textfassung muss der Owner auch als Bearbeiter erkennbar sein.
- Die SCM-Ereignisüberwachung ist aktiviert (Checkbox unter **Einstellungen → Allgemein → SCM-Ereignisse**, standardmäßig an). Ist sie deaktiviert, werden weder neue Ereignisse eingelesen noch bestehende automatisch zugestellt; die manuellen Aktionen in der Übersicht bleiben nutzbar.
- Für die automatische CLI-Verarbeitung werden derzeit Codex CLI, Claude CLI oder Devin CLI nur mit einem unterstützten, tatsächlich geprüften Schreibzugriff verwendet. Andere ausgewählte KI-Plugins können in der Ereignisverarbeitung wartend bleiben.
- Optional kannst du unter **Einstellungen → Allgemein → SCM-Ereignisse** Prompt-Anhänge pflegen: Texte, die der Anwendung bei der Zustellung je Ereignisart angehängt werden (Allgemein gilt immer, dazu kommt der Anhang für Issue, Issue-Kommentar, Pull Request oder Pull-Request-Kommentar inklusive Reviews). So erhält die CLI z. B. Hinweise auf verfügbare Skills oder Bearbeitungsregeln, ohne dass sie in jedem GitHub-Kommentar stehen müssen. Leere Felder hängen nichts an; Trigger-Keyword und gespeicherter Ereignistext bleiben unverändert.

## Schritt-für-Schritt-Anleitung

### 1. Anfrage in GitHub schreiben

Schreibe das Trigger-Keyword (Standard `$Softwareschmiede`) in die Beschreibung, einen Kommentar oder Review-Text des verknüpften Issues oder Pull Requests. Ergänze die konkrete Bitte im selben Text.

### 2. Eingang prüfen

Öffne links **SCM-Ereignisse**. Die Liste zeigt Aufgabe, Repository, Ursprung, Art, Eingangszeit und Status. Mit **Aktualisieren** lässt sich die Anzeige neu laden. Wähle einen Eintrag, um den vollständigen Text, die Ursprungsadresse und einen möglichen Wartegrund zu sehen.

### 3. Wartenden Eintrag öffnen

Wähle den Eintrag und klicke **Öffnen / CLI auswählen** oder drücke Enter. Wenn eine passende laufende CLI vorhanden ist, bestätigt ein Dialog die Übergabe an diese Sitzung. Andernfalls kannst du im Auswahlfenster eine aktivierte CLI auswählen. Ein abgebrochener Auswahlvorgang lässt den Eintrag wartend.

### 4. Status prüfen oder erneut senden

Bei einem unklaren Ausgang klicke zuerst **Status erneut prüfen**. **Neu senden…** startet nur nach einer zusätzlichen Bestätigung einen neuen Versuch. Der Dialog weist darauf hin, dass der vorherige Versuch bereits verarbeitet worden sein könnte.

### 5. Ereignis abbrechen

Ist ein Eintrag nicht mehr relevant (z. B. Anfrage hinfällig oder anderweitig erledigt), wähle ihn und klicke **Abbrechen…**. Nach einer kurzen Bestätigung erhält das Ereignis den Endstatus **Abgebrochen**: Es folgen keine weiteren Zustell- oder Prüfversuche und keine weiteren GitHub-Antworten. Eine bereits zugestellte, laufende CLI-Verarbeitung wird dadurch **nicht** beendet — sie läuft regulär weiter, nur die Ereignisverfolgung endet. Der Eintrag bleibt sichtbar und ist über den Filter **Abgebrochen** oder **Alle** auffindbar. Bereits verarbeitete oder abgebrochene Einträge können nicht erneut abgebrochen werden.

## Ergebnis

Die Liste zeigt den Verarbeitungsstatus. Bei erfolgreicher Bestätigung erhalten Kommentare und veröffentlichte Pull-Request-Reviews auf GitHub 👀; für Beschreibungen wird keine Reaktion gesetzt. Wartende Einträge zeigen den Grund, soweit er bekannt ist. Die Anwendung beendet die Aufgabe nicht automatisch; sie bleibt nach der Verarbeitung in ihrem bisherigen Aufgabenstatus.

## Barrierefreiheit

Die Ereignisliste kann mit der Tastatur bedient werden. Enter öffnet den ausgewählten Eintrag. Schaltflächen und Liste stellen Namen für unterstützende Bedienhilfen bereit.
