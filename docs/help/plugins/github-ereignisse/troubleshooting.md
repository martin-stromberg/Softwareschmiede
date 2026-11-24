← [Zurück zur Übersicht](index.md)

# GitHub-Ereignisüberwachung — Fehlerbehebung

## Es erscheint kein Ereignis

**Ursache:** Das Repository oder die Referenz ist nicht mit einer aktiven Aufgabe verknüpft, das konfigurierte Trigger-Keyword fehlt, Autor/Bearbeiter ist weder der Repository-Owner noch ein vertrauenswürdiges Automatisierungskonto (`github-actions[bot]`/`app/github-actions`), die SCM-Ereignisüberwachung ist deaktiviert — oder das eigene Konto hat auf den Kommentar bereits reagiert, etwa mit der von der Anwendung gesetzten 👀-Empfangsreaktion oder einer manuell vergebenen Reaktion. Ein solcher Kommentar gilt als bereits bearbeitet; auch ein nachträgliches Bearbeiten löst kein neues Ereignis aus.

**Lösung:** Prüfe die Verknüpfung und den Text in GitHub. Ergänze das Trigger-Keyword (Standard `$Softwareschmiede`, siehe **Einstellungen → Automatisierung → Aufgaben ausführen**) und stelle sicher, dass der Owner oder das GitHub-Actions-Konto die aktuelle Textfassung verfasst hat. Prüfe außerdem, ob die Überwachung in den Einstellungen aktiviert ist — bei deaktivierter Überwachung zeigt die Ereignisübersicht einen entsprechenden Hinweis und „Quellen erneut prüfen" liest keine neuen Ereignisse ein. Prüfe schließlich, ob unter dem Kommentar bereits eine Reaktion deines Kontos steht — dann nur ein neuer Kommentar löst eine weitere Anfrage aus. Öffne **SCM-Ereignisse** und klicke **Aktualisieren**.

## Ein Eintrag bleibt wartend

**Ursache:** Es ist keine geeignete Standard-CLI konfiguriert, eine Aufgabe ist pausiert/beendet/archiviert, die CLI ist nicht bereit oder eine Berechtigung fehlt.

**Lösung:** Wähle den Eintrag und lies den angezeigten Wartegrund. Prüfe die CLI-Einstellungen oder den Aufgabenstatus und nutze anschließend **Erneut prüfen** beziehungsweise **Öffnen / CLI auswählen**.

## Der Wartegrund meldet eine kurze Stabilisierungsphase

**Ursache:** Die CLI der Aufgabe ist zwar am Eingabeprompt, steht dort aber erst seit wenigen Sekunden ununterbrochen. Vor jeder Prompt-Zustellung wartet die Anwendung eine kurze Stabilisierungsphase von etwa zehn Sekunden ab, damit die Sitzung wirklich ruht, bevor die Eingabe eintrifft. Der Eintrag zeigt in dieser Zeit den Wartegrund „Die CLI ist erst seit Kurzem eingabebereit; die Zustellung wird nach einer kurzen Stabilisierungsphase erneut versucht." (bei Aufbereitungsaufträgen „…die Aufbereitung wird nach einer kurzen Stabilisierungsphase erneut versucht."). Wechselt die Sitzung zwischendurch in einen anderen Zustand, beginnt die Frist von vorn.

**Lösung:** Kein Eingriff nötig — der nächste Versuch läuft automatisch, sobald die Frist erfüllt ist; je nach Prüfzyklus kann die effektive Verzögerung etwas über zehn Sekunden liegen. Bleibt der Eintrag ungewöhnlich lange auf diesem Grund stehen, prüfe, ob die CLI-Sitzung tatsächlich lebt und am Prompt steht (Aufgabe öffnen), und wähle gegebenenfalls **Erneut prüfen**.

## Der Wartegrund meldet eine gespeicherte Standard-CLI-Zuordnung, die nicht mehr installiert oder aktiviert ist

**Ursache:** Eine für Aufgabe, Projekt oder als globaler Standard gespeicherte CLI-Zuordnung zeigt auf ein KI-Plugin, das deaktiviert, deinstalliert oder nicht ladbar ist, und keine andere konfigurierte Standard-Stufe liefert ein ausführbares Plugin. Die automatische Auswahl überspringt solche Zuordnungen still in Richtung des nächsten Standards; erst wenn keine Stufe mehr ein ausführbares Plugin liefert, bleibt das Ereignis mit dieser Meldung wartend. Der Anlass steht zusätzlich als Systemmeldung im Aufgabenprotokoll. Der Eintrag ist dabei nicht dauerhaft gesperrt.

**Lösung:** Aktiviere oder installiere das im Wartegrund benannte KI-Plugin unter **Einstellungen → Plugins**, oder korrigiere die gespeicherte Zuordnung (Aufgaben-/Projekt-Standard beziehungsweise **Standard KI-Plugin**). Danach genügt **Erneut prüfen** oder der nächste automatische Prüfzyklus — ein manuelles Entsperren ist nicht nötig. Alternativ kannst du das Ereignis öffnen und dort ausdrücklich eine CLI wählen; diese manuelle Auswahl wird allerdings streng geprüft und ersetzt nicht automatisch eine nicht verfügbare Wahl.

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

## Die Integrationshinweise melden ein erschöpftes GitHub-Kontingent

**Ursache:** GitHub begrenzt die Zahl der API-Aufrufe pro Konto und Zeitraum (Rate-Limit). Wenn das Kontingent erschöpft ist — etwa durch sehr viele überwachte Repositories oder weil der Token parallel von anderen Werkzeugen (z. B. `gh`-Aufrufe der CLI-Agenten) genutzt wird — stellt die Anwendung weitere Aufrufe des betroffenen Kontos und Zugriffswegs bis zum von GitHub gemeldeten Reset-Zeitpunkt ein, statt weiter gegen das Limit zu laufen. Der Hinweis „GitHub-Kontingent erschöpft; nächster Versuch nach dem Rate-Limit-Reset." in den **Integrationshinweisen** der Ereignisübersicht nennt die betroffene Quelle; in dieser Zeit kommen keine neuen Ereignisse aus dem betroffenen Kontingent an, wartende Ereignisse bleiben aber erhalten.

**Lösung:**
1. Zunächst den Reset-Zeitpunkt abwarten — die Überwachung setzt danach automatisch fort, ein manueller Eingriff ist nicht nötig.
2. Wiederholt sich das Problem regelmäßig, prüfe, ob weitere Werkzeuge denselben Token nutzen — im Modus **„Separates Konto"** verwenden auch die `gh`-Aufrufe der Agenten-CLIs das Kontingent des separaten Kontos.
3. Viele überwachte Repositories erhöhen den Grundverbrauch; deaktiviere nicht benötigte Projekt-Repositories.

> **Hinweis:** Die Anwendung schont das Kontingent bereits aktiv — Repository-Auflösungen laufen gecacht und über das eigene, meist freie GraphQL-Kontingent. Eine tatsächliche Erschöpfung deutet daher meist auf externe Verbraucher desselben Tokens oder eine sehr große überwachte Repository-Menge hin.

## Das Speichern meldet einen fehlenden Token des separaten Kontos

**Ursache:** Unter **Einstellungen → Automatisierung → Aufgaben ausführen** wurde der **Überwachungsmodus „Separates Konto"** gewählt, ohne dass ein Token des separaten Kontos hinterlegt ist.

**Lösung:** Trage unter **Einstellungen → Plugins → GitHub → Authentifizierung** im Feld **„Personal Access Token des separaten Kontos"** einen gültigen Personal Access Token des Bot-Kontos ein (empfohlene Scopes `notifications` und `repo`) und speichere erneut. Alternativ den Überwachungsmodus zurück auf **„Benutzerkonto"** stellen.

## Im Modus „Separates Konto" erscheint kein Ereignis

**Ursache:** Die Erwähnung ist nicht frei stehend (z. B. innerhalb eines Zitats oder Wortes), der Autor der Erwähnung ist weder der Repository-Owner noch das vertrauenswürdige `github-actions`-Konto, das Bot-Konto hat keinen Zugriff auf das Repository, die Benachrichtigung betrifft ein Repository, das an keinem Projekt erfasst und aktiviert ist, oder der Benachrichtigungsgrund ist kein ausgewerteter Auslöser (`reason = mention` oder `reason = assign`).

Bei einer **Zuweisungs-Benachrichtigung** (`reason = assign`) kommen zusätzlich in Frage: Die Zuweisung wurde inzwischen entzogen (`unassigned` auf das Bot-Konto — die Notification bleibt in der Inbox, gilt aber nicht mehr als wirksam), die Zuweisung stammt von einem Konto außerhalb des berechtigten Kreises (Repository-Owner, das Bot-Konto selbst oder `github-actions`), oder die Referenzbeschreibung ist leer. Diese Ablehnungen stehen begründet in den **Integrationshinweisen** (z. B. „Zuweisung nicht durch berechtigtes Konto", „Referenzbeschreibung ist leer", „Zuweisender nicht bestimmbar").

**Lösung:** Erwähne das separate Konto als Repository-Owner frei stehend als `@Kontoname` in einer Beschreibung oder einem Kommentar — aus einem Repository-Workflow heraus funktioniert das auch über das `github-actions`-Konto. Alternativ weise dem Bot-Konto ein Issue oder einen Pull Request mit gefüllter Beschreibung zu (Zuweisender: Owner, Bot-Konto oder `github-actions`); wurde die Zuweisung versehentlich entzogen, einfach erneut zuweisen. Prüfe die **Integrationshinweise** in der SCM-Ereignisübersicht: Verworfene Benachrichtigungen zu nicht registrierten Repositories und begründete Zuweisungs-Ablehnungen werden dort sichtbar. Stelle außerdem sicher, dass das Bot-Konto als Collaborator mit Schreibrecht auf dem Repository eingeladen ist.

> **Hinweis:** Im Modus „Separates Konto" wertet die Ereignisüberwachung ausschließlich Benachrichtigungen aus — weder die repository-weite Enumeration noch verknüpfte Referenzen vorhandener Aufgaben laufen in diesem Modus. Die Backmerge-Prüfung und die Entdeckung der Issue-Aufbereitung sind davon unberührt: Sie laufen in eigenen Zyklen immer unter dem Benutzerkonto — eine aktivierte Aufbereitung funktioniert daher auch in diesem Modus.

## Im Modus „Separates Konto" schlägt ein Aufbereitungsereignis mit „Repository- oder Benutzeridentität hat sich geändert" fehl

**Ursache:** In älteren Versionen legte die Issue-Aufbereitung das Monitoring-Konto nur als Flag auf dem vom Benutzerkonto enumerierten Snapshot ab — das Ereignis trug also `SeparatesKonto`, aber die Viewer-Identität des Benutzerkontos. Der Identitätsschutz der Antwortausführung (Vergleich von Repository- und Viewer-ID gegen eine frische Auflösung unter dem gespeicherten Konto) schlägt auf solchen Alt-Snapshots zu Recht Alarm. Neu eingereihte Ereignisse sind seit der Korrektur automatisch konto-konsistent (das Repository wird vor dem Einreihen unter dem Ausführungskonto neu aufgelöst).

**Lösung (einmalige Reparatur bereits persistierter Ereignisse):** Breche das betroffene Issue-Aufbereitungsereignis in der SCM-Ereignisübersicht über **Abbrechen…** ab und lösche — falls die Aufgabe erneut laufen soll — den Auswertungs-Checkpoint `issueaufbereitung-issue/{provider}/{host}/{repositoryId}/{nummer}` des Issues (Datenbanktabelle `ScmObservationCheckpoints`), damit der Entdeckungszyklus das Issue erneut auswertet und ein konsistentes Ereignis einreiht. **Achtung bei bereits teilausgeführten Ereignissen:** Prüfe vor der Wiederholung in GitHub, ob Beschreibung oder Kommentare des Issues bereits remote geändert wurden — die Marker-Idempotenz (`<!-- softwareschmiede:`) verhindert zwar echte Doppel-POSTs, ein erneuter Lauf kann aber erneute Analyse-Kommentare erzeugen, wenn der ursprüngliche Lauf sie unter einer anderen Identität nicht markergefunden hat. Läuft das Ereignis in einer Aufgabe mit bereits erledigten Aktionen, ist das Abbrechen ohne Checkpoint-Löschung die sichere Variante: Das Issue wird dann nicht nochmals aufbereitet.

## Ein vom Workflow erzeugtes Review-Issue löst kein Ereignis aus

**Ursache:** Die geplanten Review-Workflows erzeugen zeitgesteuert Issues mit frei stehender `@softwarewerkstatt`-Erwähnung (siehe `docs/CI_CD.md`, Abschnitt „Geplante Review-Workflows"). Ein Issue des GitHub-Actions-Kontos (`github-actions[bot]`/`app/github-actions`) wird regulär angenommen — die Workflow-Autorenschaft ist keine Fehlerursache. Bleibt das Ereignis dennoch aus, kommt eine der folgenden Ursachen in Frage: Der Issue-Body wurde nachträglich von einem Konto bearbeitet, das weder Owner noch `github-actions` ist (bei bearbeiteten Textfassungen zählt der Bearbeiter statt des Autors), `@softwarewerkstatt` ist nicht als Collaborator mit Schreibrecht eingeladen (GitHub stellt dann keine Mention-Benachrichtigung zu — das Issue existiert, erreicht die Inbox aber nie), oder das Repository ist an keinem Projekt erfasst und aktiviert.

**Lösung:** Prüfe Bearbeitungsstand des Issues in GitHub sowie die **Integrationshinweise** in der SCM-Ereignisübersicht — verworfene Benachrichtigungen zu nicht registrierten Repositories werden dort gezählt. Stelle außerdem sicher, dass `@softwarewerkstatt` Collaborator mit Schreibrecht auf dem Repository ist und das Repository an einem Projekt erfasst und aktiviert ist.

## Der Wartegrund nennt eine zweite Installation

**Ursache:** Auf die gespeicherte Textfassung wurde bereits mit dem Ereignis-Konto reagiert — typischerweise, weil eine parallel betriebene zweite Installation (z. B. eine Vorabversion mit eigener Datenbank) denselben Kommentar bereits übernommen und quittiert hat. Der Eintrag wird bewusst nicht zugestellt, damit die Anfrage nicht zweimal bearbeitet wird.

**Lösung:** Ist die zweite Installation beabsichtigt und verarbeitet die Anfrage, kann der Eintrag über **Abbrechen…** beendet werden — die Verarbeitung läuft in der anderen Installation weiter. **Erneut prüfen** und **Neu senden…** führen zum selben Ergebnis, solange die Reaktion des Kontos auf dem Kommentar besteht.

## Ein Pull-Request-Ereignis hat eine neue Aufgabe angelegt, obwohl dazu schon eine Aufgabe existiert

**Ursache:** Die Kontextzuordnung greift nur bei genau einer **aktiven** Aufgabe: Ist die frühere Aufgabe bereits beendet oder archiviert, stammt der Pull Request aus einem Fork/Fremd-Repository, weicht sein Head-Branch vom `BranchName` der Aufgabe ab oder referenziert sein Text nicht das verknüpfte Issue (`Closes`/`Fixes`/`Resolves #N`), gilt die Referenz als ohne lokale Aufgabe — und die Anwendung legt bewusst eine neue Aufgabe aus dem Ereignis an. Zeigen die Kontextsignale dagegen auf mehrere unterschiedliche Aufgaben, bleibt das Ereignis mit der Diagnose „Mehrdeutige Zuordnung zu Aufgaben" stehen und es wird gar keine Aufgabe angelegt.

**Lösung:** Prüfe die neue sowie die frühere Aufgabe in der Aufgabenverwaltung. War die frühere Aufgabe bereits beendet, ist die Neuanlage das vorgesehene Verhalten — die neue Aufgabe kann regulär genutzt oder gelöscht werden. Soll das Ereignis doch an eine laufende Aufgabe gehen, sicherstellen, dass der Pull Request auf deren Aufgaben-Branch liegt oder im PR-Text das Aufgaben-Issue per Closing-Direktive referenziert wird, und den Eintrag anschließend über **Erneut prüfen** erneut einlesen lassen.

## Zu einem Issue existieren zwei Aufgaben

**Ursache:** Ältere Versionen legten bei einer Issue-Neuanlage mit Trigger-Kommentar neben der Aufbereitungs-Aufgabe eine zweite, reguläre Aufgabe an. Solche Altbestand-Paare werden nicht nachträglich bereinigt oder zusammengeführt. Neu eintreffende Kommentare des Issues ordnen sich dabei weiterhin der regulären Aufgabe zu — sie landen nicht in der Aufbereitungs-Aufgabe, deren Auftrag davon unberührt bleibt.

**Lösung:** Beide Aufgaben lassen sich regulär weiterverwenden. Soll das Durcheinander aufgeräumt werden, kann die nicht mehr benötigte Aufgabe manuell beendet oder gelöscht werden; bei neuen Issue-Neuanlagen entsteht das Paar nicht mehr — Aufbereitungsauftrag und Trigger-Kommentar laufen dann in derselben Aufgabe.

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

## Die Integrationshinweise melden einen offenen Screenshot-Nachweis

**Ursache:** Nach dem Abschluss eines Laufs sollten vom Agenten im `attachmentsPath`-Verzeichnis des Antwortkanals abgelegte Nachweis-Bilder als GitHub-Kommentar veröffentlicht werden; der Upload oder das Posten des Kommentars ist fehlgeschlagen oder steht noch aus. Der Hinweis „Screenshot-Nachweis (`<owner/repo>` Issue/Pull Request #N): `<Fehler>`" in den **Integrationshinweisen** der Ereignisübersicht nennt den Grund.

**Lösung:** Öffne das zugehörige Ereignis über den Filter **Verarbeitet** oder **Alle** und wähle **Erneut prüfen** — das setzt den Warte-Backoff zurück und führt die Aktion erneut aus. Ein bereits angekommener Nachweis-Kommentar wird über einen unsichtbaren Marker erkannt und nicht doppelt erzeugt. Meldet der Hinweis einen ungewissen Ausgang, prüfe zuerst auf GitHub, ob der Kommentar bereits angekommen ist — in diesem Fall sendet die Anwendung bewusst nicht automatisch erneut. Enthält ein veröffentlichter Kommentar ausschließlich Übersprungen-Zeilen, nennt er für jede Datei den Verwerfungsgrund — etwa ungültiges Format, zu große Datei oder „Upload wird für diesen Host nicht unterstützt" auf Hosts außerhalb `github.com`.

## Die Aufgabe wird nicht automatisch beendet

**Ursache:** Die Abschlussüberwachung setzt neben einer geschlossenen oder gemergten SCM-Referenz voraus, dass kein noch offenes, sich weiterbewegendes Ereignis der Aufgabe existiert: Die Stati „Aufgenommen", „Verzögert", „Wartend", „ZustellungVorbereitet", „Zugestellt" und „InBearbeitung" stellen die Beobachtung zurück — ebenso jeder unbekannte Status. Verarbeitete, abgebrochene und terminal ungeklärte Ereignisse („Verarbeitung ungeklärt", „Zustellung ungewiss") halten den automatischen Abschluss dagegen nicht auf. Zusätzlich muss die Abschlussüberwachung aktiviert sein (**Einstellungen → Automatisierung → Aufgaben beenden**), der Prüfmodus muss die Aufgabe abdecken (im Standard nur ereignisgesteuerte Aufgaben), und offene To-Dos oder ein nicht löschbarer Klon können den Abschluss selbst blockieren.

**Lösung:** Prüfe in der **SCM-Ereignisse**-Übersicht, ob noch ein offenes Ereignis der Aufgabe wartet, und lies seinen Wartegrund — ein blockierter Eintrag (etwa fehlende Berechtigung oder fehlende Standard-CLI) hält den automatischen Abschluss bewusst zurück; nach der Korrektur über **Erneut prüfen** oder **Abbrechen…** läuft die Beobachtung weiter. Kontrolliere außerdem Schalter und Prüfmodus unter **Einstellungen → Automatisierung → Aufgaben beenden** sowie offene To-Dos der Aufgabe.

## Aufgabenprotokoll meldet „Abschlussüberwachung … ausgesetzt"

**Ursache:** Die Referenzprüfung oder der automatische Abschluss der Aufgabe ist mehrfach hintereinander an derselben Fehlerursache gescheitert (Fehlersignatur = Exception-Typ und Meldung) — etwa ein nicht löschbarer, von einem Prozess gesperrter lokaler Klon, wiederholt scheiternde SCM-Aufrufe oder offene To-Dos. Nach zehn Fehlversuchen mit wachsendem Abstand (1 Minute bis 60 Minuten Backoff) setzt die Anwendung die automatische Überwachung dieser Aufgabe aus, statt endlos weiterzuprobieren. Der Protokolleintrag nennt den letzten Fehler; bei einer Klon-Dateisperre zusätzlich die erkannten sperrenden Prozesse („offen gehalten von: …"), z. B. eine verwaiste CLI-/Editor-Sitzung, die noch Dateien oder ihr Arbeitsverzeichnis im Klon offen hält.

**Lösung:** Behebe die genannte Ursache — schließe bei einer Dateisperre den genannten Prozess (häufig eine alte CLI-Session oder ein Editor/Terminal mit dem Klon als Arbeitsverzeichnis), kläre die SCM-Anbindung (Token, Netzwerk, Rate-Limit) oder schließe die offenen To-Dos. Danach die Aufgabe manuell abschließen — oder die Anwendung neu starten: Das Wiederholungsbudget liegt im Arbeitsspeicher und beginnt nach einem Neustart neu, sodass die Überwachung von vorn und bei Erfolg wie gewohnt abschließt.
