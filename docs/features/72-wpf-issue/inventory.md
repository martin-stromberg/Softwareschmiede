# Bestandsaufnahme: SCM-Issues in Aufgabenlisten integrieren (Feature 72)

Diese Bestandsaufnahme analysiert den bestehenden Projektcode bezogen auf Anforderung 72, die die Integration von SCM-Issues in die Aufgabenlisten-UI vorsieht. Das Dokument erfasst, welche Komponenten bereits existieren und welche Strukturen für die Implementierung genutzt werden können.

## Zusammenfassung

**Vorhanden:**
- **Datenmodell:** `Aufgabe`, `IssueReferenz` und `Issue` (Value Object) sind vollständig implementiert. `Aufgabe` besitzt bereits eine optionale Navigation zu `IssueReferenz`.
- **Service-Logik:** `AufgabeService.CreateFromIssueAsync()` ist bereits implementiert und erstellt Aufgaben aus Issues mit automatischer `IssueReferenz`-Zuweisung.
- **Plugin-Interface:** `IGitPlugin.GetIssuesAsync()` ist definiert und wird von `GitHubPlugin` implementiert (mit funktionsfähigem Code und Error-Handling). `LocalDirectoryPlugin` wirft `NotSupportedException`.
- **ViewModels:** `ProjectDetailViewModel` und `TaskDetailViewModel` existieren mit grundlegender Struktur, aber ohne Issue-spezifische Features.
- **Tests:** Es existieren Unit- und Integrationstests für `AufgabeService`, einschließlich Tests für `CreateFromIssueAsync`.

**Fehlt noch:**
- **ProjectDetailViewModel:** Keine `IssueVorschlaege`-Collection, keine `LadenIssuesAsync()`-Methode, kein `AufgabeAusIssueErstellenCommand`, keine `KannIssuesLaden`-Property.
- **TaskDetailViewModel:** Keine `CanAssignIssue`-Property, kein `CurrentIssueReferenz`, keine `IssueZuweisenAsync()` oder Ribbon-Commands für Issue-Management.
- **UI-Komponenten:** Kein `IssueSelectionDialog` vorhanden. `ProjectDetailView` und `TaskDetailView` benötigen Issue-UI-Integration.
- **Tests:** Keine Tests für Issue-Laden im ProjectDetailViewModel, keine Tests für Issue-Dialoge oder Ribbon-Buttons im TaskDetailViewModel.

## Details

- [Datenmodell](inventory/models.md) — `Aufgabe`, `IssueReferenz`, `Issue` Value Object
- [Logik](inventory/logic.md) — `AufgabeService`, `ProjectDetailViewModel`, `TaskDetailViewModel`
- [Interfaces](inventory/interfaces.md) — `IGitPlugin` mit `GetIssuesAsync()` Methode
- [Enums](inventory/enums.md) — `AufgabeStatus`
- [Tests](inventory/tests.md) — Bestehende Unit- und Integrationstests, Plugin-Implementierungen

## Kritische Erkenntnisse

1. **`CreateFromIssueAsync` ist bereits vollständig implementiert:** Die Service-Methode erstellt eine neue `Aufgabe` mit Status `Neu`, übernimmt Titel und Body aus dem Issue und erstellt automatisch eine `IssueReferenz` mit allen erforderlichen Feldern (IssueNummer, Titel, Body, LabelsJson, Milestone, IssueUrl). Dies ist die technische Grundlage für die Anforderung.

2. **GitHub Plugin lädt Issues bereits:** `GitHubPlugin.GetIssuesAsync()` nutzt `gh issue list` CLI und lädt bis zu 100 offene Issues. Das Parsing funktioniert und konvertiert zu `Issue` Value Objects. Error-Handling ist implementiert (Grace-Degradation mit leerer Liste).

3. **LocalDirectory Plugin blockiert Issues explizit:** Wirft `NotSupportedException`, was für die dynamische UI-Sichtbarkeit genutzt werden kann.

4. **ViewModels sind Foundation-ready:** Beide ViewModels haben Dependency Injection Setup, Command-Pattern und ObservableCollection-Handling. Sie benötigen nur Issue-spezifische Properties und Commands.

5. **Keine E2E-Tests für Issue-Feature:** Tests für Issue-Dialoge, Issue-Zuweisung und Plugin-Kompatibilität (LocalDirectory keine Issue-Buttons) sind noch nicht implementiert.

## Abhängigkeitsmap

```
Aufgabe ──┐
          ├─→ IssueReferenz
          │
Projekt ──┘

IGitPlugin.GetIssuesAsync() ──→ Issue (Value Object)

AufgabeService.CreateFromIssueAsync(Issue) ──→ erstellt Aufgabe + IssueReferenz

ProjectDetailViewModel
  ├─ AufgabeService
  └─ (zukünftig) IGitPlugin für LadenIssuesAsync()

TaskDetailViewModel
  ├─ AufgabeService
  └─ (zukünftig) IGitPlugin für Issue-Dialog
```

## Nächste Schritte (nach Planung)

1. **ProjectDetailViewModel erweitern:** `IssueVorschlaege` Collection, `LadenIssuesAsync()`, `AufgabeAusIssueErstellenCommand`
2. **TaskDetailViewModel erweitern:** `CanAssignIssue`, `CurrentIssueReferenz`, `IssueZuweisenAsync()`, Ribbon-Commands
3. **UI-Dialoge implementieren:** `IssueSelectionDialog` und `IssueSelectionDialogViewModel`
4. **ProjectDetailView und TaskDetailView anpassen:** Issue-Vorschläge in Aufgabenliste, Issue-Buttons im Ribbon
5. **Tests erweitern:** Unit-Tests für ViewModel-Issue-Funktionalität, E2E-Tests für Workflows
