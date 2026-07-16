# Anforderungsanalyse: Toten Code zu Agentenpaketen entfernen

**Aufgaben-ID:** 6be99e44-0b72-44cd-b197-976fddba4dc6  
**Branch:** task/issue-131-6be99e440b7244cdb197976fddba4dc6-toten-code-zu-agentenpaketen-e  
**Datum:** 2026-07-16

---

## Fachliche Zusammenfassung

Die Anwendung enthält Code-Artefakte zum Feature „Agentenpakete" (Interfaces `IAgentPackageService`, `IAgentPackageFileService`, ValueObject `AgentPackageInfo`, Implementierungen und Tests), das aus der Dokumentation (README.md, Architektur-Diagramm) bereits entfernt wurde. Diese Artefakte werden nicht registriert (kein DI-Eintrag in `Program.cs`), nicht aufgerufen (keine Referenz außerhalb ihrer Testklassen) und sind daher toter Code. Sie sollen vollständig aus dem Repository entfernt werden, um Code und Dokumentation zu synchronisieren.

---

## Betroffene Klassen und Komponenten

### Zu löschende Dateien

#### Interfaces (Domain Layer)
- `src/Softwareschmiede/Domain/Interfaces/IAgentPackageService.cs`  
  Interface für Agentenpaket-Abfragen (`GetPackagesAsync`, `GetPackageAsync`)
- `src/Softwareschmiede/Domain/Interfaces/IAgentPackageFileService.cs`  
  Interface für Dateiverwaltung in Agentenpaketen

#### ValueObjects (Domain Layer)
- `src/Softwareschmiede/Domain/ValueObjects/AgentPackageInfo.cs`  
  Record mit Eigenschaften: `Name`, `Pfad`, `Agenten` (Liste von `AgentInfo`), `Dateien`

#### Services (Infrastructure Layer)
- `src/Softwareschmiede/Infrastructure/Services/AgentPackageReader.cs`  
  Implementiert `IAgentPackageService`; liest Agentenpakete aus dem Dateisystem
- `src/Softwareschmiede/Infrastructure/Services/AgentPackageFileService.cs`  
  Implementiert `IAgentPackageFileService`

#### Tests
- `src/Softwareschmiede.Tests/Infrastructure/Services/AgentPackageReaderTests.cs`  
  Unit-Tests für `AgentPackageReader`
- `src/Softwareschmiede.IntegrationTests/Services/AgentPackageFileServiceTests.cs`  
  Integrationstests für `AgentPackageFileService`

### Nicht zu löschen

- **`src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/AgentInfo.cs`**  
  Dieses ValueObject wird noch in `CliKiPluginBase.cs` verwendet und ist nicht Teil des toten Codes.
- **`src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/CliKiPluginBase.cs`**  
  Verwendet `AgentInfo`, ist aber noch aktiv und wird nicht gelöscht.

---

## Implementierungsansatz

### 1. Bestandsaufnahme vor Deletion

- Überprüfen Sie mittels `grep`/`rg`, ob `AgentInfo` tatsächlich nur in `AgentPackageInfo` und in `CliKiPluginBase.cs` (wo es benötigt wird) verwendet wird.
- Prüfen Sie, ob die sieben Dateien von Projektdateien (`.csproj`) explizit included sind oder über Convention geladen werden.
- Bestätigen Sie mittels Search, dass keine weiteren versteckten Abhängigkeiten (Reflection, Dependency Injection Container-Registrierungen, XML-Konfiguration) auf diese Typen zeigen.

### 2. Code-Entfernung

- Löschen Sie die sieben identifizierten Dateien aus dem Dateisystem.
- Überprüfen Sie, ob `*.csproj`-Dateien einen expliziten `<ItemGroup><Compile ... />` oder ähnliches für gelöschte Dateien enthalten; falls ja, entfernen Sie diese Einträge.

### 3. Dokumentation aktualisieren (README.md)

Nach Code-Entfernung müssen in `README.md` folgende Abschnitte bereinigt werden:

**Abschnitt „Architektur" (Mermaid-Diagramm):**
- Knoten `APL6["AgentPackageReader / IAgentPackageService"]` (Application Layer) entfernen
- Knoten `INL6["AgentPackageReader"]` (Infrastructure Layer) entfernen
- Alle zugehörigen Pfeile/Referenzen zu diesen Knoten entfernen

**Abschnitt „Tests":**
- Zeile mit Verweis auf `AgentPackageReader I/O-Fallback: src/Softwareschmiede.Tests/Infrastructure/Services/AgentPackageReaderTests.cs` entfernen

**Globale Suche:**
- Nach `AgentPackage` in `README.md` und allen `docs/**/*.md`-Dateien suchen und sicherstellen, dass keine Referenz übrig bleibt.

### 4. Build und Tests validieren

- Vollständiger `dotnet build` durchführen (ggf. mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` für E2E-Tests)
- `dotnet test` ausführen, um sicherzustellen, dass keine verdeckte Abhängigkeit durch Reflection oder Konvention übersehen wurde

### 5. Prüfung der Dokumentation

- Suchen Sie nach Dokumentations-Seiten unter `docs/help/` oder `docs/features/`, die sich auf „Agentenpakete" beziehen
- Falls vorhanden: entfernen oder als „deprecated/historisch" markieren
- Cross-Check in `docs/help/index.md` und Navigation-Files, falls solche Referenzen dort existieren

---

## Abhängigkeiten und Annahmen

- **Annahme 1:** `AgentInfo` (in `Softwareschmiede.Plugin.Contracts`) ist nicht Teil des zu löschenden Codes und wird weiterhin benötigt → wird nicht gelöscht
- **Annahme 2:** Keine DI-Registrierung der zu löschenden Typen existiert in `Program.cs` oder Startup-Hooks → kann mit Grep verifiziert werden
- **Annahme 3:** Keine versteckten Abhängigkeiten durch Reflection/MEF/Plugin-Discovery → wird durch Build/Test-Verifikation bestätigt

---

## Offene Fragen

1. **Historische Archivierung:** Sollen die zu löschenden Dateien vor Löschung in `docs/` oder einem Archiv-Verzeichnis dokumentiert werden (z. B. zur Referenz für zukünftige Maintainer)?
2. **Git-History:** Ist es ausreichend, die Dateien einfach zu löschen, oder soll ein Commit mit Message wie „refactor: remove dead agent package code" erstellt werden?
3. **README-Zeitpunkt:** Soll die README-Aktualisierung im selben Commit wie die Code-Entfernung erfolgen, oder als separater Commit?

---

## Zusammengefasste Aufgaben

| # | Aufgabe | Verantwortung | Status |
|---|---------|--------------|--------|
| T1 | Glob-Suche nach weiteren `AgentPackage*`-Referenzen ausführen | Code-Review vor Löschung | ⏳ |
| T2 | Sieben identifizierte Dateien löschen | Implementierung | ⏳ |
| T3 | `.csproj`-Dateien bereinigen (falls nötig) | Implementierung | ⏳ |
| T4 | README.md: Mermaid-Diagramm-Knoten entfernen | Dokumentation | ⏳ |
| T5 | README.md: Test-Referenz entfernen | Dokumentation | ⏳ |
| T6 | README.md: Glob-Suche nach `AgentPackage`-Referenzen | Dokumentation | ⏳ |
| T7 | Dokumentation unter `docs/help/` prüfen | Dokumentation | ⏳ |
| T8 | Vollständiger Build durchführen | Verifikation | ⏳ |
| T9 | Testlauf durchführen | Verifikation | ⏳ |
