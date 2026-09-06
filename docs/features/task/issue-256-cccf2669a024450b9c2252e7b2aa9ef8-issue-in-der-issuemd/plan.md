# Umsetzungsplan: Issue in der issue.md

## Übersicht

`EntwicklungsprozessService.CreateIssueFileAsync()` wird erweitert: Ist an der übergebenen `Aufgabe` eine `IssueReferenz` hinterlegt, wird dem Markdown-Inhalt der `issue.md` ein eigener Abschnitt mit Issue-Nummer (als `#42`) und Titel hinzugefügt. Ist keine Referenz vorhanden, bleibt die Ausgabe unverändert. Weder Datenmodell noch Datenbankstruktur noch Interfaces ändern sich.

## Designentscheidungen

Keine — folgt bestehenden Mustern.

## Programmabläufe

### Issue-Abschnitt in `issue.md` schreiben

1. `ProzessStartenAsync` / `ProzessStartenUndCliStartenAsync` ruft `FinalizeStartAsync` auf.
2. `FinalizeStartAsync` ruft `CreateIssueFileAsync(lokalerKlonPfad, aufgabe, branchName, startKonfiguration, ct)` auf.
3. In `CreateIssueFileAsync` wird `aufgabe.IssueReferenz` ausgewertet:
   - Ist `aufgabe.IssueReferenz` nicht `null` **und** `IssueNummer` größer als 0, wird ein Markdown-Block `## Verknüpftes Issue` mit `**Kennung:** #<Nummer>` und `**Titel:** <Titel>` zusammengestellt.
   - Ist `aufgabe.IssueReferenz` `null` **oder** `IssueNummer` `null` oder ≤ 0, wird kein Issue-Block eingefügt — der generierte Inhalt bleibt unverändert.
4. Der vollständige Markdown-String wird wie bisher per `File.WriteAllTextAsync` in die `issue.md` geschrieben.

Beteiligte Klassen/Komponenten: `EntwicklungsprozessService`, `Aufgabe`, `IssueReferenz`

## Neue Klassen

Keine.

## Änderungen an bestehenden Klassen

### `EntwicklungsprozessService` (Service)

- **Geänderte Methoden:** `CreateIssueFileAsync` — Der zusammengestellte Markdown-`inhalt`-String wird um einen optionalen Issue-Block erweitert. Die Logik prüft `aufgabe.IssueReferenz != null` **und** `IssueNummer > 0`; nur wenn beide Bedingungen erfüllt sind, wird ein `## Verknüpftes Issue`-Abschnitt mit `IssueNummer` (als `#<n>`) und `Titel` eingefügt (Position: nach dem Metadaten-Block, vor `## Anforderung`). Fehlt die `IssueReferenz` oder ist die `IssueNummer` nicht gesetzt, wird kein Block ausgegeben. Keine Änderung an Signatur, Fehlerbehandlung oder Logging.

**Angestrebtes Ausgabeformat der `issue.md` bei gesetzter `IssueReferenz` mit Nummer:**

```
# Aufgabe: {aufgabe.Titel}

**Aufgaben-ID:** {aufgabe.Id}
**Branch:** {branchName}
**Erstellt:** {aufgabe.ErstellungsDatum:yyyy-MM-dd}

## Verknüpftes Issue

**Kennung:** #{issueNummer}
**Titel:** {issueReferenz.Titel}

## Anforderung

{beschreibung}
```

Ist `IssueReferenz` `null` oder `IssueNummer` `null` oder ≤ 0, entfällt der gesamte `## Verknüpftes Issue`-Block.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine.

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **Bestehende Tests zu `CreateIssueFileAsync`:** Die Tests `CreateIssueFileAsync_ShouldCreateIssueFileWithCorrectContent_WhenAufgabeExists` und `CreateIssueFileAsync_ShouldUseFallbackText_WhenAnforderungsBeschreibungIsNullOrEmpty` erstellen Aufgaben ohne `IssueReferenz`. Ihr Verhalten ändert sich nicht — der Issue-Block erscheint nicht, wenn `IssueReferenz` `null` ist. Die bestehenden `content.Should().Contain(...)`-Assertions bleiben gültig; es ist jedoch sicherzustellen, dass `content.Should().NotContain("## Verknüpftes Issue")` im Sinne der Regression ergänzt werden kann (optional, kein Pflichtbruch).

## Umsetzungsreihenfolge

1. **`CreateIssueFileAsync` in `EntwicklungsprozessService` erweitern**
   - Voraussetzungen: Keine — alle Datentypen (`Aufgabe`, `IssueReferenz`) und die Methode selbst sind vorhanden.
   - Beschreibung: Nach dem Metadaten-Block (`**Erstellt:**`) und vor dem `## Anforderung`-Abschnitt wird ein optionaler `## Verknüpftes Issue`-Block eingefügt, wenn `aufgabe.IssueReferenz != null` **und** `IssueNummer > 0`. Die Nummer wird als `#<n>` formatiert. Ist die Bedingung nicht erfüllt (keine Referenz, oder Nummer fehlt/null/≤ 0), wird kein Block ausgegeben. Keine Änderung an Signatur, `try/catch` oder Logging.

2. **Neue Testmethoden in `EntwicklungsprozessServiceTests` ergänzen**
   - Voraussetzungen: Schritt 1 muss abgeschlossen sein.
   - Beschreibung: Zwei neue Testmethoden hinzufügen:
     - `CreateIssueFileAsync_ShouldIncludeIssueReference_WhenIssueReferenzIsSet` — Aufgabe mit vollständig befüllter `IssueReferenz` (Nummer und Titel); prüft, dass `## Verknüpftes Issue`, `#<n>` und `<Titel>` im Dateiinhalt vorhanden sind.
     - `CreateIssueFileAsync_ShouldNotIncludeIssueReference_WhenIssueReferenzIsNullOrHasNoNumber` — Aufgabe ohne `IssueReferenz` (null) sowie Aufgabe mit `IssueReferenz` aber ohne gültige Nummer; prüft jeweils, dass `## Verknüpftes Issue` nicht im Dateiinhalt erscheint.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `CreateIssueFileAsync_ShouldIncludeIssueReference_WhenIssueReferenzIsSet` | `EntwicklungsprozessServiceTests` | `issue.md` enthält `## Verknüpftes Issue`, die formatierte Kennung `#<n>` und den `Titel` der `IssueReferenz`, wenn eine `IssueReferenz` mit gültiger Nummer an der Aufgabe hängt |
| `CreateIssueFileAsync_ShouldNotIncludeIssueReference_WhenIssueReferenzIsNullOrHasNoNumber` | `EntwicklungsprozessServiceTests` | `issue.md` enthält keinen `## Verknüpftes Issue`-Abschnitt, wenn `IssueReferenz` `null` ist oder keine gültige Nummer gesetzt ist |

### Betroffene bestehende Tests

Keine.

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Aufgabe mit `IssueReferenz` starten → `issue.md` enthält Issue-Abschnitt | `EntwicklungsprozessServiceTests` (`CreateIssueFileAsync_ShouldIncludeIssueReference_WhenIssueReferenzIsSet`) | Issue-Nummer und Titel erscheinen in der erzeugten `issue.md` |
| Aufgabe ohne `IssueReferenz` starten → `issue.md` enthält keinen Issue-Abschnitt | `EntwicklungsprozessServiceTests` (`CreateIssueFileAsync_ShouldNotIncludeIssueReference_WhenIssueReferenzIsNull`) | Kein `## Verknüpftes Issue` in der erzeugten `issue.md` |

> Hinweis: Die Anforderung bezieht sich auf eine rein serverseitige Datei-Erzeugung ohne eigenen UI-Flow. Als E2E-Tests im Sinne des Projekts gelten daher die Integrationstests gegen die echte Datei-I/O, die `ProzessStartenAsync` als Einstiegspunkt verwenden — analog zu `CreateIssueFileAsync_ShouldCreateIssueFileWithCorrectContent_WhenAufgabeExists`.

Welche bestehenden E2E-Tests müssen angepasst werden?

Keine.

## Offene Punkte

Keine — alle Punkte wurden durch Anwendervorgaben geklärt:

| # | Offener Punkt | Entscheidung |
|---|---------------|--------------|
| 1 | Anzeigeformat der Kennung | `#<Nummer>` — konsistent mit bestehendem Muster im Service |
| 2 | Verhalten bei fehlender Nummer | Gesamter Issue-Block entfällt (Block nur ausgeben, wenn `IssueNummer > 0`) |
| 3 | Position im Dokument | Nach dem Metadaten-Block (`**Erstellt:**`), vor `## Anforderung` |
| 4 | Bezeichnung im Markdown | `## Verknüpftes Issue` |
