# Plan-Review: Issue-Anlage aus der Aufgabendetailansicht

Status: **Offene Aufgaben vorhanden**

## Ergebnis

Die aktuelle Implementierung deckt den wesentlichen Issue-Anlagepfad ab: Create-/Template-/KI-Verträge, GitHub- und Jira-Anlage, Dialog, Capability-Prüfung, Fehlerbehandlung, getrennte Persistenz sowie Ribbon-Aktion sind vorhanden. Die archivierten Code-Befunde zur KI-Ausfüllhilfe und zu GitHub-Templates sind behoben.

Der zuletzt offene atomare Zuordnungsbefund ist behoben. `TaskDetailViewModel` verwendet nach erfolgreicher Provider-Anlage `TryAssignIssueReferenzIfNoneAsync` statt der überschreibenden Update-Methode. Die lokale 1:1-Beziehung besitzt einen eindeutigen Index auf `IssueReferenz.AufgabeId`; ein konkurrierender Insert führt daher nicht zu einem Überschreiben. Der Service behandelt den konkurrierenden Persistenzfehler als fehlgeschlagene Zuordnung. Ein Integrationstest prüft, dass eine zwischenzeitlich gesetzte Referenz erhalten bleibt.

## Abgedeckte Planelemente

- Providerunabhängige Create-, Result-, Template- und KI-Verträge mit `CancellationToken` sowie Default-Nichtunterstützung sind vorhanden.
- GitHub erstellt Issues mit `owner/repo`, lädt ausschließlich Markdown-Templates und ignoriert YAML-Issue-Forms sowie `config.yml`.
- Jira verwendet Projekt, Summary, Issue-Typ und ADF-Beschreibung; Providerfehler werden als fehlgeschlagene Ergebnisse zurückgegeben.
- Der Dialog übernimmt Titel und Originalanforderung, unterstützt editierbare Template-Komposition, KI-Auswahl, Abbruch, Fehleranzeige und Submit-Zustände.
- Vor Öffnen und Absenden wird eine bestehende Zuordnung geprüft. Provideranlage erfolgt vor lokaler Persistenz; nach Erfolg wird die Detailansicht neu geladen.
- Die neue Ribbon-Aktion bleibt von der bestehenden Issue-Zuweisung getrennt und berücksichtigt Providerfähigkeit, Repository und bestehende Referenz.
- Tests für Capability, LocalDirectory-Nichtunterstützung, Template-Komposition/-fehler, KI-Fehler, Abbruch, Persistenz und den atomaren Zuordnungsfall sind vorhanden.

## Offene Aufgaben

1. Contract-/Base- und Provider-Tests für Cancellation, vollständige Erfolgsantworten, Pflichtfeld-/Konfigurationsvalidierung sowie weitere Authentifizierungs- und Netzwerkfehler vervollständigen.
2. `IssueCreateDialogViewModelTests` um Provider-Create-Fehler, Cancellation während Template-Laden/KI/Submit und doppelte Submit-Vorgänge erweitern.
3. `TaskDetailViewModelTests` um nicht unterstützten Provider, Providerfehler, Persistenzfehler mit URL/Nummer sowie laufende bzw. doppelte Create-Aktionen vervollständigen.
4. Die elf Abnahmekriterien aus `requirement.md` einzeln auf Tests oder nachvollziehbare UI-/Provider-Verifikation zurückführen.

## Verifikation

Ausgeführt:

```text
dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --no-build --nologo --filter "FullyQualifiedName~IssueCreateDialogViewModelTests|FullyQualifiedName~TaskDetailViewModelTests|FullyQualifiedName~GitPluginBaseTests|FullyQualifiedName~GitHubPluginTests|FullyQualifiedName~BitbucketPluginTests|FullyQualifiedName~LocalDirectoryPluginTests|FullyQualifiedName~AufgabeServiceTests"
```

Ergebnis: **266 erfolgreich, 0 fehlgeschlagen, 0 übersprungen**.

Die breite Verifikation aus `test-results.md` ist ebenfalls erfolgreich dokumentiert.
