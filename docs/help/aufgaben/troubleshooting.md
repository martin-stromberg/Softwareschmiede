← [Zurück zur Übersicht](index.md)

# Aufgaben & KI-Entwicklungsprozess — Fehlerbehebung

## Export-Dialog wurde abgebrochen

**Symptom:** Nach Klick auf **Rohausgabe exportieren** passiert scheinbar nichts.

**Ursache:** Der Speichern-Dialog wurde mit **Abbrechen** geschlossen.

**Lösung:**
1. Klicke erneut auf **Rohausgabe exportieren**.
2. Wähle diesmal einen Zielpfad und bestätige den Dialog.

> **Hinweis:** Ein Abbruch ist kein Fehlerzustand. Es wird bewusst keine Datei angelegt.

## Export-Zielpfad wird mit `.raw`-Fehler abgewiesen

**Symptom:** Die Detailansicht zeigt die Meldung `Export-Zielpfad muss auf .raw enden.`

**Ursache:** Der gewählte Dateiname hat keine `.raw`-Endung.

**Lösung:**
1. Starte den Export erneut.
2. Wähle einen Dateinamen mit der Endung `.raw`.

> **Hinweis:** Der Dateifilter im Dialog schlägt bereits `.raw` vor, aber der gewählte Name wird zusätzlich geprüft.

## Datei konnte nicht exportiert werden

**Symptom:** Die Detailansicht zeigt eine Meldung wie `CLI-Rohausgabe konnte nicht exportiert werden: ...`.

**Ursache:** Typische Ursachen sind fehlende Schreibrechte, ein gesperrter Zielpfad oder ein Pfad, der auf ein Verzeichnis statt auf eine Datei zeigt.

**Lösung:**
1. Wähle einen anderen Speicherort, auf den du Schreibrechte hast.
2. Achte darauf, dass der Zielpfad auf eine Datei und nicht auf ein Verzeichnis zeigt.
3. Wiederhole den Export.

> **Hinweis:** Die bisher gespeicherten Protokolle bleiben dabei unverändert erhalten.
