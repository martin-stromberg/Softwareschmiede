# Plan-Gegenpruefung

## Ergebnis

**Status:** Plan vollstaendig

## Kurzbewertung

Der Plan deckt alle funktionalen Anforderungen und Akzeptanzkriterien mit konkreten Umsetzungsschritten ab. Die benoetigten Contract-, Plugin-, Persistenz-, Service-, ViewModel-, Integrations- und Desktop-E2E-Tests sind verbindlich benannt. Die zuvor fehlenden E2E-Nachweise fuer den gemeinsamen Bitbucket/Jira-Vorschlagsfluss und fuer den normalen Aufgabenstart ohne Pullrequest-Verknuepfung sind enthalten.

Die Gegenpruefung wurde mangels verfuegbarer Unteragenten im lokalen Workflow direkt ausgefuehrt.

## Abgleich der Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Offene GitHub-Pullrequests werden zusaetzlich zu Issues als Vorschlaege angezeigt. | `GetOpenPullRequestsAsync` mit vollstaendiger Pagination sowie die Zusammenfuehrung von Issues, PRs und Alerts fuer den ausgewaehlten Repository-Snapshot sind in den Schritten 2 und 6 beschrieben. | Provider-Tests pruefen mehrere Seiten und nur offene Ergebnisse. Das Desktop-E2E-Hauptszenario zeigt GitHub-Issue und GitHub-PR desselben Repositorys gemeinsam an. | Abgedeckt |
| Offene Bitbucket/Jira-Pullrequests werden zusaetzlich zu Issues als Vorschlaege angezeigt. | Cloud- und Server/Data-Center-Abruf, getrennte Parser, vollstaendige Pagination und der gemeinsame Vorschlagsfluss sind in den Schritten 2 und 6 festgelegt. | Plugin- und Integrationstests decken beide Hosting-Modi ab. Ein verbindliches Desktop-E2E-Szenario prueft die gleichzeitige sichtbare und bedienbare Anzeige von Jira-Issue und Bitbucket-PR; je eine Cloud- und Server/Data-Center-Fixture prueft zusaetzlich den providerspezifischen UI- und Anlagepfad. | Abgedeckt |
| Pullrequest-Vorschlaege sind eindeutig von Issue-Vorschlaegen unterscheidbar. | Sichtbarer Typtext, Providername, passendes Icon und stabile Automation-Eigenschaften sind in Schritt 6 vorgesehen. | Das GitHub-Hauptszenario und die Bitbucket-E2E-Szenarien pruefen Typ- und Providerkennzeichnung. | Abgedeckt |
| Ein bereits einer Aufgabe zugeordneter Pullrequest erscheint nicht erneut. | Die globale Existenzpruefung und der eindeutige Index verwenden Provider, kanonische Repository-ID und PR-Nummer unabhaengig von Projekt und Archivstatus. | Persistenz-, Service- und Konkurrenztests sowie Desktop-E2E fuer ein anderes Projekt beziehungsweise eine archivierte Aufgabe und das erneute Laden nach der Anlage sind vorgesehen. | Abgedeckt |
| Aus einem Pullrequest-Vorschlag kann eine neue Aufgabe angelegt werden. | Der Doppelklickpfad uebergibt den unveraenderlichen Repository-Snapshot an den atomaren Create-Pfad und navigiert nach Erfolg zur Aufgabe. | Das Desktop-E2E-Hauptszenario und die beiden Bitbucket-Fixtures legen Aufgaben ueber den realen UI-Pfad an. | Abgedeckt |
| Die neue Aufgabe ist mit dem Pullrequest verknuepft. | Aufgabe und genau eine als `ReviewSource` markierte `PullRequestReferenz` werden transaktional mit Provider-, Repository- und Source-Daten gespeichert. | Persistenz-, Service- und Bitbucket-Integrationstests sowie Datenbankpruefungen im Desktop-E2E-Hauptszenario weisen die Verknuepfung und ihre Checkout-Daten nach. | Abgedeckt |
| Beim Start wird der Pullrequest-Quell-Branch ausgecheckt und kein neuer Branch erzeugt. | Die Startentscheidung verwendet ausschliesslich die Referenzrolle. `CheckoutPullRequestSource` besitzt keinen Fallback auf `CreateBranchAsync` und behandelt Same-Repository-, Default-Branch- und Fork-Faelle explizit. | Service-Tests und Desktop-E2E pruefen Hauptfluss, Default-Branch, Fork, nicht fetchbaren Ref, zusaetzliche `CreatedByTask`-Referenzen, JSONL-Aufrufe und den tatsaechlichen Git-HEAD. | Abgedeckt |
| Der Start von Aufgaben ohne Pullrequest-Verknuepfung bleibt unveraendert. | Ohne `ReviewSource` bleibt der bestehende `CreateTaskBranch`-/`CreateBranchAsync`-Pfad aktiv; erzeugte `CreatedByTask`-Referenzen aendern diese Entscheidung nicht. | Neben Service-Tests ist ein verbindlicher Desktop-E2E-Regressionstest fuer eine Aufgabe ohne jegliche `PullRequestReferenz` vorgesehen. Er startet ueber den realen UI-Button, weist `CreateBranchAsync` nach und schliesst `CheckoutPullRequestSourceAsync` aus. Ein weiteres E2E-Szenario prueft eine normale Aufgabe mit `CreatedByTask`-Referenz. | Abgedeckt |

## Testbedarfspruefung

- Contract- und Normalizer-Tests sichern Providerwerte, Referenzrollen, Monitoringphasen, Hosting-Modi und kanonische Repository-Identitaeten ab.
- Plugin-Tests pruefen GitHub sowie Bitbucket Cloud und Server/Data Center einschliesslich Pagination, offenen und geschlossenen Ergebnissen, Source-Metadaten und Checkout-Fehlerpfaden.
- Persistenz-, Service- und Integrationstests decken atomare Anlage, globale Doppelzuordnung, Migration, Konkurrenz, Repository-Snapshot, Rollen, Monitoring-Policy und Branch-Startentscheidung ab.
- ViewModel- und UI-Tests pruefen Repositorywechsel, veraltete Ladeergebnisse, Kennzeichnung, Filterung, Anlagefehler und den unveraenderten Issue-Fluss.
- Die Desktop-E2E-Architektur prueft den realen separaten App-Prozess ohne Netzwerk mit lokalen Git-Remotes, persistierter Testdatenbank, stabilen Automation-IDs und JSONL-Aufrufprotokoll.
- Die konkreten Desktop-E2E-Szenarien decken Anzeige, Unterscheidung, Anlage, Persistenz, Filterung, Start, Forks, Fehlerfaelle, beide Bitbucket-Hosting-Modi sowie die Regression normaler Aufgaben ab. Fehlende oder uebersprungene E2E-Szenarien gelten ausdruecklich nicht als Abnahme.

## Fehlende oder unvollstaendige Planbestandteile

Keine.

## Offene Punkte

Keine fachlichen oder technischen offenen Punkte. Der Plan ist fuer die Implementierung und die anschliessende Abnahme ausreichend konkret.
