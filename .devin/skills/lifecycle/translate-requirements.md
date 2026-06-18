# Kundenanforderung analysieren und übersetzen

Du bekommst eine Kundenanforderung als Eingabe. Analysiere sie und übersetze sie in die technische Fachsprache des Projekts.

## Schritt 1: Anforderung verstehen

Lies die Anforderung sorgfältig durch. Identifiziere:

- **Fachlicher Kern:** Was soll das System tun? Was ist der Auslöser, was das Ergebnis?
- **Betroffene Objekte:** Welche Entitäten, Daten oder Abläufe sind betroffen?
- **Benutzerinteraktion:** Wird eine Aktion, ein automatischer Ablauf oder eine Anzeige verlangt?
- **Konfigurationsbedarf:** Muss das Verhalten konfigurierbar sein?

## Schritt 2: Projektkontext herstellen

Lies `docs/features.md` sowie alle relevanten Detaildateien unter `docs/features/`, um zu verstehen:

- Welche vergleichbaren Features bereits existieren
- Welche Namenskonventionen für Klassen, Methoden und Eigenschaften im Projekt verwendet werden
- Ob ein bestehender Mechanismus wiederverwendet werden kann

## Schritt 3: Übersetzte Anforderungsbeschreibung ausgeben

Gib eine strukturierte Beschreibung aus mit folgenden Abschnitten:

### Fachliche Zusammenfassung
Kurze Beschreibung (2–4 Sätze) in technischer Fachsprache: Was wird erweitert, welches Verhalten wird hinzugefügt?

### Betroffene Klassen und Komponenten
Liste der voraussichtlich betroffenen oder neu zu erstellenden Artefakte:
- Datenmodellklassen (neue Klassen oder neue Eigenschaften in bestehenden Klassen)
- Logikklassen / Services
- Interfaces
- Enums
- UI-Komponenten / Controller (falls zutreffend)
- Tests

### Implementierungsansatz
Kurze Beschreibung des technischen Vorgehens:
- Welche Events, Hooks oder Erweiterungspunkte sind relevant?
- Wird eine neue Klasse, eine Erweiterung einer bestehenden Klasse oder ein neues Interface benötigt?
- Gibt es Abhängigkeiten zu bestehenden Klassen oder Komponenten?

### Konfiguration
Falls das Feature konfigurierbar sein soll: Vorschlag für die Konfigurationsebene (z. B. Anwendungseinstellungen, benutzerspezifisch, pro Datensatz).

### Offene Fragen
Liste von Punkten, die vor der Implementierung mit dem Kunden oder intern geklärt werden müssen.

## Hinweise

- Technische Bezeichnungen (Klassen- und Methodennamen) immer im Original und in Backticks.
- Keine Implementierungsdetails erfinden — halte dich an das, was aus der Anforderung ableitbar ist, und kennzeichne Annahmen explizit als solche.
- Wenn die Anforderung unklar oder mehrdeutig ist, stelle gezielte Rückfragen, bevor du eine Übersetzung ausgibst.
