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

## Aufgabe lässt sich nicht starten („pausiert")

**Symptom:** **Starten**, **CLI neu starten** oder **Wiederherstellen** sind deaktiviert bzw. schlagen mit „Die Aufgabe ist bis … pausiert." fehl; die Kachel in der Seitenleiste zeigt „⏸ Pausiert (noch …)".

**Ursache:** Für die Aufgabe ist eine Pause aktiv — entweder manuell über **Pause einstellen** gesetzt oder automatisch durch ein Session-Limit des verwendeten KI-Plugins.

**Lösung:**
1. Prüfe im Ribbon den Hinweis „⏸ Pausiert bis …" und im Protokoll den Eintrag zur Pause (dort steht, ob ein Session-Limit die Ursache war).
2. Um die Pause vorzeitig zu beenden: **Pause einstellen** öffnen und **Pause aufheben** wählen.
3. Alternativ das Pausenende abwarten — die Pause endet automatisch zum eingestellten Zeitpunkt. Ein automatischer Neustart erfolgt danach nicht; starte die Aufgabe bei Bedarf manuell.

## Session-Limit-Marker erscheint im Protokoll, aber keine Aufgabe wird pausiert

**Symptom:** Im Protokoll steht ein Rate-Limit-Eintrag, doch weder die auslösende noch andere Aufgaben zeigen eine Pause.

**Mögliche Ursachen:**
- Der Marker enthielt keinen gültigen Zeitstempel — dann wird nur der Protokolleintrag geschrieben.
- Der gemeldete Zeitpunkt lag bereits in der Vergangenheit — er wird gespeichert, löst aber keine Pause aus.
- Die auslösende Aufgabe hat kein zugeordnetes KI-Plugin — dann kann kein pluginweites Limit gespeichert werden.
- Andere Aufgaben mit demselben Plugin waren zu dem Zeitpunkt nicht aktiv (Ausführung läuft nicht) oder sind Autonome Aufgaben — beide werden nicht automatisch pausiert.

**Lösung:** Pausiere betroffene Aufgaben bei Bedarf manuell über **Pause einstellen**.

## Geplanter Prompt wird nicht zur eingestellten Zeit versendet

**Symptom:** Ein zeitgesteuert geplanter Prompt wird später als geplant ausgeführt.

**Ursache:** Die Aufgabe war zum geplanten Zeitpunkt pausiert. Geplante Prompts werden nicht verworfen, sondern auf das Pausenende verschoben.

**Lösung:** Hebe die Pause vorzeitig über **Pause einstellen** → **Pause aufheben** auf oder warte das Pausenende ab; danach wird der Prompt zur verschobenen Zeit versendet.
