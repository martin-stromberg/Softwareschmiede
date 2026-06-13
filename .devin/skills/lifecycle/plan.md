# Umsetzungsplanung

Du bekommst eine übersetzte Anforderung (`requirement.md`) und eine Bestandsaufnahme (`inventory.md` inkl. Detaildokumente). Erstelle daraus einen konkreten Umsetzungsplan für die Implementierung.

**Ziel:** Einen vollständigen, umsetzbaren Plan erstellen — keine Implementierung, keine Codeänderungen.

---

## Schritt 1: Eingaben einlesen

Lies folgende Dateien vollständig:

- `docs/features/{branchname}/requirement.md` — übersetzte Anforderung
- `docs/features/{branchname}/inventory.md` — Übersicht der Bestandsaufnahme
- Alle verlinkten Detaildokumente unter `docs/features/{branchname}/inventory/`

Wurden zusammen mit den Eingaben **Antworten auf offene Punkte** übergeben, notiere sie als geklärt. Diese Antworten sind in den Plan einzuarbeiten (z. B. als Designentscheidungen oder Programmablaufdetails) und dürfen in der Tabelle der offenen Punkte nicht mehr erscheinen.

## Schritt 2: Lückenanalyse

Stelle für jeden in der Anforderung genannten Bereich fest:

- Was ist bereits vorhanden (aus Bestandsaufnahme)?
- Was fehlt noch (neue Klassen, neue Felder, neue Methoden, neue Events)?
- Was muss angepasst werden (bestehende Klassen erweitern)?
- Welche Programmabläufe (Sequenzen von Methodenaufrufen, Events, Datenbankzugriffen) sind neu oder ändern sich?
- Welche Datenbankmigrationen sind notwendig?
- Welche Validierungsregeln sind für neue oder geänderte Eingaben erforderlich?
- Welche Konfigurationseinträge werden hinzugefügt oder geändert?
- Welche bestehenden Features oder Klassen sind von den Änderungen betroffen (Seiteneffekte)?
- Welche bestehenden Tests brechen durch die Änderungen?
- Welche neuen E2E-Tests sind für die geänderten oder neuen Benutzerabläufe erforderlich?
- Welche bestehenden E2E-Tests sind betroffen?
- Welche Designentscheidungen stehen für neue Komponenten an, bei denen mehrere sinnvolle Ansätze existieren?
- Welche Voraussetzungen fehlen im Repo für die geplanten Schritte? (fehlende NuGet-Pakete, Basisklassen, Testinfrastruktur, Konfigurationsklassen — alles, ohne das ein Schritt nicht ausführbar wäre)

## Schritt 3: Plan erstellen

Erstelle die Datei `docs/features/{branchname}/plan.md` mit folgender Struktur:

```
# Umsetzungsplan: {Feature-/Anforderungstitel}

## Übersicht

Kurze Beschreibung (2–3 Sätze): Was wird umgesetzt, welche Bereiche sind betroffen?

## Designentscheidungen

Für jede neue Komponente oder jeden Ablauf, bei dem mehrere sinnvolle Strukturansätze existieren, wird hier festgehalten, welcher Ansatz gewählt wird und warum.

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| `...` | ... | ... |

Falls alle Designentscheidungen durch bestehende Konventionen eindeutig vorgegeben sind: „Keine — folgt bestehenden Mustern."

## Programmabläufe

Beschreibung der Abläufe, die implementiert werden sollen. Für jeden Ablauf ein Unterabschnitt:

### {Ablaufname}

Schritt-für-Schritt-Beschreibung des Ablaufs (kein Code — Methodennamen, Klassen und Events in Backticks):

1. ...
2. ...

Beteiligte Klassen/Komponenten: `...`, `...`

## Neue Klassen

Liste aller neu zu erstellenden Klassen:

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `...`  | Klasse / Datenmodellklasse / Enum / Interface / ... | ... |

## Änderungen an bestehenden Klassen

Für jede zu ändernde Klasse ein Abschnitt:

### `{Klassenname}` ({Typ})

- **Neue Eigenschaften:** `{Name}` ({Typ}) — Zweck
- **Neue Methoden:** `{Methodenname}` — Zweck, Parameter, Rückgabewert
- **Geänderte Methoden:** `{Methodenname}` — Was ändert sich und warum?
- **Neue Events:** `{Eventname}` — Wann wird es ausgelöst?
- **Neue Event-Handler:** Auf welches Event wird reagiert?

## Datenbankmigrationen

Welche Migrationen sind erforderlich?

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `...`          | ...                        | ...                       |

Falls keine Migrationen erforderlich sind: „Keine."

## Validierungsregeln

Welche Validierungen müssen für neue oder geänderte Eingaben implementiert werden?

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `...`         | ...   | ...        |

Falls keine Validierungen erforderlich sind: „Keine."

## Konfigurationsänderungen

Welche Einträge in Konfigurationsklassen oder `appsettings` werden hinzugefügt oder geändert?

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `...`   | ... | ...          | ...   |

Falls keine Konfigurationsänderungen erforderlich sind: „Keine."

## Seiteneffekte und Risiken

Welche bestehenden Features, Klassen oder Abläufe werden durch die Änderungen beeinflusst?

- **{Bereich}:** {Beschreibung des Seiteneffekts oder Risikos}

Falls keine bekannten Seiteneffekte bestehen: „Keine bekannten Seiteneffekte."

## Umsetzungsreihenfolge

Geordnete Liste der Arbeitsschritte. Jeder Schritt listet explizit, was vor seiner Ausführung im Repo vorhanden sein muss. Fehlt eine Voraussetzung noch, muss sie als eigener früherer Schritt erscheinen.

1. **{Aufgabe}**
   - Voraussetzungen: {NuGet-Pakete, Basisklassen, Konfigurationseinträge, Interfaces — die im Repo bereits vorhanden oder durch frühere Schritte angelegt sein müssen. „Keine" wenn der Schritt voraussetzungslos ist.}
   - Beschreibung: {Was in diesem Schritt konkret getan wird}

2. ...

## Tests

### Neue Tests

Welche neuen Testmethoden und Hilfsmethoden sind erforderlich?

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| ... | ... | ... |

### Betroffene bestehende Tests

Welche vorhandenen Tests müssen angepasst werden, weil sich Signaturen, Verhalten oder Datenstrukturen ändern?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| ... | ... |

Falls keine bestehenden Tests betroffen sind: „Keine."

### E2E-Tests (Pflicht)

Für jede neue oder geänderte Benutzerinteraktion mindestens ein E2E-Test. Der Happy Path jedes neuen Features muss durch einen E2E-Test abgedeckt sein.

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| ... | ... | ... |

Welche bestehenden E2E-Tests müssen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| ... | ... |

Falls keine bestehenden E2E-Tests betroffen sind: „Keine."

## Offene Punkte

Ungeklärte technische oder fachliche Fragen, die vor oder während der Implementierung geklärt werden müssen.

**Wichtig:** Punkte, auf die in diesem Durchlauf Antworten vorlagen, erscheinen hier nicht mehr — sie gelten als geklärt und sind bereits im Plan eingearbeitet.

Falls es offene Punkte gibt, wird für jeden — sofern möglich — ein konkreter Lösungsvorschlag angegeben:

| # | Offener Punkt | Empfohlener Vorschlag |
|---|---------------|----------------------|
| 1 | ... | ... |

Falls keine offenen Punkte bestehen: „Keine."
```

## Schritt 4: Tasks-Datei erstellen

Erstelle die Datei `docs/features/{branchname}-tasks.md` — **nicht** im Branch-Unterverzeichnis, da sie nach dem Aufräumen erhalten bleiben soll.

Die Datei enthält alle Einzelaufgaben aus dem Plan in einer thematisch geordneten Tabelle. Themen sind funktionale Bereiche (z. B. „Datenmodell", „Logik", „UI", „Validierung", „Konfiguration", „Tests", „E2E-Tests") — keine Implementierungsreihenfolge.

Jede Zeile entspricht einer abgrenzbaren Einzelaufgabe: eine Klasse anlegen, eine Methode hinzufügen, eine Migration erstellen, einen Test schreiben. Keine Sammelzeilen wie „alle neuen Klassen anlegen".

```
# Tasks: {Feature-/Anforderungstitel}

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | {Bereich} | {Konkrete Einzelaufgabe, z. B. "`OrderStatus` Enum anlegen"} | Offen | — |
| 2 | ... | ... | Offen | — |
```

**Befüllung:** Alle Zeilen werden mit Status `Offen` und Testnachweis `—` initialisiert. Aktualisierung erfolgt durch `/review-plan`.

---

## Hinweise

- Nur planen, was die Anforderung verlangt — keine Extras, keine spekulativen Erweiterungen.
- Technische Bezeichnungen (Klassen- und Methodennamen) immer im Original und in Backticks.
- Abhängigkeiten zwischen Klassen explizit benennen (z. B. „Enum muss vor der Datenmodellklasse angelegt werden").
- Jeder Schritt in der Umsetzungsreihenfolge muss ausführbar sein: Alle Voraussetzungen (NuGet-Pakete, Basisklassen, Testinfrastruktur) müssen entweder im Repo bereits vorhanden sein oder in einem früheren Schritt explizit angelegt werden. Kein Schritt darf stillschweigend auf etwas Fehlendes aufbauen.
- Designentscheidungen nur dort explizit ausformulieren, wo echte Alternativen bestehen. Erweiterungen bestehender Klassen folgen den vorhandenen Mustern — kein Eintrag nötig.
- Als Vokabular für Designentscheidungen PoEAA-Muster verwenden, wenn sie passen: Repository, Value Object, Service Layer, Data Mapper, Transaction Script, Domain Model, Gateway, Specification u. a.
- Wenn Designentscheidungen mehrere Optionen haben, die eine bevorzugte Option benennen und kurz begründen — keine Abwägungslisten.
- Keine Implementierungsdetails (kein Code) — nur Struktur und Absicht.
- Für jeden offenen Punkt einen Empfehlungsvorschlag angeben, wenn die Antwort aus Anforderung, Bestandsaufnahme oder gängiger Praxis ableitbar ist. Falls ein Punkt wirklich ungeklärt ist und kein sinnvoller Vorschlag möglich ist, „Kein Vorschlag möglich — Klärung erforderlich" eintragen.
- Wurden Antworten auf offene Punkte übergeben, müssen alle geklärten Punkte vollständig aus der Tabelle entfernt werden. Ein beantworteter Punkt darf in der aktualisierten `plan.md` nicht mehr erscheinen — auch nicht mit dem Status „geklärt".
