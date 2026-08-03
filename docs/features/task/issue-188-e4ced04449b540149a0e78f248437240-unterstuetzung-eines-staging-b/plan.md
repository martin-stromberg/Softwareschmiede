# Umsetzungsplan: Unterstützung eines Staging-Branch / Basis-Branch-Konfiguration

## Übersicht

Implementierung einer konfigurierbaren Basis-Branch-Auswahl pro Git-Repository, von dem neue Feature-Branches für Aufgaben abgezweigt werden. Der Basis-Branch wird persistent in `GitRepository` gespeichert, validiert beim Aufgabenstart und bei der PR-Erstellung als Ziel-Branch verwendet. Feature-Branch-Erstellung und PR-Ziele nutzen den konfigurierten Basis-Branch statt automatisch den Remote-Standard-Branch.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Speicherort der Basis-Branch-Konfiguration | Eigenschaft `DefaultSourceBranchName` in `GitRepository` | Repository-spezifisch, keine Abhängigkeit vom Startskript; zentrale Ort für Repository-Einstellungen |
| Validierungszeitpunkt | Lazy-Validierung beim Aufgabenstart (in `ProzessStartenAsync()`) | Erlaubt Szenario, bei dem Branch später erstellt wird; Fehlermeldung ist konkret und zeitnah |
| Fallback ohne Konfiguration | Verwendet Remote-Standard-Branch (via `GetDefaultBranchAsync()`) | Abwärtskompatibilität mit bestehenden Repositories ohne Konfiguration |
| Feature-Branch-Erstellung | `IGitPlugin.CreateBranchAsync()` erweitert um `sourceBranchName`-Parameter | Konsistent mit bestehender Plugin-API-Struktur; ermöglicht Plugin-Implementierungen, Basis-Branch korrekt zu handhaben |
| PR-Ziel-Branch | `IGitPlugin.CreatePullRequestAsync()` erweitert um `baseBranch`-Parameter | Plugin-API gibt explizit Ziel-Branch an, nicht Plugin-Fallback |

## Programmabläufe

### Aufgabe starten mit Basis-Branch-Validierung

1. Benutzer startet Aufgabe über `EntwicklungsprozessService.ProzessStartenAsync()`
2. Service ermittelt `GitRepository` und dessen `DefaultSourceBranchName`
3. Falls `DefaultSourceBranchName` nicht `null`: Validierung durchführen
   - `IGitPlugin.GetRemoteBranchesAsync()` aufrufen, um verfügbare Remote-Branches zu prüfen
   - Wenn konfigurierter Basis-Branch nicht in Liste: `GitBranchNotFoundException` werfen mit aussagekräftiger Meldung
4. Falls Validierung erfolgreich oder kein Basis-Branch konfiguriert: Repository klonen, Feature-Branch erstellen
5. `SetupBranchAsync()` wird mit `baseBranchName` aufgerufen (optional, kann null sein)

Beteiligte Klassen/Komponenten: `EntwicklungsprozessService`, `IGitPlugin`, `GitRepository`

### Feature-Branch vom Basis-Branch erstellen

1. `EntwicklungsprozessService.SetupBranchAsync()` wird mit `baseBranchName` aufgerufen
2. Wenn `baseBranchName` vorhanden und nicht der Remote-Standard-Branch:
   - `IGitPlugin.CheckoutRemoteBranchAsync()` aufrufen, um Remote-Branch lokal verfolgbar zu machen
   - Anschließend `IGitPlugin.CreateBranchAsync()` mit `sourceBranchName = baseBranchName` aufrufen
3. Wenn `baseBranchName` null/leer oder ist Standard-Branch:
   - `IGitPlugin.CreateBranchAsync()` ohne `sourceBranchName` (oder `sourceBranchName` auf null/Remote-Standard) aufrufen

Beteiligte Klassen/Komponenten: `EntwicklungsprozessService`, `IGitPlugin`

### Pull Request mit Basis-Branch als Ziel erstellen

1. Benutzer erstellt PR über Ribbon-Action oder `GitOrchestrationService.PullRequestErstellenAsync()`
2. Service ermittelt `GitRepository` aus Aufgabe/Projekt
3. Wenn `GitRepository.DefaultSourceBranchName` konfiguriert:
   - `IGitPlugin.CreatePullRequestAsync()` mit `baseBranch = DefaultSourceBranchName` aufrufen
4. Wenn nicht konfiguriert:
   - `IGitPlugin.CreatePullRequestAsync()` mit `baseBranch = null` (Plugin-Fallback auf Remote-Standard)
5. `PullRequestReferenzService.SaveCreatedAsync()` speichert PR mit `TargetBranch` (vom Plugin zurück)

Beteiligte Klassen/Komponenten: `GitOrchestrationService`, `IGitPlugin`, `PullRequestReferenzService`, `GitRepository`

## Neue Klassen

Keine.

## Änderungen an bestehenden Klassen

### `GitRepository` (Domain Model)

- **Neue Eigenschaften:** 
  - `DefaultSourceBranchName` (`string?`) — Konfigurierter Basis-Branch-Name; `null` bedeutet Remote-Standard-Branch wird verwendet

### `IGitPlugin` (Service Interface)

- **Geänderte Methoden:**
  - `CreateBranchAsync(localPath: string, branchName: string, sourceBranchName: string?, ct: CancellationToken)` — Neuer optionaler Parameter `sourceBranchName` spezifiziert den Basis-Branch, von dem der neue Branch abgezweigt werden soll. Wenn `null`/leer, wird der aktuelle HEAD verwendet (Fallback).
  - `CreatePullRequestAsync(repositoryId: string, branchName: string, baseBranch: string?, title: string, body: string, ct: CancellationToken)` — Neuer optionaler Parameter `baseBranch` spezifiziert den Ziel-Branch. Wenn `null`/leer, wird Plugin-Fallback (Remote-Standard) verwendet.

### `EntwicklungsprozessService` (Application Service)

- **Geänderte Methoden:**
  - `ProzessStartenAsync()` — Validiert `GitRepository.DefaultSourceBranchName`, falls konfiguriert:
    1. Ruft `IGitPlugin.GetRemoteBranchesAsync()` auf
    2. Prüft, ob `DefaultSourceBranchName` in der Liste enthalten ist
    3. Wirft `GitBranchNotFoundException` (oder ähnlich), wenn Branch nicht existiert
  - `SetupBranchAsync()` — Erweitert um Logik zur Basis-Branch-Nutzung:
    1. Liest `DefaultSourceBranchName` aus `GitRepository`
    2. Falls vorhanden: Ruft `IGitPlugin.CheckoutRemoteBranchAsync(remoteBaseBranch)` auf
    3. Ruft `IGitPlugin.CreateBranchAsync()` mit `sourceBranchName = DefaultSourceBranchName` auf

### `GitOrchestrationService` (Application Service)

- **Geänderte Methoden:**
  - `PullRequestErstellenAsync()` — Erweitert um Basis-Branch-Handling:
    1. Ermittelt `GitRepository` (existiert bereits über `ResolveRepositoryAsync()`)
    2. Liest `DefaultSourceBranchName`
    3. Ruft `IGitPlugin.CreatePullRequestAsync()` mit `baseBranch = DefaultSourceBranchName` (kann null sein)

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `AddDefaultSourceBranchNameToGitRepository` | `git_repository` → neue Spalte `DefaultSourceBranchName (varchar(255), nullable)` | Fügt optionales Feld für konfigurierten Basis-Branch hinzu |

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `GitRepository.DefaultSourceBranchName` (bei Aufgabenstart) | Wenn gesetzt, muss im Remote-Repository vorhanden sein (geprüft via `IGitPlugin.GetRemoteBranchesAsync()`) | `GitBranchNotFoundException` – Aufgabenstart schlägt mit Fehlermeldung fehl |
| `GitRepository.DefaultSourceBranchName` (beim Speichern) | Keine Validierung beim Speichern (Lazy-Validierung); Benutzer kann zukünftig erstellte Branches konfigurieren | — |

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **Feature-Branch-Erstellung:** Bestehende Aufgaben, die ohne `DefaultSourceBranchName` gestartet wurden, erstellen Feature-Branches weiterhin vom aktuellen HEAD des aktuellen Remote-Standard-Branch (Fallback). Kein Breaking Change.
- **PR-Ziel-Branch:** Existierende PRs, deren Basis-Branch sich ändert, werden nicht nachträglich angepasst (einmalige PR-Erstellung). Zukünftige PRs verwenden den neuen Basis-Branch.
- **UI-Integration:** Benutzer muss informiert werden, dass nicht alle Repository-Branches automatisch vorgeschlagen werden (ggf. nur eine Dropdown-Liste häufiger Branches oder freie Eingabe mit Validierung beim Start).
- **Plugin-Implementierungen:** GitHub-Plugin und andere Implementierungen von `IGitPlugin` müssen die neuen Parameter (`sourceBranchName`, `baseBranch`) verarbeiten. Alte Plugin-Implementierungen, die die neuen Parameter ignorieren, werden weiterhin funktionieren (Optional), aber Basis-Branch-Feature bleibt inaktiv.

## Umsetzungsreihenfolge

1. **Datenmodell erweitern: `GitRepository.DefaultSourceBranchName` hinzufügen**
   - Voraussetzungen: Keine
   - Beschreibung: Eigenschaft `DefaultSourceBranchName: string?` in `GitRepository` hinzufügen (Property, keine DB noch)

2. **Datenbankmigrationen erstellen und anwenden**
   - Voraussetzungen: `GitRepository.DefaultSourceBranchName`-Eigenschaft existiert
   - Beschreibung: EF Core Migration `AddDefaultSourceBranchNameToGitRepository` erstellen, Spalte in `git_repository` hinzufügen

3. **IGitPlugin-Interface erweitern**
   - Voraussetzungen: Keine (Contracts-Projekt, Plugin-Infrastruktur existiert)
   - Beschreibung: 
     - `CreateBranchAsync()` um optionalen Parameter `sourceBranchName` erweitern
     - `CreatePullRequestAsync()` um optionalen Parameter `baseBranch` erweitern

4. **GitHub-Plugin-Implementierung anpassen**
   - Voraussetzungen: `IGitPlugin`-Interface erweitert
   - Beschreibung:
     - `CreateBranchAsync()` um Logik zur Nutzung von `sourceBranchName` erweitern (wenn gesetzt: `git checkout -b <branchName> <remote>/<sourceBranchName>`)
     - `CreatePullRequestAsync()` um Logik zur Nutzung von `baseBranch` erweitern (wenn gesetzt: GitHub API `base` auf `baseBranch` setzen)

5. **Validierungslogik in `EntwicklungsprozessService` implementieren**
   - Voraussetzungen: `GitRepository.DefaultSourceBranchName` existiert, `IGitPlugin`-Interface erweitert
   - Beschreibung:
     - Neue private Methode `ValidateBaseBranchExistsAsync(gitRepository, gitPlugin)` erstellen
     - In `ProzessStartenAsync()` aufrufen, bevor Repository geklont wird
     - Bei Fehler: `GitBranchNotFoundException` werfen mit aussagekräftiger Meldung (z.B. "Branch 'staging' existiert nicht im Repository")

6. **`EntwicklungsprozessService.SetupBranchAsync()` anpassen**
   - Voraussetzungen: `GitRepository.DefaultSourceBranchName` existiert, `IGitPlugin` erweitert
   - Beschreibung:
     - `DefaultSourceBranchName` aus `gitRepository` auslesen
     - Logik anpassen: Wenn `DefaultSourceBranchName` vorhanden und nicht null:
       - `CheckoutRemoteBranchAsync(remoteBaseBranch)` aufrufen
       - `CreateBranchAsync()` mit `sourceBranchName = DefaultSourceBranchName` aufrufen
     - Sonst: Bestehendes Verhalten (Feature-Branch vom HEAD)

7. **`GitOrchestrationService.PullRequestErstellenAsync()` anpassen**
   - Voraussetzungen: `GitRepository.DefaultSourceBranchName` existiert, `IGitPlugin` erweitert
   - Beschreibung:
     - `DefaultSourceBranchName` aus `gitRepository` auslesen
     - `IGitPlugin.CreatePullRequestAsync()` mit `baseBranch = DefaultSourceBranchName` aufrufen (kann null sein)

8. **Unit-Tests für Validierungslogik schreiben**
   - Voraussetzungen: Validierungslogik in `EntwicklungsprozessService` implementiert
   - Beschreibung:
     - Test: Aufgabenstart schlägt fehl, wenn `DefaultSourceBranchName` nicht im Remote existiert
     - Test: Aufgabenstart erfolgreich, wenn `DefaultSourceBranchName` im Remote existiert
     - Test: Aufgabenstart erfolgreich, wenn `DefaultSourceBranchName` null (Fallback auf Standard-Branch)

9. **Unit-Tests für Feature-Branch-Erstellung vom Basis-Branch schreiben**
   - Voraussetzungen: `SetupBranchAsync()` angepasst
   - Beschreibung:
     - Test: Feature-Branch wird vom Basis-Branch abgezweigt, wenn `DefaultSourceBranchName` gesetzt
     - Test: Feature-Branch wird vom HEAD abgezweigt, wenn `DefaultSourceBranchName` null

10. **Unit-Tests für PR-Erstellung mit Basis-Branch schreiben**
    - Voraussetzungen: `PullRequestErstellenAsync()` angepasst
    - Beschreibung:
      - Test: PR wird mit `baseBranch = DefaultSourceBranchName` erstellt, wenn konfiguriert
      - Test: PR wird mit `baseBranch = null` erstellt, wenn nicht konfiguriert

11. **Integrationstests für DB-Persistierung schreiben**
    - Voraussetzungen: Migration angewendet, Eigenschaft in `GitRepository` vorhanden
    - Beschreibung:
      - Test: `DefaultSourceBranchName` wird in DB gespeichert und kann gelesen werden
      - Test: Existierende `GitRepository`-Einträge haben `DefaultSourceBranchName = null` (Migration)

12. **E2E-Test: Repository-Zuordnung mit Basis-Branch-Auswahl**
    - Voraussetzungen: UI-Komponenten für Basis-Branch-Eingabe (siehe Schritt 13)
    - Beschreibung:
      - Test: Benutzer ordnet Repository zu Projekt zu und wählt Basis-Branch
      - Verifikation: `DefaultSourceBranchName` wird in DB gespeichert

13. **E2E-Test: Aufgabe starten mit Basis-Branch-Validierung**
    - Voraussetzungen: Validierungslogik, Feature-Branch-Erstellung angepasst
    - Beschreibung:
      - Test: Aufgabenstart mit existierendem Basis-Branch erfolgreich
      - Test: Aufgabenstart mit nicht-existierendem Basis-Branch zeigt Fehlermeldung
      - Verifikation: Feature-Branch wird vom Basis-Branch abgezweigt (Git-Verifikation)

14. **E2E-Test: PR-Erstellung mit Basis-Branch als Ziel**
    - Voraussetzungen: PR-Erstellung angepasst, E2E-Testinfrastruktur
    - Beschreibung:
      - Test: PR wird mit konfiguriertem Basis-Branch als Ziel erstellt
      - Verifikation: PR im GitHub zeigt korrekten Ziel-Branch

15. **Betroffene bestehende Tests anpassen**
    - Voraussetzungen: Alle Logik-Änderungen abgeschlossen
    - Beschreibung:
      - Prüfe `EntwicklungsprozessServiceTests` und `GitOrchestrationServiceTests` auf Auswirkungen
      - Anpassungen vornehmen, wenn Signaturen oder Verhalten sich geändert haben

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `ProzessStartenAsync_ShouldThrow_WhenBaseBranchDoesNotExist` | `EntwicklungsprozessServiceTests` | Aufgabenstart schlägt fehl, wenn konfigurierter Basis-Branch nicht im Remote existiert |
| `ProzessStartenAsync_ShouldSucceed_WhenBaseBranchExists` | `EntwicklungsprozessServiceTests` | Aufgabenstart erfolgreich, wenn konfigurierter Basis-Branch im Remote existiert |
| `ProzessStartenAsync_ShouldSucceed_WhenNoBranchConfigured` | `EntwicklungsprozessServiceTests` | Aufgabenstart erfolgreich mit Fallback auf Standard-Branch, wenn kein Basis-Branch konfiguriert |
| `SetupBranchAsync_ShouldCreateBranchFromBaseBranch_WhenConfigured` | `EntwicklungsprozessServiceTests` | Feature-Branch wird vom konfigurierten Basis-Branch abgezweigt |
| `SetupBranchAsync_ShouldCreateBranchFromHead_WhenNotConfigured` | `EntwicklungsprozessServiceTests` | Feature-Branch wird vom HEAD abgezweigt, wenn kein Basis-Branch konfiguriert |
| `PullRequestErstellenAsync_ShouldCallPluginWithBaseBranch_WhenConfigured` | `GitOrchestrationServiceTests` | PR-Erstellung übergibt konfigurierten Basis-Branch an Plugin |
| `PullRequestErstellenAsync_ShouldCallPluginWithoutBaseBranch_WhenNotConfigured` | `GitOrchestrationServiceTests` | PR-Erstellung übergibt `baseBranch = null`, wenn nicht konfiguriert |
| `DefaultSourceBranchName_ShouldBePersisted` | `EntwicklungsprozessServiceTests` (Integration) | `DefaultSourceBranchName` wird in DB gespeichert und kann gelesen werden |
| E2E: Repo-Zuordnung mit Basis-Branch-Auswahl | `E2E_RepositoryManagementTests` (TBD) | Benutzer wählt Basis-Branch bei Repository-Zuordnung, wird in DB gespeichert |
| E2E: Aufgabenstart mit erfolgreicher Validierung | `E2E_TaskStartupTests` (TBD) | Aufgabe startet mit existierendem Basis-Branch, Feature-Branch wird korrekt abgezweigt |
| E2E: Aufgabenstart mit Validierungsfehler | `E2E_TaskStartupTests` (TBD) | Aufgabenstart zeigt Fehlermeldung, wenn Basis-Branch nicht existiert |
| E2E: PR-Erstellung mit Basis-Branch | `E2E_PullRequestTests` (TBD) | PR wird mit konfiguriertem Basis-Branch als Ziel erstellt |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `EntwicklungsprozessServiceTests` (alle) | Möglicherweise anpassen, wenn `SetupBranchAsync()`-Signatur sich ändert (neue optionale Parameter) |
| `GitOrchestrationServiceTests` (alle) | Möglicherweise anpassen, wenn `PullRequestErstellenAsync()`-Signatur sich ändert |
| `EntwicklungsprozessServiceTests_WorkingDirectoryValidation` (alle) | Überprüfen, ob Änderungen im Aufgabenstart-Flow diese Tests beeinflussen |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Repository-Zuordnung mit Basis-Branch-Auswahl | `E2E_RepositoryManagementTests` (neu anzulegen) | Benutzer kann Basis-Branch konfigurieren, wird persistent gespeichert |
| Aufgabenstart mit Basis-Branch-Validierung (Happy Path) | `E2E_TaskStartupTests` (neu anzulegen) | Aufgabe startet erfolgreich mit existierendem Basis-Branch, Feature-Branch vom richtigen Basis-Branch abgezweigt |
| Aufgabenstart mit Basis-Branch-Validierung (Error Case) | `E2E_TaskStartupTests` (neu anzulegen) | Aufgabenstart schlägt mit Fehlermeldung fehl, wenn Basis-Branch nicht existiert |
| PR-Erstellung mit konfiguriertem Basis-Branch | `E2E_PullRequestTests` (neu anzulegen oder erweitern) | PR wird mit korrektem Basis-Branch als Ziel-Branch erstellt (Verifikation via GitHub API) |

Welche bestehenden E2E-Tests müssen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| Existierende E2E-Tests für Aufgabenstart | Überprüfen, ob neue Validierungslogik Mock-GitRepository-Objekte mit `DefaultSourceBranchName = null` benötigt (sollte transparent sein, da Fallback auf Standard) |

Keine bekannten Breaking Changes erwartet, da neue Properties optional sind.

## Offene Punkte

Keine. Alle ursprünglichen 8 offenen Punkte aus requirement.md wurden wie folgt geklärt:

1. **Basis-Branch bei Gelöschung:** Lazy-Validierung beim Aufgabenstart wirft Fehler mit Meldung "Branch existiert nicht". Konfiguration wird nicht automatisch zurückgesetzt.
2. **Validierungszeitpunkt:** Lazy-Validierung beim Aufgabenstart (nicht beim Speichern), ermöglicht Szenarien mit später erstellten Branches.
3. **Auto-Ermittlung Default-Branch:** Nicht implementiert in dieser Phase; Benutzer wählt/gibt Basis-Branch manuell ein.
4. **Autocomplete / Branch-Liste:** UI-Detail; ggf. freie Eingabe mit Validierung beim Start.
5. **Abwärtskompatibilität:** Fallback auf Remote-Standard-Branch via `GetDefaultBranchAsync()`.
6. **PR-Verhalten mit Custom-Branch:** Wird beobachtet; keine speziellen Maßnahmen nötig (Merge-Konflikte sind GitHub-Verantwortung).
7. **Fehlerbehandlung Workflows:** Klare Fehlermeldung beim Aufgabenstart, Navigationshinweise optional (UI-Design).
8. **Multi-Branch-Strategie:** Zukünftige Anforderung, nicht jetzt; Plan bleibt 1:1 Basis-Branch pro Repository.
