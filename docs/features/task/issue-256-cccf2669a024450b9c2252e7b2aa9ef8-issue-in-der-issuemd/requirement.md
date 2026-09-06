# Übersetzte Anforderung – Issue in der issue.md

## Fachliche Zusammenfassung

Beim Start einer Aufgabe erzeugt `EntwicklungsprozessService.CreateIssueFileAsync()` eine `issue.md` im effektiven Arbeitsverzeichnis. Diese Methode soll erweitert werden: Ist an der `Aufgabe` eine `IssueReferenz` hinterlegt, werden `IssueNummer` (Kennung) und `Titel` (Name) des verknüpften Issues zusätzlich in den Dateiinhalt geschrieben. Ist keine Referenz vorhanden, bleibt die Ausgabe unverändert.

## Betroffene Klassen und Komponenten

### Logikklassen / Services
- `EntwicklungsprozessService` – Methode `CreateIssueFileAsync()`: Markdown-Inhalt der `issue.md` um einen optionalen Issue-Abschnitt ergänzen.

### Datenmodellklassen
- `Aufgabe` – Navigationseigenschaft `IssueReferenz` (bereits vorhanden, kein Änderungsbedarf am Modell).
- `IssueReferenz` – Properties `IssueNummer` (`int?`) und `Titel` (`string`) werden ausgelesen (kein Änderungsbedarf).

### Tests
- `EntwicklungsprozessServiceTests` – bestehender Test `CreateIssueFileAsync_*` erweitern oder neue Testmethoden ergänzen:
  - `CreateIssueFileAsync_ShouldIncludeIssueReference_WhenIssueReferenzIsSet()`
  - `CreateIssueFileAsync_ShouldNotIncludeIssueReference_WhenIssueReferenzIsNull()`

## Implementierungsansatz

- `GetDetailAsync()` in `AufgabeService` lädt `IssueReferenz` bereits per Eager Loading (`.Include(a => a.IssueReferenz)`). Das `aufgabe`-Objekt, das `CreateIssueFileAsync()` übergeben wird, enthält die Referenz daher immer vollständig befüllt – kein zusätzliches Laden notwendig.
- In `CreateIssueFileAsync()` wird geprüft, ob `aufgabe.IssueReferenz` nicht `null` ist. Trifft dies zu, wird dem Markdown-String der `issue.md` ein weiterer Abschnitt mit `IssueNummer` und `Titel` hinzugefügt. Ist `IssueNummer` nicht gesetzt (`null`), wird nur der `Titel` ausgegeben (Annahme – zu bestätigen, siehe Offene Fragen).
- Es wird kein neues Interface, keine neue Klasse und keine Datenbankänderung benötigt.

## Konfiguration

Kein Konfigurationsbedarf. Das Verhalten ist rein datengetrieben: Inhalt der `issue.md` richtet sich danach, ob `Aufgabe.IssueReferenz` gesetzt ist oder nicht.

## Offene Fragen

1. **Anzeigeformat der Kennung:** Soll die Nummer als `#42` (Raute-Präfix), als reine Zahl `42` oder als vollständige URL (`IssueReferenz.IssueUrl`) ausgegeben werden?
2. **Verhalten bei fehlender Nummer:** `IssueReferenz.IssueNummer` ist `int?`. Soll bei `null`-Nummer nur der Titel erscheinen, oder soll die gesamte Issue-Sektion dann unterdrückt werden?
3. **Position im Dokument:** Soll der Issue-Abschnitt vor der Anforderungsbeschreibung (z. B. als Metadaten-Block) oder nach ihr erscheinen?
4. **Bezeichnung im Markdown:** Welche Überschrift oder welches Label soll für den Issue-Abschnitt verwendet werden (z. B. `**Issue:**`, `## Verknüpftes Issue`)?
