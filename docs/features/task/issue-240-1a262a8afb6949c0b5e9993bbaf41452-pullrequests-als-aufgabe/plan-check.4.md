# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan lückenhaft

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Offene GitHub-Pullrequests werden zusätzlich zu Issues als Vorschläge angezeigt. | `GetOpenPullRequestsAsync` mit vollständiger Pagination sowie Zusammenführung von Issues, PRs und Alerts in der Projektdetailansicht sind in den Schritten 2 und 6 beschrieben. | GitHub-Plugintests mit zwei Seiten sowie das E2E-Hauptszenario mit gleichzeitig sichtbarem Issue und PR aus Repository A. | Abgedeckt |
| Offene Bitbucket/Jira-Pullrequests werden zusätzlich zu Issues als Vorschläge angezeigt. | Getrennte Cloud- und Server/Data-Center-Parser, Pagination, Providerwerte und die gemeinsame Vorschlagsliste sind in den Schritten 2 und 6 beschrieben. | Plugin- und Integrationstests decken Abruf und Mapping ab; die Bitbucket-E2E-Szenarien prüfen jedoch nur den sichtbaren und anlegbaren PR, nicht das gleichzeitige Fortbestehen des Jira-Issues in der Liste. | Lücke |
| Pullrequest-Vorschläge sind eindeutig von Issue-Vorschlägen unterscheidbar. | Schritt 6 plant Typtext, Providername, Icon und stabile Automation-Eigenschaften. | Das E2E-Hauptszenario und die Bitbucket-E2E-Szenarien prüfen PR-Kennzeichnung und Provideranzeige. | Abgedeckt |
| Ein bereits zugeordneter Pullrequest erscheint nicht erneut. | Globale Existenzprüfung über Provider, kanonische Repository-ID und Nummer sowie Filterung und Datenbankindex sind in den Schritten 3 und 6 festgelegt. | Persistenz-/Service-Tests sowie ein E2E-Szenario für eine archivierte Aufgabe beziehungsweise ein anderes Projekt sind vorgesehen. | Abgedeckt |
| Aus einem Pullrequest-Vorschlag kann eine neue Aufgabe angelegt werden. | Der atomare Create-Pfad und der Doppelklick-/Navigationsfluss sind in den Schritten 3 und 6 konkret beschrieben. | Das E2E-Hauptszenario sowie die Bitbucket-Cloud- und Server/Data-Center-Szenarien legen die Aufgabe über den realen UI-Pfad an. | Abgedeckt |
| Die neue Aufgabe ist mit dem Pullrequest verknüpft. | Die Aufgabe wird atomar mit genau einer `ReviewSource` und allen Identitäts- und Source-Feldern gespeichert. | Das E2E-Hauptszenario prüft `GitRepositoryId`, Rolle, Provider, Nummer, URL, Source-Branch, Source-Ref und Source-Repository in der Datenbank; Persistenz- und Bitbucket-Integrationstests ergänzen den Nachweis. | Abgedeckt |
| Beim Start wird der PR-Quell-Branch ausgecheckt und kein neuer Branch erzeugt. | Der explizite Modus `CheckoutPullRequestSource` ist rollenbasiert; Same-Repository-, Default-Branch- und Fork-Pfade schließen `CreateBranchAsync` aus. | Service-Tests und konkrete Desktop-E2E-Szenarien für Hauptfluss, Default-Branch, Fork, nicht fetchbaren Ref und zusätzliche `CreatedByTask`-Referenzen prüfen Checkout, HEAD und den negativen Create-Branch-Nachweis. | Abgedeckt |
| Der Start von Aufgaben ohne Pullrequest-Verknüpfung bleibt unverändert. | Keine `ReviewSource` führt unabhängig von erzeugten PR-Referenzen in den bestehenden `CreateBranchAsync`-Pfad. | Ein Service-Test deckt den Fall ohne Referenz ab. Das geplante E2E-Regressionsszenario startet dagegen eine Aufgabe mit einer `CreatedByTask`-Referenz; ein E2E-Start einer Aufgabe ohne jegliche `PullRequestReferenz` fehlt. | Lücke |

## Fehlende oder unvollständige Testanforderungen

- [ ] Für Bitbucket Cloud und Bitbucket Server/Data Center im jeweiligen Desktop-E2E-Szenario neben dem gekennzeichneten PR auch ein Jira-Issue bereitstellen und explizit nachweisen, dass beide gleichzeitig in der Vorschlagsliste sichtbar und bedienbar bleiben.
- [ ] Einen Desktop-E2E-Regressionstest für eine normale Aufgabe ohne jegliche `PullRequestReferenz` planen: Aufgabe über den regulären Start-Button starten, sichtbaren erfolgreichen Start nachweisen, den bisherigen `CreateBranchAsync`-Pfad im JSONL-Protokoll belegen und sicherstellen, dass `CheckoutPullRequestSourceAsync` nicht aufgerufen wird.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| GitHub-Issue und GitHub-PR des ausgewählten Repositorys gemeinsam anzeigen und unterscheiden | `Projektdetail_PRVorschlag_AufgabeAnlegen_undStartetMitSourceBranch_E2E`, Schritte 1 und 2 | Abgedeckt |
| Bitbucket/Jira-Issue und Bitbucket-PR gemeinsam anzeigen | Je eine Cloud- und Server/Data-Center-Fixture ist vorgesehen, aber ohne ausdrückliche Assertion auf das weiterhin sichtbare Jira-Issue. | Lücke |
| PR-Vorschlag als Aufgabe anlegen und Verknüpfung persistieren | E2E-Hauptszenario, Schritte 3 und 5, sowie beide Bitbucket-Szenarien | Abgedeckt |
| Review-Aufgabe starten und exakt den Source-Branch ohne neuen Branch verwenden | E2E-Hauptszenario, Schritt 4, plus Default-Branch-, Fork- und Wiederanlauf-Szenarien | Abgedeckt |
| Nicht fetchbarer Source-Ref beim Start | Sichtbarer Startfehler, keine Finalisierung und kein `CreateBranchAsync` | Abgedeckt |
| Fehlende strukturelle Source-Daten beim Anlegen | Sichtbarer Create-Fehler und keine Aufgabe beziehungsweise Referenz in der Datenbank | Abgedeckt |
| Bereits global zugeordneter PR | E2E-Szenario mit anderem Projekt beziehungsweise archivierter Aufgabe | Abgedeckt |
| Normale Aufgabe ohne Pullrequest-Verknüpfung starten | Nur ein Service-Test ohne Referenz; kein konkreter Desktop-E2E-Test über den Start-Button. | Lücke |
| Normale Aufgabe mit ausschließlich `CreatedByTask`-Referenz starten | E2E-Szenario mit Nachweis des bestehenden `CreateBranchAsync`-Pfads | Abgedeckt |

## Fehlende oder unvollständige Planbestandteile

Keine fehlenden Umsetzungsbestandteile identifiziert. Die Lücken betreffen ausschließlich die verbindliche E2E-Abnahme.

## Hinweise

Die Implementierungs-, Service-, Integrations- und Testinfrastruktur-Schritte decken die Anforderung ansonsten vollständig und ausführbar ab. Wegen der zwei fehlenden UI-Nachweise bleibt der Status gemäß `/plan-check` dennoch `Plan lückenhaft`; Unit- oder Service-Tests können diese E2E-Abdeckung nicht ersetzen.
