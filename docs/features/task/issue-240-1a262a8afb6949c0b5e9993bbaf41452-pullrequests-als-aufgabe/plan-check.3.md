# Plan-Check: Pullrequests als Aufgabe

## Status

**Plan lueckenhaft**

## Kurzbewertung

Der ueberarbeitete Plan schliesst die bisherigen Luecken zu Pagination, kanonischer Repository-Identitaet, Bitbucket-Providerwerten, Fork-Checkout, Source-Validierung, Bitbucket-Monitoring und prozessuebergreifenden Desktop-E2E-Tests weitgehend. Zwei zentrale Anschlussentscheidungen fehlen jedoch noch.

Das vorhandene 1:n-Modell unterscheidet nicht zwischen einem Pullrequest, aus dem eine Review-Aufgabe importiert wurde, und einem Pullrequest, den eine normale Aufgabe spaeter selbst erzeugt hat. Der geplante Startmodus leitet den Review-Checkout trotzdem allein aus der Anzahl vorhandener Referenzen ab. Ausserdem soll der Vorschlagsabruf auf alle aktiven Repositories erweitert werden, ohne den Vorschlag unveraenderlich einer lokalen Repository-Zuordnung zuzuordnen oder den dadurch geaenderten Issue-Fluss zu definieren. Damit sind FR-2, FR-5, FR-6 und insbesondere die Regressionsanforderung FR-7 noch nicht durchgehend abgesichert.

## Anforderungsabdeckung

| Anforderung | Bewertung | Begruendung |
|---|---|---|
| FR-1 Offene Pullrequests abrufen | Abgedeckt | GitHub und Bitbucket Cloud/Server sind einschliesslich vollstaendiger Pagination, Provider-Mapping und Source-Metadaten geplant. |
| FR-2 Pullrequests als Vorschlaege anzeigen | Teilweise | Anzeige und Kennzeichnung sind geplant. Bei der vorgesehenen Aggregation aller aktiven Repositories fehlt die verbindliche Zuordnung jedes Vorschlags zum lokalen Repository und Plugin. |
| FR-3 Pullrequests kennzeichnen | Abgedeckt | Typtext, Providername, Icon und stabile Automation-Eigenschaften sind vorgesehen. |
| FR-4 Bereits verknuepfte Pullrequests ausblenden | Abgedeckt | Globale, normalisierte Existenzpruefung, archivierte Aufgaben, andere Projekte und Konkurrenzsicherung sind beschrieben. |
| FR-5 Aufgabe aus Pullrequest anlegen | Teilweise | Die atomare Anlage ist geplant. Es fehlt aber eine persistierte Semantik, die diese Referenz als Review-Quelle kennzeichnet, sowie bei repositoryuebergreifender Anzeige die sichere Wahl der zugehoerigen `GitRepositoryId`. |
| FR-6 Pullrequest-Quell-Branch verwenden | Teilweise | Checkout-Spec, Fork-Verhalten und Ausschluss von `CreateBranchAsync` sind detailliert. Die Auswahl der massgeblichen PR-Referenz ist bei null, einer oder mehreren Referenzen jedoch fachlich nicht korrekt definiert. |
| FR-7 Bestehenden Aufgabenstart erhalten | Nicht vollstaendig | Eine normale Aufgabe kann bereits ueber `GitOrchestrationService.PullRequestErstellenAsync` eine `PullRequestReferenz` erhalten. Bei genau einer solchen Referenz wuerde die geplante Ableitung sie faelschlich als importierte Review-Aufgabe behandeln. |

## Testbedarfspruefung

Der Plan enthaelt eine belastbare Testarchitektur fuer Contract-, Plugin-, Persistenz-, Service-, ViewModel- und Desktop-E2E-Tests. Der reale UI-Fluss vom Vorschlag ueber die Anlage bis zum Branch-Checkout ist mit einem testmodusgebundenen SCM-Plugin, lokalen Git-Remotes und JSONL-Aufrufnachweis konkret und ausfuehrbar beschrieben.

Nicht abgedeckt sind jedoch die fuer die verbleibenden Modellentscheidungen notwendigen Regressionen:

- Eine normale Aufgabe mit genau einer durch `SaveCreatedAsync` erzeugten PR-Referenz verwendet weiterhin den normalen Startpfad.
- Eine importierte Review-Aufgabe verwendet auch nach Hinzukommen weiterer erzeugter PR-Referenzen weiterhin eindeutig ihre urspruengliche Review-Quelle.
- Periodisches Monitoring bzw. Auto-Complete verarbeitet eine importierte GitHub-Review-Quelle nur gemaess einer ausdruecklich festgelegten Policy und schliesst sie nicht unbeabsichtigt vor dem Review ab.
- Bei mehreren aktiven Repositories wird die Aufgabe dem Repository des ausgewaehlten Vorschlags zugeordnet; gleiche Issue- oder PR-Nummern in verschiedenen Repositories werden nicht vermischt. Alternativ wird nachgewiesen, dass die Vorschlagsliste wie bisher strikt auf das ausgewaehlte Repository begrenzt bleibt.

## Zu schliessende Luecken

### 1. Rolle einer Pullrequest-Referenz und Startauswahl festlegen

`Aufgabe.PullRequests` ist eine 1:n-Beziehung. `GitOrchestrationService.PullRequestErstellenAsync` legt ueber `PullRequestReferenzService.SaveCreatedAsync` bereits Referenzen fuer Pullrequests an, die aus normalen Aufgaben heraus erstellt wurden. Der Plan bestimmt dagegen den Modus `CheckoutPullRequestSource`, sobald "genau eine PR-Referenz" geladen wurde. Damit ist weder FR-6 noch FR-7 stabil: Eine bestehende normale Aufgabe mit einem erzeugten PR kann falsch klassifiziert werden, waehrend eine importierte Review-Aufgabe nach Hinzukommen eines weiteren PR ihre Checkout-Quelle verliert.

Der Plan muss eine dauerhafte, migrationsfaehige Unterscheidung vorsehen, beispielsweise eine Referenzrolle `ReviewSource` gegenueber `CreatedByTask` oder eine eigene eindeutige Review-Source-Beziehung. Verbindlich festzulegen sind:

- Kennzeichnung der atomar importierten Referenz als Review-Quelle und aller ueber `SaveCreatedAsync` erzeugten Referenzen als Ausgabe der Aufgabe,
- Migration bzw. eindeutige Behandlung vorhandener Referenzen, ohne bestehende normale Aufgaben nachtraeglich zu Review-Aufgaben zu machen,
- hoechstens eine Review-Quelle pro Aufgabe oder ein ausdruecklicher Fehler bei widerspruechlichen Daten,
- Startentscheidung ausschliesslich anhand dieser Rolle und nicht anhand der Gesamtzahl der PR-Referenzen,
- Verhalten beim Neustart einer importierten Aufgabe, wenn weitere PR-Referenzen vorhanden sind,
- Monitoring- und Auto-Complete-Policy fuer importierte GitHub-Review-Quellen. Insbesondere darf die bestehende periodische Auto-Complete-Logik nicht unbeabsichtigt einen zu pruefenden PR abschliessen; falls Monitoring gewollt ist, muss der Abschlussmodus rollenabhaengig festgelegt und getestet werden.

### 2. Repository-Kontext der Vorschlagsliste widerspruchsfrei definieren

Der aktuelle `ProjectDetailViewModel` laedt Anforderungen fuer `_selectedRepository` und uebergibt beim Erstellen dessen `Id`. Der Plan fordert dagegen Issues und Pullrequests fuer jedes aktive Projekt-Repository, beschreibt fuer `ScmRequirement` aber keine unveraenderliche lokale `GitRepositoryId` bzw. kein gleichwertiges Repository-Kontextobjekt. Provider und normalisierte API-ID allein ersetzen diese Zuordnung nicht. Ein Wechsel der Auswahl oder gleiche Kennungen in mehreren Repositories kann deshalb die Aufgabe mit dem falschen Clone-/Start-Repository verbinden.

Zudem filtert der bestehende Issue-Fluss nur ueber `IssueReferenz.IssueNummer`. Eine Aggregation mehrerer Repositories wuerde Issues mit gleichen Nummern vermischen und waere eine nicht geplante Aenderung des ausdruecklich zu erhaltenden Issue-Workflows.

Der Plan muss sich fuer eine der folgenden konsistenten Varianten entscheiden:

- Vorschlaege bleiben wie bisher auf das ausgewaehlte Repository begrenzt; PR-Abruf, Filter und Create-Pfad verwenden einen beim Laden festgehaltenen Snapshot dieses Repository-Kontexts.
- Oder jeder Vorschlag traegt mindestens lokale `GitRepositoryId`, Plugin-Prefix und kanonische Ziel-Repository-ID. Der Create-Pfad verwendet ausschliesslich diesen Kontext. Dann muessen auch Issue-Identitaet, Filterung und Tests repositorybezogen erweitert werden.

In beiden Varianten muss ein E2E-Test mit mindestens zwei Projekt-Repositories nachweisen, dass Anzeige, Anlage, Persistenz und spaeterer Checkout dasselbe Repository verwenden.

## Erforderliche Plananpassung

Nach Ergaenzung der Referenzrollen-/Monitoring-Entscheidung und des Repository-Kontexts ist der Plan erneut zu pruefen. Schritt 5b bleibt bis zu einem Ergebnis mit Status `Plan vollstaendig` offen.
