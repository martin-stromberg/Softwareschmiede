# Plan-Check: Überwachung von GitHub

Status: Plan lückenhaft

Datum: 2026-10-10  
Geprüfter Branch: `task/issue-276-307b8bcce93a4413b82bcd8ccab0027b-ueberwachung-von-github`

## Prüfgrundlage

Geprüft wurden `requirement.md`, `inventory.md`, beide Inventardetails und `plan.md`. Kritische Annahmen wurden zusätzlich mit dem vorhandenen GitHub-Plugin, der Plugin-Auswahl, den Aufgaben-/Entwicklungsprozessdiensten, der Terminal-Eingabe, dem WPF-Auswahldialog und der E2E-Infrastruktur abgeglichen. Dies ist ein Plan-Check; Produktcode wurde nicht geändert, Implementierungstests wurden nicht ausgeführt.

Die vom Nutzer bereits bestätigten Entscheidungen sind verbindliche Prüfgrundlage: Polling für lokal verknüpfte GitHub-Kontexte, authentifizierter GitHub-Nutzer als hinreichende Owner-Identität, case-insensitiver Trigger, vollständiger neuer Beschreibungstext ohne Empfangsreaktion, Warte-Kommentar sowie keine automatische Reaktivierung beendeter/archivierter Aufgaben. Diese Entscheidungen werden nicht erneut infrage gestellt. Sie präzisieren beziehungsweise ändern die ursprünglichen Akzeptanzkriterien; die Abnahme muss diese Präzisierungen berücksichtigen.

## Ergebnis

Der Plan enthält die richtige fachliche Grundstruktur und benennt alle Hauptbereiche. Für eine verlässliche Implementierung fehlen jedoch verbindliche Regeln an den Übergängen zwischen Polling, Datenbank, externen Schreiboperationen und CLI sowie konkrete Integrations- und Testpfade. Die Zusagen „genau einmal“, „ohne verlorene Ereignisse“ und „verarbeitet“ sind derzeit stärker als der geplante technische Nachweis.

Die folgenden acht Befunde sind vor dem Planungscommit zu schließen. P1 bezeichnet eine Lücke, die Kernverhalten oder Zuverlässigkeit gefährdet; P2 eine für Umsetzung und Abnahme erforderliche Konkretisierung. Ein Befund ist geschlossen, wenn Entscheidung, betroffene Integration und passender Nachweis im Plan stehen; vollständiger Implementierungscode ist dafür nicht erforderlich.

## Befunde

### PC-01 – P1: Polling-Ereignisse und Beschreibungsversionen sind nicht hinreichend definiert

**Bezug:** Leitentscheidungen 1, 2 und 4; Arbeitsschritte 1 und 2.

Der Plan verwendet eine „GitHub-Ereignis-ID“, benennt aber keine konkrete Pollingquelle und deren Abbildung auf Ereignisse. Bei Kommentarabfragen liefern die Endpunkte Kommentarobjekte mit ID, Inhalt, Autor und Änderungszeit; normale PR-Kommentare und Review-Kommentare haben unterschiedliche API-Bereiche. Ein fachlicher Ereignisschlüssel und der unterstützte Kommentarumfang müssen daraus ausdrücklich abgeleitet werden. Die Kommentarabfragen sind zudem paginiert. [GitHub-Dokumentation zu Issue- und PR-Kommentaren](https://docs.github.com/en/rest/issues/comments)

Für Beschreibungen reicht der geplante Schlüssel aus Remote-Referenz und Änderungszeit nicht aus, um eine tatsächliche Textänderung nachzuweisen. Der Plan muss eine persistierte vorherige Textversion beziehungsweise einen Inhaltsvergleich und die Autor-/Bearbeiterzuordnung vorsehen. Sonst kann die Implementierung Änderungen anderer Metadaten als neue Aufträge behandeln oder fremde Bearbeitungen einem berechtigten ursprünglichen Autor zurechnen. Die bestätigte Owner-Identität ersetzt nicht die Prüfung des tatsächlichen Ereignisautors.

**Erforderliche Ergänzung:** Konkrete Quellen und DTO-Felder, Kommentar-/Beschreibungsschlüssel mit Provider-/Repository-Namensraum, Umgang mit bearbeiteten Kommentaren, erstmaliger Beobachtung und initial bereits vorhandenem Trigger festlegen. Initialbeschreibung und spätere Beschreibung unterscheiden; neue Referenzen nach bereits fortgeschrittenem Repository-Cursor berücksichtigen. Für nicht zuverlässig zuordenbare Bearbeitungen eine eindeutige Behandlung festlegen. Falls Polling nur zuletzt sichtbare Textstände erfassen kann, diese Grenze benennen; keine unbelegte Erfassung sämtlicher Zwischenversionen versprechen.

**Nachweis:** Issue und PR; Fremdautor bei gültiger Owner-Anmeldung; identischer Text mit geänderter Metadatenzeit; echte Textänderung; initialer Trigger; später hinzugefügte Referenz; wiederholter Poll; Kommentaränderung; voneinander unabhängige Provider-/Repository-Schlüssel.

### PC-02 – P1: Transaktionsgrenzen, Nebenläufigkeit und ungewisse Zustellung fehlen

**Bezug:** Leitentscheidung 4; Arbeitsschritte 2 bis 4; Definition of Done.

Persistente Queue, Cursor und eindeutiger Index sind genannt, aber deren Zusammenspiel ist offen. Werden Cursor vor Ereignissen gespeichert, können Ereignisse verloren gehen. Werden 👀, Warte-Kommentar oder CLI-Prompt erfolgreich gesendet und die lokale Bestätigung anschließend nicht gespeichert, führt blindes Wiederholen zu externen Duplikaten. Eine gespeicherte Remote-Kommentar-ID allein löst einen Absturz vor dem Speichern dieser ID nicht. Ebenso schützt ein eindeutiger Ereignisschlüssel nicht vor zwei Verbrauchern desselben vorhandenen Datensatzes.

**Erforderliche Ergänzung:** Ein dauerhaftes Eingangsprotokoll für alle gültigen Ereignisse einschließlich sofort weitergeleiteter Ereignisse, atomare Speicherung von Aufnahme und Cursor, vollständige Pagination sowie überlappende Zeitgrenzen definieren. Zustandsübergänge, atomaren Claim/Lease, Wiederaufnahme verwaister Claims und Serialisierung pro Aufgabe planen. Empfangsbestätigung, Wartehinweis und Promptzustellung benötigen getrennte Zustände/Versuche. Für Abstürze nach externem Erfolg eine Wiedererkennung oder einen sichtbaren Zustand „Zustellung ungewiss“ festlegen. Die absolute Genau-einmal-Zusage muss durch ein bestätigtes Protokoll abgesichert oder auf eine konkret erreichbare Garantie präzisiert werden.

Backoff, nächste Ausführung, Authentifizierungsfehler gegenüber temporären Netzwerk-/Rate-Limit-Fehlern sowie manuelle Wiederaufnahme müssen nachvollziehbar definiert sein; dauerhaft ungültige Credentials dürfen keine schnelle Endlosschleife verursachen.

**Nachweis:** Mehrseitige Antwort mit Fehler auf Folgeseite; gleiche Zeitstempel; Absturz vor/nach Datenbankcommit, Reaktion, Warte-Kommentar und Promptversand; parallel eintreffende Ereignisse derselben Aufgabe; Polling gegen gleichzeitigen Queue-Doppelklick; Wiederanlauf mit bestehendem Claim. Test mit echter SQLite-Persistenz und Migration einer bestehenden Datenbank.

### PC-03 – P1: Vorhandene Plugin-Auflösung kann den Fall „keine Standard-CLI“ umgehen

**Bezug:** Arbeitsschritt 3, Standard-KI-Konfiguration; E2E 1 und 2.

`PluginSelectionService.ResolveDevelopmentAutomationPluginAsync` fällt über `ResolvePluginAsync` auf ein beliebiges verfügbares Plugin zurück, wenn kein gespeicherter Standard passt. Dagegen liefert `ResolveDevelopmentAutomationPluginWithProjectScopeAsync` nach Aufgaben-, Projekt- und Globalauswahl gegebenenfalls `null`. Der Plan verweist pauschal auf „existierende Plugin-Auswahl“, obwohl diese Pfade für die verpflichtende Queue-Entscheidung verschiedenes Verhalten erzeugen.

**Erforderliche Ergänzung:** Präzedenz von Aufgabenwahl, Projektstandard und Globalstandard sowie Bedeutung von „verfügbar“ ausdrücklich festlegen. Der automatische SCM-Pfad muss fehlende, deaktivierte, entfernte und nicht startbare Standard-CLIs erkennen und die Queue erreichen; ein stiller Fallback darf die bestätigte Auswahlentscheidung nicht ersetzen. Konkreten Resolver und Aktivierungs-/Verfügbarkeitsprüfung benennen.

**Nachweis:** Installiertes Plugin ohne gesetzten Standard; deaktivierter oder entfernter Standard; ungültiges Executable; Projekt- gegen Globalstandard; vorhandene laufende CLI trotz fehlendem Standard. In den Queue-Fällen kein automatischer Ersatzstart.

### PC-04 – P1: Arbeitsbereich, Start und Übergabe an laufende Sitzungen bleiben Untersuchungsaufträge

**Bezug:** Arbeitsschritt 3; `inventory/tasks-cli.md`.

Der Bestand besitzt bereits `KiAusfuehrungsService.GetTerminalSession` und `ITerminalSession.WritePromptAsync`; `TaskDetailViewModel.PromptVorlageAuswaehlenAsync` nutzt diesen Weg. Die Aussage im Inventar, dass kein expliziter Übergabepfad erkennbar sei, ist daher zu präzisieren. Im Plan bleibt gerade dieses Kernverhalten mit „untersuchen“ offen.

`StartTerminalSessionAsync` setzt einen geeigneten lokalen Arbeitsbereich voraus. `EntwicklungsprozessService.ProzessStartenUndCliStartenAsync` übernimmt dagegen Klon/Branch und enthält einen Rollback bei Startfehlern; `CliNeustartenAsync` setzt einen vorhandenen Klon voraus und beachtet Pausen. Nur den fachlichen Status zu setzen und den Terminalstarter aufzurufen reicht für eine neue Aufgabe nicht.

**Erforderliche Ergänzung:** Einen gemeinsamen Application-Einstieg mit folgenden konkreten Verzweigungen planen: vorhandene lebende Sitzung wiederverwenden; neue Aufgabe mit Arbeitsbereich vorbereiten; bestehenden Klon wiederverwenden; Startbereitschaft der CLI vor Promptübergabe sicherstellen. Laufende/arbeitende CLI, bereits beendete oder während des Schreibens ausfallende Sitzung und mehrere wartende Ereignisse müssen geordnet behandelt werden. Bestehende Pausen, Start-Rollback und manuelle Parallelstarts dürfen nicht umgangen werden. `Wartend` als fachlichen Aufgabenstatus, Queue-Warten und CLI-Eingabebereitschaft auseinanderhalten. DI-/Hosted-Service-Registrierung und Service-Scopes im WPF-Host benennen.

**Nachweis:** Neue Aufgabe ohne Klon; vorhandener Klon; pausierte Aufgabe; Prozessende zwischen Lookup und Write; Startprozess ohne Eingabebereitschaft; zwei Ereignisse für dieselbe Aufgabe; CLI bleibt nach Ereignisbearbeitung verfügbar und Aufgabe aktiv. Rollback verändert keinen bereits verwendeten Arbeitsbereich.

### PC-05 – P1: „Verarbeitet“ wird nicht von „an CLI übergeben“ unterschieden

**Bezug:** Leitentscheidung 6; Arbeitsschritt 3; E2E 2; Akzeptanzkriterium Prozessende.

E2E 2 lässt den Eintrag nach dem Senden auf „verarbeitet“ wechseln. `PseudoConsoleSession.WritePromptAsync` bestätigt jedoch ausschließlich Schreiben/Flush von Eingabebytes. Ein laufender Prozess oder dessen Laufzeitstatus weist für sich keinen erfolgreichen Abschluss genau dieses Ereignisprompts nach. Das ist insbesondere bei einer bereits arbeitenden CLI und mehreren Ereignissen relevant.

**Erforderliche Ergänzung:** Definieren, welches beobachtbare Signal den Ereignislauf beendet und wie es mit Ereignis und CLI-Lauf korreliert wird. Queue-Zustände mindestens semantisch zwischen aufgenommen/wartend, zugestellt und durch KI verarbeitet unterscheiden. Falls der vorhandene CLI-Vertrag keinen Verarbeitungsnachweis ermöglicht, diese Grenze ausdrücklich behandeln und keine erfolgreiche KI-Verarbeitung allein aus `WritePromptAsync` ableiten. CLI-Exit, Fehler, Timeout und fehlende Abschlussmeldung dürfen weder die Aufgabe automatisch abschließen noch einen ungeprüften erneuten Versand auslösen.

**Nachweis:** Eingabe angenommen, KI arbeitet noch; Abschluss desselben Ereignisses bei weiterlaufender CLI; nachfolgender Prompt; Fehler nach Eingabeannahme; kein automatischer Wechsel der Aufgabe auf `Beendet`.

### PC-06 – P1: Rückantwort im Ursprung ist nur als Prompttext geplant

**Bezug:** Arbeitsschritt 3; Konsequenzen der Bestandsaufnahme zum Antwortkontext.

Der Plan verlangt von der KI eine GitHub-Antwort, plant aber keinen überprüfbaren Zugriff dafür. Im Bestand stellt `GitHubPlugin.GetGhEnvironment` das Credential nur seinen eigenen `gh`-Aufrufen bereit. Der Terminalstart baut seine Umgebung aus der KI-Plugin-Spec auf; daraus folgt keine automatische Verfügbarkeit derselben GitHub-Anmeldung in der KI-Sitzung. Ein Ursprungshinweis allein stellt diese Fähigkeit nicht her.

**Erforderliche Ergänzung:** Einen unterstützten Rückantwortkanal einschließlich Berechtigungsprüfung und Ursprungskontext festlegen, etwa vorhandene authentifizierte Werkzeuge oder eine kontrollierte SCM-Werkzeugintegration. Neue und bereits laufende Sitzungen berücksichtigen; bei fehlender Fähigkeit einen sichtbaren, wiederaufnehmbaren Zustand vorsehen. Secrets gehören weder in Prompt, Queue-Inhalt, CLI-Argumente noch Protokolle. Ereignistext muss als Text über sichere Argument-/Eingabeschnittstellen transportiert werden; Steuerzeichen dürfen nicht als Terminalbefehle wirken. Programmseitige Warte-Kommentare dürfen selbst keinen neuen Auftrag auslösen.

**Nachweis:** KI kann über den vorgesehenen Kanal die richtige Issue-/PR-Referenz adressieren; fehlende Schreibberechtigung und vorhandene Sitzung ohne passenden Zugriff; keine Token in protokollierter Umgebung/Argumenten/Prompt; mehrzeiliger Text, Quotes und Steuerzeichen; keine Selbsttrigger durch den Wartehinweis. Dafür isolierte Fakes verwenden, keine unbeauftragten Schreibtests in echten Repositories.

### PC-07 – P2: Der Queue-Benutzerfluss ist noch nicht konkret implementierbar

**Bezug:** Arbeitsschritt 4; UI-E2E 2 bis 4.

„UI-Einstieg ergänzen“ benennt weder Navigationsort noch zuständige View/ViewModel/Commands. Die vorhandene `TaskDetailViewModel.ResolvePluginViaDialogAsync` überspringt bei genau einem Plugin den Dialog; Queue-Öffnen verlangt ausdrücklich einen Auswahldialog. `PluginSelectionDialogService` beziehungsweise `IDialogService.ShowPluginSelectionDialogAsync` ist der konkrete wiederverwendbare Dialogweg.

E2E 4 verweist auf eine spätere explizite Reaktivierung, die in den Arbeitsschritten nicht enthalten ist. `AufgabeService.SetStatusAsync` erlaubt aus `Beendet`/`Archiviert` keinen Übergang auf `Gestartet`; ein nutzbarer Reaktivierungsablauf ist in den geprüften UI-Pfaden nicht belegt. Die bestätigte Sperre ist korrekt, der behauptete Auflösungsweg jedoch noch offen.

**Erforderliche Ergänzung:** WPF-Navigation, Queue-Ansicht, Öffnen/Starten-Commands, Dialog, sichtbare Status-/Fehlertexte und Aktualisierungsbenachrichtigung benennen. Abbruch, keine auswählbare CLI, ein einzelnes Plugin, Startfehler und Doppelklick konkret behandeln: Eintrag bleibt erhalten und wieder öffnbar. Festlegen, was bei einer inzwischen laufenden CLI geschieht. Für abgeschlossene/archivierte Aufgaben entweder den expliziten Reaktivierungsablauf vollständig planen oder den gesperrten Zustand ehrlich ohne unbelegten Verweis darstellen. Gegebenenfalls verwendete UI-Aktionen benötigen einen Handler in der tatsächlichen WPF-View.

**Nachweis:** Navigation von der Hauptansicht bis zum Queue-Eintrag und zurück; Dialog auch mit genau einem Plugin; Abbrechen und später erneut öffnen; keine CLI verfügbar; sichtbarer Startfehler mit Wiederholung; kein zweiter Start durch Doppelklick; gesperrte abgeschlossene/archivierte Aufgabe.

### PC-08 – P2: E2E-Ausführung und vollständige Abnahmematrix fehlen

**Bezug:** Arbeitsschritt 5; vier E2E-Szenarien; Definition of Done.

Die vier Szenarien bilden hauptsächlich den Queue-Erfolgsweg ab. Es fehlt ein konkreter Testzugang, der ein GitHub-Ereignis durch die neue Pipeline in den separaten WPF-App-Prozess bringt und den tatsächlich empfangenen Prompt sowie ausgehende Reaktionen nachweist. Der vorhandene `WpfTestBase` verwendet isolierte Testdatenbanken; der dokumentierte E2E-Testmodus stellt bisher nur das LocalDirectory-SCM bereit. Ein direkt vorab eingefügter Queue-Datensatz würde die Aufnahme/Orchestrierung nicht prüfen.

**Erforderliche Ergänzung:** Testdateien/-klassen und ausführbare Testgruppen benennen. Eine ausschließlich im isolierten Testmodus registrierte Ereignisquelle beziehungsweise einen GitHub-API-Fake samt Protokoll ausgehender Aufrufe vorsehen, kombiniert mit bestehendem CLI-Simulator/Promptaufzeichnung. Den echten Queue-Dialog, Host-Registrierung, Persistenz und Application-Ablauf durchlaufen. Issue und PR sowie Kommentar und Beschreibung als Varianten abdecken. Fehlende/nicht gelaufene UI-E2Es bleiben Abnahmefehler; vorhandene Unit-Tests ersetzen sie nicht.

Zusätzlich zu PC-01 bis PC-07 sind die automatischen Pfade „vorhandene CLI“ und „Standard-CLI starten“ durch Integrationstests nachzuweisen. E2E mindestens um Dialogabbruch, Fehler/Wiederholung und Neustart mit wartendem Eintrag ergänzen. Die im Plan schon verlangten Reaktions-, Authentifizierungs- und Rate-Limit-Tests sind auf konkrete erwartete Nebenwirkungen auszurichten, einschließlich keiner zusätzlichen Abschlussreaktion und keinem Warte-Kommentar bei ungültigem Ereignis.

**Nachweis:** Eine Zuordnung jedes Akzeptanzkriteriums zu Test und Testebene, konkrete Start-/Testbefehle für den Windows-WPF-Kontext, Altbestandsmigration, zweiter Provider ohne Ereignisfähigkeit, erfolgreiche Ausführung der verbindlichen E2E-Szenarien. Kein echter GitHub-Zugriff und kein Benutzercredential als Voraussetzung der automatisierten Tests.

## Bereits ausreichend verankert

- Providerneutrale Ereignisverträge und optionale Erweiterbarkeit bestehender SCM-Plugins.
- Gemeinsamer Orchestrator, persistente Queue und eindeutiger Ereignisschlüssel als Grundstruktur.
- Bestätigte Trigger-/Beschreibung-/Wartehinweisregeln und Verzicht auf automatische Abschlussreaktionen.
- Trennung zwischen fachlichem Aufgabenstatus und CLI-Lauf; kein automatischer Abschluss der Aufgabe.
- Eigene Unit-, Plugin-, Orchestrator-, Persistenz- und UI-E2E-Prüfungen als Testebenen.

## Freigabebedingung

`plan.md` ist anhand PC-01 bis PC-08 zu konkretisieren und anschließend erneut zu prüfen. Die bereits bestätigten fachlichen Entscheidungen benötigen keine erneute Freigabe. Routinemäßige Integrationsentscheidungen sind anhand des Bestands zu treffen. Eine Einschränkung zugesagten Fachverhaltens oder eine unbelegbare Genau-einmal-/Verarbeitungszusage darf dagegen nicht stillschweigend als implementiert vorausgesetzt werden.
