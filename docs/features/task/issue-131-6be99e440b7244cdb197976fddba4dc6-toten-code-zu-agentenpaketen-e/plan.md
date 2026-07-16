# Umsetzungsplan: Toten Code zu Agentenpaketen entfernen

## Übersicht

Der aus Dokumentation und Architektur bereits entfernte Feature-Code „Agentenpakete" (Interfaces, ValueObjects, Services und deren Tests) wird vollständig aus dem Repository entfernt, um Code und Dokumentation zu synchronisieren. Betroffen sind der Domain- und Infrastructure-Layer von `Softwareschmiede` sowie die Testprojekte `Softwareschmiede.Tests` und `Softwareschmiede.IntegrationTests`. Zusätzlich wird die `README.md` bereinigt (Architektur-Diagramm und Test-Verweis). Es handelt sich um eine reine Löschung/Bereinigung ohne neue Funktionalität.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| `FileTreeNode` (`src/Softwareschmiede/Domain/ValueObjects/FileTreeNode.cs`) | **Ebenfalls löschen** (über die in der Anforderung genannten 7 Dateien hinaus) | Verifiziert per Grep: `FileTreeNode` wird ausschließlich von `IAgentPackageFileService` und `AgentPackageFileService` referenziert. Nach deren Entfernung ist es verwaistes ValueObject; sein XML-Kommentar beschreibt es explizit als „Knoten im Dateibaum eines Agentenpakets". Es gehört fachlich zum toten Feature und wird toter Code, sobald die 7 Dateien entfernt sind. |
| `docs/help/plugins/api.md` (`GetAvailableAgentsAsync`, `IsAgentPackageCompatibleAsync`, `DeployAgentPackageAsync`) | **Unverändert lassen** | Diese Methoden gehören zur weiterhin aktiven Schnittstelle `IKiPlugin : IPlugin` (Parameter `agentPackagePath`), nicht zum gelöschten `IAgentPackageService`/`AgentPackageFileService`. Die Textsuche nach `AgentPackage` trifft sie zwar, sie sind aber lebender Plugin-Vertragscode und dürfen nicht entfernt werden. |
| `.csproj`-Bereinigung | **Keine Änderung nötig** | Verifiziert: Die `.csproj`-Dateien enthalten keine expliziten `<Compile>`/`<None>`/`<EmbeddedResource>`-Einträge für die betroffenen Dateien (SDK-Style, Convention-basiertes Globbing). Das Löschen der Dateien genügt. |

## Programmabläufe

Keine neuen oder geänderten Laufzeit-Programmabläufe. Die zu entfernenden Typen sind nicht in `Program.cs` registriert und werden ausschließlich von ihren eigenen Testklassen instanziiert; nach der Löschung entfällt lediglich ungenutzter Code.

## Neue Klassen

Keine. Diese Anforderung entfernt ausschließlich Code.

## Änderungen an bestehenden Klassen

Keine Änderungen an bestehenden C#-Klassen. Es werden ausschließlich vollständige Dateien gelöscht. Die verbleibenden Typen (`AgentInfo`, `CliKiPluginBase`, `IKiPlugin`) bleiben unangetastet.

### Zu löschende Dateien (Code)

- `src/Softwareschmiede/Domain/Interfaces/IAgentPackageService.cs`
- `src/Softwareschmiede/Domain/Interfaces/IAgentPackageFileService.cs`
- `src/Softwareschmiede/Domain/ValueObjects/AgentPackageInfo.cs`
- `src/Softwareschmiede/Domain/ValueObjects/FileTreeNode.cs` *(verwaist nach Löschung; siehe Designentscheidung)*
- `src/Softwareschmiede/Infrastructure/Services/AgentPackageReader.cs`
- `src/Softwareschmiede/Infrastructure/Services/AgentPackageFileService.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Services/AgentPackageReaderTests.cs`
- `src/Softwareschmiede.IntegrationTests/Services/AgentPackageFileServiceTests.cs`

### Zu ändernde Dokumentationsdatei

- `README.md`:
  - Mermaid-Diagramm-Knoten `APL6["AgentPackageReader / IAgentPackageService"]` (Application Layer, Zeile ~390) entfernen.
  - Mermaid-Diagramm-Knoten `INL6["AgentPackageReader"]` (Infrastructure Layer, Zeile ~406) entfernen.
  - Test-Verweis-Zeile `- AgentPackageReader I/O-Fallback: src/Softwareschmiede.Tests/Infrastructure/Services/AgentPackageReaderTests.cs` (Zeile ~502) entfernen.
  - Keine Diagramm-Kanten (`-->`) referenzieren `APL6`/`INL6` direkt (die Kanten verlaufen zwischen Subgraphen), daher sind keine weiteren Kanten-Anpassungen nötig.

### Ausdrücklich NICHT zu löschen / ändern

- `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/AgentInfo.cs` — weiterhin von `CliKiPluginBase` und `IKiPlugin` genutzt.
- `src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/CliKiPluginBase.cs` — aktiv.
- `docs/help/plugins/api.md` — beschreibt die lebende `IKiPlugin`-Schnittstelle.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine.

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **Build-Integrität:** Da keiner der zu löschenden Typen außerhalb der eigenen Testklassen referenziert wird (per Grep verifiziert), sollte der Build unverändert grün bleiben. Das Löschen von `FileTreeNode` ist zusätzlich abzusichern, da es nicht Teil der ursprünglichen 7-Dateien-Liste war — nach Löschung Build ausführen, um sicherzustellen, dass kein verbleibender Referenzpunkt (z. B. XAML, ViewModel) existiert.
- **Dokumentationskonsistenz:** Nach der README-Bereinigung ist per erneuter Grep-Suche sicherzustellen, dass in `README.md` keine `AgentPackage`-Referenz mehr existiert. In `docs/**/*.md` verbleiben ausschließlich die legitimen `IKiPlugin`-Referenzen in `docs/help/plugins/api.md` (bewusst erhalten) sowie die Feature-Arbeitsdokumente unter `docs/features/task/issue-131-.../` (Arbeitsartefakte, keine Produktdokumentation).
- **Self-Hosting-Risiko:** Für den Verifikations-Build gilt die Projektregel — bei einem MSB3027/MSB3026-Kopierfehler durch eine gesperrte `Softwareschmiede.App.exe`-DLL niemals eigenständig den Prozess beenden, sondern den Anwender bitten oder auf einen Teil-Build (`Softwareschmiede.csproj`) ausweichen.

## Umsetzungsreihenfolge

1. **Referenzen final verifizieren**
   - Voraussetzungen: Keine.
   - Beschreibung: Per Grep bestätigen, dass `AgentPackageInfo`, `IAgentPackageService`, `IAgentPackageFileService`, `AgentPackageReader`, `AgentPackageFileService` und `FileTreeNode` außerhalb der 8 Löschkandidaten nicht referenziert werden und keine DI-Registrierung in `Program.cs` besteht. (Bereits im Rahmen der Planung durchgeführt und bestätigt; vor der Löschung erneut ausführen.)

2. **Acht Code-Dateien löschen**
   - Voraussetzungen: Schritt 1 abgeschlossen.
   - Beschreibung: Die sieben in der Anforderung genannten Dateien plus `FileTreeNode.cs` aus dem Dateisystem entfernen.

3. **README.md bereinigen**
   - Voraussetzungen: Schritt 2 abgeschlossen (damit Code und Doku im selben logischen Schritt synchron sind).
   - Beschreibung: Mermaid-Knoten `APL6` und `INL6` sowie die `AgentPackageReader`-Test-Verweiszeile entfernen.

4. **Doku-Gegenprüfung**
   - Voraussetzungen: Schritt 3 abgeschlossen.
   - Beschreibung: Erneute Grep-Suche nach `AgentPackage` in `README.md` und `docs/**/*.md`. Sicherstellen, dass nur die bewusst erhaltenen `IKiPlugin`-Einträge in `docs/help/plugins/api.md` und die Feature-Arbeitsdokumente verbleiben.

5. **Build verifizieren**
   - Voraussetzungen: Schritte 2–4 abgeschlossen.
   - Beschreibung: Vollständigen `dotnet build` ausführen (kein `--no-build` bei Folgetests). Bei App-DLL-Lock: Projektregel beachten (Teil-Build auf `src/Softwareschmiede/Softwareschmiede.csproj` ausweichen, Anwender informieren).

6. **Tests verifizieren**
   - Voraussetzungen: Schritt 5 erfolgreich.
   - Beschreibung: `dotnet test` synchron (nie im Hintergrund) mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` ausführen, um sicherzustellen, dass keine verdeckte Abhängigkeit übersehen wurde und die verbliebene Testsuite grün ist.

## Tests

### Neue Tests

Keine. Es wird ausschließlich Code entfernt; für gelöschte Funktionalität werden keine neuen Tests benötigt.

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| — | — | Keine neuen Tests erforderlich |

### Betroffene bestehende Tests

Die beiden Testklassen des Features werden vollständig gelöscht (nicht angepasst). Keine weiteren bestehenden Tests referenzieren die entfernten Typen.

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `AgentPackageReaderTests` (`src/Softwareschmiede.Tests/Infrastructure/Services/AgentPackageReaderTests.cs`) | Wird gelöscht — testet den entfernten `AgentPackageReader`. |
| `AgentPackageFileServiceTests` (`src/Softwareschmiede.IntegrationTests/Services/AgentPackageFileServiceTests.cs`) | Wird gelöscht — testet den entfernten `AgentPackageFileService`. |

### E2E-Tests (Pflicht)

Keine. Das entfernte Feature besitzt keine Benutzeroberfläche und keinen Benutzerablauf; es existiert kein Happy Path, der durch einen E2E-Test abzudecken wäre. Es werden weder neue E2E-Tests benötigt noch bestehende E2E-Tests berührt.

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| — | — | Kein Benutzerablauf betroffen |

Bestehende E2E-Tests: Keine betroffen.

## Offene Punkte

Die in der Anforderung genannten Punkte (historische Archivierung, Git-History, README-Commit-Zeitpunkt) sind organisatorische Fragen des `/implement`- bzw. `/lifecycle`-Workflows und werden hier mit Empfehlung geklärt:

| # | Offener Punkt | Empfohlener Vorschlag |
|---|---------------|----------------------|
| 1 | Historische Archivierung der gelöschten Dateien | Nicht erforderlich — die Git-History bewahrt den vollständigen Stand; ein separates Archivverzeichnis würde erneut toten Code einführen. |
| 2 | Commit-Struktur (Code-Löschung vs. README) | Code-Löschung, `FileTreeNode`-Entfernung und README-Bereinigung in einem logischen Commit zusammenfassen (z. B. „refactor: remove dead agent package code and sync README"), da sie eine einzige Synchronisierungsänderung bilden. Die konkrete Commit-Erstellung übernimmt der Lifecycle-Workflow. |
