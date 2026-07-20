# Plan-Review: Issue-Anlage aus der Aufgabendetailansicht

Status: **Offene Aufgaben vorhanden**

## Ergebnis

Die aktuellen uncommitted Änderungen schließen die beiden Befunde aus `review-code.1.md`: Codex und Claude CLI implementieren nun die rückgabefähige KI-Ausfüllhilfe, und GitHub berücksichtigt ausschließlich Markdown-Templates und filtert YAML-Issue-Forms sowie `config.yml` aus. Der fokussierte Testlauf war erfolgreich: **229 Tests bestanden, 0 fehlgeschlagen, 0 übersprungen**.

Die fachliche Umsetzung ist weitgehend vorhanden, aber die im Plan geforderte vollständige Absicherung der Fehler-, Cancellation-, Parallelitäts- und Integrationsfälle ist noch nicht nachgewiesen. Daher verbleiben offene Planelemente.

## Abgedeckte Planelemente

- Providerunabhängige Create-, Result-, Template- und KI-Verträge mit optionalen Fähigkeiten und `CancellationToken` sind vorhanden.
- GitHub- und Jira-Create-Pfade, Jira-ADF-Mapping sowie GitHub-Markdown-Templates sind umgesetzt.
- Der Issue-Create-Dialog unterstützt initiale Titel/Beschreibung, Template-Komposition, editierbare Inhalte, KI-Ausfüllung, Fehleranzeige, Abbruch und Lade-/Submit-Zustände.
- Die Aktion prüft Providerfähigkeit, vorhandene Zuordnung und parallele Zuordnung; Provideranlage und lokale Persistenz sind getrennt.
- Tests für Default-Nichtunterstützung, LocalDirectory-Nichtunterstützung, Template-Komposition, Template-Fehler, KI-Fehler, Abbruch und parallele Zuordnung sind vorhanden.

## Offene Aufgaben

1. Contract-/Base- und Provider-Tests für Cancellation, vollständige Erfolgsantworten, Pflichtfeld-/Konfigurationsvalidierung sowie weitere Authentifizierungs- und Netzwerkfehler ergänzen; insbesondere ist für den neuen Create-Vertrag kein durchgängiger Cancellation-Nachweis vorhanden.
2. `IssueCreateDialogViewModelTests` um Provider-Create-Fehler, Cancellation während Template-Laden/KI/Submit, doppeltes Submit und den vollständigen Abbruchpfad erweitern. Der aktuelle Stand testet Template- und KI-Fehler, aber keinen Provider-Fehler und keine Cancellation.
3. `TaskDetailViewModelTests` um nicht unterstützten Provider, Provider-Fehler, Persistenzfehler mit URL/Nummer sowie laufende bzw. doppelte Create-Aktionen ergänzen. Parallele Zuordnung und Abbruch sind abgedeckt, die übrigen Fehlerzustände nicht vollständig.
4. Einen integrationsnahen Test ergänzen, der den tatsächlichen Aufruf von Provider-Create vor `UpdateIssueReferenzAsync` prüft. Der vorhandene Erfolgstest prüft das Endergebnis, aber die Reihenfolge nicht explizit.
5. Die elf Abnahmekriterien aus `requirement.md` einzeln auf Test- oder nachvollziehbare UI-/Provider-Verifikation zurückführen; aktuell existiert kein vollständiger Kriteriennachweis als Test oder Review-Artefakt.

## Verifikation

Ausgeführt:

`dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~IssueCreateDialogViewModelTests|FullyQualifiedName~TaskDetailViewModelTests|FullyQualifiedName~GitHubPluginTests|FullyQualifiedName~BitbucketPluginTests|FullyQualifiedName~GitPluginBaseTests|FullyQualifiedName~LocalDirectoryPluginTests" --nologo`

Ergebnis: **229 erfolgreich, 0 fehlgeschlagen, 0 übersprungen**.
