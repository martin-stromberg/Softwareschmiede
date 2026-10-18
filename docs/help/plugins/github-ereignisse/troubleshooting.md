← [Zurück zur Übersicht](index.md)

# GitHub-Ereignisüberwachung — Fehlerbehebung

## Es erscheint kein Ereignis

**Ursache:** Das Repository oder die Referenz ist nicht mit einer aktiven Aufgabe verknüpft, der Trigger fehlt, oder Autor/Bearbeiter ist nicht der angemeldete Repository-Owner.

**Lösung:** Prüfe die Verknüpfung und den Text in GitHub. Ergänze `$Softwareschmiede` und stelle sicher, dass der Owner die aktuelle Textfassung verfasst hat. Öffne **SCM-Ereignisse** und klicke **Aktualisieren**.

## Ein Eintrag bleibt wartend

**Ursache:** Es ist keine geeignete Standard-CLI konfiguriert, eine Aufgabe ist pausiert/beendet/archiviert, die CLI ist nicht bereit oder eine Berechtigung fehlt.

**Lösung:** Wähle den Eintrag und lies den angezeigten Wartegrund. Prüfe die CLI-Einstellungen oder den Aufgabenstatus und nutze anschließend **Erneut prüfen** beziehungsweise **Öffnen / CLI auswählen**.

## Der Wartegrund meldet eine Vertrauensabfrage der CLI

**Ursache:** Claude Code, Codex oder Devin fragt beim ersten Start in einem neuen Aufgabenverzeichnis, ob der Ordner vertrauenswürdig ist („Do you trust …"). Bei Devin überspringt der empfohlene Parameter `--respect-workspace-trust false` diese Abfrage; ohne ihn bleibt der Dialog offen. Solange der Dialog offen ist, darf die Anwendung nichts in die CLI eingeben.

**Lösung:** Beantworte die Abfrage direkt im Terminal der laufenden CLI-Sitzung; die Zustellung wird danach automatisch fortgesetzt — ein Neustart oder ein Datenbankeingriff ist nicht nötig. Die Anwendung trägt die Vertrauensentscheidung für neue, von ihr angelegte Aufgabenverzeichnisse bereits in die benutzerbezogene CLI-Konfiguration ein (Claude Code: `projects.<Ordner>.hasTrustDialogAccepted` in `~/.claude.json`; Codex: `[projects."<Ordner>"] trust_level = "trusted"` in `~/.codex/config.toml`), sodass der Dialog dort gar nicht erst erscheint. Enthält das Arbeitsverzeichnis eine eigene `.claude`- bzw. `.codex`-Konfiguration, bleibt die Abfrage bewusst erhalten.

## Der Wartegrund meldet eine ausstehende lokale Kanalprobe

**Ursache:** Vor der ersten Zustellung muss die CLI einen tatsächlichen Dateitool-Zugriff im Antwortkanal nachweisen. Ging diese Probe verloren (z. B. weil sie während eines geöffneten Dialogs gesendet wurde), sendet die Anwendung sie bei sichtbarer Eingabebereitschaft automatisch erneut — begrenzt auf wenige Versuche.

**Lösung:** Zunächst abwarten beziehungsweise **Erneut prüfen** — die Probe wird wiederholt, sobald die CLI wieder am Eingabeprompt steht. Meldet der Eintrag, dass die Kanalprobe wiederholt ohne Nachweis gesendet wurde, starte die CLI der Aufgabe regulär neu; danach läuft der Nachweis erneut.

## GitHub-Anmeldung oder Repositoryzugriff schlägt fehl

**Ursache:** Der Personal Access Token fehlt, ist ungültig oder hat keinen ausreichenden Zugriff.

**Lösung:** Öffne **Einstellungen → Plugins → GitHub → Authentifizierung**, aktualisiere den **Personal Access Token**, speichere die Änderung und wähle beim Ereignis **Erneut prüfen**. Prüfe auch Repositoryzugriff sowie Lese- und Schreibrechte für Issues und Pull Requests.

## Der Ausgang einer CLI-Zustellung ist ungewiss

**Ursache:** Die Eingabe könnte die CLI erreicht haben, bevor eine lokale Bestätigung gespeichert werden konnte.

**Lösung:** Klicke zuerst **Status erneut prüfen**. Verwende **Neu senden…** nur, wenn ein weiterer Versuch gewünscht ist; dadurch kann die Anfrage doppelt verarbeitet werden.
