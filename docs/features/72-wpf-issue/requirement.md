# Anforderung 72: SCM-Issues in Aufgabenlisten integrieren

## Fachliche Zusammenfassung

Die Aufgabenliste in der Projektdetailansicht wird um eine Vorschlagsfunktion erweitert: zusätzlich zu den persistierten Aufgaben (`Aufgabe`) werden offene Issues aus dem SCM-Plugin angezeigt. Jedes Issue erhält den Status "Offene Anforderung" als Anzeigeelement. Klickt ein Anwender auf ein solches Issue-Vorschlag, wird nach Bestätigung automatisch eine neue persistierte `Aufgabe` mit Status `Neu` erstellt, deren Beschreibung das Issue-`Body` übernimmt und die automatisch dem Issue über eine `IssueReferenz` zugewiesen ist. In der Aufgabendetailansicht werden neue Ribbon-Aktionsbuttons hinzugefügt, um Issues einem Task zuzuweisen, die Zuweisung zu ändern oder das Issue direkt im Browser zu öffnen. Diese Buttons werden nur angezeigt, wenn das SCM-Plugin Issues unterstützt (z.B. GitHub Plugin, nicht LocalDirectory Plugin).

## Betroffene Klassen und Komponenten

### Datenmodellklassen
- **`Aufgabe`** (bestehend): 
  - Die Navigation `IssueReferenz` wird bereits unterstützt.
  - Keine neuen Eigenschaften erforderlich.

- **`IssueReferenz`** (bestehend):
  - Enthält bereits alle erforderlichen Felder: `IssueNummer`, `Titel`, `Body`, `LabelsJson`, `Milestone`, `IssueUrl`.
  - Keine Änderungen erforderlich.

- **`Issue`** (bestehend, Value Object):
  - Record mit: `Nummer`, `Titel`, `Body`, `Labels`, `Milestone`, `IssueUrl`.
  - Bereits implementiert, keine Änderungen erforderlich.

### ViewModels
- **`ProjectDetailViewModel`**:
  - Neue Eigenschaft: `ObservableCollection<object> IssueVorschlaege` (oder `List<Issue>` + kombiniert mit `Aufgaben` für die UI).
  - Neue Eigenschaft: `bool IsLoadingIssues` (Lade-Status für Issues).
  - Neue Methode: `LadenIssuesAsync(CancellationToken ct)` — lädt Issues vom aktuellen SCM-Plugin.
  - Neues Event/Command: `AufgabeAusIssueErstellenCommand` — Bestätigung + Erstellen einer `Aufgabe` aus einem `Issue`.
  - Neue Eigenschaft: `bool KannIssuesLaden` — `true` wenn das Repository ein SCM-Plugin mit `GetIssuesAsync` unterstützt.

- **`TaskDetailViewModel`** (bestehend):
  - Neue Eigenschaft: `bool CanAssignIssue` — `true` wenn Aufgabe nicht persistiert ist oder ein Issue-Support vorhanden ist.
  - Neue Eigenschaft: `IssueReferenz? CurrentIssueReferenz` — aktuelle Issue-Zuweisung (read-only aus `Aufgabe.IssueReferenz`).
  - Neue Methode: `IssueZuweisenAsync()` — öffnet Dialog zur Issue-Auswahl.
  - Neue Methode: `IssueZuweisungAendernAsync()` — ändert bestehende Issue-Zuweisung.
  - Neues Command: `IssueZuweisenCommand`.
  - Neues Command: `IssueBrowserOeffnenCommand`.

### Services
- **`AufgabeService`** (bestehend, erweitern):
  - Neue Methode: `CreateAufgabeFromIssueAsync(Guid projektId, Issue issue, Guid? repositoryId, CancellationToken ct)` — erstellt persistierte Aufgabe mit `IssueReferenz`.

- **`IGitPlugin`** (bestehend):
  - Methode `GetIssuesAsync(string repositoryId, CancellationToken ct)` ist bereits im Interface definiert.
  - Plugins müssen diese implementieren (GitHub: lädt offene Issues, LocalDirectory: würde `NotSupportedException` werfen oder leere Liste).

### UI-Komponenten / Dialogs
- **`ProjectDetailView.xaml` (bestehend)**:
  - Aufgabenliste wird erweitert um Issue-Vorschläge.
  - Diese können als separate Listenelemente angezeigt werden (z.B. mit anderem visuellen Stil oder Icon).
  - Doppelklick oder Bestätigungsdialog triggert `AufgabeAusIssueErstellenCommand`.

- **`TaskDetailView.xaml` (bestehend)**:
  - Ribbon-Menü wird um neue Buttons erweitert:
    - **„Issue zuweisen"** Button (neue Gruppe oder zu bestehender Gruppe).
    - **„Issue öffnen"** Button (öffnet `IssueUrl` im Standard-Browser).
  - Diese Buttons sind sichtbar nur wenn `CanAssignIssue` und das SCM-Plugin Issues unterstützt.

- **`IssueSelectionDialog.xaml` / `IssueSelectionDialogViewModel`** (neu):
  - Dialog zur Auswahl eines Issues aus der Liste verfügbarer Issues.
  - Filtermöglichkeit nach Issue-Nummer oder Titel (optional).
  - OK- und Abbrechen-Buttons.

### Interfaces
- Keine neuen Interfaces erforderlich; `IGitPlugin.GetIssuesAsync` ist bereits definiert.

### Enums
- Keine neuen Enums erforderlich.

### Tests
- **`AufgabeServiceTests`** (erweitern):
  - `CreateAufgabeFromIssueAsync_Success_CreatesTaskWithIssueReference`.
  - `CreateAufgabeFromIssueAsync_CreatesTaskWithCorrectTitle`.
  - `CreateAufgabeFromIssueAsync_CreatesTaskWithIssueBodyAsDescription`.

- **`ProjectDetailViewModelTests`** (erweitern):
  - `LadenIssuesAsync_LoadsIssuesFromPlugin`.
  - `LadenIssuesAsync_FilteredOutIfPluginDoesNotSupport`.
  - `AufgabeAusIssueErstellenAsync_Success_CreatesAufgabeAndRefreshesUI`.

- **`TaskDetailViewModelTests`** (erweitern):
  - `IssueZuweisenAsync_ShowsDialogAndAssigns`.
  - `IssueBrowserOeffnenAsync_OpensUrlInBrowser`.

- **E2E-Tests** (neu/erweitert):
  - `IssueAusAufgabenliste_KlickOeffnetBestaetigung_E2E`.
  - `IssueBestaetigung_ErzeugtAufgabe_E2E`.
  - `IssueZuweisenImTask_DialogZeigtIssues_E2E`.
  - `LocalDirectoryPlugin_ZeigtKeineIssueButtons_E2E`.

## Implementierungsansatz

### Schritt 1: Issue-Laden in ProjectDetailViewModel
- Nach dem Laden der `Aufgaben` im `LadenAsync` wird geprüft, ob das gewählte Repository ein SCM-Plugin hat.
- Wenn ja, wird `_gitPlugin.GetIssuesAsync(repositoryId, ct)` aufgerufen.
- Geladene Issues werden in `IssueVorschlaege` gespeichert.
- Bei Fehler wird `IsLoadingIssues` zurückgesetzt, ggf. Fehler geloggt, aber nicht in der UI gezeigt (Grace-Degradation).

### Schritt 2: UI-Integration in ProjectDetailView
- Die Aufgabenliste wird in zwei Sektionen aufgeteilt oder zusammengeführt (je nach Design):
  - **Persistierte Aufgaben**: normale Darstellung (Titel, Status).
  - **Issue-Vorschläge**: mit Label „Offene Anforderung", anderem Icon/Design.
- Ein Klick auf einen Issue-Vorschlag triggert ein Bestätigungsdialog:
  - Text: „Issue ‚{Titel}' als Aufgabe erstellen?".
  - OK/Abbrechen.
- Bei OK wird `AufgabeAusIssueErstellenCommand` ausgeführt.

### Schritt 3: Aufgabe aus Issue erstellen
- `AufgabeService.CreateAufgabeFromIssueAsync` wird aufgerufen:
  - Neue `Aufgabe` mit `Titel` = Issue.`Titel`.
  - `AnforderungsBeschreibung` = Issue.`Body`.
  - `Status` = `AufgabeStatus.Neu`.
  - Eine neue `IssueReferenz` wird erstellt und an die Aufgabe gehängt:
    - `IssueNummer` = Issue.`Nummer`.
    - `Titel` = Issue.`Titel`.
    - `Body` = Issue.`Body`.
    - `LabelsJson` = serialisierte `Labels`.
    - `Milestone` = Issue.`Milestone`.
    - `IssueUrl` = Issue.`IssueUrl`.
  - Task wird in der Datenbank gespeichert.
- Nach Erfolg wird `IssueVorschlaege` aktualisiert (Issue entfernt oder Hinweis „bereits erstellt").

### Schritt 4: Ribbon-Buttons in TaskDetailView
- **„Issue zuweisen"** Button:
  - Sichtbar wenn: `Aufgabe != null && !IsCliRunning && ScmPlugin.SupportsGetIssues()`.
  - Klick öffnet `IssueSelectionDialog`.
  - Dialog lädt Issues via `_gitPlugin.GetIssuesAsync(…)`.
  - Bei Auswahl wird `Aufgabe.IssueReferenz` neu gesetzt und gespeichert.
  - Callback triggert Property-Changes in TaskDetailViewModel.

- **„Issue öffnen"** Button:
  - Sichtbar wenn: `Aufgabe?.IssueReferenz?.IssueUrl != null`.
  - Klick öffnet `IssueUrl` im Standard-Browser via `Process.Start(url)`.

### Schritt 5: Plugin-Kompatibilität
- `IGitPlugin.GetIssuesAsync` wird von GitHub Plugin implementiert (Aufruf der GitHub API).
- LocalDirectory Plugin erbt von `IGitPlugin` oder implementiert diese Methode als `throw new NotImplementedException()` oder `return Task.FromResult(Enumerable.Empty<Issue>())`.
- Im ViewModel wird vor Aufruf geprüft: `if (scmPlugin is IGitPlugin gitPlugin) { ... }` — nur dann Issues laden.
- Buttons werden nur angezeigt wenn `gitPlugin.GetIssuesAsync` erfolgreich aufgerufen werden kann.

### Abhängigkeiten
- `AufgabeService` abhängig von: `SoftwareschmiededDbContext` (bereits vorhanden).
- `ProjectDetailViewModel` abhängig von: `AufgabeService`, `IGitPlugin`, Repository-Plugin-Resolver (bereits vorhanden).
- `TaskDetailViewModel` abhängig von: `AufgabeService`, `IGitPlugin`, Dialog-Service.
- `IssueSelectionDialog` abhängig von: Dialog-Service, Plugin-Service.

## Konfiguration

- **Keine neue Konfiguration erforderlich.**
- Die Sichtbarkeit von Issue-Buttons ist dynamisch an den Repository-Typ gebunden:
  - GitHub Plugin: Buttons aktiv.
  - LocalDirectory Plugin: Buttons inaktiv.
  - Andere Plugins: Buttons aktiv/inaktiv je nach `GetIssuesAsync`-Implementierung.
- Die Lade-Strategie für Issues könnte zukünftig konfigurierbar sein (z.B. nur geschlossene Issues, nur Labels), ist aber nicht Teil dieser Anforderung.

## Offene Fragen

1. **Visual Präsentation**: Sollen Issue-Vorschläge und persistierte Aufgaben in der Aufgabenliste gemischt oder getrennt angezeigt werden? Welches Icon/Label für „Offene Anforderung"?

2. **Issue-Filterung**: Werden nur offene Issues geladen oder auch geschlossene? Die Anforderung sagt „offene Issues" — sollte `GetIssuesAsync` bereits auf GitHub-Seite gefiltert werden?

3. **Caching / Aktualisierung**: Werden Issues beim Laden der Projektdetailansicht einmalig geladen oder bei jeder Aktualisierung neu abgerufen? Gibt es einen Refresh-Button?

4. **Fehlerbehandlung**: Falls `GetIssuesAsync` fehlschlägt (z.B. API-Rate-Limit, Netzwerkfehler), sollen Benutzer benachrichtigt werden oder stumm degraded?

5. **Issue-Doppel**: Wenn ein Issue bereits in eine Aufgabe konvertiert wurde (via `IssueReferenz`), soll das Issue in der Vorschlagsliste ausgeblendet werden oder mit Badge „bereits erstellt" gekennzeichnet sein?

6. **TaskDetailViewModel — Issue-Dialog**: Soll der Issue-Auswahl-Dialog auch die Möglichkeit bieten, die Zuweisung zu löschen (Issue-Referenz entfernen)? Oder gibt es dafür einen separaten Button „Issue entfernen"?

7. **Namenskonvention**: Der Dialog heißt `IssueSelectionDialog` oder `IssueZuweisenDialog`? ViewModel-Klasse: `IssueSelectionDialogViewModel` oder `IssueAssignmentViewModel`?

8. **GitHub Plugin — Filter**: Lädt `GitHubPlugin.GetIssuesAsync` default nur `state: open`, oder müssen alle Issues geladen und dann gefiltert werden?
