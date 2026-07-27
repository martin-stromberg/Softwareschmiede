# Bestandsaufnahme: Security and quality alerts

Diese Bestandsaufnahme analysiert den vorhandenen Weg von SCM-Anforderungen ueber Plugin-Contracts, GitHub-/Bitbucket-Plugins, Projektdetailansicht und Aufgabenpersistenz. Ziel ist die Einordnung, wo GitHub Security-/Quality-Alerts als eigene Anforderungsart gelesen, angezeigt und beim Auswaehlen in eine lokale Aufgabe plus neues GitHub-Issue ueberfuehrt werden muessen.

## Zusammenfassung

| Aspekt | Status | Anmerkung |
|--------|--------|-----------|
| SCM-Contract | Teilweise vorhanden | `IGitPlugin.GetIssuesAsync()` liefert nur `Issue`; es gibt keine Alert-Quelle und keine Anforderungstyp-Unterscheidung. |
| Issue-Modell | Vorhanden, zu eng | `Issue` enthaelt Nummer, Titel, Body, Labels, Milestone und URL; Alert-spezifische Felder wie Rule, Severity, Tool, Fingerprint oder Alert-URL fehlen. |
| Issue-Anlage | Vorhanden | `IIssueCreateProvider` und GitHub `CreateIssueAsync()` koennen externe Issues anlegen. Diese Logik ist fuer Alerts wiederverwendbar. |
| GitHub-Plugin | Teilweise vorhanden | Nutzt `gh issue list` und `gh issue create`; Code-Scanning-Alerts werden noch nicht gelesen. |
| Bitbucket/Jira | Vorhanden, nicht betroffen | Bitbucket mappt Jira-Issues auf `Issue`; keine Alert-Unterstuetzung erforderlich. |
| Projektdetail-UI | Teilweise vorhanden | Abschnitt "Offene Anforderungen" zeigt aktuell nur `IssueVorschlaege` und konvertiert diese direkt in Aufgaben. |
| Aufgabenpersistenz | Vorhanden, zu eng | `CreateFromIssueAsync()` erstellt Aufgabe und `IssueReferenz`; keine Quellenart oder Alert-Referenz vorhanden. |
| Duplikatfilter | Vorhanden, zu eng | Filter basiert nur auf `IssueNummer`; fuer Alerts braucht es eine stabile Alert-Quellkennung. |
| Tests | Vorhanden fuer Issues | Tests decken Issue-Laden, Filtern, Konvertieren und GitHub-Issue-Anlage ab; Alert-Tests fehlen. |

## Relevante Beobachtungen

- Die Anforderung sollte nicht durch ein stilles Mapping von Alerts auf `Issue` umgesetzt werden. Die Contracts brauchen eine fachliche Unterscheidung zwischen normalen Issues und Alerts.
- Die Anzeige "Offene Anforderungen" ist bereits der richtige fachliche Ort, aber das ViewModel ist aktuell hart an `ObservableCollection<Issue>` und `AsyncRelayCommand<Issue>` gekoppelt.
- Die automatische GitHub-Issue-Erstellung beim Alert-Klick kann technisch auf `IIssueCreateProvider.CreateIssueAsync()` aufsetzen. Der Ablauf sollte jedoch vor dem lokalen Task-Speichern sicherstellen, dass das externe Issue erfolgreich erstellt wurde.
- Das Datenmodell `IssueReferenz` kann das nachtraeglich angelegte GitHub-Issue speichern, nicht aber eindeutig dokumentieren, aus welchem Alert die Aufgabe entstanden ist. Ohne Erweiterung ist kein sauberer Duplikatschutz fuer Alerts moeglich.
- Initial ist Code Scanning am besten passend, weil die Anforderung "von GitHub-Code-Scanning-Bots erkannte Sicherheits- und Qualitaetsprobleme" nennt und das GitHub CLI `gh api` bereits im Plugin genutzt wird.

## Details

- [Contracts und Modelle](inventory/contracts-models.md)
- [GitHub- und Bitbucket-Plugins](inventory/plugins.md)
- [UI- und Aufgabenworkflow](inventory/ui-workflow.md)
- [Tests und Risiken](inventory/tests-risks.md)

