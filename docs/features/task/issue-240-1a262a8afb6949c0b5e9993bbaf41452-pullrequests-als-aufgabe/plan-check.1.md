# Plan-Check: Pullrequests als Aufgabe

## Status

**Plan lueckenhaft**

## Kurzbewertung

Der Plan deckt die fachlichen Kernschritte und die geforderten Testebenen weitgehend ab. Er ist jedoch noch nicht durchgehend umsetzbar und nachweisbar. Insbesondere fehlt fuer den verbindlichen Desktop-E2E-Fluss ein konkreter Testmechanismus, und der geplante Branch-Start deckt den vorhandenen Servicevertrag sowie Pullrequests aus Forks nicht vollstaendig ab.

## Anforderungsabdeckung

| Anforderung | Bewertung | Begruendung |
|---|---|---|
| FR-1 Offene Pullrequests abrufen | Teilweise | GitHub und Bitbucket Cloud/Self-Hosted sind vorgesehen. Vollstaendige Pagination und die kanonische Repository-ID je Provider sind nicht verbindlich beschrieben. |
| FR-2 Pullrequests als Vorschlaege anzeigen | Abgedeckt | Gemeinsames Laden und Zusammenfuehren mit Issues ist eingeplant. |
| FR-3 Pullrequests kennzeichnen | Abgedeckt | Typtext, eigenes Icon bzw. Automation-Name und UI-Tests sind vorgesehen. |
| FR-4 Bereits verknuepfte Pullrequests ausblenden | Weitgehend abgedeckt | Provider/Repository/Nummer, Vorabpruefung und Datenbankindex sind vorgesehen. Die Normalisierung der Repository-ID muss noch festgelegt werden. |
| FR-5 Aufgabe aus Pullrequest anlegen | Abgedeckt | Atomarer Create-Pfad und Persistenz aller PR-Felder sind eingeplant. |
| FR-6 Pullrequest-Quell-Branch verwenden | Nicht vollstaendig | Der vorhandene Startpfad unterscheidet anhand des Default-Branches und kann bei Namensgleichheit weiterhin `CreateBranchAsync` aufrufen. `CheckoutRemoteBranchAsync` setzt zudem `origin/<branch>` voraus und deckt Fork-PRs nicht ab. |
| FR-7 Bestehenden Aufgabenstart erhalten | Abgedeckt | Der bestehende Pfad und passende Regressionstests sind explizit vorgesehen. |

## Zu schliessende Luecken

### 1. Ausfuehrbarer E2E-Testaufbau fehlt

`WpfTestBase` startet die Anwendung als separaten Prozess. In diesem Testmodus laedt `PluginManager.IsAllowedInTestMode` als SCM-Plugin ausschliesslich das `LocalDirectoryPlugin`; ein im Testprozess erzeugter `IGitPlugin`-Mock kann daher nicht in die Anwendung injiziert werden. Der Plan fordert dennoch einen deterministischen Provider-Mock und die Beobachtung von `CheckoutRemoteBranchAsync` und `CreateBranchAsync`, ohne den notwendigen prozessuebergreifenden Mechanismus zu planen.

Der Plan muss eine konkrete E2E-Testarchitektur festlegen, zum Beispiel:

- ein dediziertes, nur im Testmodus geladenes SCM-Testplugin mit dateibasierter PR-/Issue-Fixture,
- eine explizite Freigabe und Bereitstellung dieses Plugins im App-Testprozess,
- eine dateibasierte Aufzeichnung der Clone-/Checkout-/CreateBranch-Aufrufe,
- Einrichtung von Projekt, Repository und lokalem Git-Remote fuer den realen UI-Startpfad,
- die dafuer betroffenen Dateien und Build-/Copy-Schritte (`PluginManager`, `WpfTestBase`, Testplugin und E2E-Page-Objects).

Erst damit kann das verbindliche Szenario den sichtbaren Vorschlagsfluss und den echten Aufgabenstart ohne Netzwerk deterministisch nachweisen.

### 2. Branch-Startvertrag ist fuer PR-Aufgaben nicht eindeutig genug

Der vorhandene `EntwicklungsprozessService.SetupBranchAsync` behandelt einen uebergebenen Branch nur dann als bestehenden Arbeitsbranch, wenn er nicht dem Remote-Default-Branch entspricht. Fuer FR-6 braucht der Plan einen expliziten Startmodus fuer PR-Aufgaben, der unabhaengig vom Branchnamen immer den vorhandenen PR-Branch auscheckt und `CreateBranchAsync` ausschliesst.

Zusaetzlich setzt `IGitPlugin.CheckoutRemoteBranchAsync` einen Branch unter `origin/<branch>` voraus. Offene GitHub- und Bitbucket-Pullrequests koennen jedoch aus Forks stammen. Der Plan muss entweder den Provider-Ref bzw. das Quell-Repository mitfuehren und einen geeigneten Checkout/FETCH-Pfad vorsehen oder eine fachlich begruendete, sichtbare Unterstuetzungsgrenze definieren. Ein vorhandener `BranchName` allein garantiert keinen auscheckbaren `origin`-Branch.

Verbindliche Tests muessen mindestens abdecken:

- Source-Branch entspricht dem Default-Branch: Checkout, kein CreateBranch,
- Source-Branch liegt im konfigurierten Repository: Checkout erfolgreich,
- PR stammt aus einem Fork bzw. der Branch fehlt unter `origin`: festgelegtes Verhalten ohne stillen Fallback,
- normale Aufgabe: bestehender Branch-Erzeugungspfad unveraendert.

### 3. Abruf und Identitaet muessen providerfest definiert werden

Der Plan muss festlegen, dass `GitRepository.RepositoryName` als kanonische API-Repository-ID und `RepositoryUrl` nur fuer Clone-/Git-Operationen verwendet wird. Die persistierte und verglichene Repository-ID ist providerbezogen zu normalisieren, damit URL-Formen oder Gross-/Kleinschreibung keine Doppelzuordnung erlauben. Der Abruf muss alle Seiten offener Pullrequests verarbeiten; ein implizites erstes API-Ergebnis bzw. ein festes Limit reicht fuer FR-1 nicht aus.

Die Filter- und Konkurrenztests muessen auch archivierte Aufgaben und Aufgaben ausserhalb der aktuell geladenen Projektliste beruecksichtigen, da der eindeutige Datenbankindex global ueber Provider, Repository und PR-Nummer gilt.

### 4. Verhalten bei fehlendem Source-Branch widerspruchsfrei festlegen

Die technischen Entscheidungen und der UI-Schritt sagen, dass bei fehlendem Source-Branch keine Aufgabe angelegt wird. Das ergaenzende E2E-Szenario verlangt dagegen, dass eine solche PR-Aufgabe erst beim Start scheitert. Der Plan muss einen Zeitpunkt fuer die Validierung festlegen und alle Service-, UI- und E2E-Szenarien daran ausrichten. Ein stiller Fallback auf einen neu erzeugten Branch bleibt in beiden Varianten ausgeschlossen.

## Erforderliche Plananpassung

Nach Ergaenzung der vier Punkte ist der Plan erneut zu pruefen. Schritt 5b bleibt bis zu einem Ergebnis mit Status `Plan vollstaendig` offen.
