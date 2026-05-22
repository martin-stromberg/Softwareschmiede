# F022 – Diff-Vergleichskomponente

## Einleitung

Mit dieser Funktion vergleichen Sie zwei Versionen einer Datei direkt miteinander.
Sie sehen, welche Inhalte neu sind und welche entfernt wurden.
So erkennen Sie Änderungen schneller und treffen sichere Freigabeentscheidungen.
Die Funktion hilft besonders bei Rückfragen zu unerwarteten Dateiänderungen.

---

## Wer nutzt es?

Diese Funktion nutzen Fachanwender, die Änderungen nachvollziehen müssen.
Sie ist auch für Teamleitungen wichtig, die Ergebnisse prüfen und freigeben.
Neue Mitarbeitende nutzen sie, um Bearbeitungsschritte besser zu verstehen.

---

## Schritt-für-Schritt-Anleitung

1. Öffnen Sie einen bereitgestellten Vergleichslink.
2. Prüfen Sie oben Dateiname und Versionsstand.
3. Wechseln Sie mit **👬 Side-by-Side**, **⬌ Split** oder **📄 Unified** die Ansicht.
4. Suchen Sie mit **🔍** gezielt nach Begriffen im Vergleich.
5. Nutzen Sie **⬆ Prev** und **Next ⬇**, um zwischen Treffern zu springen.
6. Markieren Sie wichtige Zeilen und nutzen Sie **📋 Copy** bei Bedarf.

---

## Beispiel

Sie passen eine Preisregel in einer Datei an.
Im Vergleich sehen Sie sofort die alte und die neue Fassung nebeneinander.
So erkennen Sie schnell, ob nur die gewünschte Stelle geändert wurde.
Fehlerhafte Zusatzänderungen fallen direkt auf.

---

## Was passiert im Hintergrund?

Die Anwendung erstellt aus beiden Fassungen ein Vergleichsergebnis.
Dabei werden neue und entfernte Zeilen gezählt.
Ergebnisse können zwischengespeichert und später schneller geladen werden.
Bei Bedarf wird ein gespeichertes Ergebnis gezielt ungültig gemacht.

---

## Durch Tests abgesicherte Zusagen

- Ein Vergleich wird nur für vorhandene Aufgaben erzeugt.
- Leere Eingaben werden abgelehnt und klar als Fehler behandelt.
- Vergleichsergebnisse werden zuverlässig gespeichert und wieder geladen.
- Sehr große Inhalte werden nicht vollständig im Ergebnis abgelegt.
- Die Zählung von neuen und entfernten Zeilen bleibt nachvollziehbar korrekt.
- Zwischengespeicherte Ergebnisse verfallen kontrolliert oder werden gezielt entfernt.
- Auswertungen zeigen die Gesamtzahlen pro Aufgabe zuverlässig an.

---

## Häufige Fragen (FAQ)

**Was sehe ich im Vergleich genau?**  
Sie sehen neue und entfernte Zeilen klar markiert.

**Werden frühere Vergleiche gespeichert?**  
Ja, Ergebnisse bleiben je Aufgabe verfügbar und abrufbar.

**Welche Ansichten kann ich nutzen?**  
Sie können zwischen **👬 Side-by-Side**, **⬌ Split** und **📄 Unified** wechseln.

**Kann ich einen alten Vergleich löschen?**  
Ja, ein gespeicherter Vergleich kann entfernt werden.

---

## Verwandte Funktionen

- [F021 – Live Project Browser mit Git-Status](./F021-live-project-browser-git-status.md)
- [F002 – Aufgabenverwaltung](./F002-aufgabenverwaltung.md)
- [F005 – Aufgabenprotokoll](./F005-aufgabenprotokoll.md)
- [F016 – Fehlerbehandlung & Recovery](./F016-fehlerbehandlung-und-recovery.md)
- [Zurück zur Übersicht](../features.md)
