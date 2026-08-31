# Plan-Check: Pullrequests als Aufgabe

## Status

**Plan lueckenhaft**

## Kurzbewertung

Der ueberarbeitete Plan schliesst die vier Luecken des vorherigen Checks weitgehend. Pagination und kanonische Repository-Identitaet sind festgelegt, der PR-Start besitzt einen expliziten Checkout-Modus inklusive Fork-Verhalten, der Validierungszeitpunkt ist widerspruchsfrei und die Desktop-E2E-Architektur ist fuer den separaten App-Prozess konkret beschrieben.

Zwei fuer Bitbucket notwendige Anschlussentscheidungen fehlen jedoch noch. Das bestehende Provider-Modell kann Bitbucket-Pullrequests nicht korrekt repraesentieren, und das vorhandene Monitoring wuerde eine neu angelegte Bitbucket-Referenz unmittelbar als nicht unterstuetzten Fehler markieren. Damit sind insbesondere FR-1, FR-4 und FR-5 fuer Bitbucket noch nicht durchgehend umsetzbar.

## Anforderungsabdeckung

| Anforderung | Bewertung | Begruendung |
|---|---|---|
| FR-1 Offene Pullrequests abrufen | Teilweise | Abruf, Mapping und Pagination fuer GitHub sowie Bitbucket Cloud/Server sind geplant. `PullRequestProvider` enthaelt aktuell aber nur `GitHub`; die notwendige Bitbucket-Auszeichnung fehlt im Plan. |
| FR-2 Pullrequests als Vorschlaege anzeigen | Abgedeckt | Zusammenfuehrung mit Issues und Alerts fuer alle aktiven Projekt-Repositories ist vorgesehen. |
| FR-3 Pullrequests kennzeichnen | Abgedeckt | Typtext, Icon und stabile Automation-Eigenschaften sind einschliesslich UI-Tests eingeplant. |
| FR-4 Bereits verknuepfte Pullrequests ausblenden | Teilweise | Globale Abfrage, kanonischer Schluessel, Migration und Konkurrenzsicherung sind beschrieben. Ohne explizite Bitbucket-Provider-Repraesentation kann der Schluessel fuer Bitbucket jedoch nicht korrekt gespeichert und verglichen werden. |
| FR-5 Aufgabe aus Pullrequest anlegen | Teilweise | Atomare Anlage und strukturelle Source-Validierung sind geplant. Das Provider- und Monitoring-Verhalten einer dabei erzeugten Bitbucket-Referenz ist noch nicht festgelegt. |
| FR-6 Pullrequest-Quell-Branch verwenden | Abgedeckt | Expliziter Startmodus, Same-Repo-, Default-Branch- und Fork-Checkout sowie der Ausschluss von `CreateBranchAsync` sind verbindlich beschrieben. |
| FR-7 Bestehenden Aufgabenstart erhalten | Abgedeckt | Der normale Branch-Erzeugungspfad bleibt getrennt und wird auf Service- und E2E-Ebene regressionsgetestet. |

## Testbedarfspruefung

Der Plan benennt die erforderlichen Contract-, Plugin-, Persistenz-, Service-, ViewModel- und Desktop-E2E-Tests. Besonders der reale Benutzerfluss vom PR-Vorschlag ueber die atomare Aufgabenanlage bis zum Checkout des Source-Branches ist mit einem prozessuebergreifenden Testplugin, lokalen Git-Remotes und einem negativen `CreateBranchAsync`-Nachweis ausreichend konkret.

Noch nicht abgedeckt sind Tests, die eine Bitbucket-Provider-Identitaet durch Mapping, Persistenz, Doppelzuordnungsfilter und erneutes Laden verfolgen, sowie Tests fuer das beabsichtigte Monitoring-/Refresh-Verhalten einer aus einem Bitbucket-Vorschlag erzeugten Referenz.

## Zu schliessende Luecken

### 1. Bitbucket im Provider-Modell explizit einplanen

`src/Softwareschmiede.Plugin.Contracts/Domain/Enums/PullRequestProvider.cs` definiert derzeit ausschliesslich `GitHub`. `PullRequest` verwendet diesen Wert zudem als Default. Der Plan nennt weder diese Datei noch die verbindliche Erweiterung des Enums. Ohne diese Anpassung wuerden Bitbucket-Pullrequests als GitHub-Pullrequests persistiert oder koennten gar nicht korrekt gemappt werden. Das gefaehrdet den globalen Schluessel `Provider + RepositoryId + PullRequestNumber`, die Doppelzuordnungspruefung und jede spaetere Plugin-Aufloesung.

Der Plan muss festlegen:

- welche Enum-Werte Bitbucket Cloud und Server/Data Center repraesentieren,
- wie der Hosting-Modus in die Normalisierung eingeht, falls beide Varianten denselben Providerwert verwenden,
- wie Mapping, Persistenz, Anzeige und Provider-Aufloesung den Wert durchreichen,
- welche Tests eine Bitbucket-Referenz vom API-Ergebnis bis zum erneuten Laden und Filtern absichern.

### 2. Monitoring-Verhalten importierter Bitbucket-Pullrequests festlegen

`PullRequestReferenzService.GetDueForMonitoringAsync` nimmt Referenzen mit Phase `Created` und auch ohne `NextCheckUtc` in die Verarbeitung auf. `PullRequestMonitoringService` setzt derzeit jeden Provider ungleich GitHub auf `Failed` mit dem Fehler "Provider wird nicht unterstuetzt". `TaskDetailView` zeigt Phase und `LastError` sichtbar an. Eine neu aus einem Bitbucket-Vorschlag angelegte Aufgabe wuerde daher ohne weitere Planung in einen irrefuehrenden Fehlerzustand wechseln.

Der Plan muss entweder Bitbucket-Statusabfragen fuer das bestehende Monitoring vorsehen oder importierte, nicht monitorbare Referenzen bewusst vom automatischen und manuellen Monitoring ausnehmen und diesen Zustand ohne Fehler darstellen. Die betroffenen Services, die Aufgabendetailansicht beziehungsweise deren Refresh-Faehigkeit sowie Unit-/Integrationstests sind als Umsetzungspunkte aufzunehmen.

## Erforderliche Plananpassung

Nach Ergaenzung der Provider- und Monitoring-Entscheidungen ist der Plan erneut zu pruefen. Schritt 5b bleibt bis zu einem Ergebnis mit Status `Plan vollstaendig` offen.
