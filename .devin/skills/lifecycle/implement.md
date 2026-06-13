# Implementierung nach Plan

Du bekommst einen Umsetzungsplan (`plan.md`) sowie die zugehörige Anforderung und Bestandsaufnahme. Implementiere den Plan vollständig im Projekt.

**Ziel:** Den Plan Schritt für Schritt umsetzen — keine Planung, keine Analyse, keine Abweichungen vom Plan ohne Rückfrage.

---

## Schritt 1: Eingaben einlesen

Lies folgende Dateien vollständig:

- `docs/features/{branchname}/plan.md` — maßgeblicher Umsetzungsplan
- `docs/features/{branchname}/requirement.md` — fachliche Anforderung
- `docs/features/{branchname}/inventory.md` — Bestandsaufnahme inkl. Detaildokumente unter `docs/features/{branchname}/inventory/`

Entnimm aus `plan.md`:
- Die **Umsetzungsreihenfolge** — diese bestimmt die Bearbeitungsreihenfolge
- Alle **neuen Klassen** mit Typ und Zweck
- Alle **Änderungen an bestehenden Klassen** mit Eigenschaften, Methoden und Events

## Schritt 2: Bestehende Dateien lokalisieren

Suche für jede in `plan.md` genannte bestehende Klasse die zugehörige Quelldatei unter `src/`. Lies jede betroffene Datei vollständig, bevor du Änderungen vornimmst.

## Schritt 3: Implementierung durchführen

**Fehlerkorrekturen (aus `continue.md`, `review.md` oder `review-code.md`):** Folge für jeden zu behebenden Fehler dem Ablauf aus `/fix-with-test` — schreibe zuerst einen reproduzierenden Test, dann den Fix, dann verifiziere.

Arbeite die **Umsetzungsreihenfolge** aus `plan.md` sequenziell ab. Für jeden Schritt:

### Neue Klassen anlegen

Erstelle eine neue Quelldatei unter `src/` mit dem korrekten Typ. Halte dich dabei an folgende Konventionen:

- Klassen- und Methodennamen entsprechend den im Projekt verwendeten Namenskonventionen (aus Bestandsaufnahme ablesen)
- Dateiname entspricht dem Klassennamen
- Keine Eigenschaften, Methoden oder Werte anlegen, die nicht im Plan stehen
- Keine Kommentare, außer wenn der Zweck einer Methode nicht aus dem Namen hervorgeht

### Bestehende Klassen erweitern

Bearbeite die vorhandene Quelldatei direkt:

- Neue Eigenschaften an der im Plan genannten Stelle einfügen
- Neue Methoden am Ende der Klasse anfügen
- Event-Handler als separate Methoden anlegen
- Bestehenden Code nur dort ändern, wo der Plan es explizit vorsieht

### Tests implementieren

Implementiere alle in `plan.md` unter **Tests** aufgeführten Testmethoden und Hilfsmethoden:

- Testmethoden in die genannte Testklasse einfügen oder neue Testklasse anlegen
- Hilfsmethoden in die genannte Hilfsklasse einfügen
- Jede Testmethode prüft genau das, was im Plan beschrieben ist

## Schritt 4: Konsistenzprüfung

Nach Abschluss aller Änderungen:

- Stelle sicher, dass alle im Plan genannten Klassen, Eigenschaften, Methoden und Events vorhanden sind
- Prüfe, ob Abhängigkeiten korrekt aufgelöst wurden (z. B. Enum existiert vor seiner Verwendung in einer Klasse)
- Prüfe, dass keine Klassen oder Eigenschaften angelegt wurden, die nicht im Plan stehen
- Stelle sicher dass sich alle kompilierbaren Elemente kompilieren lassen.
- Stelle sicher, dass alle Tests im Projekt erfolgreich sind.

---

## Hinweise

- Halte dich strikt an den Plan — implementiere nichts, was dort nicht steht.
- Wenn der Plan eine Angabe enthält, die technisch nicht umsetzbar ist oder einen Widerspruch zur Bestandsaufnahme zeigt, brich ab und frage nach.
- Klassen- und Methodennamen immer im Original und in Backticks.
- Keine spekulativen Erweiterungen, keine „während wir schon dabei sind"-Änderungen.
- Der Code muss compilierbar sein — keine Platzhalter oder TODOs im Code hinterlassen.
