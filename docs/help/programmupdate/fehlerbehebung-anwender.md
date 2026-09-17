← [Zurück zur Übersicht](index.md)

# Programmupdate — Fehlerbehebung für Anwender

Hinweise und Fehlermeldungen zum Programmupdate erscheinen als Hinweistext unter den Schaltflächen am Fuß der Seitenleiste oder im Fortschrittsdialog „Update vorbereiten".

## „⟳ Prüfen" ist deaktiviert

**Mögliche Ursachen:**

1. Der Update-Modus steht auf **Aus** — öffnen Sie die Einstellungen, Registerkarte **Allgemein**, Abschnitt **Updates**, wählen Sie einen anderen Modus und klicken Sie **Speichern**.
2. Es läuft bereits eine Prüfung oder Update-Vorbereitung — warten Sie deren Abschluss ab.
3. Die Update-Einstellungen konnten noch nicht gelesen werden — z. B. nach einem Lesefehler beim Start. Öffnen Sie die Einstellungen und speichern Sie sie erneut; danach wird die Schaltfläche wieder freigegeben.

## „⇧ Update" wird nicht angezeigt

**Mögliche Ursachen:**

1. Es wurde noch keine Prüfung durchgeführt oder kein Update gefunden — klicken Sie auf **⟳ Prüfen**.
2. Das angebotene Update ist nur als Prerelease verfügbar — aktivieren Sie in den Einstellungen die Checkbox **„Prerelease-Versionen laden"**, speichern Sie und prüfen Sie erneut.
3. Die Update-Einstellungen wurden geändert — ein zuvor angezeigtes Angebot wird dabei zurückgenommen; prüfen Sie danach erneut.

## Hinweis „Kein Update verfügbar. Die installierte Version ist aktuell."

Die Prüfung war erfolgreich — es gibt keine neuere passende Version. Wenn Sie eine Vorabversion erwarten, prüfen Sie, ob **„Prerelease-Versionen laden"** aktiviert und gespeichert ist.

## Hinweis „GitHub-Release ist nicht prüfbar." oder „Update-Prüfung ist fehlgeschlagen."

Der Update-Server konnte nicht zuverlässig befragt werden (z. B. Netzwerkproblem, Zeitüberschreitung, fehlerhafte Antwort).

1. Prüfen Sie Ihre Internetverbindung und ob `github.com` erreichbar ist.
2. Warten Sie einen Moment und klicken Sie erneut auf **⟳ Prüfen**.
3. Die Anwendung bleibt uneingeschränkt bedienbar; es wird nichts installiert.

## Hinweis „Lokale Version ist nicht prüfbar."

Die installierte Version kann nicht ermittelt werden. Dies deutet auf eine beschädigte oder unvollständige Installation hin — installieren Sie die Anwendung im Zweifel neu.

## Hinweis „Die Update-Einstellungen konnten nicht gelesen werden. Eine Update-Prüfung ist nicht möglich."

Die gespeicherten Update-Einstellungen konnten nicht aus der Anwendungsdatenbank gelesen werden. Aus Sicherheitsgründen finden dann weder Prüfung noch Installation statt.

1. Öffnen Sie die **Einstellungen** und prüfen Sie den Abschnitt **Updates**.
2. Wählen Sie den gewünschten Modus erneut und klicken Sie **Speichern** — danach kann wieder geprüft werden, ohne die Anwendung neu zu starten.
3. Tritt der Fehler wiederholt auf, wenden Sie sich an den Support bzw. prüfen Sie das Anwendungsprotokoll.

## Hinweis/Dialog „Die Update-Einstellungen wurden geändert. Der Update-Vorgang wurde abgebrochen."

Während des Update-Vorgangs wurden die Update-Einstellungen gespeichert — der laufende Vorgang wird verworfen, damit kein Update gegen die neue Auswahl installiert wird.

- Gewünscht war die Änderung: Starten Sie das Update bei Bedarf erneut über **⇧ Update** — es wird mit den neuen Einstellungen geprüft.

## Hinweis „Update-Vorbereitung wurde abgebrochen."

Der Vorgang wurde abgebrochen — durch den **Abbrechen**-Button im Fortschrittsdialog, durch geänderte Einstellungen oder weil die Anwendung geschlossen wird. Es wurde nichts installiert; die Anwendung läuft normal weiter. Sie können das Update jederzeit erneut starten.

## Hinweis/Dialog „Update konnte nicht vorbereitet werden."

Das Herunterladen, Entpacken oder die Vorbereitung des Update-Pakets ist fehlgeschlagen.

1. Prüfen Sie die Internetverbindung und den freien Speicherplatz im Programmverzeichnis.
2. Prüfen Sie, ob ein Virenscanner oder fehlende Schreibrechte den Dateizugriff blockieren.
3. Wiederholen Sie den Vorgang über **⇧ Update**.

## Sicherheitsabfrage „Update starten?" erscheint

Es laufen noch CLI-Aufgaben, die nicht auf eine Eingabe warten — deren Abbruch durch den Programm-Neustart könnte laufende Arbeiten beeinträchtigen.

- **Fortfahren**, wenn die aufgelisteten Aufgaben unterbrochen werden dürfen.
- **Abbrechen** und die Aufgaben zunächst zu Ende laufen lassen; das Update kann später erneut gestartet werden.

## Update installiert, Anwendung startet nicht neu / wirkt nicht aktualisiert

Der eigentliche Dateiaustausch erfolgt durch ein externes Update-Skript, nachdem die Anwendung beendet wurde.

1. Warten Sie einige Sekunden — das Skript wartet zunächst auf das Beenden der Anwendung und startet sie dann neu.
2. Startet die Anwendung nicht von selbst, starten Sie sie manuell und prüfen Sie die Versionsanzeige in der Seitenleiste.
3. Ein Protokoll des Update-Skripts liegt im Update-Arbeitsverzeichnis des Programmordners (`updates\update.log`) und gibt im Fehlerfall Aufschluss.
