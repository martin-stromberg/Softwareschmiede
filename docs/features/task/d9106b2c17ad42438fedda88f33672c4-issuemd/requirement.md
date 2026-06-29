# Anforderungsübersetzung: Automatische issue.md-Dateierstellung beim Repository-Setup

## Fachliche Zusammenfassung

Wenn eine Aufgabe gestartet wird und das Repository geklont wird, soll automatisch eine `issue.md`-Datei im Repository-Verzeichnis angelegt werden. Diese Datei enthält die Aufgabenbeschreibung (die `AnforderungsBeschreibung` aus der `Aufgabe`-Entität). Zusätzlich wird die `issue.md` in die `.gitignore`-Datei des Repositories eingetragen, um zu verhindern, dass diese lokale, aufgabenspezifische Datei versionskontrolliert wird.

## Betroffene Klassen und Komponenten

### Services
- **`EntwicklungsprozessService`** (Erweiterung)
  - Neue private Methode: `CreateIssueFileAsync(string lokalerKlonPfad, Aufgabe aufgabe, CancellationToken ct)`
  - Neue private Methode: `UpdateGitignoreAsync(string lokalerKlonPfad, CancellationToken ct)`
  - Bestehende Methode `ProzessStartenAsync` erweitern: nach `CloneRepositoryAsync` und vor `StartenAsync` die Issue-Datei-Erstellung aufrufen

### Datenmodell
- **`Aufgabe`** Entität (bestehend, keine Änderung erforderlich)
  - Property `AnforderungsBeschreibung : string?` wird bereits verwendet

## Implementierungsansatz

1. **Neue Methode `CreateIssueFileAsync` in `EntwicklungsprozessService`:**
   - Parameter: `lokalerKlonPfad` (string), `aufgabe` (Aufgabe), `ct` (CancellationToken)
   - Erstellt `{lokalerKlonPfad}/issue.md` mit folgendem Inhalt:
     - Titel (basierend auf `aufgabe.Titel`)
     - Aufgabenbeschreibung (aus `aufgabe.AnforderungsBeschreibung`)
     - Optional: Aufgaben-ID, Erstellungsdatum, Branch-Name
   - Falls `aufgabe.AnforderungsBeschreibung` null/leer ist: Dummy-Inhalt oder Warnung im Log

2. **Neue Methode `UpdateGitignoreAsync` in `EntwicklungsprozessService`:**
   - Parameter: `lokalerKlonPfad` (string), `ct` (CancellationToken)
   - Prüft, ob `.gitignore` existiert
     - Falls ja: liest die Datei und prüft, ob `issue.md` bereits eingetragen ist
     - Falls nein: erstellt `.gitignore` mit minimalem Eintrag
   - Fügt `issue.md` hinzu, falls nicht bereits vorhanden (z.B. als neue Zeile: `issue.md`)
   - Nutzt `File.ReadAllLinesAsync` / `File.WriteAllLinesAsync` für async-sichere Dateizugriffe

3. **Integration in `ProzessStartenAsync`:**
   - Nach der Zeile `await gitPlugin.CloneRepositoryAsync(...)` (Zeile 138)
   - Vor der Zeile `await _aufgabeService.StartenAsync(...)` (Zeile 180)
   - Sequenz:
     ```csharp
     await CreateIssueFileAsync(lokalerKlonPfad, aufgabe, ct);
     await UpdateGitignoreAsync(lokalerKlonPfad, ct);
     ```
   - Fehlerbehandlung: Bei Exceptions während der Datei-Erstellung soll ein Protokolleintrag erstellt werden, aber der Prozess-Start nicht unterbrochen werden (graceful degradation)

4. **Protokollierung:**
   - Log-Einträge via `ILogger<EntwicklungsprozessService>` für:
     - Erfolgreiche `issue.md`-Erstellung
     - Erfolgreiche `.gitignore`-Aktualisierung
     - Fehler bei Datei-Operationen

## Konfiguration

Keine explizite Konfiguration erforderlich. Die Dateierstellung wird standardmäßig durchgeführt, wenn `ProzessStartenAsync` oder `ProzessStartenUndCliStartenAsync` aufgerufen wird.

**Dateiformat für `issue.md`** (Vorschlag):
```markdown
# Aufgabe: [Titel]

**Aufgaben-ID:** [aufgabe.Id]  
**Branch:** [branchName]  
**Erstellt:** [aufgabe.ErstellungsDatum]  

## Anforderung

[aufgabe.AnforderungsBeschreibung]
```

**Eintrag in `.gitignore`:**
```
issue.md
```

## Offene Fragen

1. **Format der `issue.md`:** Soll das Markdown-Format verwendet werden (wie oben vorgeschlagen), oder ein anderes Format (z.B. Plain Text, JSON)?
2. **Zusätzliche Metadaten:** Sollen weitere Metadaten in die `issue.md` aufgenommen werden (z.B. Aufgaben-ID, Branch-Name, Erstellungsdatum)?
3. **Überschreiben-Verhalten:** Falls `issue.md` bereits existiert (z.B. nach einem Rollback), soll die Datei überschrieben oder erhalten bleiben?
4. **Fehlerbehandlung:** Soll ein Fehler bei der Datei-Erstellung den gesamten Prozess-Start unterbrechen oder nur geloggt werden (graceful degradation)?
5. **`.gitignore` Position:** Soll `issue.md` als separater Eintrag am Ende der `.gitignore` hinzugefügt werden, oder gibt es eine bevorzugte Position?
6. **Verzeichnisspezifische `.gitignore`:** Falls das Repository mehrere `.gitignore`-Dateien in Subdirs hat, soll nur die Root-`.gitignore` angepasst werden?
