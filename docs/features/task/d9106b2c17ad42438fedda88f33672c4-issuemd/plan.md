# Umsetzungsplan: Automatische issue.md-Dateierstellung beim Repository-Setup

## Übersicht

Beim Start eines Prozesses (Repository-Klon) wird automatisch eine `issue.md`-Datei im geklonten Repository erstellt, die die Aufgabenbeschreibung enthält. Diese Datei wird in die `.gitignore` eingetragen, um sie vor Versionskontrolle zu schützen. Die Implementierung erfolgt durch zwei neue private Methoden in `EntwicklungsprozessService`, die zwischen dem Repository-Klon und dem Aufgaben-Start aufgerufen werden.

## Designentscheidungen

Keine — folgt bestehenden Mustern (Verwendung von `ILogger`, graceful degradation wie bei `RepositoryStartskriptService`, async File-APIs).

## Programmabläufe

### Aufgaben-Repository-Setup mit issue.md-Erstellung

1. `ProzessStartenAsync` aufgerufen
2. Repository geklont via `gitPlugin.CloneRepositoryAsync(...)`
3. **NEU:** `CreateIssueFileAsync(lokalerKlonPfad, aufgabe, ct)` aufgerufen
   - Markdown-Datei `{lokalerKlonPfad}/issue.md` erstellt
   - Inhalt: Titel, Aufgaben-ID, Branch-Name, Erstellungsdatum, Anforderungsbeschreibung
   - Bei leerem `AnforderungsBeschreibung`: Fallback-Text verwenden
   - Bei Fehler: Warnung geloggt via `ILogger`, Prozess setzt sich fort (graceful degradation)
4. **NEU:** `UpdateGitignoreAsync(lokalerKlonPfad, ct)` aufgerufen
   - `.gitignore` gelesen oder neu erstellt
   - Prüfung: Ist `issue.md` bereits eingetragen?
   - Falls nicht: Eintrag `issue.md` am Ende hinzufügen
   - Geschrieben via `File.WriteAllLinesAsync`
   - Bei Fehler: Warnung geloggt via `ILogger`, Prozess setzt sich fort (graceful degradation)
5. Aufgabe gestartet via `_aufgabeService.StartenAsync(...)`

Beteiligte Klassen/Komponenten: `EntwicklungsprozessService`, `Aufgabe`, `ILogger<EntwicklungsprozessService>`, `IGitPlugin`

## Neue Klassen

Keine.

## Änderungen an bestehenden Klassen

### `EntwicklungsprozessService` (Service)

- **Neue Methoden:**
  - `CreateIssueFileAsync(string lokalerKlonPfad, Aufgabe aufgabe, CancellationToken ct)` (private, async Task)
    - Zweck: Erstellt die `issue.md` Datei mit Markdown-Inhalt
    - Parameter: lokaler Klonpfad, Aufgabe-Entität, Cancellation-Token
    - Rückgabe: Task (void)
    - Verhalten: Erstellt Markdown-Datei mit Titel, ID, Branch, Datum und Anforderungsbeschreibung. Bei null/leerer Beschreibung Fallback-Text. Bei Fehler: Log-Warnung, kein Exception.
  
  - `UpdateGitignoreAsync(string lokalerKlonPfad, CancellationToken ct)` (private, async Task)
    - Zweck: Aktualisiert `.gitignore` und trägt `issue.md` ein
    - Parameter: lokaler Klonpfad, Cancellation-Token
    - Rückgabe: Task (void)
    - Verhalten: Liest `.gitignore` (oder erstellt neue), prüft auf Duplikate, fügt `issue.md` als neue Zeile am Ende hinzu. Bei Fehler: Log-Warnung, kein Exception.

- **Geänderte Methoden:**
  - `ProzessStartenAsync(Guid aufgabeId, string? branchName = null, CancellationToken ct = default)` (public)
    - Integrationspunkt: Nach Zeile `await gitPlugin.CloneRepositoryAsync(...)` (ca. Zeile 138) und vor Zeile `await _aufgabeService.StartenAsync(...)` (ca. Zeile 180)
    - Neue Aufrufe:
      ```
      try
      {
          await CreateIssueFileAsync(lokalerKlonPfad, aufgabe, ct);
          await UpdateGitignoreAsync(lokalerKlonPfad, ct);
      }
      catch (Exception ex)
      {
          _logger.LogWarning(ex, "Fehler beim Erstellen von issue.md oder Anpassen von .gitignore für Aufgabe {AufgabeId}", aufgabeId);
          // Prozess wird nicht unterbrochen (graceful degradation)
      }
      ```

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine.

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **Dateisystem-Fehler**: Wenn das Dateisystem schreibgeschützt ist oder der Speicherplatz erschöpft ist, können `CreateIssueFileAsync` und `UpdateGitignoreAsync` Fehler verursachen. Diese werden geloggt und unterbrechen nicht den Prozess (graceful degradation, wie etabliert).

- **`AnforderungsBeschreibung` ist null/leer**: Muss mit Fallback-Text (z. B. „Keine Anforderungsbeschreibung verfügbar" oder „[Anforderung leer]") umgegangen werden.

- **`.gitignore` enthält bereits `issue.md`**: Duplikate müssen geprüft und verhindert werden.

- **Keine bekannten Seiteneffekte auf bestehende Features**: Die neuen Methoden werden sequenziell zwischen zwei bestehenden Aufrufen eingefügt und haben keine Auswirkungen auf andere Teile des Systems.

## Umsetzungsreihenfolge

1. **Methode `CreateIssueFileAsync` in `EntwicklungsprozessService` implementieren**
   - Voraussetzungen: Keine (alle Abhängigkeiten sind bereits vorhanden: `ILogger`, File-APIs)
   - Beschreibung: Private async-Methode mit Parametern `string lokalerKlonPfad`, `Aufgabe aufgabe`, `CancellationToken ct`. Erstellt `{lokalerKlonPfad}/issue.md` mit Markdown-Inhalt basierend auf `aufgabe.Titel`, `aufgabe.Id`, `aufgabe.BranchName`, `aufgabe.ErstellungsDatum` und `aufgabe.AnforderungsBeschreibung`. Falls `AnforderungsBeschreibung` null oder leer: Fallback-Text verwenden. Datei schreiben via `File.WriteAllTextAsync` (oder StringBuilder + WriteAllTextAsync). Log-Eintrag bei erfolgreicher Erstellung. Bei Exception (z. B. IOException, UnauthorizedAccessException): Warnung geloggt via `_logger.LogWarning`, Exception nicht rethrown.

2. **Methode `UpdateGitignoreAsync` in `EntwicklungsprozessService` implementieren**
   - Voraussetzungen: Keine
   - Beschreibung: Private async-Methode mit Parametern `string lokalerKlonPfad`, `CancellationToken ct`. Konstruiert Pfad `{lokalerKlonPfad}/.gitignore`. Versucht, Datei zu lesen via `File.ReadAllLinesAsync` (oder ReadAllTextAsync + Split). Falls Datei nicht existiert: leere Liste starten. Prüft, ob `issue.md` bereits in der Liste enthalten ist (Case-sensitive Vergleich, da auch auf Linux korrekt sein soll). Falls nicht enthalten: Zeile `issue.md` am Ende anfügen. Schreiben via `File.WriteAllLinesAsync`. Log-Eintrag bei erfolgreicher Aktualisierung. Bei Exception: Warnung geloggt via `_logger.LogWarning`, Exception nicht rethrown.

3. **Integration in `ProzessStartenAsync` durchführen**
   - Voraussetzungen: `CreateIssueFileAsync` und `UpdateGitignoreAsync` müssen existieren
   - Beschreibung: In `ProzessStartenAsync` nach der Zeile `await gitPlugin.CloneRepositoryAsync(repository.RepositoryUrl, lokalerKlonPfad, ct);` (ca. Zeile 138) und vor der Zeile `await _aufgabeService.StartenAsync(aufgabeId, branchName, lokalerKlonPfad, ct);` (ca. Zeile 180) einen Try-Catch-Block einfügen, der beide neuen Methoden aufruft. Bei Exception: Warnung geloggt, Prozess setzt sich fort (wie bei `RepositoryStartskriptService` implementiert).

4. **Unittest: `CreateIssueFileAsync` testen**
   - Voraussetzungen: `CreateIssueFileAsync` muss existieren; Test-Infrastruktur mit Temp-Verzeichnissen vorhanden
   - Beschreibung: 
     - Test 1: `CreateIssueFileAsync_ShouldCreateIssueFileWithCorrectContent_WhenAufgabeExists` — Datei wird erstellt, Inhalt hat Markdown-Format mit Titel, ID, Branch, Datum und Anforderungsbeschreibung
     - Test 2: `CreateIssueFileAsync_ShouldUseFallbackText_WhenAnforderungsBeschreibungIsNull` — Fallback-Text wird verwendet, wenn `AnforderungsBeschreibung` null oder leer
     - Test 3: `CreateIssueFileAsync_ShouldUseFallbackText_WhenAnforderungsBeschreibungIsEmpty` — (ggf. separater Test oder mit Test 2 kombiniert)
     - Test 4: `CreateIssueFileAsync_ShouldLogWarning_WhenFileCreationFails` — Bei IOException/UnauthorizedAccessException wird Warnung geloggt, kein Exception geworfen (Mock File-Zugriff oder echtes Temp-Verzeichnis mit Berechtigungen)

5. **Unittest: `UpdateGitignoreAsync` testen**
   - Voraussetzungen: `UpdateGitignoreAsync` muss existieren; Test-Infrastruktur mit Temp-Verzeichnissen vorhanden
   - Beschreibung:
     - Test 1: `UpdateGitignoreAsync_ShouldCreateGitignore_WhenFileDoesNotExist` — Neue `.gitignore` wird erstellt mit einzelner Zeile `issue.md`
     - Test 2: `UpdateGitignoreAsync_ShouldAddIssueEntry_WhenGitignoreExists` — `issue.md` wird zu existierender `.gitignore` hinzugefügt (z. B. wenn `.gitignore` bereits andere Einträge hat)
     - Test 3: `UpdateGitignoreAsync_ShouldNotAddDuplicate_WhenIssueEntryAlreadyExists` — Wenn `issue.md` bereits in `.gitignore` enthalten, nicht erneut hinzufügen
     - Test 4: `UpdateGitignoreAsync_ShouldLogWarning_WhenFileOperationFails` — Bei IOException/UnauthorizedAccessException wird Warnung geloggt, kein Exception geworfen

6. **Integrationstests: `ProzessStartenAsync` anpassen**
   - Voraussetzungen: `CreateIssueFileAsync`, `UpdateGitignoreAsync`, Unittests müssen funktionieren
   - Beschreibung:
     - Test 1: `ProzessStartenAsync_ShouldCreateIssueFileAndUpdateGitignore_WhenCloneSucceeds` — Integration: Nach erfolgreichem Klon werden `issue.md` und `.gitignore` korrekt erstellt/angepasst. Datei-Existenz prüfen, Inhalt validieren.
     - Test 2: `ProzessStartenAsync_ShouldContinue_WhenIssueFileCreationFails` — Wenn `CreateIssueFileAsync` Exception wirft (z. B. Mock), wird Warnung geloggt und `_aufgabeService.StartenAsync` trotzdem aufgerufen (graceful degradation)
     - Test 3: `ProzessStartenAsync_ShouldContinue_WhenGitignoreUpdateFails` — Wenn `UpdateGitignoreAsync` Exception wirft, wird Warnung geloggt und Prozess setzt sich fort
     - Anpassung: Bestehender Test `ProzessStartenAsync_ShouldCloneAndCreateBranch_WhenAufgabeExists` ggf. erweitern oder neuer Test erstellen, der beide Datei-Operationen verifiziert

7. **E2E-Tests ergänzen**
   - Voraussetzungen: Alle Unit- und Integrationstests müssen grün sein
   - Beschreibung:
     - Test 1: `ProzessStartenAsync_E2E_ShouldCreateIssueFileWithRequirementDescription` — End-to-End: Aufgabe mit Anforderungsbeschreibung wird gestartet, `issue.md` wird erstellt und ist lesbar
     - Test 2: `ProzessStartenAsync_E2E_ShouldAddIssueToGitignore` — End-to-End: Nach Prozess-Start ist `issue.md` in `.gitignore` eingetragen

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|---------------------|------------|-------------------------------------|
| `CreateIssueFileAsync_ShouldCreateIssueFileWithCorrectContent_WhenAufgabeExists` | `EntwicklungsprozessServiceTests` | Datei `issue.md` wird mit korrektem Markdown-Inhalt erstellt (Titel, ID, Branch, Datum, Anforderung) |
| `CreateIssueFileAsync_ShouldUseFallbackText_WhenAnforderungsBeschreibungIsNullOrEmpty` | `EntwicklungsprozessServiceTests` | Fallback-Text wird verwendet, wenn `AnforderungsBeschreibung` null oder leer ist |
| `CreateIssueFileAsync_ShouldLogWarning_WhenFileCreationFails` | `EntwicklungsprozessServiceTests` | Bei IOException/UnauthorizedAccessException wird Warnung geloggt, kein Exception geworfen |
| `UpdateGitignoreAsync_ShouldCreateGitignore_WhenFileDoesNotExist` | `EntwicklungsprozessServiceTests` | Neue `.gitignore` wird erstellt mit einzelner Zeile `issue.md` |
| `UpdateGitignoreAsync_ShouldAddIssueEntry_WhenGitignoreExists` | `EntwicklungsprozessServiceTests` | `issue.md` wird zu existierender `.gitignore` hinzugefügt (z. B. neben anderen Einträgen) |
| `UpdateGitignoreAsync_ShouldNotAddDuplicate_WhenIssueEntryAlreadyExists` | `EntwicklungsprozessServiceTests` | Duplikate werden verhindert: `issue.md` wird nicht zweimal hinzugefügt |
| `UpdateGitignoreAsync_ShouldLogWarning_WhenFileOperationFails` | `EntwicklungsprozessServiceTests` | Bei IOException/UnauthorizedAccessException wird Warnung geloggt, kein Exception geworfen |
| `ProzessStartenAsync_ShouldCreateIssueFileAndUpdateGitignore_WhenCloneSucceeds` | `EntwicklungsprozessServiceTests` | Integration: Nach erfolgreichem Klon werden `issue.md` erstellt und `.gitignore` angepasst |
| `ProzessStartenAsync_ShouldContinue_WhenIssueFileCreationFails` | `EntwicklungsprozessServiceTests` | Fehler bei `CreateIssueFileAsync` unterbrechen den Prozess nicht, Warnung wird geloggt |
| `ProzessStartenAsync_ShouldContinue_WhenGitignoreUpdateFails` | `EntwicklungsprozessServiceTests` | Fehler bei `UpdateGitignoreAsync` unterbrechen den Prozess nicht, Warnung wird geloggt |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `ProzessStartenAsync_ShouldCloneAndCreateBranch_WhenAufgabeExists` | Test-Verhalten könnte sich ändern: Datei-Operationen (`CreateIssueFileAsync`, `UpdateGitignoreAsync`) werden aufgerufen. Kann entweder durch echte Dateisystem-Operationen oder durch Mocks validiert werden. Test-Setup ggf. erweitern. |
| `ProzessStartenUndCliStartenAsync_Success` | Test-Setup muss ggf. Temp-Verzeichnis für `issue.md`/`.gitignore` vornehmen oder Datei-Operationen mocken. |
| `ProzessStartenUndCliStartenAsync_RepositoryCloneFails_RollbackStatus` | Keine Anpassung nötig — Fehler tritt vor den neuen Methoden auf. |
| `ProzessStartenUndCliStartenAsync_CliStartFails_RollbackStatus` | Keine Anpassung nötig — Fehler tritt nach den neuen Methoden auf. |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Aufgabe starten: `issue.md` wird erstellt | `EntwicklungsprozessServiceTests` (E2E-Bereich oder separate E2E-Testklasse) | `issue.md` wird automatisch mit korrektem Markdown-Inhalt (Titel, ID, Branch, Datum, Anforderung) erstellt |
| Aufgabe starten: `issue.md` wird in `.gitignore` eingetragen | `EntwicklungsprozessServiceTests` (E2E-Bereich) | `.gitignore` wird automatisch angepasst, `issue.md` ist als Eintrag vorhanden |
| Aufgabe starten: Fallback bei leerer Anforderung | `EntwicklungsprozessServiceTests` (E2E-Bereich) | Auch bei null/leerer `AnforderungsBeschreibung` wird `issue.md` mit Fallback-Text erstellt, Prozess nicht unterbrochen |

Welche bestehenden E2E-Tests müssen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| E2E-Test für `ProzessStartenAsync` (falls vorhanden) | Prüfung auf Existenz und Inhalt von `issue.md` muss hinzugefügt werden; Prüfung auf `.gitignore`-Eintrag muss hinzugefügt werden. |

## Offene Punkte

Alle Punkte aus der Anforderung wurden geklärt:

| # | Offener Punkt | Empfohlener Vorschlag |
|---|---------------|----------------------|
| 1 | Format der `issue.md` | **Markdown** — lesbar, gut für Dokumentation, konsistent mit anderen Markdown-Dateien im Projekt |
| 2 | Zusätzliche Metadaten in `issue.md` | **Ja** — `Id`, `BranchName`, `ErstellungsDatum` hinzufügen — hilft bei der Kontextualisierung der Aufgabe |
| 3 | Überschreiben-Verhalten bei existierender `issue.md` | **Überschreiben** — Garantiert Konsistenz mit aktuellen Aufgabendaten bei Rollbacks oder Wiederholungen |
| 4 | Fehlerbehandlung bei Datei-Operationen | **Graceful degradation** — Log-Eintrag erstellen, Prozess-Start nicht unterbrechen (wie bei `RepositoryStartskriptService` etabliert) |
| 5 | `.gitignore` Position für neuen Eintrag | **Am Ende als separate Zeile hinzufügen** — einfach zu implementieren, logisch, wenn `.gitignore` nicht strukturiert ist |
| 6 | Verzeichnisspezifische `.gitignore` in Subdirectories | **Nur Root-`.gitignore` anpassen** — Vereinfachung, da geklonte Repositories typischerweise flach sind; `.gitignore` in Subverzeichnissen würden andere Dateien betreffen, nicht `issue.md` im Root |
