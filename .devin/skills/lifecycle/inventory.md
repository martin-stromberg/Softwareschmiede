# Bestandsaufnahme zur Anforderung

Du bekommst eine übersetzte Anforderungsbeschreibung (typischerweise aus `requirement.md`). Analysiere den bestehenden Projektcode bezogen auf diese Anforderung und erstelle eine Bestandsaufnahme.

**Ziel:** Festhalten, was bereits existiert — nicht was gebaut werden soll. Keine Planung, keine Implementierungsvorschläge.

---

## Schritt 1: Anforderung einlesen

Lies die Datei `docs/features/{branchname}/requirement.md`. Entnimm daraus:
- Die betroffenen Klassen und Komponenten (Datenmodell, Logik, UI, Tests)
- Den fachlichen Bereich
- Relevante Eigenschaften, Events oder Mechanismen

## Schritt 2: Codeanalyse

Durchsuche `src/` gezielt nach den in der Anforderung genannten Klassen und verwandten Artefakten. Für jeden relevanten Bereich:

- **Datenmodellklassen:** Welche Eigenschaften existieren bereits?
- **Logikklassen:** Welche Methoden sind vorhanden? Welche Events werden abonniert oder publiziert?
- **Enums:** Welche Werte sind definiert?
- **Interfaces:** Welche Contracts sind definiert?
- **Tests:** Gibt es bestehende Testmethoden oder Hilfsmethoden für diesen Bereich?

Lies die relevanten Quelldateien vollständig, bevor du etwas dokumentierst.

## Schritt 3: Dokumentation erstellen

Erstelle folgende Dateien:

### Hauptdokument: `docs/features/{branchname}/inventory.md`

Struktur:

```
# Bestandsaufnahme: {Feature-/Anforderungstitel}

Kurze Einleitung (1–2 Sätze): Welcher Bereich wurde analysiert, bezogen auf welche Anforderung?

## Zusammenfassung

Stichpunktliste der wesentlichen Befunde — was ist vorhanden, was fehlt offensichtlich noch.

## Details

Für jeden analysierten Bereich ein Abschnitt mit Link zur Detaildatei:

- [Datenmodell](inventory/models.md)
- [Logik](inventory/logic.md)
- [Enums](inventory/enums.md)
- [Interfaces](inventory/interfaces.md)
- [Tests](inventory/tests.md)

Abschnitte, die leer wären (keine relevanten Funde), weglassen.
```

### Detaildokumente: `docs/features/{branchname}/inventory/`

Für jeden relevanten Bereich eine eigene Datei. Nur anlegen, wenn es tatsächlich Inhalt gibt.

**`models.md`** — Für jede betroffene Datenmodellklasse:
```
## `{Klassenname}`
Datei: `src/...`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| ...         | ... | ...                  |
```

**`logic.md`** — Für jede betroffene Logikklasse:
```
## `{Klassenname}`
Datei: `src/...`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| ...     | ...         | ...              |

Abonnierte Events: (falls vorhanden)
Publizierte Events: (falls vorhanden)
```

**`enums.md`** — Für jeden betroffenen Enum:
```
## `{Enumname}`
Datei: `src/...`

| Wert | Bedeutung |
|------|-----------|
| ...  | ...       |
```

**`interfaces.md`** — Für jedes betroffene Interface:
```
## `{Interfacename}`
Datei: `src/...`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| ...     | ...       | ...          | ...   |
```

**`tests.md`** — Übersicht bestehender Tests und Hilfsmethoden für den Bereich:
```
## Testklassen

### `{Testklassenname}`
- `{Testmethode}` — Was wird getestet?

## Hilfsmethoden

### `{Hilfsklassenname}`
- `{Hilfsmethode}` — Welche Testdaten oder Abläufe werden bereitgestellt?
```

---

## Hinweise

- Nur dokumentieren, was tatsächlich im Code vorhanden ist. Keine Annahmen oder Vorausblicke.
- Klassen- und Methodennamen immer im Original und in Backticks.
- Querverweise zwischen Klassen explizit benennen (z. B. „wird aufgerufen von `OrderService`").
- Wenn eine Quelldatei für die Anforderung nicht relevant ist, nicht in die Dokumentation aufnehmen.
