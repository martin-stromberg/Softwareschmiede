# Plan-Review: Issue-Anlage aus der Aufgabendetailansicht

Status: **Offene Aufgaben vorhanden**

## Ergebnis

Die uncommitted Änderungen setzen den wesentlichen Ablauf um: neue Create-/Template-/KI-Verträge, GitHub- und Jira-Create-Pfade, den Issue-Anlage-Dialog, die eigene Capability-Prüfung, die Persistenzreihenfolge sowie die Ribbon-Aktion sind vorhanden. Die fokussierten Tests kompilieren und bestehen (171 Tests, 0 Fehler).

Die Planabdeckung ist jedoch noch nicht vollständig nachgewiesen. Insbesondere fehlen mehrere ausdrücklich geforderte Fehler-, Abbruch-, Cancellation-, Persistenz- und Integrationsfälle.

## Abgedeckte Planelemente

- Providerunabhängige Create-/Result-/Template-Modelle und optionale Capability-Interfaces mit `CancellationToken` sind vorhanden.
- `GitPluginBase` liefert Default-Nichtunterstützung; GitHub verwendet `owner/repo`, Jira verwendet konfiguriertes Projekt, Issue-Typ und ADF-Beschreibung.
- GitHub-Templates werden geladen; fehlende Verzeichnisse sowie nicht lesbare Templates blockieren den No-Template-Pfad nicht.
- Der Dialog übernimmt Titel und ursprüngliche Anforderungsbeschreibung, unterstützt editierbare Template-Komposition, KI-Provider-Auswahl, Lade-/Submit-Zustände, Abbruch und Fehleranzeige.
- Vor dem Öffnen und vor dem Absenden wird eine bestehende Issue-Referenz geprüft. Provideranlage und anschließende lokale Persistenz sind getrennt; Persistenzfehler nennen die externe Referenz.
- Die bestehende Zuordnungsaktion bleibt separat. Die neue Ribbon-Aktion berücksichtigt Providerfähigkeit, Repository, laufende Operation und bestehende Referenz.
- Neue Dialog-, ViewModel- und Provider-Happy-Path-Tests sind vorhanden und erfolgreich.

## Offene Aufgaben

1. Contract-/Base-Tests für Default-Nichtunterstützung, Capability, Cancellation sowie Erfolg-versus-Fehler ergänzen.
2. GitHub- und Jira-Provider-Tests für Cancellation, Pflichtfeld-/Konfigurationsvalidierung, vollständiges Antwortmapping und weitere Authentifizierungs-/Netzwerkfehler ergänzen. Die Nichtunterstützung von `LocalDirectory` sollte ebenfalls explizit über den neuen Vertrag getestet werden.
3. `IssueCreateDialogViewModel` um Tests für Providerfehler, Template-Ladefehler, erneute Template-Auswahl mit erhaltener Originalanforderung, KI-Fehler, KI ohne verfügbaren Provider, Abbruch, Cancellation, Pflichtfeldzustände und doppelte Submit-Vorgänge erweitern.
4. `TaskDetailViewModelTests` um nicht unterstützten Provider, Providerfehler, fehlgeschlagene lokale Persistenz inklusive Fehlermeldung mit URL/Nummer, parallele Zuordnung nach dem Dialog sowie laufende/doppelte Create-Aktionen erweitern.
5. Einen integrationsnahen Test ergänzen, der die Reihenfolge „Provider-Create vor `UpdateIssueReferenzAsync`“ tatsächlich prüft. Die vorhandenen Tests liefern das angelegte Issue direkt aus dem Dialog zurück und prüfen diese Provider-/Persistenzgrenze daher nicht.
6. Die elf Abnahmekriterien aus `requirement.md` anhand der ergänzten Tests oder einer nachvollziehbaren UI-/Provider-Verifikation einzeln nachweisen.

## Verifikation

Ausgeführt:

`dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --no-restore --filter "FullyQualifiedName~IssueCreateDialogViewModelTests|FullyQualifiedName~TaskDetailViewModelTests|FullyQualifiedName~GitHubPluginTests|FullyQualifiedName~BitbucketPluginTests"`

Ergebnis: **171 erfolgreich, 0 fehlgeschlagen, 0 übersprungen**.
