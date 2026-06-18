# Technisches Code-Review

Führe ein technisches Code-Review der im aktuellen Branch geänderten Dateien durch.

**Ziel:** Qualitätsprobleme im neu geschriebenen Code aufdecken — keine funktionale Prüfung gegen den Plan (das ist Aufgabe von `/review-plan`), keine Änderungen am Code.

---

## Schritt 1: Geänderte Dateien ermitteln

Führe folgenden Befehl aus, um alle im aktuellen Branch gegenüber dem Basisbranch geänderten und neu erstellten Quelldateien zu ermitteln:

```
git diff --name-only --diff-filter=AM $(git merge-base HEAD main)
```

Falls der Basisbranch nicht `main` heißt, passe den Namen entsprechend an. Lies jede ermittelte Quelldatei vollständig.

## Schritt 2: Code-Review durchführen

Prüfe jede Datei anhand der folgenden Kriterien. Halte jeden Befund mit Dateireferenz und konkreter Beschreibung fest.

### Struktur und Verantwortlichkeiten

- **God-Klasse:** Hat eine Klasse mehr als eine klar abgrenzbare fachliche Verantwortlichkeit? Verletzt sie das Single-Responsibility-Prinzip?
- **God-Methode:** Ist eine einzelne Methode länger als ~50 Zeilen oder erledigt sie mehrere konzeptuell getrennte Aufgaben hintereinander?
- **Fehlende Kapselung:** Wird Logik, die wiederverwendbar wäre, inline wiederholt statt in eine eigene Methode ausgelagert?

### Doppelter Code

- Identische oder nahezu identische Codeblöcke innerhalb derselben Datei oder über mehrere neue Dateien hinweg
- Logik, die bereits in einer bestehenden Klasse vorhanden ist und hätte wiederverwendet werden können

### Namenskonventionen und Einheitlichkeit

- Sind Klassen-, Methoden- und Variablennamen im gesamten Branch einheitlich benannt? (z. B. durchgehend PascalCase für Typen, camelCase für Variablen)
- Weichen neue Namen vom Stil der bestehenden Codebasis ab?
- Methodennamen, die nicht beschreiben was die Methode tut (kryptische Abkürzungen, generische Namen wie `Process`, `Handle`, `DoStuff`)
- Inkonsistente Schreibweisen für dasselbe Konzept (z. B. `userId` vs. `user_id` vs. `UserId`)

### Kopplung und Erweiterbarkeit

- Direkte Abhängigkeiten zwischen Klassen, die besser über Interfaces oder Events entkoppelt werden sollten
- Fehlende Interfaces, wo Testbarkeit oder Austauschbarkeit erwartet wird
- Hardcodierte Werte, die in Konfiguration oder Konstanten gehören

### Fehlerbehandlung

- Fehlende Validierung von Eingaben oder Vorbedingungen vor kritischen Operationen
- Zu breite Exception-Handler (`catch (Exception)`) ohne sinnvolle Behandlung
- Exceptions, die still geschluckt werden (leere catch-Blöcke)
- Fehlermeldungen ohne aussagekräftigen Kontext

### Testqualität

- Testmethoden, die mehr als einen fachlichen Fall prüfen (fehlende Trennung)
- Fehlende Arrange-Act-Assert-Struktur
- Tests, die interne Implementierungsdetails statt fachliches Verhalten prüfen
- Fehlende oder unzureichende Testabdeckung für neue öffentliche Methoden

### Klassische Code Smells

**Datenstrukturen**
- **Primitive Obsession:** Primitive Typen (`string`, `int`, `bool`) werden statt kleiner spezialisierter Klassen/Value-Objects verwendet (z. B. `string` für E-Mail, Währung, Status)
- **Long Parameter List:** Methoden mit mehr als 3–4 Parametern, die besser als Parameter-Objekt oder Builder modelliert würden
- **Data Clumps:** Datengruppen (z. B. `street`, `city`, `zip`), die stets gemeinsam auftreten, aber nicht als eigenes Objekt zusammengefasst sind

**Verantwortlichkeit und Kopplung**
- **Feature Envy:** Eine Methode greift mehr auf Daten/Methoden einer fremden Klasse zu als auf die eigene — Indikator, dass die Logik in die falsche Klasse gehört
- **Inappropriate Intimacy:** Eine Klasse kennt und nutzt private Felder oder interne Implementierungsdetails einer anderen Klasse direkt
- **Message Chains:** Methodenketten über mehrere Ebenen (`a.GetB().GetC().GetD()`) — Verletzung des Law of Demeter, erhöht Kopplung und Fragility
- **Middle Man:** Eine Klasse delegiert nahezu alle Aufrufe an eine andere, ohne eigene Logik hinzuzufügen

**Überflüssiger Code**
- **Lazy Class:** Eine Klasse enthält so wenig Logik, dass ihre Existenz als eigene Klasse nicht gerechtfertigt ist
- **Speculative Generality:** Abstraktion, Interfaces oder Parameter für hypothetische zukünftige Anforderungen, die aktuell nicht benötigt werden
- **Switch Statements / Type Checks:** `switch`- oder `if/else`-Ketten über Typprüfungen (`is`, `typeof`, Enum-Flags), die durch Polymorphismus oder Strategy-Pattern ersetzt werden könnten
- **Temporäres Feld:** Instanzfelder, die nur in bestimmten Situationen gesetzt sind und sonst `null` bleiben — deutet auf eine fehlende Klasse oder Methode hin

### Toter Code

- Methoden, die angelegt, aber nirgends aufgerufen werden
- Auskommentierter Code
- Variablen oder Parameter, die deklariert, aber nicht verwendet werden

## Schritt 3: Review-Ergebnis ausgeben

Speichere das Ergebnis als `docs/features/{branchname}/review-code.md` (überschreibe eine vorhandene Datei):

```
# Code-Review

## Ergebnis

**Status:** Keine Befunde | Befunde vorhanden

## Befunde

Nur ausfüllen, wenn Status „Befunde vorhanden". Für jeden Befund:

### {Dateiname} ({Klassenname})

- **{Kategorie}** — {Konkrete Beschreibung des Problems, Zeilennummer oder Methodenname wenn möglich}

  Empfehlung: {Was genau geändert werden soll}

## Geprüfte Dateien

Liste aller geprüften Dateien:
- `src/...`
```

---

## Hinweise

- Nur die im aktuellen Branch geänderten und neu erstellten Dateien reviewen.
- Jeden Befund so konkret formulieren, dass der Implementierungsagent ohne Rückfrage handeln kann.
- Keine subjektiven Stilpräferenzen als Befunde aufnehmen — nur Verstöße gegen die oben genannten Kriterien.
- Das Review-Ergebnis muss maschinell lesbar sein: Der Status in der ersten Zeile von „## Ergebnis" ist entweder exakt `Keine Befunde` oder `Befunde vorhanden`.
