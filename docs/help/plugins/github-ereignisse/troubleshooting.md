← [Zurück zur Übersicht](index.md)

# GitHub-Ereignisüberwachung — Fehlerbehebung

## Es erscheint kein Ereignis

**Ursache:** Das Repository oder die Referenz ist nicht mit einer aktiven Aufgabe verknüpft, das konfigurierte Trigger-Keyword fehlt, Autor/Bearbeiter ist nicht der angemeldete Repository-Owner, oder die SCM-Ereignisüberwachung ist deaktiviert.

**Lösung:** Prüfe die Verknüpfung und den Text in GitHub. Ergänze das Trigger-Keyword (Standard `$Softwareschmiede`, siehe **Einstellungen → Automatisierung → Aufgaben ausführen**) und stelle sicher, dass der Owner die aktuelle Textfassung verfasst hat. Prüfe außerdem, ob die Überwachung in den Einstellungen aktiviert ist — bei deaktivierter Überwachung zeigt die Ereignisübersicht einen entsprechenden Hinweis und „Quellen erneut prüfen" liest keine neuen Ereignisse ein. Öffne **SCM-Ereignisse** und klicke **Aktualisieren**.

## Ein Eintrag bleibt wartend

**Ursache:** Es ist keine geeignete Standard-CLI konfiguriert, eine Aufgabe ist pausiert/beendet/archiviert, die CLI ist nicht bereit oder eine Berechtigung fehlt.

**Lösung:** Wähle den Eintrag und lies den angezeigten Wartegrund. Prüfe die CLI-Einstellungen oder den Aufgabenstatus und nutze anschließend **Erneut prüfen** beziehungsweise **Öffnen / CLI auswählen**.

## Der Wartegrund meldet eine Vertrauensabfrage der CLI

**Ursache:** Claude Code, Codex oder Devin fragt beim ersten Start in einem neuen Aufgabenverzeichnis, ob der Ordner vertrauenswürdig ist („Do you trust …"). Bei Devin überspringt der empfohlene Parameter `--respect-workspace-trust false` diese Abfrage; ohne ihn bleibt der Dialog offen. Solange der Dialog offen ist, darf die Anwendung nichts in die CLI eingeben.

**Lösung:** Beantworte die Abfrage direkt im Terminal der laufenden CLI-Sitzung; die Zustellung wird danach automatisch fortgesetzt — ein Neustart oder ein Datenbankeingriff ist nicht nötig. Die Anwendung trägt die Vertrauensentscheidung für neue, von ihr angelegte Aufgabenverzeichnisse bereits in die benutzerbezogene CLI-Konfiguration ein (Claude Code: `projects.<Ordner>.hasTrustDialogAccepted` in `~/.claude.json`; Codex: `[projects."<Ordner>"] trust_level = "trusted"` in `~/.codex/config.toml`), sodass der Dialog dort gar nicht erst erscheint. Enthält das Arbeitsverzeichnis eine eigene `.claude`- bzw. `.codex`-Konfiguration, bleibt die Abfrage bewusst erhalten.

## Der Wartegrund nennt eine nicht unterstützte CLI-Version

**Ursache:** Die gestartete CLI wurde erkannt, aber ihre Version liegt außerhalb des unterstützten Bereichs — etwa nach einem automatischen Update der CLI. Unterstützt sind derzeit Codex `v0.114.x` und Claude Code `v2.1.x` (jeweils gleiches Major.Minor, beliebiger Patch) sowie Devin CLI `v3000.x` (gleiches Major, beliebige Minor- und Patch-Version). Der Wartegrund benennt die CLI und die tatsächlich erkannte Version.

**Lösung:** Du kannst die angezeigte CLI-Sitzung ausdrücklich bestätigen (**Eingabebereitschaft bestätigen**) — die tatsächliche Dateitool-Probe des Antwortkanals bleibt dabei die Sicherheitsgrenze und muss weiterhin gelingen. Alternativ die CLI der Aufgabe regulär neu starten, sobald wieder eine unterstützte Version installiert ist; die Prüfung läuft dann automatisch erneut.

## Der Wartegrund meldet ein unbekanntes Promptformat oder ein fehlendes Versionsbanner

**Ursache:** Die gestartete CLI nutzt ein Promptformat, das die Anwendung nicht kennt (z. B. GitHub Copilot CLI oder eine Shell), oder das Versionsbanner einer unterstützten CLI wurde in der Startausgabe nicht erkannt — etwa weil die CLI noch initialisiert oder die Ausgabe verändert wurde. In beiden Fällen kann die Anwendung Eingabebereitschaft und Antwortkanal nicht sicher prüfen; eine manuelle Bestätigung wird deshalb nicht angeboten.

**Lösung:** Bei einer unterstützten CLI (Codex, Claude Code, Devin) die CLI der Aufgabe regulär neu starten — danach wird das Banner erneut geprüft. Eine CLI ohne unterstütztes Promptformat kann für diese Ereigniszustellung nicht verwendet werden; wähle stattdessen eine unterstützte CLI.

## Der Wartegrund meldet eine ausstehende lokale Kanalprobe

**Ursache:** Vor der ersten Zustellung muss die CLI einen tatsächlichen Dateitool-Zugriff im Antwortkanal nachweisen. Ging diese Probe verloren (z. B. weil sie während eines geöffneten Dialogs gesendet wurde), sendet die Anwendung sie bei sichtbarer Eingabebereitschaft automatisch erneut — begrenzt auf wenige Versuche.

**Lösung:** Zunächst abwarten beziehungsweise **Erneut prüfen** — die Probe wird wiederholt, sobald die CLI wieder am Eingabeprompt steht. Meldet der Eintrag, dass die Kanalprobe wiederholt ohne Nachweis gesendet wurde, starte die CLI der Aufgabe regulär neu; danach läuft der Nachweis erneut.

## GitHub-Anmeldung oder Repositoryzugriff schlägt fehl

**Ursache:** Der Personal Access Token fehlt, ist ungültig oder hat keinen ausreichenden Zugriff.

**Lösung:** Öffne **Einstellungen → Plugins → GitHub → Authentifizierung**, aktualisiere den **Personal Access Token**, speichere die Änderung und wähle beim Ereignis **Erneut prüfen**. Prüfe auch Repositoryzugriff sowie Lese- und Schreibrechte für Issues und Pull Requests. Im Überwachungsmodus „Separates Konto" gilt dieselbe Prüfung für den Token des separaten Kontos — das Bot-Konto benötigt Collaborator-Schreibrecht auf den überwachten Repositories.

## Das Speichern meldet einen fehlenden Token des separaten Kontos

**Ursache:** Unter **Einstellungen → Automatisierung → Aufgaben ausführen** wurde der **Überwachungsmodus „Separates Konto"** gewählt, ohne dass ein Token des separaten Kontos hinterlegt ist.

**Lösung:** Trage unter **Einstellungen → Plugins → GitHub → Authentifizierung** im Feld **„Personal Access Token des separaten Kontos"** einen gültigen Personal Access Token des Bot-Kontos ein (empfohlene Scopes `notifications` und `repo`) und speichere erneut. Alternativ den Überwachungsmodus zurück auf **„Benutzerkonto"** stellen.

## Im Modus „Separates Konto" erscheint kein Ereignis

**Ursache:** Die Erwähnung ist nicht frei stehend (z. B. innerhalb eines Zitats oder Wortes), der Autor der Erwähnung ist nicht der Repository-Owner, das Bot-Konto hat keinen Zugriff auf das Repository, die Benachrichtigung betrifft ein Repository, das an keinem Projekt erfasst und aktiviert ist, oder der Benachrichtigungsgrund ist keine Erwähnung (`reason = mention`).

**Lösung:** Erwähne das separate Konto als Repository-Owner frei stehend als `@Kontoname` in einer Beschreibung oder einem Kommentar. Prüfe die **Integrationshinweise** in der SCM-Ereignisübersicht: Verworfene Benachrichtigungen zu nicht registrierten Repositories werden dort gezählt. Stelle außerdem sicher, dass das Bot-Konto als Collaborator mit Schreibrecht auf dem Repository eingeladen ist.

> **Hinweis:** Im Modus „Separates Konto" wertet die Ereignisüberwachung ausschließlich Benachrichtigungen aus — weder die repository-weite Enumeration noch verknüpfte Referenzen vorhandener Aufgaben laufen in diesem Modus. Die Backmerge-Prüfung ist davon unberührt: Sie läuft in einem eigenen Zyklus immer unter dem Benutzerkonto.

## Ein Pull-Request-Ereignis hat eine neue Aufgabe angelegt, obwohl dazu schon eine Aufgabe existiert

**Ursache:** Die Kontextzuordnung greift nur bei genau einer **aktiven** Aufgabe: Ist die frühere Aufgabe bereits beendet oder archiviert, stammt der Pull Request aus einem Fork/Fremd-Repository, weicht sein Head-Branch vom `BranchName` der Aufgabe ab oder referenziert sein Text nicht das verknüpfte Issue (`Closes`/`Fixes`/`Resolves #N`), gilt die Referenz als ohne lokale Aufgabe — und die Anwendung legt bewusst eine neue Aufgabe aus dem Ereignis an. Zeigen die Kontextsignale dagegen auf mehrere unterschiedliche Aufgaben, bleibt das Ereignis mit der Diagnose „Mehrdeutige Zuordnung zu Aufgaben" stehen und es wird gar keine Aufgabe angelegt.

**Lösung:** Prüfe die neue sowie die frühere Aufgabe in der Aufgabenverwaltung. War die frühere Aufgabe bereits beendet, ist die Neuanlage das vorgesehene Verhalten — die neue Aufgabe kann regulär genutzt oder gelöscht werden. Soll das Ereignis doch an eine laufende Aufgabe gehen, sicherstellen, dass der Pull Request auf deren Aufgaben-Branch liegt oder im PR-Text das Aufgaben-Issue per Closing-Direktive referenziert wird, und den Eintrag anschließend über **Erneut prüfen** erneut einlesen lassen.

## Der Ausgang einer CLI-Zustellung ist ungewiss

**Ursache:** Die Eingabe könnte die CLI erreicht haben, bevor eine lokale Bestätigung gespeichert werden konnte.

**Lösung:** Klicke zuerst **Status erneut prüfen**. Verwende **Neu senden…** nur, wenn ein weiterer Versuch gewünscht ist; dadurch kann die Anfrage doppelt verarbeitet werden.

## Ein erkannter Backmerge-Pull-Request wird nicht gemergt

**Ursache:** Der Provider meldet den Pull Request noch nicht als mergebar (z. B. laufende oder fehlende Statuschecks, fehlende Reviews, Konflikte), der Pull Request ist noch als Entwurf markiert, oder der reguläre Merge scheitert am Branch-Schutz. Auch eine deaktivierte Funktion, ein fehlender Basis-Branch oder fehlendes Schreibrecht des Tokens kommen in Frage.

**Lösung:**
1. Öffne die **Integrationshinweise** in der SCM-Ereignisübersicht — die Diagnose des Backmerge-Checkpoints nennt den Grund (z. B. „wartet auf Mergebarkeit", „Pull Request ist ein Entwurf" oder „Merge fehlgeschlagen").
2. Bei einem Entwurfs-PR: Entweder den Entwurf in GitHub manuell aufheben oder die Option **„Draft-Pull-Requests vor dem Merge automatisch aktivieren"** unter **Einstellungen → Automatisierung → Prozessabläufe** einschalten — dann wird der Pull Request vor dem Merge automatisch als bereit für Review markiert.
3. Bei einem am Branch-Schutz scheiternden Merge: Die Option **„Bei blockiertem Merge Admin-Merge erlauben"** unter **Einstellungen → Automatisierung → Prozessabläufe** einschalten — dann wird der gescheiterte reguläre Merge einmalig als Admin-Merge wiederholt (Voraussetzung: Token mit Admin-Recht).
4. Prüfe, dass der Modus **„Backmerge-Pull-Requests"** nicht auf „Deaktiviert" steht, ein vom Hauptbranch abweichender Basis-Branch konfiguriert ist und — im Modus „Projektbezogen aktivierbar" — die Freigabe in der Projektdetailansicht eingeschaltet ist.
5. Mit **Quellen erneut prüfen** in der SCM-Ereignisübersicht lässt sich der Prüfzyklus sofort erneut anstoßen; dabei wird auch der Fehler-Backoff zurückgesetzt.
