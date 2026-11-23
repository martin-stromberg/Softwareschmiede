# Umsetzungsplan: Wöchentliche NuGet-Paketprüfung – 2026-KW48 (Issue #469)

## Übersicht

Wartungslauf ohne UI-/Codeänderungen: Zwei verfügbare direkte Paketupdates werden als
Versionshebungen in den `*.csproj`-Dateien umgesetzt — `Serilog.Extensions.Hosting`
10.0.0 → 11.0.0 (Major, `Softwareschmiede.App`) und `Microsoft.Extensions.TimeProvider.Testing`
11.0.0 → 11.0.1 (Patch, `Softwareschmiede.Tests`). Begleitend wird die nachweislich veraltete
`THIRD_PARTY_LICENSES.md` auf den aktuell aufgelösten Paketstand gebracht. Ergebnis ist ein
einzelner Pull Request gegen `staging`, dessen Body die vollständige Prüfungstabelle inklusive
Risikobewertung und die nicht umgesetzten/transitiven Befunde dokumentiert.

## Scope-Klärung

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Ein *Paketprüfungslauf* für `Softwareschmiede.slnx` (direkte `PackageReference`s der 4 Projekte mit Paketen + transitive Pakete), Stand Erhebung 2026-11-23 / Commit `cef41f6e` |
| Auslösende Ereignisse | Wöchentlicher Workflow `review-nuget-packages.yml` → Issue #469; Bearbeitung dieser Aufgabe |
| Erwartete Wirkung | PR gegen `staging` mit 2 Versionshebungen + aktualisierter `THIRD_PARTY_LICENSES.md`; PR-Body enthält Prüfungstabelle und aufgeschobene/nicht umsetzbare Empfehlungen |
| Umgang mit fehlenden Objekten | *Keine Schwachstellen/Deprecated-Pakete vorhanden:* nichts zu tun, Befund „sauber" im PR-Body dokumentieren. *Nur transitiv veraltete Pakete:* folgen automatisch dem Parent-Update bzw. werden als nicht separat hebbar dokumentiert (kein Pinning als neue direkte `PackageReference`). *Kein stabiles Update verfügbar (nur Prerelease):* nicht anwenden, dokumentieren |

### Scope-Ausschlüsse

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | Keine Umsetzung risikoreicher Empfehlungen; risikoreiche Befunde werden nur im PR-Body dokumentiert | Anforderung, fachlicher Scope | bestätigt (aus `requirement.md`) |
| 2 | Keine Prüfung/Aktualisierung von npm-Paketen, GitHub-Actions-Versionen oder .NET-SDK-Version | Anforderung explizit auf NuGet begrenzt | bestätigt (aus `requirement.md`) |
| 3 | Keine Änderung am Target Framework (`net11.0` / `net11.0-windows10.0.17763.0`) und keine Änderung am Prüf-Workflow | Wiederkehrende Mechanik steht nicht zur Disposition | bestätigt (aus `requirement.md`) |
| 4 | Keine Code-Refactorings über das zur Behebung von Update-Brüchen minimal notwendige Maß hinaus | Anforderung | bestätigt (aus `requirement.md`) |
| 5 | Keine Einführung von Central Package Management (`Directory.Packages.props`) | Versionen bleiben dezentral in den `*.csproj` | bestätigt (aus `requirement.md`) |
| 6 | Kein Pinning transitiver Pakete als neue direkte `PackageReference` | Verändert Referenzstruktur dauerhaft; hier ohnehin kein verwundbares Transitivum vorhanden | bestätigt (2026-11-23, konservative Annahme — automatisierter Lauf, Anwender-Feedback via Issue-Kommentare) |
| 7 | Prerelease-Versionen (z. B. `SQLitePCLRaw.lib.e_sqlite3` 3.53.4.1-pre1, `Serilog.Sinks.File` 8.0.0-dev-*) werden nicht installiert | Wartungslauf hebt nur auf stabile Versionen | bestätigt (2026-11-23, konservative Annahme — automatisierter Lauf, Anwender-Feedback via Issue-Kommentare) |

### Getroffene Annahmen (Auflösung der offenen Fragen aus `requirement.md`)

Automatisierter Lauf — die 8 offenen Fragen der Anforderung werden fachlich begründet selbst
beantwortet und hier festgehalten:

| # | Frage | Getroffene Annahme |
|---|-------|--------------------|
| 1 | Ergebnis ohne umsetzbare Empfehlungen | Kein PR ohne Versionsänderungen — Prüfergebnis dann als Issue-Kommentar. *Hier nicht zutreffend:* 2 Updates sind umsetzbar, PR wird erstellt. |
| 2 | Risikoklassen-Definition | Raster aus der Anforderung gilt: Patch = niedrig; Minor ohne Breaking Changes = niedrig–mittel; Major = mittel–hoch, wobei ein Major-Update **mit dokumentierten Breaking Changes** hoch ist, ein Major-Update, das nur ein TFM-/Versions-Alignment ohne API-Änderung darstellt, mittel. Paketwechsel auf Nachfolgepakete = hoch. Security-Fixes werden innerhalb ihrer Klasse priorisiert (keine vorhanden). |
| 3 | Verwundbare transitive Pakete | Bevorzugt Parent-Paket anheben; explizites Pinning nur als Fallback. *Hier nicht zutreffend:* keine Schwachstellen vorhanden. |
| 4 | PR-Body vs. Folge-Issue | Rein informative aufgeschobene Punkte → PR-Body; nur Punkte mit eigenem Umsetzungsaufwand → Folge-Issue. *Hier:* alle dokumentierten Befunde sind informativ (kein Folge-Issue nötig). |
| 5 | `THIRD_PARTY_LICENSES.md`-Bestandskorrektur | Im selben PR — fachlich naheliegend, gleiche Erhebungsmethode (`dotnet list package --include-transitive`), und die Datei muss bei Versionshebungen ohnehin mitgepflegt werden. |
| 6 | Persistenz des Prüfberichts | Nur im PR-Body (plus Issue-Bezug) — wöchentliche Kadenz würde Repo-Artefakte veralten lassen; konsistent mit anderen Review-Workflows. |
| 7 | Mehrere vs. ein PR | Ein gemeinsamer PR gegen `staging` für alle unbedenklichen Updates (Anforderung: „einem Pull Request", Singular). |
| 8 | EF-Core-Updates und Migrationen | *Nicht zutreffend:* kein EF-Core-Update verfügbar. Grundsatz für künftige Läufe: ein EF-Core-Update, das eine neue Migration oder `DbContext`-Anpassung erfordert, wird als risikoreich eingestuft und nur dokumentiert. |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|------------------|------------|
| `Serilog.Extensions.Hosting` 10.0.0 → 11.0.0 | Umsetzen (Risiko: mittel) | Release-Changelog (serilog/serilog-extensions-hosting, Release v11.0.0 / PR #105) zeigt ausschließlich .NET-11-Alignment: neue `net11.0`-TFMs, Entfernung net8/net9, Abhängigkeiten auf `Microsoft.Extensions.*` 11.0.0 sowie `Serilog` 4.4.0 / `Serilog.Extensions.Logging` 11.0.0 gehoben — keine API-Breaking-Changes. Das Paket versioniert explizit nach `Microsoft.Extensions.Hosting` („choose the version matching your target framework"); für TFM `net11.0` ist 11.0.0 die **vorgesehene** Version, die App nutzt bereits `Microsoft.Extensions.*` 11.0.0. Einzige Nutzungsstelle ist `.UseSerilog()` in `App.xaml.cs` — Signatur unverändert. |
| `Microsoft.Extensions.TimeProvider.Testing` 11.0.0 → 11.0.1 | Umsetzen (Risiko: niedrig) | Reine Patch-Hebung; deklarierte Abhängigkeit unverändert (`Microsoft.Bcl.TimeProvider` ≥ 8.0.1). Nutzung nur über `TestTimeProvider.cs` (Test-Hilfsklasse). |
| Transitive Updates | Automatisch mitführen, nicht separat pinnen | `Serilog` 4.3.0 → 4.4.0 und `Serilog.Extensions.Logging` 10.0.0 → 11.0.0 folgen dem `Serilog.Extensions.Hosting`-Update; dadurch hebt sich dasselbe Transitivum auch im Paketgraph von `Softwareschmiede.Tests` (via `ProjectReference` auf App). |
| Aufgeschobene/nicht umsetzbare Befunde | Nur im PR-Body dokumentieren, kein Folge-Issue | Alle verbleibenden Befunde sind informativ (kein eigener Umsetzungsaufwand): `SQLitePCLRaw.lib.e_sqlite3` ohne stabiles Update, transitive Test-/Dev-Time-Updates, die nur über Parent-Updates erreichbar sind. |

## Programmabläufe

### Paketupdate mit Verifikation

1. In `src/Softwareschmiede.App/Softwareschmiede.App.csproj` (Zeile 57) `Serilog.Extensions.Hosting` auf `11.0.0` heben; in `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj` (Zeile 33) `Microsoft.Extensions.TimeProvider.Testing` auf `11.0.1` heben.
2. `dotnet restore Softwareschmiede.slnx` — aufgelöste Transitiva prüfen (`Serilog` 4.4.0, `Serilog.Extensions.Logging` 11.0.0).
3. Voller `dotnet build Softwareschmiede.slnx` (kein `--no-build`); anschließend Verifikation mit `-p:TreatWarningsAsErrors=true` analog zum CI-Gate — neue Obsolet-/Compiler-Warnungen würden den PR-Build brechen. Bei Fehlschlag: minimal notwendige Codekorrektur oder Update zurücknehmen und im PR-Body als risikoreich dokumentieren.
4. Stabile Test-Lane: `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"` — Erwartung: 2800 erfolgreich / 0 fehlgeschlagen / 3 OS-übersprungen (Vorher-Nachweis aus `inventory/tests.md`).
5. `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj` — Erwartung: 436 erfolgreich / 0 fehlgeschlagen.
6. `dotnet list Softwareschmiede.slnx package --vulnerable --include-transitive` — muss sauber bleiben (CI-Gate `security-scan`).

Beteiligte Artefakte: `Softwareschmiede.App.csproj`, `Softwareschmiede.Tests.csproj`, `App.xaml.cs` (`UseSerilog`), `TestTimeProvider.cs`

### `THIRD_PARTY_LICENSES.md` aktualisieren

1. Paketgraph neu erheben: `dotnet list package --include-transitive` für die ausgelieferten Projekte (`Softwareschmiede`, `Softwareschmiede.App`; Plugin-Projekte und `Plugin.Contracts` ziehen keine NuGet-Pakete) sowie `Softwareschmiede.Tests` / `Softwareschmiede.IntegrationTests` für die Test-/Build-Sektion.
2. Abschnitt „Ausgelieferte Abhängigkeiten": alle `Microsoft.*`-/EF-Core-Einträge von 10.0.9 auf 11.0.0, `SQLitePCLRaw.*` auf die tatsächlich aufgelösten Versionen (`lib.e_sqlite3` 3.53.3, `core`/`bundle_e_sqlite3`/`provider.e_sqlite3`/`config.e_sqlite3` 3.0.5, natives `SQLite` 3.53.4 ergänzen), Serilog-Zeilen auf die neuen Versionen (`Serilog` 4.4.0, `Serilog.Extensions.Hosting` 11.0.0, `Serilog.Extensions.Logging` 11.0.0; `Serilog.Sinks.*` unverändert).
3. Abschnitt „Nur Test/Build": `xunit` 2.9.3 → `xunit.v3` 4.0.3 (inkl. `xunit.v3.*`-Transitiva/`xunit.analyzers` 2.3.0, soweit aufgeführt), `xunit.runner.visualstudio` → 4.0.1, `Xunit.SkippableFact`-Zeile entfernen (nicht mehr referenziert), `Microsoft.NET.Test.Sdk` → 18.12.0, `coverlet.collector` → 10.1.1, `FluentAssertions` → 8.11.0, `Microsoft.EntityFrameworkCore.InMemory`/`.Design` → 11.0.0, `Microsoft.Extensions.TimeProvider.Testing` → 11.0.1, `Moq` → 4.21.0; fehlende relevante Transitiva ergänzen (`Interop.UIAutomationClient` 10.19041.0 via FlaUI, `Castle.Core` 5.2.1 via Moq, `Microsoft.Bcl.TimeProvider`, `Microsoft.Testing.*`-Familie 2.5.x — letztere ggf. summarisch wie die `Microsoft.CodeAnalysis.*`-Familie).
4. SPDX-Kennungen für neu aufgenommene Pakete ergänzen; Stand-Datum im Kopf von „2026-07-16" auf das Umsetzungsdatum setzen.

Beteiligte Artefakte: `THIRD_PARTY_LICENSES.md`

### PR-Erstellung und Abschluss

1. Commit(s) mit Versionshebungen + Lizenzdatei; Commit-Message enthält `Closes #469` (Closing-Keyword, damit das Issue bei Promotion nach `main` automatisch schließt).
2. PR gegen `staging` erstellen; Body enthält: Prüfungstabelle (Paket, aktuell → Ziel, Kategorie, Risiko, Aktion), Befund „keine Schwachstellen / keine veralteten Pakete", Liste nicht umgesetzter Empfehlungen mit Begründung (siehe „Aufgeschobene Empfehlungen" unten), Hinweis auf die mitkorrigierte `THIRD_PARTY_LICENSES.md`.
3. PR-Checks mit `gh pr checks <nr> --watch --fail-fast` überwachen; bei `fail` sofort `gh run view --job <id> --log-failed` diagnostizieren (Security-scan, Format-Gate, TreatWarningsAsErrors-Build, Test-Lanes inkl. `OsInterface`).

Beteiligte Artefakte: Git-Branch, `gh`-CLI, `.github/workflows/pr-staging-ci.yml`

## Neue Klassen

Keine.

## Änderungen an bestehenden Klassen

Keine Fachklassen betroffen. Geänderte Dateien:

### `src/Softwareschmiede.App/Softwareschmiede.App.csproj`

- `PackageReference` `Serilog.Extensions.Hosting`: `Version="10.0.0"` → `Version="11.0.0"` (Zeile 57). Zieht transitiv `Serilog` 4.4.0 und `Serilog.Extensions.Logging` 11.0.0 nach (auch im Paketgraph von `Softwareschmiede.Tests` via `ProjectReference`).

### `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj`

- `PackageReference` `Microsoft.Extensions.TimeProvider.Testing`: `Version="11.0.0"` → `Version="11.0.1"` (Zeile 33).

### `THIRD_PARTY_LICENSES.md`

- Bestandskorrektur aller veralteten Versionsangaben (siehe Programmablauf oben) + Mitpflege der durch die beiden Updates geänderten Versionen + neues Stand-Datum.

### Aufgeschobene Empfehlungen (nur PR-Body, keine Umsetzung)

- `SQLitePCLRaw.lib.e_sqlite3` 3.53.3 → natives `SQLite` 3.54.1 nicht erreichbar: kein stabiles Paket-Update (nur Prerelease `3.53.4.1-pre1`); Paketpflege hinkt hinterher — beobachten.
- `Microsoft.ApplicationInsights` 2.23.0 → 3.2.0: nur transitiv (Test-Telemetrie via `Microsoft.Testing.Extensions.Telemetry`), Major-Sprung; folgt einem künftigen `Microsoft.NET.Test.Sdk`-Update.
- `Microsoft.Testing.Platform*`/`Microsoft.Testing.Extensions.*` 2.5.2 → 2.5.3: transitiv, folgt `Microsoft.NET.Test.Sdk`/`xunit.v3`.
- `Microsoft.CodeAnalysis.*` 5.9.0 → 5.12.0, `Microsoft.Build.*` 18.9.6 → 18.12.2, `System.Composition.*`/`System.CodeDom`/`Microsoft.Bcl.AsyncInterfaces`/`System.Management`/`System.Diagnostics.EventLog`: reine Dev-Time-/Test-Transitiva, hebbar nur über Parent-Updates (`Microsoft.EntityFrameworkCore.Design` bzw. Test-Stack) — kein stabiles Parent-Update verfügbar.
- `Serilog.Sinks.File` 7.0.0 → 8.0.0-dev-*: nur Prereleases, keine stabile Version.
- `Microsoft.Bcl.AsyncInterfaces` 6.0.0 → 11.0.0, `System.Management` 8.0.0 → 11.0.0, `System.Diagnostics.EventLog` 6.0.0 → 11.0.0: Test-Transitiva, kein direkter Hebel.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine.

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **Build-Gate `TreatWarningsAsErrors`:** Das `Serilog.Extensions.Hosting`-Update könnte neue Obsolet-Warnungen einführen; die PR-CI baut mit `-p:TreatWarningsAsErrors=true`. Mitigation: lokaler Build mit derselben Option vor dem Push; bei nicht minimal behebbaren Warnungen Update zurücknehmen und als risikoreich dokumentieren.
- **`Softwareschmiede.Tests` Paketgraph:** Durch die `ProjectReference` auf `Softwareschmiede.App` zieht das Testprojekt die Serilog-Transitiva mit — das Update verändert also indirekt auch die Test-Laufzeitumgebung. Die stabile Lane deckt `App`-bezogene Tests ab.
- **`OsInterface`-Lane ohne lokalen Vorher-Nachweis:** FlaUI-Pakete bleiben unverändert (5.0.0), daher ist das Risiko gering; die Lane wird in der PR-CI verifiziert. Lokal läuft sie in dieser Sandbox nicht (ConPTY-/Desktop-Einschränkungen laut CLAUDE.md).
- **Selbsthosting-Risiko:** `dotnet build` kann auf einen Datei-Lock durch eine laufende `Softwareschmiede.App.exe` treffen (MSB3026/MSB3027). Kein Prozess darf beendet werden — stattdessen Teil-Build ohne App-Projekt bzw. Anwender informieren (Regel aus CLAUDE.md).
- **Security-Scan-Gate:** Der PR darf keine verwundbaren Pakete einführen — wird durch Schritt 6 des Update-Ablaufs und das CI-Gate doppelt abgesichert.

## Umsetzungsreihenfolge

1. **Versionshebungen in den `*.csproj`-Dateien**
   - Voraussetzungen: Keine (nur deklarative Änderung).
   - Beschreibung: `Serilog.Extensions.Hosting` → 11.0.0 in `Softwareschmiede.App.csproj`; `Microsoft.Extensions.TimeProvider.Testing` → 11.0.1 in `Softwareschmiede.Tests.csproj`. Gekoppelte Gruppen prüfen: Serilog-Direktreferenzen (`Sinks.File`/`Sinks.Console`) haben kein Update, EF-Core- und Test-Stack-Gruppen sind aktuell — keine weiteren Hebungen nötig.

2. **Restore + voller Build inkl. Warnungsprüfung**
   - Voraussetzungen: Schritt 1; .NET SDK 11.x; NuGet-Zugriff; keine blockierende `Softwareschmiede.App.exe`-Instanz (bei MSB3026/MSB3027: Anwender informieren, nichts beenden).
   - Beschreibung: `dotnet restore Softwareschmiede.slnx`, dann `dotnet build Softwareschmiede.slnx -c Debug -p:TreatWarningsAsErrors=true`. Erwartung: 0 Warnungen / 0 Fehler.

3. **Testläufe**
   - Voraussetzungen: Schritt 2 (voller Build vor Tests, niemals `--no-build`); `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` inline gesetzt; synchroner Vordergrund-Lauf (kein Backgrounding von `dotnet test`).
   - Beschreibung: stabile Lane `Category!=OsInterface` auf `Softwareschmiede.Tests` (Soll: 2800/0 fail/3 skip) und kompletter Lauf `Softwareschmiede.IntegrationTests` (Soll: 436/0/0). Jeder Fehlschlag ist ein neuer Befund (Ausgangszustand war sauber).

4. **Schwachstellen-Verifikation**
   - Voraussetzungen: Schritt 1 (Restore-Graph aktuell).
   - Beschreibung: `dotnet list Softwareschmiede.slnx package --vulnerable --include-transitive` — Output darf „has the following vulnerable packages"/„Severity" nicht enthalten (identisch zum CI-Gate).

5. **`THIRD_PARTY_LICENSES.md` aktualisieren**
   - Voraussetzungen: Schritt 1 (finaler Paketgraph).
   - Beschreibung: siehe Programmablauf — komplette Bestandskorrektur (nicht nur Delta der zwei Updates), Stand-Datum anpassen.

6. **Commit + PR gegen `staging` mit Dokumentation**
   - Voraussetzungen: Schritte 1–5 grün; `gh`-CLI verfügbar.
   - Beschreibung: Commit-Message mit `Closes #469`; ein PR; Body mit Prüfungstabelle, Risikobewertung, aufgeschobenen Empfehlungen mit Begründung, Lizenzdatei-Hinweis.

7. **PR-Checks überwachen**
   - Voraussetzungen: Schritt 6.
   - Beschreibung: `gh pr checks <nr> --watch --fail-fast`; bei `fail` sofort in Diagnose (`gh run view --job <id> --log-failed`).

## Tests

### Neue Tests

Keine — die Anforderung ändert kein Verhalten und keinen Code; Paketversionen werden durch Build und bestehende Suites abgesichert.

### Betroffene bestehende Tests

Keine — keine Signatur- oder Verhaltensänderung erwartet. Falls das Serilog-Update entgegen der Changelog-Analyse doch ein Verhalten ändert (z. B. Log-Ausgabeformat), betroffene Tests wären ein neuer Befund und im Rahmen des minimal notwendigen Fixes zu behandeln.

### E2E-Tests (primärer Funktionsnachweis)

Keine erforderlich. Begründung: Der Wartungslauf berührt keinen über UI oder Nutzeraktion erreichbaren Ablauf — es ändern sich ausschließlich Paketversionen und eine Dokumentationsdatei. Es gibt keinen neuen oder geänderten Benutzerfluss, dessen Happy Path per E2E nachgewiesen werden müsste. Der Funktionsnachweis erfolgt über (a) den vollständigen Build inkl. `TreatWarningsAsErrors`, (b) die bestehenden stabilen Test-Lanes, die die betroffenen Laufzeitpfade (`UseSerilog`-Logging-Pipeline beim App-Start, `TestTimeProvider`-Nutzung) mit abdecken, und (c) die PR-CI, die zusätzlich die `OsInterface`-Lane (FlaUI-E2E) ausführt.

Angepasste bestehende E2E-Tests: Keine.

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | Happy Path: Updates umgesetzt, PR erstellt | Bearbeitung von Issue #469 | `Serilog.Extensions.Hosting` 10.0.0, `TimeProvider.Testing` 11.0.0; `THIRD_PARTY_LICENSES.md` veraltet (Stand 2026-07-16); Build + stabile Lanes vorher sauber (Vorher-Nachweis vorhanden) | Versionshebungen → Restore/Build (0 Warnungen) → Testläufe grün → Schwachstellen-Scan sauber → Lizenzdatei aktualisiert → PR gegen `staging` | PR mit Versions-Diff in 2 `*.csproj` + aktualisierter `THIRD_PARTY_LICENSES.md`; PR-Body enthält Prüfungstabelle und dokumentierte aufgeschobene Empfehlungen; CI-Checks grün | Build-/Test-Logs, `gh pr checks` |
| 2 | Update bricht Build oder Tests | Schritt 2/3 schlägt fehl (z. B. neue Obsolet-Warnung unter `TreatWarningsAsErrors`) | Wie Szenario 1; PR noch nicht erstellt | Ursache diagnostizieren → minimal notwendige Codekorrektur; falls nicht minimal lösbar: betreffendes Update zurücknehmen (Revert der `Version=`-Zeile) und im PR-Body als risikoreich/aufgeschoben dokumentieren | PR enthält nur die unbedenklichen Änderungen; der zurückgenommene Punkt ist im Body mit Begründung gelistet | Build-Log; PR-Body |
| 3 | Update führt verwundbares Paket ein | Schritt 4 meldet „vulnerable"/„Severity" | Wie Szenario 1 | Betreffendes Update zurücknehmen; Befund als risikoreiches Update im PR-Body dokumentieren (Strategie: Parent anheben — hier nicht möglich — oder Pinning, als Folgepunkt benennen) | PR-Body benennt die Schwachstelle inkl. betroffenem Paket/Pfad | `dotnet list package --vulnerable`-Output |
| 4 | Kein umsetzbares Update (Kontrast-Szenario, hier nicht zutreffend) | Audit meldet 0 veraltete direkte Referenzen | Alle Pakete aktuell | Kein PR; Prüfergebnis als Kommentar an Issue #469 | Issue-Kommentar mit „keine Updates nötig" + Scan-Nachweis | Issue-Kommentar |

## Offene Punkte

Keine — alle 8 offenen Fragen der Anforderung wurden per fachlich begründeter Annahme geklärt (Abschnitt „Getroffene Annahmen"). Keine blockierenden Entscheidungen verbleiben.
