# Testergebnisse

**Status: Keine Fehler**

Branch: `task/issue-410-c69c8e7f6eb14c4cbe407bdbdc9e1cd6-ausfuehrungsbegrenzung-schritt-4-issue-body-update`
Umgebung: Windows, .NET 10, `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` auf jedem
`dotnet test`-Aufruf gesetzt; alle Läufe synchron im Vordergrund, jeweils nach
vollständigem Build (kein `--no-build`).

## Builds

- `dotnet build Softwareschmiede.slnx` (vollständig, inkl. App und beider Testprojekte): **0 Warnungen, 0 Fehler**
- `dotnet format Softwareschmiede.slnx --verify-no-changes`: **sauber** (Exit 0)

## Reguläre Lane

`dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"`

- **2661 erfolgreich, 0 Fehler, 3 übersprungen, 2664 gesamt** (3 m 13 s)
- Übersprungen: 3 betriebssystemabhängige Fälle (`SystemShutdownServiceTests` Linux/macOS,
  `ArbeitsverzeichnisOeffnenServiceTests` Nicht-Windows) — bekannte, plattformbedingte Skips.
- Darin enthalten: 13 neue Fälle in `Infrastructure/Plugins/GitHubEventSourceTests.cs`
  (`IssueBodyUpdate_Execute_PatchesDescriptionWithMarker`, `IssueBodyUpdate_AlreadyApplied_*`,
  `IssueBodyUpdate_Reconcile_*`, `IssueBodyUpdate_RequiresIssueReferenceAndBody` als Theory,
  `IssueBodyUpdate_Failures_TypedProviderErrors` als Theory über 401/403/429/500/404,
  `IssueBodyUpdate_RoutesByReferenceKonto` als Theory Benutzerkonto/SeparatesKonto u. a.) —
  belegen PATCH-Endpunkt, Marker im Body, fehlendes `title`-Feld, keinen zweiten Schreibzugriff
  nach Abgleich, Validierung und die typisierte Fehlerabbildung.

## Integrationstests

`dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj`

- Erstlauf: **335 erfolgreich, 1 Fehler, 336 gesamt** —
  `ScmEventDispatchTests_Startpruefung.StartPruefung_UeberlappendeAusloeser_KeinDoppelstart`
  meldete `Wartend` statt `Zugestellt` für das erste Ereignis.
- Nachweis der Flüchtigkeit: derselbe Test isoliert **bestanden**, die komplette
  Startprüfung-Klasse isoliert **7/7 bestanden**, Zweitlauf der Gesamtsuite
  **336 erfolgreich, 0 Fehler**. Der Ausfall entstand nur unter der Parallel-Last
  des Gesamtlaufs (timingempfindlicher Schritt-3-Test); der Branch-Diff kann den
  Pfad nicht verändern — für diese Ereignisse existiert ausschließlich die
  `receipt`-Aktion, die bereits zuvor vom Hostaktions-Sweep erfasst wurde.
- 5 neue Fälle in `Services/ScmEventDispatchTests_IssueBodyUpdate.cs`
  (Erfolg, deterministische Aktions-ID, Abgleich ohne Doppelschreiben, Sperrfehler
  mit `RetryAsync`-Heilung, temporärer Fehler mit Backoff-Wiederholung).

## Echtes E2E (OsInterface)

`dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "FullyQualifiedName~General_BasisUndEinstellungen_E2E"`

- **1 erfolgreich, 0 Fehler** (1 m 8 s) — startet die echte WPF-App samt geändertem
  `ScmEventQueueViewModel` und der SCM-Hosted-Service-Verdrahtung; ein DI- oder
  Startfehler würde den App-Start brechen — kein Befund.

Ein neues dediziertes FlaUI-Szenario ist nicht anwendbar: Der Schritt ändert keinen
Bedienfluss — die neue Antwort-Art wird noch von keinem Aufrufer erzeugt (folgt in
Schritt 6); nachweisbar ist sie ausschließlich über den Outbox-Pfad, den die
Integrationstests mit skriptbarer API vollständig durchspielen (Anlage, Ausführung,
Abgleich, Sperren, Wiederholung). Ein realer GitHub-Aufruf ist für diesen Schritt
ausdrücklich nicht gefordert.

## Fehlerhistorie während der Umsetzung

Keine. Alle fokussierten Läufe bestanden beim ersten Versuch; der einzige
Gesamtlauf-Ausfall (s. o.) erwies sich als vorhandene, nicht von diesem Diff
verursachte Zeitabhängigkeit.
