# Bestandsaufnahme: Issue-Anlage aus der Aufgabendetailansicht

## Scope und Ergebnis

Untersucht wurden die Aufgabendetailansicht inklusive Ribbon-Aktionen, die vorhandene Issue-Zuordnung, Domain- und Persistenzmodelle, Repository-Provider-Verträge und -Implementierungen sowie Templates und KI-Integration.

Die Anwendung besitzt bereits einen durchgängigen Pfad zum **Auswählen und Zuordnen eines vorhandenen Issues**. Die geforderte **Issue-Neuanlage** ist dagegen noch nicht als Provider-Funktion modelliert: `IGitPlugin` bietet nur `GetIssuesAsync`, aber weder `CreateIssueAsync` noch Template-Abfragen. Ebenso existiert kein Issue-Anlage-Dialog und kein spezialisierter KI-Dienst zum Ausfüllen eines Issue-Templates.

## Relevante Bestandsbereiche

| Bereich | Bestand | Relevanz für die Anforderung |
|---|---|---|
| Aufgabendetailansicht/Ribbon | `TaskDetailView.xaml` mit Gruppe `Issue`, bestehendem Button „Issue zuweisen“ und Browser-Aktion | Neuer Button kann in derselben Gruppe ergänzt werden; Sichtbarkeit und `CanExecute` müssen die vorhandene Issue-Referenz berücksichtigen. |
| Detail-ViewModel | `TaskDetailViewModel` löst Provider auf, öffnet Dialoge und lädt die Aufgabe neu | Naheliegender Orchestrierungspunkt für Öffnen, Absenden, Fehleranzeige und Aktualisierung der Detailansicht. |
| Bestehende Zuordnung | `IssueSelectionDialog`, `IssueSelectionDialogViewModel`, `AufgabeService.UpdateIssueReferenzAsync` | Dialog- und Persistenzmuster wiederverwendbar; die bestehende Methode erlaubt allerdings auch Überschreiben/Entfernen und erzwingt die Geschäftsregel „höchstens ein Issue“ nicht allein. |
| Provider-Vertrag | `IGitPlugin.GetIssuesAsync`; gemeinsame Basis `GitPluginBase<TPlugin>` | Vertrag muss um Create- und optional Template-Funktionen erweitert werden, inklusive sauberer Nichtunterstützt-/Fehlersemantik. |
| Provider | GitHub liest Issues via `gh`; Bitbucket verwendet Jira REST zum Lesen; LocalDirectory wirft `NotSupportedException` | GitHub, Bitbucket/Jira und nicht unterstützte Provider müssen getrennt behandelt werden. |
| KI | `IKiPlugin`/`IAiCliProvider` starten CLI-Prozesse; `PromptVorlagenService` verwaltet lokale Promptvorlagen | Für die Template-Ausfüllaktion fehlt ein synchronisiertes Ergebnis-API; bestehende CLI-Ausführung ist kein direktes Textgenerierungs-Interface. |

## Detaildokumente

- [Aufgabendetailansicht und bestehende Zuordnung](inventory/task-detail-ui.md)
- [Issue-Domain, Persistenz und Serviceablauf](inventory/issue-persistence.md)
- [Repository-Provider und fehlende Create-/Template-Schnittstellen](inventory/provider-contracts.md)
- [Templates und KI-Integration](inventory/templates-ai.md)

## Technische Leitplanken und Risiken

- Die Aufgabe enthält `Titel`, `AnforderungsBeschreibung`, optional `GitRepository` und optional `IssueReferenz`. Der Provider-Issue-Wert enthält Nummer, Titel, Body, Labels, Milestone und URL; ein separates Create-Request-Modell fehlt.
- `AufgabeService.GetDetailAsync` lädt die Issue-Referenz und das Repository per `Include`, sodass die Detailansicht nach erfolgreicher Zuordnung durch erneutes Laden aktualisiert werden kann.
- Die bestehende `UpdateIssueReferenzAsync` speichert erst die lokale Referenz. Für die Neuanlage muss die Reihenfolge umgekehrt sein: Provider-Issue erstellen, danach Referenz persistieren. Bei Persistenzfehlern nach erfolgreicher Provider-Anlage kann ein externes Issue ohne lokale Zuordnung verbleiben; dieses Teilrisiko muss im Plan berücksichtigt werden.
- Repository-URLs werden heute als Repository-Identifier an Provider weitergereicht. GitHub erwartet `owner/repo`; Bitbucket/Jira nutzt konfigurierte Jira-Zugangsdaten und Projekt-Key, nicht den Repository-Identifier als Jira-Projektauflösung.
- Die offenen Punkte aus `requirement.md` sind für Titel, Provider-Feldumfang, Jira-/Bitbucket-Templates, KI-Ausführung, Platzhalter und Berechtigungsfehler weiterhin relevant. Die Bestandsaufnahme liefert dafür technische Ansatzpunkte, entscheidet diese Fachfragen aber nicht.

## Empfohlene nächste Untersuchungen im Plan

1. Create-Request und Ergebnisvertrag für Issue-Anlage definieren, insbesondere Titel und providerübergreifende optionale Felder.
2. Template-API als optionale Provider-Fähigkeit mit Fallback ohne Template modellieren.
3. Atomaren bzw. fehlertoleranten Ablauf zwischen Provider-Erstellung und `IssueReferenz`-Persistenz festlegen.
4. Einen KI-Anwendungsfall mit rückgabefähigem Textresultat festlegen; CLI-Session-Start allein erfüllt diese Funktion nicht.
5. Tests für Sichtbarkeit/Einmaligkeit, Template-Komposition, Abbruch, Providerfehler und Persistenzreihenfolge ergänzen.
