# Implementierung gegen Plan prüfen

Du bekommst einen Umsetzungsplan (`plan.md`) und die zugehörige Bestandsaufnahme. Prüfe, ob die Implementierung im Code alle Planelemente vollständig umsetzt.

**Ziel:** Lücken zwischen Plan und tatsächlicher Implementierung aufdecken — keine Implementierung, keine Bewertung der Qualität, keine Abweichungskorrektur.

---

## Schritt 1: Plan und Tasks einlesen

Lies folgende Dateien vollständig:

- `docs/features/{branchname}/plan.md` — Umsetzungsplan
- `docs/features/{branchname}-tasks.md` — Aufgabentabelle (falls vorhanden)

Erstelle intern eine Liste aller zu prüfenden Planelemente:

- Alle neuen Objekte (Name, Typ)
- Alle neuen Felder je Objekt (Name, Typ)
- Alle neuen Methoden je Objekt (Name, Sichtbarkeit)
- Alle neuen Events und Eventsubscriptions
- Alle Testmethoden und TestHelper-Erweiterungen

## Schritt 2: Implementierung prüfen

Suche für jedes Planelement die zugehörige Umsetzung im Code unter `src/`. Lies jede relevante Quelldatei vollständig.

Stelle für jedes Element fest:

- **Umgesetzt:** Das Element ist vollständig im Code vorhanden
- **Teilweise umgesetzt:** Das Element ist vorhanden, aber unvollständig (z. B. Methode ohne Inhalt, fehlendes Feld in einem sonst vorhandenen Objekt)
- **Fehlend:** Das Element ist im Code nicht auffindbar

## Schritt 3: Tasks-Datei aktualisieren

Aktualisiere `docs/features/{branchname}-tasks.md` auf Basis der Prüfergebnisse aus Schritt 2:

- Für jede Aufgabe, die vollständig umgesetzt ist: Status auf `Erledigt` setzen und in der Spalte „Testnachweis" den Test nennen, der das absichert (z. B. `OrderTests.PlaceOrder_ValidData_CreatesOrder`).
- Für Aufgaben ohne direkten Test (z. B. Enum anlegen, Konfigurationseintrag): den Test nennen, der fehlschlagen würde, wenn die Aufgabe fehlte — oder `Kein direkter Test` falls keiner existiert.
- Für Aufgaben, die fehlend oder teilweise umgesetzt sind: Status bleibt `Offen`. Kein Testnachweis.
- Für Aufgaben, die im Plan stehen aber nicht in der Tasks-Datei: als neue Zeile mit Status `Offen` ergänzen.

Die Tasks-Datei wird überschrieben — nicht nur die geänderten Zeilen.

## Schritt 4: Review-Ergebnis ausgeben

Gib das Ergebnis in folgender Struktur aus und speichere es als `docs/features/{branchname}/review.md`:

```
# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt | Offene Aufgaben vorhanden

## Umgesetzte Planelemente

- [x] `{Objektname}` ({Typ}) — angelegt / erweitert
- [x] Feld `{Feldname}` in `{Objekt}` — vorhanden
- [x] Methode `{Methodenname}` in `{Objekt}` — vorhanden
- ...

## Offene Aufgaben

Nur ausfüllen, wenn Status „Offene Aufgaben vorhanden":

- [ ] `{Planelement}` — fehlt vollständig / teilweise umgesetzt: {konkrete Beschreibung der Lücke}
- ...

## Hinweise

Optionale Beobachtungen, die für die Nachimplementierung relevant sein könnten (z. B. Abhängigkeiten, die zuerst geschlossen werden müssen).
```

---

## Hinweise

- Nur prüfen, was im Plan steht — keine eigenen Qualitätskriterien anlegen.
- „Teilweise umgesetzt" immer mit konkreter Beschreibung der Lücke dokumentieren.
- Wenn eine Quelldatei nicht unter `src/` gefunden wird, gilt das Element als fehlend.
- Keine Korrekturen vornehmen — ausschließlich prüfen und dokumentieren.
- Das Review-Ergebnis muss maschinell lesbar sein: Der Status in der ersten Zeile von „## Ergebnis" ist entweder exakt `Vollständig umgesetzt` oder `Offene Aufgaben vorhanden`.
- Die Tasks-Datei (`{branchname}-tasks.md`) ist das primäre Sichtbarkeitsdokument für offene Arbeit — sie muss nach jedem Review-Durchlauf aktuell sein.
