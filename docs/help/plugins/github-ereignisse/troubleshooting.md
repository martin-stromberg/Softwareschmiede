← [Zurück zur Übersicht](index.md)

# GitHub-Ereignisüberwachung — Fehlerbehebung

## Es erscheint kein Ereignis

**Ursache:** Das Repository oder die Referenz ist nicht mit einer aktiven Aufgabe verknüpft, der Trigger fehlt, oder Autor/Bearbeiter ist nicht der angemeldete Repository-Owner.

**Lösung:** Prüfe die Verknüpfung und den Text in GitHub. Ergänze `$Softwareschmiede` und stelle sicher, dass der Owner die aktuelle Textfassung verfasst hat. Öffne **SCM-Ereignisse** und klicke **Aktualisieren**.

## Ein Eintrag bleibt wartend

**Ursache:** Es ist keine geeignete Standard-CLI konfiguriert, eine Aufgabe ist pausiert/beendet/archiviert, die CLI ist nicht bereit oder eine Berechtigung fehlt.

**Lösung:** Wähle den Eintrag und lies den angezeigten Wartegrund. Prüfe die CLI-Einstellungen oder den Aufgabenstatus und nutze anschließend **Erneut prüfen** beziehungsweise **Öffnen / CLI auswählen**.

## GitHub-Anmeldung oder Repositoryzugriff schlägt fehl

**Ursache:** Der Personal Access Token fehlt, ist ungültig oder hat keinen ausreichenden Zugriff.

**Lösung:** Öffne **Einstellungen → Plugins → GitHub → Authentifizierung**, aktualisiere den **Personal Access Token**, speichere die Änderung und wähle beim Ereignis **Erneut prüfen**. Prüfe auch Repositoryzugriff sowie Lese- und Schreibrechte für Issues und Pull Requests.

## Der Ausgang einer CLI-Zustellung ist ungewiss

**Ursache:** Die Eingabe könnte die CLI erreicht haben, bevor eine lokale Bestätigung gespeichert werden konnte.

**Lösung:** Klicke zuerst **Status erneut prüfen**. Verwende **Neu senden…** nur, wenn ein weiterer Versuch gewünscht ist; dadurch kann die Anfrage doppelt verarbeitet werden.
