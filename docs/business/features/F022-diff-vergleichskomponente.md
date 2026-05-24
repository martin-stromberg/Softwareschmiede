# F022 – Diff-Vergleichskomponente

## Einleitung

Mit dieser Funktion prüfen Sie Änderungen an Dateien direkt in der Aufgabenansicht.
Sie müssen dafür nicht in eine andere Seite wechseln.
Sie sehen schnell, was neu ist und was entfernt wurde.
So treffen Sie Freigaben sicherer und klären Rückfragen schneller.

---

## Wer nutzt es?

Diese Funktion nutzen Fachanwender in der Sachbearbeitung und Teamleitungen.
Sie eignet sich für alle, die Dateistände prüfen und Änderungen bestätigen.
Neue Mitarbeitende verstehen damit schneller, was sich in einer Aufgabe geändert hat.

---

## Schritt-für-Schritt-Anleitung

1. Öffnen Sie eine Aufgabe und wechseln Sie über **🗂️ Projektverzeichnis** in die Dateiansicht.
2. Wählen Sie links eine geänderte Datei aus.
3. Lesen Sie den Vergleich direkt im Bereich **Dateivorschau**.
4. Bleiben Sie in derselben Aufgabe und prüfen Sie bei Bedarf weitere Dateien nacheinander.
5. Nutzen Sie **🔎 Diff anzeigen**, wenn Sie die gleiche Änderung als eigene Seite öffnen möchten.
6. Teilen Sie bei Bedarf einen Direktlink im Format `/diff/{DiffResultId}`.

---

## Beispiel

Sie prüfen eine abgeschlossene Aufgabe mit mehreren geänderten Dateien.
Sie klicken zuerst auf `angebot.cs` und direkt danach auf `preisregel.cs`.
Die Vorschau zeigt am Ende nur die zuletzt gewählte Datei.
Wenn für eine Datei kein Vergleich vorliegt, sehen Sie sofort eine klare Hinweismeldung.

---

## Was passiert im Hintergrund?

Die Anwendung zeigt den Vergleich in der Aufgabenansicht eingebettet an.
Für jede ausgewählte Datei wird der passende Vergleich dateispezifisch ermittelt.
Der bisherige Direktaufruf über `/diff/{DiffResultId:guid}` bleibt vollständig nutzbar.
Falls kein Vergleich verfügbar ist, erhalten Sie klare Hinweise statt leerer Flächen.
Bei schnellem Wechsel zwischen Dateien bleibt die Anzeige stabil auf der letzten Auswahl.

---

## Häufige Fragen (FAQ)

**Muss ich für den Vergleich in eine andere Seite wechseln?**  
Nein. Die Vorschau läuft direkt in der Aufgabe.

**Kann ich den Vergleich trotzdem als eigenen Link öffnen?**  
Ja. Über **🔎 Diff anzeigen** oder direkt mit `/diff/{DiffResultId:guid}`.

**Was sehe ich, wenn kein Vergleich vorhanden ist?**  
Sie erhalten eine klare Meldung nur dann, wenn für die ausgewählte Datei wirklich kein Vergleich vorliegt, zum Beispiel bei gelöschten Dateien.

**Was passiert bei sehr schnellem Dateiwechsel?**  
Die Anzeige bleibt stabil und zeigt nur die zuletzt gewählte Datei.

---

## Verwandte Funktionen

- [F021 – Live Project Browser mit Git-Status](./F021-live-project-browser-git-status.md)
- [F002 – Aufgabenverwaltung](./F002-aufgabenverwaltung.md)
- [F005 – Aufgabenprotokoll](./F005-aufgabenprotokoll.md)
- [F016 – Fehlerbehandlung & Recovery](./F016-fehlerbehandlung-und-recovery.md)
- [Technischer Ablauf: Diff-Pipeline](../../flows/diff-service-flow.md)
- [Technische Detailseite: Diff Viewer und Direktlink](../../api/diff-viewer.md)
- [Zurück zur Übersicht](../features.md)
