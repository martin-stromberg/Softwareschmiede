# Bestandsaufnahme: Pullrequests als Aufgabe

## Zusammenfassung

Die Codebasis verfügt bereits über ein vollständiges Pullrequest-Modell, Persistenz, Monitoring, Statusanzeige und den technischen Checkout eines bestehenden Remote-Branches. Die neue Funktion ist daher primär eine Erweiterung des gemeinsamen SCM-Anforderungs-Vertrags, der Plugin-Listenabrufe, des Vorschlags-/Erstellungsflusses und der Startentscheidung.

Die Inventur wurde mangels verfügbarer Unteragenten im lokalen Workflow direkt anhand des Repository-Inhalts erstellt.

## Relevante Detaildokumente

- [Domäne und Persistenz](inventory/domain-and-persistence.md)
- [Plugins und Verträge](inventory/plugins-and-contracts.md)
- [UI und Aufgabenstart](inventory/ui-and-start-workflow.md)
- [Testbasis und erwartete Abdeckung](inventory/tests.md)

## Ist-Zustand nach Anforderungsbereich

| Bereich | Bestand | Lücke für die Anforderung |
|---|---|---|
| PR-Datenmodell | `PullRequest` und `PullRequestReferenz` vorhanden | Create-/Ladepfad für Aufgaben ergänzen |
| GitHub | Issue-Abruf und Einzel-PR-Status vorhanden | Liste offener PRs implementieren |
| Bitbucket/Jira | Jira-Issue-Abruf und Bitbucket-PR-Erstellung vorhanden | Liste offener Bitbucket-PRs implementieren |
| Gemeinsamer Vertrag | `IGitPlugin.GetIssuesAsync` vorhanden | PR-Abruf und Anforderungsart ergänzen |
| Projektdetailansicht | gemeinsame Liste für Issues und Alerts | PRs laden, kennzeichnen, filtern und anlegen |
| Doppelzuordnung | Issue-Filter über `IssueReferenz` vorhanden | eindeutige PR-Zuordnung über Provider/Repository/Nummer |
| Aufgabenstart | Checkout vorhandener Branches technisch vorhanden | PR-Source-Branch automatisch verwenden |
| Regression | bestehende Unit-/Integrations-/E2E-Basen vorhanden | gezielte PR- und E2E-Szenarien ergänzen |

## Änderungsgrenzen

Voraussichtlich betroffen sind die Contract-/Value-Object-Schicht, beide SCM-Plugins, `ProjectDetailViewModel` und das Vorschlags-Template, `AufgabeService`/ggf. `EntwicklungsprozessService` sowie zugehörige Tests. Das bestehende PR-Monitoring und der Issue-Workflow sollten unverändert weitergenutzt werden, abgesehen von der erforderlichen gemeinsamen Abstraktion.

## Offene technische Punkte für die Planung

- Festlegen, ob der PR-Abruf als neue Methode in `IGitPlugin` oder als optionaler Provider-Vertrag modelliert wird.
- Festlegen, wie der gemeinsame `ScmRequirement` PR-Daten trägt, ohne bestehende Issue-/Alert-Bindings zu brechen.
- Festlegen, ob bei einer Aufgabe maximal ein Pullrequest erlaubt ist oder die vorhandene 1:n-Struktur bewusst weiter gilt. Die Anforderung spricht fachlich von einer eindeutigen Verknüpfung.
- Festlegen, wie ein PR-Quell-Branch bei fehlender/ungültiger Branch-Information behandelt wird.
