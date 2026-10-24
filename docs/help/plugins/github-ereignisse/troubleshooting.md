← [Zurück zur Übersicht](index.md)

# GitHub-Ereignisüberwachung — Fehlerbehebung

## Es erscheint kein Ereignis

**Ursache:** Das Repository oder die Referenz ist nicht mit einer aktiven Aufgabe verknüpft, das konfigurierte Trigger-Keyword fehlt, Autor/Bearbeiter ist nicht der angemeldete Repository-Owner, oder die SCM-Ereignisüberwachung ist deaktiviert.

**Lösung:** Prüfe die Verknüpfung und den Text in GitHub. Ergänze das Trigger-Keyword (Standard `$Softwareschmiede`, siehe **Einstellungen → Allgemein → SCM-Ereignisse**) und stelle sicher, dass der Owner die aktuelle Textfassung verfasst hat. Prüfe außerdem, ob die Überwachung in den Einstellungen aktiviert ist — bei deaktivierter Überwachung zeigt die Ereignisübersicht einen entsprechenden Hinweis und „Quellen erneut prüfen" ist wirkungslos. Öffne **SCM-Ereignisse** und klicke **Aktualisieren**.

## Ein Eintrag bleibt wartend

**Ursache:** Es ist keine geeignete Standard-CLI konfiguriert, eine Aufgabe ist pausiert/beendet/archiviert, die CLI ist nicht bereit oder eine Berechtigung fehlt.

**Lösung:** Wähle den Eintrag und lies den angezeigten Wartegrund. Prüfe die CLI-Einstellungen oder den Aufgabenstatus und nutze anschließend **Erneut prüfen** beziehungsweise **Öffnen / CLI auswählen**.

## Der Wartegrund meldet eine Vertrauensabfrage der CLI

**Ursache:** Claude Code, Codex oder Devin fragt beim ersten Start in einem neuen Aufgabenverzeichnis, ob der Ordner vertrauenswürdig ist („Do you trust …"). Bei Devin überspringt der empfohlene Parameter `--respect-workspace-trust false` diese Abfrage; ohne ihn bleibt der Dialog offen. Solange der Dialog offen ist, darf die Anwendung nichts in die CLI eingeben.

**Lösung:** Beantworte die Abfrage direkt im Terminal der laufenden CLI-Sitzung; die Zustellung wird danach automatisch fortgesetzt — ein Neustart oder ein Datenbankeingriff ist nicht nötig. Die Anwendung trägt die Vertrauensentscheidung für neue, von ihr angelegte Aufgabenverzeichnisse bereits in die benutzerbezogene CLI-Konfiguration ein (Claude Code: `projects.<Ordner>.hasTrustDialogAccepted` in `~/.claude.json`; Codex: `[projects."<Ordner>"] trust_level = "trusted"` in `~/.codex/config.toml`), sodass der Dialog dort gar nicht erst erscheint. Enthält das Arbeitsverzeichnis eine eigene `.claude`- bzw. `.codex`-Konfiguration, bleibt die Abfrage bewusst erhalten.

## Der Wartegrund nennt eine nicht unterstützte CLI-Version

**Ursache:** Die gestartete CLI wurde erkannt, aber ihre Version liegt außerhalb des unterstützten Bereichs — etwa nach einem automatischen Update der CLI. Unterstützt sind derzeit Codex `v0.114.x`, Claude Code `v2.1.x` und Devin CLI `v3000.11.x` (jeweils gleiches Major.Minor, beliebiger Patch). Der Wartegrund benennt die CLI und die tatsächlich erkannte Version.

**Lösung:** Du kannst die angezeigte CLI-Sitzung ausdrücklich bestätigen (**Eingabebereitschaft bestätigen**) — die tatsächliche Dateitool-Probe des Antwortkanals bleibt dabei die Sicherheitsgrenze und muss weiterhin gelingen. Alternativ die CLI der Aufgabe regulär neu starten, sobald wieder eine unterstützte Version installiert ist; die Prüfung läuft dann automatisch erneut.

## Der Wartegrund meldet ein unbekanntes Promptformat oder ein fehlendes Versionsbanner

**Ursache:** Die gestartete CLI nutzt ein Promptformat, das die Anwendung nicht kennt (z. B. GitHub Copilot CLI oder eine Shell), oder das Versionsbanner einer unterstützten CLI wurde in der Startausgabe nicht erkannt — etwa weil die CLI noch initialisiert oder die Ausgabe verändert wurde. In beiden Fällen kann die Anwendung Eingabebereitschaft und Antwortkanal nicht sicher prüfen; eine manuelle Bestätigung wird deshalb nicht angeboten.

**Lösung:** Bei einer unterstützten CLI (Codex, Claude Code, Devin) die CLI der Aufgabe regulär neu starten — danach wird das Banner erneut geprüft. Eine CLI ohne unterstütztes Promptformat kann für diese Ereigniszustellung nicht verwendet werden; wähle stattdessen eine unterstützte CLI.

## Der Wartegrund meldet eine ausstehende lokale Kanalprobe

**Ursache:** Vor der ersten Zustellung muss die CLI einen tatsächlichen Dateitool-Zugriff im Antwortkanal nachweisen. Ging diese Probe verloren (z. B. weil sie während eines geöffneten Dialogs gesendet wurde), sendet die Anwendung sie bei sichtbarer Eingabebereitschaft automatisch erneut — begrenzt auf wenige Versuche.

**Lösung:** Zunächst abwarten beziehungsweise **Erneut prüfen** — die Probe wird wiederholt, sobald die CLI wieder am Eingabeprompt steht. Meldet der Eintrag, dass die Kanalprobe wiederholt ohne Nachweis gesendet wurde, starte die CLI der Aufgabe regulär neu; danach läuft der Nachweis erneut.

## GitHub-Anmeldung oder Repositoryzugriff schlägt fehl

**Ursache:** Der Personal Access Token fehlt, ist ungültig oder hat keinen ausreichenden Zugriff.

**Lösung:** Öffne **Einstellungen → Plugins → GitHub → Authentifizierung**, aktualisiere den **Personal Access Token**, speichere die Änderung und wähle beim Ereignis **Erneut prüfen**. Prüfe auch Repositoryzugriff sowie Lese- und Schreibrechte für Issues und Pull Requests.

## Der Ausgang einer CLI-Zustellung ist ungewiss

**Ursache:** Die Eingabe könnte die CLI erreicht haben, bevor eine lokale Bestätigung gespeichert werden konnte.

**Lösung:** Klicke zuerst **Status erneut prüfen**. Verwende **Neu senden…** nur, wenn ein weiterer Versuch gewünscht ist; dadurch kann die Anfrage doppelt verarbeitet werden.
