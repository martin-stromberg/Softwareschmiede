# Anforderungsanalyse: Plugin-Resolution für autonome Aufgaben beim Repository-Klon

## Fachliche Zusammenfassung

Beim Anlegen autonomer Aufgaben schlägt das Klonen des Repositories fehl, wenn das global konfigurierte "Default"-SCM-Plugin nicht dem am `GitRepository.PluginTyp` der konkreten Aufgabe konfigurierten Plugin entspricht. Der `AutonomAufgabenInitialisierungsService` soll das korrekte Plugin-Plugin anhand der Aufgabe auflösen und verwenden — analog zum regulären Start-Pfad (`EntwicklungsprozessService.ProzessStartenAsync` → `ResolvePluginAsync`), der dieses Verhalten bereits korrekt umsetzt.

## Betroffene Klassen und Komponenten

### Logikklassen / Services

- **`AutonomAufgabenInitialisierungsService`** (Änderung erforderlich / durchgeführt)
  - Konstruktor-Abhängigkeiten: `PluginSelectionService` injizieren statt `IGitPlugin`
  - Methode `InitialisiereAsync(...)`: Plugin anhand von `aufgabe.GitRepository.PluginTyp` via `_pluginSelectionService.ResolveSourceCodeManagementPluginAsync(...)` auflösen
  - Methode `KloneHauptRepositoryAsync(...)`: Das aufgelöste Plugin als Parameter erhält (nicht mehr das injizierte Default-Plugin verwenden)
  - Methode `ErstelleProjektbranchAsync(...)`: Das aufgelöste Plugin als Parameter erhält (nicht mehr das injizierte Default-Plugin verwenden)

- **`EntwicklungsprozessService`** (Referenzimplementierung, keine Änderung erforderlich)
  - Zeigt korrektes Pattern für Plugin-Auflösung in `ResolvePluginAsync(...)`
  - Nutzt `PluginSelectionService.ResolveSourceCodeManagementPluginAsync(resolvedPluginPrefix, ct)` zur Auflösung

- **`PluginSelectionService`** (Bestehend, wird verwendet)
  - Methode `ResolveSourceCodeManagementPluginAsync(string? pluginTyp, CancellationToken ct): Task<IGitPlugin>`

### Interfaces und Domain-Objekte

- **`IGitPlugin`** (Bestehend, wird verwendet)
  - Methode `CloneRepositoryAsync(string repositoryUrl, string targetPath, CancellationToken ct)`
  - Methode `CreateBranchAsync(string localRepositoryPath, string branchName, string? startPoint, CancellationToken ct)`

- **`Aufgabe`** (Bestehend)
  - Property `GitRepository?: GitRepository`

- **`GitRepository`** (Bestehend)
  - Property `PluginTyp: string?`

### Tests

- **`AutonomAufgabenInitialisierungsServiceTests`** (ggf. Anpassung erforderlich)
- **`AutonomAufgabenDetailViewModelTests`** (ggf. Anpassung erforderlich)
- **`AutonomAufgabeInitialisierungsDialogViewModelTests`** (ggf. Anpassung erforderlich)
- **`E2E_AutonomAufgabenInitialisierung`** (ggf. Anpassung erforderlich)

## Implementierungsansatz

### Architektur und Abhängigkeiten

1. **Dependency Injection anpassen**
   - `AutonomAufgabenInitialisierungsService`: `IGitPlugin` aus Konstruktor-Abhängigkeiten entfernen
   - `PluginSelectionService` bereits injiziert (keine Änderung nötig)

2. **Plugin-Auflösung durchführen**
   - In `InitialisiereAsync(Aufgabe aufgabe, ...)` vor dem Klon:
     ```csharp
     var gitPlugin = await _pluginSelectionService.ResolveSourceCodeManagementPluginAsync(
         aufgabe.GitRepository?.PluginTyp, 
         ct);
     ```
   - Das aufgelöste `gitPlugin` an `KloneHauptRepositoryAsync(...)` und `ErstelleProjektbranchAsync(...)` übergeben

3. **Methoden-Signaturen anpassen**
   - `KloneHauptRepositoryAsync(IGitPlugin gitPlugin, Aufgabe aufgabe, string zielPfad, CancellationToken ct)`: Das Plugin als Parameter übergeben (derzeit nicht vorhanden, wird generiert)
   - `ErstelleProjektbranchAsync(IGitPlugin gitPlugin, Aufgabe aufgabe, string repoMainPfad, string projektBranchName, CancellationToken ct)`: Das Plugin als Parameter übergeben (derzeit nicht vorhanden, wird generiert)

### Fehlerfallbehandlung

- Wenn `aufgabe.GitRepository?.PluginTyp` null ist, ist die Default-Auflösung via `PluginSelectionService.ResolveSourceCodeManagementPluginAsync(null, ct)` etabliert
- Fehler beim Klonen/Branch-Erstellen werden mit Kontext-Information der Aufgabe geworfen (analog `EntwicklungsprozessService`)

## Konfiguration

Keine neue Konfiguration erforderlich. Das Verhalten wird durch die Aufgaben-Daten (`aufgabe.GitRepository.PluginTyp`) gesteuert, die bereits vom Anwender beim Repository-Setup konfiguriert werden.

## Offene Fragen

1. **Rollout für bestehende autonome Aufgaben:** Wie sollen bestehende, bereits initialisierte autonome Aufgaben mit falschem Plugin behandelt werden? Wird ein Reinitialisierungs-Mechanismus bereitgestellt?

2. **Plugin-Auflösungs-Fehler:** Welcher Fehler wird geworfen, wenn `aufgabe.GitRepository` null ist oder kein Plugin für den angegebenen `PluginTyp` vorhanden ist? Soll ein aussagekräftiger Fehler an der Benutzeroberfläche angezeigt werden?

3. **Test-Abdeckung:** Wird ein E2E-Test mit mehreren (nicht-Default-)Plugins durchgeführt, um zu überprüfen, dass der Klon mit dem korrekten Plugin erfolgt?
