# Bestandsaufnahme: Automatische issue.md-Dateierstellung beim Repository-Setup

Diese Bestandsaufnahme analysiert die bestehende Code-Struktur des Projekts bezogen auf die Anforderung zur automatischen Erstellung einer `issue.md`-Datei während des Aufgaben-Repository-Setups und deren Eintragung in `.gitignore`.

## Zusammenfassung

### Vorhanden

- **`EntwicklungsprozessService`** – Zentrale Service-Klasse für Repository-Setup
  - Methode `ProzessStartenAsync` orchestriert den kompletten Prozess (Klon, Branch, Startskript)
  - Methode `ProzessStartenUndCliStartenAsync` kombiniert Setup mit KI-CLI-Start
  - Abhängigkeiten für File-System-Operationen sind vorhanden (IGitPlugin, ILogger)
  - Fehlerbehandlung und Logging-Pattern bereits etabliert (graceful degradation)

- **`Aufgabe`-Entität** – Datenmodell mit allen notwendigen Eigenschaften
  - `AnforderungsBeschreibung` – Text für `issue.md` Inhalt
  - `Titel` – für Markdown-Überschrift
  - `LokalerKlonPfad` – Zielverzeichnis für Dateien
  - `ErstellungsDatum` – optional für Metadaten
  - `Id` – für Metadaten

- **`AufgabeService`** – Service für Aufgabenverwaltung
  - Stellt Aufgaben-Details bereit (wird bereits von `EntwicklungsprozessService` genutzt)
  - Setzt `LokalerKlonPfad` nach erfolgreichen Klonen

- **`ProtokollService`** – Protokollierungs-Service
  - `AddEintragAsync` zum Hinzufügen von Git-Aktions-Protokolleinträgen
  - Pattern bereits etabliert für Fehlerbehandlung

- **Test-Infrastruktur** – Umfangreiche Testabdeckung
  - `EntwicklungsprozessServiceTests` mit Mock-Setup für Git-Plugin
  - `TestDbContextFactory` für In-Memory-Datenbank
  - AAA-Pattern (Arrange-Act-Assert) etabliert

### Nicht vorhanden / Zu implementieren

- **`CreateIssueFileAsync` Methode** – muss neu implementiert werden
  - Erstellt `{lokalerKlonPfad}/issue.md` mit Markdown-Inhalt
  - Fallback bei leerem `AnforderungsBeschreibung`

- **`UpdateGitignoreAsync` Methode** – muss neu implementiert werden
  - Liest/erstellt `.gitignore` Datei
  - Prüft auf Duplikate
  - Fügt `issue.md` Eintrag hinzu

- **Integration in `ProzessStartenAsync`** – muss angepasst werden
  - Neue Methoden-Aufrufe zwischen Zeile 138 (CloneRepositoryAsync) und 180 (StartenAsync)
  - Fehlerbehandlung mit Logging

- **Tests für neue Funktionalität** – müssen neu geschrieben werden
  - Tests für `CreateIssueFileAsync` (erfolgreicher Datei-Schreib, leere Beschreibung)
  - Tests für `UpdateGitignoreAsync` (neue Datei, existierende Datei, Duplikat-Handling)
  - Integration Tests für den kompletten Prozess

## Details

- [Logik](inventory/logic.md) – `EntwicklungsprozessService` mit Methoden und Abhängigkeiten
- [Datenmodell](inventory/models.md) – `Aufgabe`-Entität mit Eigenschaften
- [Tests](inventory/tests.md) – Existierende Test-Infrastruktur und Test-Patterns

## Hinweise zu Integrationspunkten

### Zeile 138: Nach `CloneRepositoryAsync`
```csharp
await gitPlugin.CloneRepositoryAsync(repository.RepositoryUrl, lokalerKlonPfad, ct);
```

Hier wird das Repository geklont. Nach dieser Zeile können die neue Methoden eingefügt werden:
```csharp
await CreateIssueFileAsync(lokalerKlonPfad, aufgabe, ct);
await UpdateGitignoreAsync(lokalerKlonPfad, ct);
```

### Zeile 180: `StartenAsync` Call
```csharp
await _aufgabeService.StartenAsync(aufgabeId, branchName, lokalerKlonPfad, ct);
```

Nach `CloneRepositoryAsync` und vor `StartenAsync` sollte die Integration erfolgen, damit:
1. Das Verzeichnis existiert (Klon erfolgreich)
2. Der `LokalerKlonPfad` noch lokal verfügbar ist (bevor Status geändert wird)

### Fehlerbehandlung
Das bestehende Pattern bei `RepositoryStartskriptService` (Zeilen 162-178) zeigt die Implementierung von graceful degradation:
- Fehler wird geloggt
- Hinweis wird in Protokoll-Nachricht aufgenommen
- Prozess wird nicht unterbrochen

Dasselbe Pattern sollte für `CreateIssueFileAsync` und `UpdateGitignoreAsync` verwendet werden.

### Logging
Methoden verwenden `ILogger<EntwicklungsprozessService>` mit strukturierten Log-Einträgen:
```csharp
_logger.LogInformation("...", aufgabeId);
_logger.LogWarning(ex, "...", aufgabeId);
```

Zusätzlich werden wichtige Operationen im `ProtokollService` dokumentiert:
```csharp
await _protokollService.AddEintragAsync(aufgabeId, ProtokollTyp.GitAktion, message, ct: ct);
```
