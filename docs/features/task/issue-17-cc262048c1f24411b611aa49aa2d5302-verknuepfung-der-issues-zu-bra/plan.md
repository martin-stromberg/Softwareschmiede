# Umsetzungsplan: Verknüpfung der Issues zu Branches

## Übersicht

Die Anwendung speichert Issue-Referenz und Branch bereits an derselben Aufgabe. Der Hauptpfad zur Pull-Request-Erstellung (`GitOrchestrationService.PullRequestErstellenAsync`) ergänzt im aktuellen Arbeitsbaum bereits eine GitHub-kompatible Closing-Direktive (`Closes #<IssueNummer>`) und verhindert Duplikate für dieselbe Issue. Die Umsetzung fokussiert daher auf Konsolidierung, vollständige Absicherung und einen kleinen Kompatibilitätsabschluss: Die PR-Body-Normalisierung wird als wiederverwendbare Application-Logik gekapselt, im bestehenden Hauptpfad weiterverwendet und auch im älteren PR-Pfad von `EntwicklungsprozessService` genutzt, damit kein interner Aufrufer die Issue-Verknüpfung umgehen kann.

## Designentscheidungen

| Bereich | Entscheidung | Begründung |
|---------|--------------|------------|
| Ort der Closing-Logik | Application-Schicht, nicht GitHub-Plugin | Die Logik benötigt `Aufgabe` und `IssueReferenz`; das Plugin soll weiterhin nur `repositoryId`, Branch, Titel und Body an den Provider übergeben. |
| Wiederverwendung | Neue interne/statische Hilfskomponente für PR-Body-Normalisierung | Verhindert doppelte Regex- und Body-Aufbau-Logik zwischen `GitOrchestrationService` und dem älteren `EntwicklungsprozessService`-PR-Pfad. |
| Direktive | Immer `Closes #<IssueNummer>` | GitHub unterstützt diese Direktive zuverlässig; keine neue Konfiguration erforderlich. Die Anforderung nennt genau dieses Verhalten als Ziel. |
| Provider-Behandlung | Provider-neutral in der Application-Schicht ergänzen, sobald eine gültige Issue-Nummer vorhanden ist | Der bestehende Service kennt an dieser Stelle nicht sicher, ob ein Plugin GitHub-semantische Closing-Direktiven auswertet. Für nicht unterstützende Provider bleibt es regulärer PR-Text; GitHub schließt beim Merge automatisch. |
| Gültige Issue-Nummer | Nur `IssueNummer > 0` | `IssueNummer` ist nullable; `null`, `0` und negative Werte dürfen das bisherige PR-Verhalten nicht verändern. |
| UI-Hinweis | Keine UI-Änderung | Die Anforderung verlangt automatische Verknüpfung ohne manuelle Nacharbeit. Ein zusätzlicher Hinweis in der UI ist nicht nötig und würde die Oberfläche ohne zwingenden Nutzen erweitern. |

## Programmablauf

### Pull Request aus Aufgabe erstellen

1. Der PR-Start lädt die Aufgabe inklusive `IssueReferenz`, Branch und Repository-Kontext.
2. Der PR-Titel bleibt unverändert: expliziter Titel oder Fallback auf `Aufgabe.Titel`.
3. Der Body wird über eine zentrale Normalisierung aufgebaut:
   - `body == null` → Fallback `Automatisch erstellt für Aufgabe: {Titel}`;
   - `body` leer/Whitespace und gültige Issue-Nummer → `Closes #<IssueNummer>`;
   - gültige Issue-Nummer und noch keine Closing-Direktive für dieselbe Issue → zwei Zeilen Abstand und `Closes #<IssueNummer>` anhängen;
   - bereits vorhandene Closing-Direktive für dieselbe Issue → Body unverändert lassen;
   - keine gültige Issue-Nummer → Body unverändert zum bisherigen Verhalten lassen.
4. `IGitPlugin.CreatePullRequestAsync(...)` erhält den normalisierten Body.
5. Bei gültiger Issue-Nummer protokolliert der Hauptpfad weiterhin, dass Auto-Close aktiv ist.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `PullRequestBodyBuilder` | statische Hilfsklasse in `Softwareschmiede.Application.Services` | Kapselt Aufbau und Prüfung des Pull-Request-Bodys für Aufgaben mit optionaler Issue-Referenz. |

## Änderungen an bestehenden Klassen

### `GitOrchestrationService`

- `BuildPullRequestBody(Aufgabe, string?)` und `ContainsClosingDirectiveForIssue(...)` werden entfernt oder auf dünne Weiterleitung reduziert.
- `PullRequestErstellenAsync(Guid, string?, string?, ...)` ruft `PullRequestBodyBuilder.Build(aufgabe, body)` auf.
- Das bestehende Protokollverhalten mit Hinweis auf `Issue #<nr>` bleibt erhalten.

### `EntwicklungsprozessService`

- Der ältere PR-Pfad `PullRequestErstellenAsync(Guid aufgabeId, string repositoryId, string title, string body, ...)` lädt die Aufgabe mit `GetDetailAsync(...)` statt nur `GetByIdAsync(...)`, damit `IssueReferenz` verfügbar ist.
- Vor `CreatePullRequestAsync(...)` wird ebenfalls `PullRequestBodyBuilder.Build(aufgabe, body)` verwendet.
- Der vorhandene Signaturvertrag bleibt unverändert; bestehende Aufrufer müssen nicht angepasst werden.
- Optional wird der Protokolleintrag analog zum Hauptpfad um den Auto-Close-Hinweis ergänzt, wenn `IssueNummer > 0`.

## Datenbankmigrationen

Keine.

## Konfigurationsänderungen

Keine.

## Validierungsregeln

Keine neuen Eingabevalidierungen. Die bestehende Regel "nur Issue-Nummern größer 0 erzeugen Closing-Direktiven" wird explizit testgesichert.

## Seiteneffekte und Risiken

- `Closes #...` wird provider-neutral in den Body geschrieben. Bei GitHub ist das gewünschtes Verhalten; bei anderen PR-fähigen Providern ist es Text ohne garantierte Schließwirkung.
- Die vorhandene Regex erkennt gängige Direktiven wie `close`, `closes`, `closed`, `fix`, `fixes`, `fixed`, `resolve`, `resolves`, `resolved` sowie optional `owner/repo#<nr>`. Mehrfachlisten wie `Closes #1, #2` werden für spätere Nummern nicht zwingend erkannt; diese Erweiterung ist nicht Bestandteil der Anforderung.
- Die Extraktion in eine Hilfsklasse ist verhaltensneutral, muss aber durch die bestehenden PR-Body-Tests abgesichert bleiben.

## Umsetzungsreihenfolge

1. **`PullRequestBodyBuilder` anlegen**
   - Neue Datei unter `src/Softwareschmiede/Application/Services/`.
   - Methode `Build(Aufgabe aufgabe, string? body)` implementieren.
   - Closing-Direktiven-Erkennung aus `GitOrchestrationService.ContainsClosingDirectiveForIssue(...)` übernehmen.

2. **`GitOrchestrationService` auf Builder umstellen**
   - `BuildPullRequestBody(...)` durch Builder-Aufruf ersetzen.
   - Private Regex-Methode entfernen, falls sie nicht mehr verwendet wird.
   - Bestehendes Logging unverändert lassen.

3. **Älteren PR-Pfad in `EntwicklungsprozessService` absichern**
   - Aufgabe per `GetDetailAsync(...)` laden.
   - Body mit `PullRequestBodyBuilder.Build(...)` normalisieren.
   - `CreatePullRequestAsync(...)` mit normalisiertem Body aufrufen.

4. **Bestehende Testdokumentation/Kommentarfehler bereinigen**
   - Doppelte XML-`summary`-Kommentare in `GitOrchestrationServiceTests` bei den PR-Body-Tests korrigieren, weil sie aktuell sichtbar fehlerhaft sind.

5. **Tests ergänzen**
   - Fehlende Fälle für `IssueNummer == null` und `IssueNummer <= 0` ergänzen.
   - Optional Regex-Fälle für `owner/repo#<nr>` und andere Direktiven-Schreibweisen absichern.
   - Einen Test für den älteren `EntwicklungsprozessService`-PR-Pfad ergänzen oder erweitern.

6. **Verifikation ausführen**
   - Fokussierte Tests für `GitOrchestrationServiceTests` und relevante `EntwicklungsprozessServiceTests`.
   - Danach den betroffenen Testprojektlauf ausführen, sofern Laufzeit und lokale Abhängigkeiten es zulassen.

## Tests

### Neue Tests

| Test | Testklasse | Erwartung |
|------|------------|-----------|
| `PullRequestErstellenAsync_ShouldKeepBodyUnchanged_WhenIssueReferenceHasNoIssueNumber` | `GitOrchestrationServiceTests` | Aufgabe mit `IssueReferenz`, aber `IssueNummer == null`, übergibt den Body unverändert an das Plugin. |
| `PullRequestErstellenAsync_ShouldKeepBodyUnchanged_WhenIssueNumberIsNotPositive` | `GitOrchestrationServiceTests` | `IssueNummer == 0` oder negativ erzeugt keine Closing-Direktive. |
| `PullRequestErstellenAsync_ShouldNotDuplicateClosingDirective_WhenBodyContainsQualifiedIssueReference` | `GitOrchestrationServiceTests` | Bereits vorhandenes `Fixes owner/repo#<nr>` wird erkannt und nicht dupliziert. |
| `PullRequestErstellenAsync_ShouldNotDuplicateClosingDirective_WhenBodyUsesAlternativeClosingVerb` | `GitOrchestrationServiceTests` | Schreibweisen wie `resolved #<nr>` oder `Closed #<nr>` werden erkannt. |
| `PullRequestErstellenAsync_ShouldAppendClosingDirective_WhenLegacyEntwicklungsprozessPathHasIssueReference` | `EntwicklungsprozessServiceTests` oder passende Service-Testklasse | Der ältere PR-Pfad ergänzt ebenfalls `Closes #<IssueNummer>`. |

### Bestehende betroffene Tests

| Test | Erwartung nach Umsetzung |
|------|--------------------------|
| `PullRequestErstellenAsync_ShouldAppendClosingDirectiveAndLogIssue_WhenAufgabeHasIssueReference` | Bleibt grün; prüft Ergänzung und Protokoll-Hinweis. |
| `PullRequestErstellenAsync_ShouldNotDuplicateClosingDirective_WhenBodyAlreadyContainsDirective` | Bleibt grün; prüft Duplikatvermeidung. |
| `PullRequestErstellenAsync_ShouldUseOnlyClosingDirective_WhenBodyIsWhitespaceAndIssueExists` | Bleibt grün; prüft Whitespace-Body. |
| `PullRequestErstellenAsync_ShouldAppendClosingDirectiveForCurrentIssue_WhenBodyContainsDirectiveForAnotherIssue` | Bleibt grün; prüft Erhalt anderer Direktiven und Ergänzung der aktuellen Issue. |
| `ProzessStartenAsync_ShouldPersistIssueBasedBranch_WhenAufgabeWasCreatedFromIssue` | Bleibt grün; sichert Branch-Persistenz aus Issue-Aufgaben. |
| `CreateFromIssueAsync_ShouldPersistAufgabeWithIssueReferenz_WhenIssueGiven` | Bleibt grün; sichert persistierte Issue-Referenz. |

### E2E-Tests

Kein neuer E2E-Test erforderlich. Die Anforderung betrifft Service-Logik beim Aufbau des Pull-Request-Bodys und lässt sich deterministisch mit Unit-/Integrationstests gegen gemockte Git-Plugins prüfen. Ein echter GitHub-Merge ist nicht sinnvoll automatisierbar und würde externe Credentials sowie Netzwerkzustand in den Test einführen.

## Offene Punkte

Keine.
