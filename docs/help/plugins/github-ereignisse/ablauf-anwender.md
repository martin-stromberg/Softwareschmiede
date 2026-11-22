← [Zurück zur Übersicht](index.md)

# GitHub-Ereignisüberwachung — Ablauf für Anwender

## Voraussetzungen

- Das GitHub-Repository ist mit einer Aufgabe verknüpft; die Aufgabe enthält eine Issue- oder Pull-Request-Referenz.
- In **Einstellungen → Plugins → GitHub → Authentifizierung** ist ein gültiger **Personal Access Token** gespeichert, der auf das Repository zugreifen darf.
- Der Text enthält das Trigger-Keyword (Standard `$Softwareschmiede`, einstellbar unter **Einstellungen → Automatisierung → Aufgaben ausführen**) und stammt vom Repository-Owner oder dem GitHub-Actions-Konto des Repositories. Bei einer bearbeiteten Textfassung muss eine dieser Identitäten auch als Bearbeiter erkennbar sein. Im Überwachungsmodus **„Separates Konto“** gilt stattdessen: Der Repository-Owner oder eine Repository-Automatisierung über das GitHub-Actions-Konto erwähnt das separate Konto frei stehend als `@Kontoname`; das Trigger-Keyword ist dann nicht erforderlich. Voraussetzung ist ein unter **Einstellungen → Plugins → GitHub → Authentifizierung** hinterlegter Token des separaten Kontos.
- Die SCM-Ereignisüberwachung ist aktiviert (Checkbox „Aufgaben automatisch starten" unter **Einstellungen → Automatisierung → Aufgaben ausführen**, standardmäßig an). Ist sie deaktiviert, werden weder neue Ereignisse eingelesen noch bestehende automatisch zugestellt; die manuellen Aktionen in der Übersicht bleiben nutzbar.
- Für die automatische CLI-Verarbeitung werden derzeit Codex CLI, Claude CLI oder Devin CLI nur in einer unterstützten Version (Codex `v0.114.x`, Claude Code `v2.1.x`, Devin CLI `v3000.x`) und mit einem tatsächlich geprüften Schreibzugriff verwendet. Eine erkannte Version außerhalb des Bereichs lässt sich beim wartenden Ereignis ausdrücklich bestätigen; andere ausgewählte KI-Plugins können in der Ereignisverarbeitung wartend bleiben.
- Die CLI darf erlaubte Issue-/PR-Aktionen (Kommentar, Review, Merge inkl. Admin-Merge, Commit/Push, Schließen u. a.) selbst über `gh`/git ausführen: Die Sitzung erhält beim Start dieselbe GitHub-Anmeldung wie die Anwendung (Token aus **Einstellungen → Plugins → GitHub → Authentifizierung** als Prozessumgebung). Der Aktionsumfang bleibt durch die Token-/Repository-Berechtigungen begrenzt; `gh` muss auf dem Rechner erreichbar sein.
- Optional kannst du unter **Einstellungen → Automatisierung → Aufgaben ausführen** Prompt-Anhänge pflegen: Texte, die der Anwendung bei der Zustellung je Ereignisart angehängt werden (Allgemein gilt immer, dazu kommt der Anhang für Issue, Issue-Kommentar, Pull Request oder Pull-Request-Kommentar inklusive Reviews). So erhält die CLI z. B. Hinweise auf verfügbare Skills oder Bearbeitungsregeln, ohne dass sie in jedem GitHub-Kommentar stehen müssen. Leere Felder hängen nichts an; Trigger-Keyword und gespeicherter Ereignistext bleiben unverändert.

## Schritt-für-Schritt-Anleitung

### 1. Anfrage in GitHub schreiben

Schreibe das Trigger-Keyword (Standard `$Softwareschmiede`) in die Beschreibung, einen Kommentar oder Review-Text des verknüpften Issues oder Pull Requests. Ergänze die konkrete Bitte im selben Text.

> **Hinweis:** Im selben Text kannst du mit dem Verzögerungs-Keyword (Standard `#verzögert`, einstellbar unter Einstellungen → Automatisierung → Aufgaben ausführen) eine zeitversetzte Ausführung anfordern — etwa `#verzögert 30min`, `#verzögert 4h`, `#verzögert 2t`, `#verzögert 1w` oder `#verzögert m1` für den 1. eines Monats (bereits erreichte Tage zählen für den Folgemonat), maximal 32 Tage. Das Ereignis erhält dann den Status **Verzögert**, die Aufgabe wird bis dahin pausiert und der Empfang auf GitHub kommentiert; der Wartezustand übersteht Neustarts und blockiert den Auto-Shutdown. Eine ungültige Angabe wird auf GitHub begründet zurückgewiesen und die Anfrage regulär verarbeitet.

> **Hinweis:** Im Überwachungsmodus **„Separates Konto“** wird die Anfrage stattdessen ausgelöst, indem der Repository-Owner — oder ein Repository-Workflow über das GitHub-Actions-Konto — das separate Konto frei stehend als `@Kontoname` in der Beschreibung oder einem Kommentar erwähnt. Benachrichtigungen zu Repositories, die nicht an einem Projekt erfasst und aktiviert sind, werden verworfen — ihre Anzahl erscheint in den Integrationshinweisen der Ereignisübersicht.

> **Hinweis:** Ein Kommentar, auf den dein Konto bereits reagiert hat — etwa mit der von der Anwendung gesetzten 👀-Reaktion oder einer manuell vergebenen Reaktion —, gilt als bereits bearbeitet. Auch ein späteres Bearbeiten dieses Kommentars löst keine neue Anfrage aus; schreibe stattdessen einen neuen Kommentar.

### 2. Eingang prüfen

Öffne links **SCM-Ereignisse**. Die Liste zeigt Aufgabe, Repository, Ursprung, Art, Eingangszeit und Status. Mit **Aktualisieren** lässt sich die Anzeige neu laden. Wähle einen Eintrag, um den vollständigen Text, die Ursprungsadresse und einen möglichen Wartegrund zu sehen.

### 3. Wartenden Eintrag öffnen

Wähle den Eintrag und klicke **Öffnen / CLI auswählen** oder drücke Enter. Wenn eine passende laufende CLI vorhanden ist, bestätigt ein Dialog die Übergabe an diese Sitzung. Andernfalls kannst du im Auswahlfenster eine aktivierte CLI auswählen. Ein abgebrochener Auswahlvorgang lässt den Eintrag wartend.

### 4. Status prüfen oder erneut senden

Bei einem unklaren Ausgang klicke zuerst **Status erneut prüfen**. **Neu senden…** startet nur nach einer zusätzlichen Bestätigung einen neuen Versuch. Der Dialog weist darauf hin, dass der vorherige Versuch bereits verarbeitet worden sein könnte.

### 5. Ereignis abbrechen

Ist ein Eintrag nicht mehr relevant (z. B. Anfrage hinfällig oder anderweitig erledigt), wähle ihn und klicke **Abbrechen…**. Nach einer kurzen Bestätigung erhält das Ereignis den Endstatus **Abgebrochen**: Es folgen keine weiteren Zustell- oder Prüfversuche und keine weiteren GitHub-Antworten. Eine bereits zugestellte, laufende CLI-Verarbeitung wird dadurch **nicht** beendet — sie läuft regulär weiter, nur die Ereignisverfolgung endet. Der Eintrag bleibt sichtbar und ist über den Filter **Abgebrochen** oder **Alle** auffindbar. Bereits verarbeitete oder abgebrochene Einträge können nicht erneut abgebrochen werden.

## Ergebnis

Die Liste zeigt den Verarbeitungsstatus. Bei erfolgreicher Bestätigung erhalten Kommentare und veröffentlichte Pull-Request-Reviews auf GitHub 👀; für Beschreibungen wird keine Reaktion gesetzt. Wartende Einträge zeigen den Grund, soweit er bekannt ist. Gehen zu einer Aufgabe mehrere Anfragen ein, werden sie grundsätzlich in Eingangsreihenfolge verarbeitet: Ein jüngeres Ereignis wartet, solange ein früheres noch offen ist. Ein früheres Ereignis, das nur noch manuell zu klären ist („Verarbeitung ungeklärt" oder „Zustellung ungewiss"), hält die Zustellung späterer Ereignisse dagegen nicht auf — es bleibt in der Übersicht sichtbar und lässt sich weiterhin manuell klären. Die Ereignisverarbeitung selbst beendet die Aufgabe nicht; unabhängig davon kann die optionale **Abschlussüberwachung** (Einstellungen → Automatisierung → Aufgaben beenden) die Aufgabe später automatisch beenden, sobald ihre verknüpfte SCM-Referenz geschlossen oder gemergt ist. Ein noch offenes, sich weiterbewegendes Ereignis stellt diese Beobachtung zurück; ein bereits verarbeitetes, abgebrochenes oder nur noch manuell zu klärendes Ereignis hält sie dagegen nicht auf.

## Screenshot-Nachweise

Hat die CLI während des Laufs Nachweis-Bilder im vorgegebenen `attachmentsPath`-Verzeichnis des Antwortkanals abgelegt, veröffentlicht die Anwendung sie nach dem Abschluss als eigenen GitHub-Kommentar „Screenshot-Nachweise des abgeschlossenen Laufs.": Jedes gültige Bild erscheint eingebettet, eine vom Agenten deklarierte Unterschrift steht kursiv darunter. Dateien, die die Prüfung nicht bestehen (Format, Größe, unzulässiger Pfad u. a.), stehen im selben Kommentar als kursive Zeile „Übersprungen: `<Name>` (`<Grund>`)" — war nichts hochladbar, trägt der Kommentar die Überschrift „Die Screenshot-Nachweise des abgeschlossenen Laufs konnten nicht hochgeladen werden." und nennt ausschließlich die Übersprungen-Gründe.

Schlägt der Upload oder das Posten des Kommentars fehl, bleibt die Aktion offen und erscheint in der Ereignisübersicht unter den **Integrationshinweisen** als „Screenshot-Nachweis (`<owner/repo>` Issue/Pull Request #N): `<Fehler>`". Für einen erneuten Versuch öffne das zugehörige Ereignis über den Filter **Verarbeitet** oder **Alle** und wähle **Erneut prüfen** — das setzt den Warte-Backoff zurück und führt die Aktion erneut aus; ein bereits angekommener Nachweis-Kommentar wird über einen unsichtbaren Marker erkannt und nicht doppelt erzeugt. Abgebrochene Ereignisse wiederholen nichts. Meldet der Hinweis einen ungewissen Ausgang, ist lediglich unklar, ob der Kommentar GitHub bereits erreicht hat — prüfe ihn in diesem Fall auf GitHub; ein automatischer Neuversand erfolgt nicht.

## Barrierefreiheit

Die Ereignisliste kann mit der Tastatur bedient werden. Enter öffnet den ausgewählten Eintrag. Schaltflächen und Liste stellen Namen für unterstützende Bedienhilfen bereit.
