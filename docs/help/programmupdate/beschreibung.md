← [Zurück zur Übersicht](index.md)

# Programmupdate — Beschreibung

## Zweck

Das Programmupdate-Feature hält die Softwareschmiede-Anwendung auf dem aktuellen Stand, ohne dass der Anwender die Anwendung manuell schließen und neue Dateien herunterladen muss. Der Update-Prozess prüft auf neue Versionen, lädt das Update-Paket herunter, entpackt die Dateien und bereitet die Installation vor. Die eigentliche Installation führt ein externes Skript nach dem Beenden der Anwendung aus; die Anwendung startet anschließend automatisch neu.

Ob und wie Updates geprüft und installiert werden, legt der Anwender in den Einstellungen fest.

## Update-Einstellungen

Auf der Registerkarte **Allgemein** der Einstellungsseite befindet sich der Abschnitt **Updates** mit zwei Eingaben:

- **Auswahlbox (Update-Modus)** mit den Optionen:
  - **Aus** — die Update-Prüfung ist deaktiviert. Es findet weder eine Prüfung beim Programmstart noch eine manuelle Prüfung statt; die Schaltfläche „⟳ Prüfen" ist deaktiviert.
  - **Nur prüfen** — (Standard) die Anwendung prüft beim Programmstart einmalig und auf manuellen Klick, ob ein Update verfügbar ist. Ein gefundenes Update wird angeboten, aber nie automatisch installiert.
  - **Bei Programmstart prüfen und ausführen** — die Anwendung prüft einmalig beim Programmstart und installiert ein gefundenes Update automatisch. Nach dem Start können weiterhin manuell Prüfungen ausgelöst und angebotene Updates per Klick installiert werden.
- **Checkbox „Prerelease-Versionen laden"** — ist sie aktiviert, werden bei der Update-Prüfung auch Vorabversionen (z. B. Release Candidates) berücksichtigt. Ist sie deaktiviert, werden nur stabile Versionen als Update angeboten.

Beide Werte werden erst mit einem Klick auf **Speichern** wirksam; die Erfolgsmeldung „Einstellungen gespeichert." bestätigt die Übernahme. Werden die Update-Einstellungen geändert, während ein Update-Angebot angezeigt wird oder ein Update-Vorgang läuft, wird das Angebot zurückgenommen bzw. der laufende Vorgang abgebrochen.

## Benutzer-sichtbare Komponenten

### Schaltflächen in der Seitenleiste

Am Fuß der Navigations-Seitenleiste — unterhalb der Versionsanzeige — stehen zwei Schaltflächen:

- **⟳ Prüfen** (Tooltip „Auf Programmupdate prüfen") — startet eine manuelle Update-Prüfung. Die Schaltfläche ist deaktiviert, solange der Update-Modus auf **Aus** steht oder ein Update-Vorgang läuft; der Tooltip bleibt im deaktivierten Zustand sichtbar und erklärt den Grund (z. B. „Update-Prüfung ist in den Einstellungen deaktiviert.").
- **⇧ Update** (Tooltip „Update auf Version … vorbereiten") — erscheint nur, wenn eine Prüfung eine neuere Version gefunden hat. Ein Klick startet den Update-Ablauf.

Unter den Schaltflächen kann ein Hinweistext erscheinen, der das Ergebnis der letzten Prüfung anzeigt (z. B. „Kein Update verfügbar. Die installierte Version ist aktuell.") oder auf Fehler hinweist.

### Sicherheitsabfrage

Laufen beim Start eines Updates noch CLI-Aufgaben, die nicht auf eine Eingabe warten, erscheint zunächst eine Sicherheitsabfrage mit dem Titel „Update starten?". Sie listet die betroffenen Aufgaben auf und fragt, ob das Update trotzdem vorbereitet und die Anwendung beendet werden soll. Diese Abfrage gilt auch bei der automatischen Installation beim Programmstart.

### Update-Fortschrittsdialog

Während der Vorbereitung wird ein modaler Dialog mit dem Titel **„Update vorbereiten"** über dem Hauptfenster angezeigt. Er enthält:

- **Phase-Anzeige** — die gerade ausgeführte Phase: „Download", „Entpacken" oder „Update-Vorbereitung".
- **Fortschrittsbalken** — prozentualer Fortschritt beim Download (0–100 %) bzw. eine unbestimmte Fortschrittsanzeige in den übrigen Phasen.
- **Meldungstext** — eine aussagekräftige Meldung zum aktuellen Zustand (z. B. „Update wird heruntergeladen.").
- **Abbrechen-Schaltfläche** — unterbricht die laufende Vorbereitung. Sie ist deaktiviert, sobald die Vorbereitung abgeschlossen ist oder ein Fehlerzustand angezeigt wird.
- **Schließen-Schaltfläche** — erscheint in Fehler- und Abschlusszuständen und schließt den Dialog explizit.

Bei einem Fehler wird die Fehlermeldung im Dialog angezeigt und der Dialog kann über „Schließen" geschlossen werden. Nach erfolgreicher Vorbereitung zeigt der Dialog „Update wird gestartet. Die Anwendung wird beendet." — anschließend startet das externe Update-Skript und die Anwendung wird beendet.

## Funktionsweise

Der Update-Vorgang läuft in mehreren Phasen ab:

1. **Prüfung** — die installierte Version wird mit der neuesten passenden Version auf dem Update-Server verglichen. Je nach Einstellung werden nur stabile Versionen oder auch Prerelease-Versionen berücksichtigt.
2. **Sicherheitsprüfung** — vor der Installation wird geprüft, ob laufende CLI-Aufgaben das Update blockieren würden.
3. **Download** — das Update-Paket wird heruntergeladen (Fortschritt in Prozent).
4. **Entpacken** — das Paket wird in ein Arbeitsverzeichnis entpackt und auf Vollständigkeit geprüft.
5. **Update-Vorbereitung** — das Update-Skript wird erzeugt.
6. **Installation** — das Skript wird gestartet, die Anwendung beendet sich, das Skript tauscht die Programmdateien aus und startet die Anwendung neu.

## Einschränkungen

- Die eigentliche Installation erfolgt durch ein externes Skript nach Beendigung der Anwendung; die Anwendung selbst bereitet das Update nur vor.
- Die automatische Installation findet ausschließlich einmalig beim Programmstart statt — nur im Modus „Bei Programmstart prüfen und ausführen". Spätere manuelle Prüfungen bieten ein gefundenes Update lediglich an.
- Der Update-Vorgang kann nicht pausiert werden, nur abgebrochen.
- Während der Update-Vorbereitung können keine anderen Operationen in der Anwendung ausgeführt werden (modaler Dialog).
- Kann der Update-Server nicht erreicht werden oder sind die Update-Einstellungen nicht lesbar, wird ein Hinweistext statt eines Update-Angebots angezeigt; es findet dann keine Installation statt.
- Schlägt das externe Update-Skript fehl, wird die Anwendung möglicherweise in einem inkonsistenten Zustand hinterlassen. Dies liegt außerhalb des Features; das Skript schreibt ein eigenes Protokoll.
