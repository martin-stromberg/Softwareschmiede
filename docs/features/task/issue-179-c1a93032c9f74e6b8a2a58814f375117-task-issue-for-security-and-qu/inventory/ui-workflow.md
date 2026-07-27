# UI- und Aufgabenworkflow

## Projektdetailansicht

**Dateien:**

- `src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs`
- `src/Softwareschmiede.App/Views/ProjectDetailView.xaml`

Die Projektdetailansicht enthaelt den Abschnitt "Offene Anforderungen". Der ViewModel-Stand ist aktuell issue-spezifisch:

| Element | Aktueller Typ/Zweck | Alert-Auswirkung |
|---------|---------------------|------------------|
| `IssueVorschlaege` | `ObservableCollection<Issue>` | Muss zu gemeinsamer Anforderungsliste oder separater Alert-Liste erweitert werden. |
| `LadenIssuesAsync()` | Laedt `gitPlugin.GetIssuesAsync()` | Muss Issues und GitHub-Alerts zusammenfuehren. |
| `AufgabeAusIssueErstellenCommand` | `AsyncRelayCommand<Issue>` | Muss zwischen Issue- und Alert-Auswahl unterscheiden. |
| `AufgabeAusIssueErstellenAsync()` | Erstellt lokale Aufgabe via `CreateFromIssueAsync()` | Fuer Alerts zusaetzlich externes GitHub-Issue erstellen und dieses referenzieren. |

Die XAML zeigt `IssueVorschlaege` mit `#<Nummer>`, `Titel` und dem festen Text "Offene Anforderung". Fuer Alerts braucht die UI mindestens eine Quellenanzeige, z. B. "GitHub Code Scanning Alert", und darf sich nicht auf Issue-Nummern verlassen.

## Lade- und Duplikatlogik

`LadenIssuesAsync()` filtert bereits konvertierte Vorschlaege so:

```csharp
var bereitsKonvertierteNummern = Aufgaben
    .Where(a => a.IssueReferenz?.IssueNummer != null)
    .Select(a => a.IssueReferenz!.IssueNummer!.Value)
    .ToHashSet();
```

Das funktioniert nur fuer normale Issues. Alerts brauchen eine stabile Quelle, z. B. Alert-Nummer plus Alert-Typ plus Repository, oder eine Alert-URL/Fingerprint-Kombination. Wenn kein Duplikatschutz umgesetzt wird, bleibt eine offene fachliche Frage aus `requirement.md` bestehen.

## Aufgabe-Service

**Datei:** `src/Softwareschmiede/Application/Services/AufgabeService.cs`

Aktuelle relevante Methoden:

| Methode | Zweck |
|---------|-------|
| `CreateAsync()` | Erstellt freie Aufgabe ohne Issue-Referenz. |
| `CreateFromIssueAsync()` | Erstellt Aufgabe aus `Issue` und legt `IssueReferenz` an. |
| `UpdateIssueReferenzAsync()` | Aendert oder entfernt die Issue-Referenz einer bestehenden Aufgabe. |
| `TryAssignIssueReferenzIfNoneAsync()` | Weist eine Issue-Referenz race-condition-resistenter zu. |

Fuer Alerts ist ein neuer Workflow erforderlich:

1. Alert aus offener Anforderungsliste auswaehlen.
2. GitHub-Issue aus Alert-Daten erstellen.
3. Lokale Aufgabe mit Titel/Beschreibung aus Alert und `IssueReferenz` auf das neu erstellte GitHub-Issue speichern.
4. Optional Alert-Quellreferenz speichern, damit derselbe Alert nicht mehrfach angeboten wird.

Die Reihenfolge sollte externes Issue zuerst erstellen, weil die Anforderung verlangt, dass zu der erzeugten Aufgabe automatisch ein GitHub-Issue existiert. Schlaegt die externe Anlage fehl, sollte keine lokale Aufgabe entstehen.

## TaskDetail-Issue-Anlage

**Datei:** `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`

TaskDetail hat bereits eine manuelle Issue-Anlage ueber `IssueAnlegenAsync()`. Diese prueft `IIssueCreateProvider`, initialisiert `IssueCreateDialogViewModel`, zeigt einen Dialog und ordnet das erzeugte Issue per `TryAssignIssueReferenzIfNoneAsync()` zu.

Fuer Alerts ist der Dialog wahrscheinlich nicht gewuenscht, aber die Provider- und Zuordnungslogik ist fachlich wiederverwendbar. Um Duplikation im `ProjectDetailViewModel` zu vermeiden, bietet sich eine Service-Kapselung fuer "Issue beim SCM-Provider anlegen und referenzieren" an.

