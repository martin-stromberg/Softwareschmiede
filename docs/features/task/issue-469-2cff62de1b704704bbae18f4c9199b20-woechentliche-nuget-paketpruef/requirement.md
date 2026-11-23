# Übersetzte Anforderung: Wöchentliche NuGet-Paketprüfung – 2026-KW48

**Quelle:** Issue #469 / Aufgaben-ID `2cff62de-1b70-4704-bbae-18f4c9199b20`
**Branch:** `task/issue-469-2cff62de1b704704bbae18f4c9199b20-woechentliche-nuget-paketpruef`
**Basisbranch:** `staging`

## Fachliche Zusammenfassung

Wiederkehrender Wartungslauf: Alle NuGet-Abhängigkeiten der Solution `Softwareschmiede.slnx` (direkte `PackageReference`s in allen `src/`- und `plugins/`-Projekten sowie transitive Pakete) sind auf verfügbare Updates, Security-Updates, bekannt gewordene Schwachstellen, Veraltung/fehlende Pflege, empfohlene Nachfolgepakete und mögliche Breaking Changes zu prüfen. Ergebnis ist eine konkrete Update-Empfehlungsliste mit Risikobewertung. Empfehlungen mit niedrigem bis mittlerem Risiko werden direkt als Versionshebungen in den Projektdateien umgesetzt und als ein Pull Request gegen `staging` eingereicht; risikoreiche oder aufgeschobene Empfehlungen werden im PR-Body bzw. in einem eigenen Folge-Issue dokumentiert.

## Fachlicher Scope

- **Verarbeitungseinheit:** Ein *Paketprüfungslauf* — die gebündelte Analyse aller NuGet-Paketreferenzen der Solution `Softwareschmiede.slnx` zum Zeitpunkt der Bearbeitung dieses Issues (Kalenderwoche 2026-KW48). Zum Scope gehören alle direkten `PackageReference`-Einträge der 13 Projektdateien (`src/Softwareschmiede`, `src/Softwareschmiede.App`, `src/Softwareschmiede.Plugin.Contracts`, `src/Softwareschmiede.Tests`, `src/Softwareschmiede.IntegrationTests` sowie die 8 Plugin-Projekte unter `plugins/`, die aktuell selbst keine direkten `PackageReference`s deklarieren und nur transitiv über `Softwareschmiede.Plugin.Contracts` einziehen) sowie deren transitive Abhängigkeiten (für Schwachstellen- und Veraltungsprüfung, analog zum CI-Scan in `.github/actions/security-scan/action.yml` mit `--include-transitive`). Nicht zum Scope gehören: npm-Abhängigkeiten (`package.json`/`package-lock.json`, ausschließlich CI-Release-Werkzeug), GitHub-Actions-Versionen und das .NET SDK selbst (`dotnet-version: '11.0.x'` in den Workflows).
- **Auslösende Ereignisse:**
  - Externer Trigger: Zeitgesteuerter Workflow `.github/workflows/review-nuget-packages.yml` (`cron: '0 6 * * 1'`, wöchentlich montags 06:00 UTC, 2 h versetzt zu `security-scan.yml`) erstellt über den wiederverwendbaren Workflow `.github/workflows/create-review-issue.yml` das Prüf-Issue mit `@softwarewerkstatt`-Mention und Label `automated-review-nuget`; die SCM-Ereignisüberwachung der Anwendung erzeugt daraus die Aufgabe (fachlicher Vertrag: `docs/help/plugins/github-ereignisse/`).
  - Konkreter Anlass hier: Zuweisung/Bearbeitung von Issue #469.
- **Erwartete Wirkung:**
  1. Ein dokumentiertes Prüfergebnis: konkrete Update-Empfehlungen je Paket inklusive Risikobewertung (Updates, Security, Schwachstellen, Veraltung/Pflegestatus, Nachfolgepakete, Breaking Changes).
  2. Ein Pull Request gegen `staging`, der alle als niedriges bis mittleres Risiko eingestuften Empfehlungen als Versionsänderungen in den `*.csproj`-Dateien umsetzt (inkl. `dotnet restore`/`dotnet build`-Verifikation und Testlauf).
  3. Dokumentation der risikoreichen bzw. aufgeschobenen Empfehlungen im PR-Body oder — bei nachverfolgungsbedürftigem Umfang — als eigenes Folge-Issue.
- **Umgang mit fehlenden Objekten:**
  - *Keine Updates verfügbar / keine Empfehlungen:* Das Prüfergebnis ist trotzdem zu dokumentieren (Nachweis der Prüfung als Ergebnis des Laufs); ob dafür ein PR nötig ist, ist zu klären (siehe Offene Fragen) — Annahme: Dokumentation als Issue-Kommentar statt leerem PR.
  - *Keine niedrig-/mittelriskanten Empfehlungen, aber risikoreiche Befunde:* PR entfällt ggf. oder enthält nur Dokumentation; die Befunde müssen trotzdem nach Issue-Vorgabe festgehalten werden (PR-Body oder Folge-Issue — Annahme: Folge-Issue, wenn kein PR existiert).
  - *Verwundbares Paket nur transitiv vorhanden* (keine direkte Referenz zum Anheben): Strategie zu wählen — Parent-Paket anheben oder transitive Version explizit pinnen (würde eine neue direkte `PackageReference` erzeugen); zu klären (siehe Offene Fragen).
  - *Nachfolgepaket empfohlen, aber Paket noch nicht veraltet:* Empfehlung dokumentieren statt vorschnell zu migrieren — Paketwechsel ist prinzipiell risikoreicher als Versionshebung.
- **Scope-Ausschlüsse:**
  - Keine Umsetzung risikoreicher Empfehlungen: Updates mit hohem Risiko (z. B. Major-Sprünge mit dokumentierten Breaking Changes, Paketwechsel auf Nachfolgepakete) werden **nur dokumentiert**, nicht angewendet.
  - Keine Prüfung/Aktualisierung von npm-Paketen, GitHub-Actions-Versionen oder der .NET-SDK-Version — die Anforderung ist explizit auf NuGet-Pakete begrenzt.
  - Keine Änderung am Target Framework (`net11.0` / `net11.0-windows10.0.17763.0`) und keine Änderung am Prüf-Workflow selbst — die wiederkehrende Mechanik (Workflow, Issue-Erzeugung) steht nicht zur Disposition.
  - Keine Code-Refactorings über das zur Behebung von Update-Brüchen (veraltete APIs, Obsolet-Warnungen) minimal notwendige Maß hinaus.
  - Keine Einführung von Central Package Management (`Directory.Packages.props`) — Versionen bleiben dezentral in den `*.csproj`-Dateien deklariert (kein solcher Umbau in der Anforderung enthalten).

## Betroffene Klassen und Komponenten

Keine Fachklassen im eigentlichen Sinn — betroffen sind Projekt-/Infrastruktur-Artefakte:

### Zu prüfende bzw. zu ändernde Projektdateien (direkte `PackageReference`s)

- `src/Softwareschmiede/Softwareschmiede.csproj` (TFM `net11.0`): `Microsoft.EntityFrameworkCore.Sqlite` 11.0.0, `Microsoft.EntityFrameworkCore.Design` 11.0.0, `Microsoft.Extensions.Caching.Memory` 11.0.0, `Microsoft.Extensions.DependencyInjection` 11.0.0, `Microsoft.Extensions.Logging` 11.0.0, `SQLitePCLRaw.lib.e_sqlite3` 3.53.3.
- `src/Softwareschmiede.App/Softwareschmiede.App.csproj` (TFM `net11.0-windows10.0.17763.0`, WPF): `Microsoft.Extensions.DependencyInjection`/`Hosting`/`Logging`/`Logging.Console` je 11.0.0, `Serilog.Extensions.Hosting` 10.0.0, `Serilog.Sinks.File` 7.0.0, `Serilog.Sinks.Console` 6.1.1, `Microsoft.EntityFrameworkCore.Sqlite` 11.0.0, `SQLitePCLRaw.lib.e_sqlite3` 3.53.3.
- `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj` (TFM `net11.0-windows10.0.17763.0`): `FlaUI.Core`/`FlaUI.UIA3` je 5.0.0, `coverlet.collector` 10.1.1, `FluentAssertions` 8.11.0, `Microsoft.EntityFrameworkCore.InMemory` 11.0.0, `Microsoft.Extensions.TimeProvider.Testing` 11.0.0, `Microsoft.NET.Test.Sdk` 18.12.0, `Moq` 4.21.0, `xunit.v3` 4.0.3, `xunit.runner.visualstudio` 4.0.1.
- `src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj` (TFM `net11.0`): `coverlet.collector` 10.1.1, `FluentAssertions` 8.11.0, `Microsoft.EntityFrameworkCore.Sqlite` 11.0.0, `Microsoft.NET.Test.Sdk` 18.12.0, `Moq` 4.21.0, `xunit.v3` 4.0.3, `xunit.runner.visualstudio` 4.0.1.
- `src/Softwareschmiede.Plugin.Contracts/Softwareschmiede.Plugin.Contracts.csproj` (TFM `net11.0`): keine direkten `PackageReference`s.
- `plugins/*/Softwareschmiede.Plugin.*.csproj` (8 Projekte: BitBucket, ClaudeCli, Codex, Devin, GitHub, GitHubCopilot, KiSimulator, LocalDirectory): keine direkten `PackageReference`s — nur `ProjectReference` auf `Softwareschmiede.Plugin.Contracts`; betroffen nur über transitive Pakete (z. B. `Microsoft.Extensions.Logging.Abstractions`).

### Versionsgekoppelte Gruppen (müssen beim Update konsistent angehoben werden)

- **EF-Core-Familie:** `Microsoft.EntityFrameworkCore.Sqlite` (3 Vorkommen: Domain, App, IntegrationTests), `Microsoft.EntityFrameworkCore.Design` (Domain), `Microsoft.EntityFrameworkCore.InMemory` (Tests) — immer identische Versionsnummern.
- **`SQLitePCLRaw.lib.e_sqlite3`:** 2 Vorkommen (Domain, App) — identisch halten.
- **`Microsoft.Extensions.*`:** `Caching.Memory`, `DependencyInjection` (2×), `Hosting`, `Logging` (2×), `Logging.Console`, `TimeProvider.Testing` — üblicherweise auf gemeinsames .NET-Versionsniveau zu halten (Kompatibilität mit TFM `net11.0` prüfen).
- **Test-Stack:** `xunit.v3`, `xunit.runner.visualstudio`, `coverlet.collector`, `FluentAssertions`, `Moq`, `Microsoft.NET.Test.Sdk` — jeweils doppelt in Tests + IntegrationTests, identisch halten.

### Mitbetroffene Dokumentations-/CI-Artefakte

- `THIRD_PARTY_LICENSES.md` — listet ausgelieferte Pakete **mit konkreten Versionsnummern** (direkt + transitiv) und ist bei jeder Versionshebung mitzupflegen. Feststellung: Die Datei ist bereits jetzt veraltet (Stand: 2026-07-16; führt u. a. `Microsoft.EntityFrameworkCore.Sqlite` 10.0.9 statt aktuell 11.0.0, `xunit` 2.9.3 statt `xunit.v3` 4.0.3, `xunit.runner.visualstudio` 3.1.5 statt 4.0.1) — die Aktualisierung gehört fachlich zum Prüflauf, da sie per `dotnet list package --include-transitive` ermittelt wird.
- `.github/actions/security-scan/action.yml` — Referenz für den Schwachstellen-Check (`dotnet list package --vulnerable --include-transitive`); der PR-CI-Gate „Security scan" in `pr-staging-ci.yml` verifiziert das Ergebnis erneut — der PR darf keine neuen verwundbaren Pakete einführen.
- Quellcode — nur soweit betroffen, wie Updates Breaking Changes auslösen (z. B. obsolet gewordene APIs, Verhaltensänderungen in EF Core/Serilog/Test-Framework).

### Tests / Verifikation

- Nach jeder Versionshebung: `dotnet restore` + voller `dotnet build` (laut CLAUDE.md immer vor Tests), dann `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"` als stabile Lane; `Category=OsInterface` separat (E2E/ConPTY; in dieser Sandbox mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`). Besondere Aufmerksamkeit bei Updates von `FlaUI.*` (UI-Automatisierung), `xunit.v3`/`Microsoft.NET.Test.Sdk` (Testausführung), `Microsoft.EntityFrameworkCore.*` (Migrationen/Datenverhalten beim App-Start via `db.Database.MigrateAsync()`) und `Serilog.*` (Logging-Pipeline in `App.xaml.cs`).

## Implementierungsansatz

- **Bestandsaufnahme/Audit:** Für die Solution ausführen:
  - `dotnet list Softwareschmiede.slnx package --outdated --include-transitive` (verfügbare Updates),
  - `dotnet list Softwareschmiede.slnx package --vulnerable --include-transitive` (Schwachstellen — identisch zum CI-Gate, Ergebnis muss mit dem wöchentlichen `security-scan.yml`-Lauf konsistent sein),
  - `dotnet list Softwareschmiede.slnx package --deprecated` (veraltete/abgekündigte Pakete),
  - ergänzend nuget.org-/Projekt-Metadaten je Paket für Pflegestatus (letztes Release, Maintainer-Aktivität) und empfohlene Nachfolgepakete sowie Release Notes/Changelogs für Breaking-Change-Hinweise.
- **Risikobewertung je Paket** (Annahme als Klassifikationsraster, da die Anforderung keine Kriterien vorgibt): Patch-Hebungen ohne API-Änderung = niedrig; Minor-Hebungen ohne dokumentierte Breaking Changes = niedrig bis mittel; Major-Hebungen, Paketwechsel auf Nachfolgepakete, Hebungen mit dokumentierten Verhaltensänderungen oder solche, die die E2E-/ConPTY-/Publish-Kette berühren = mittel bis hoch. Security-Fixes werden innerhalb ihrer Risikoklasse priorisiert behandelt; ob ein hochriskantes Security-Update ausnahmsweise doch umgesetzt wird, ist zu klären (siehe Offene Fragen).
- **Umsetzung:** Direkte Versionsanpassung der `Version="..."`-Attribute in den `*.csproj`-Dateien (kein CPM vorhanden). Gekoppelte Gruppen immer gemeinsam anheben. Danach `dotnet restore`, vollständiger Build (dabei auf neue Warnungen/Obsolet-Meldungen achten — `TreatWarningsAsErrors` in der CI-Static-Analysis beachten), anschließend Testlauf. In einem gemeinsamen PR gegen `staging` (Anforderung: „einem Pull Request", Singular).
- **Dokumentation:** Der PR-Body enthält die vollständige Prüfungstabelle (Paket, aktuelle Version, Zielversion, Befundkategorie, Risiko, Aktion: umgesetzt/aufgeschoben) sowie die Liste nicht umgesetzter Empfehlungen mit Begründung. Umfangreiche aufgeschobene Punkte (z. B. Paketmigration) als eigenes Folge-Issue — mit Closing-/Referenz-Bezug zu Issue #469 gemäß Commit-Message-Konvention (`Closes #469` für den Umsetzungs-PR).
- **Mitpflicht bei Versionsänderung:** `THIRD_PARTY_LICENSES.md` neu ermitteln (`dotnet list package --include-transitive` je ausgeliefertem Projekt) und Versionsnummern/SPDX-Einträge aktualisieren — inklusive Bestandskorrektur der bereits veralteten Einträge.
- **Abhängigkeiten:** .NET SDK 11.x (wie in den Workflows), Zugriff auf nuget.org (Restore/Metadaten), `gh`-CLI für PR-/Issue-Erstellung. Keine neuen Interfaces, Services oder UI-Komponenten — rein deklarative Projektdatei-Änderung plus Dokumentation.

## Konfiguration

Kein Konfigurationsbedarf aus der Anforderung ableitbar. Die Laufparameter (wöchentlicher Rhythmus, Zielbranch `staging`, Issue-Body) sind bereits im Workflow `review-nuget-packages.yml` fixiert. Optional denkbar, aber nicht gefordert: ein festes Risiko-Klassifikationsraster oder eine persistierte Prüf-History im Repo (siehe Offene Fragen).

## Offene Fragen

1. **Ergebnis ohne umsetzbare Empfehlungen:** Wenn keine Updates anfallen oder alle Befunde risikoreich sind — wird trotzdem ein PR erstellt (nur Dokumentation im Body ohne Diff ist nicht sinnvoll), oder genügt ein dokumentierender Kommentar/Abschluss von Issue #469? Annahme: ohne Versionsänderungen kein PR; Prüfergebnis dann als Issue-Kommentar.
2. **Risikoklassen-Definition:** Die Anforderung nennt „niedriges bis mittleres Risiko", ohne Kriterien zu definieren. Gilt das oben vorgeschlagene Raster (Patch = niedrig, Minor = niedrig–mittel, Major/Paketwechsel = hoch) — oder sollen z. B. Security-Fixes grundsätzlich umgesetzt werden, auch bei höherem Risiko?
3. **Verwundbare transitive Pakete:** Soll bei einer nur transitiv eingezogenen verwundbaren Version bevorzugt das Parent-Paket angehoben oder die transitive Version explizit als direkte `PackageReference` gepinnt werden? Letzteres verändert die Referenzstruktur dauerhaft.
4. **PR-Body vs. Folge-Issue:** „bzw. als eigenes Folge-Issue" lässt die Wahl offen — gilt als Kriterium, dass aufgeschobene Punkte mit eigenem Umsetzungsaufwand (z. B. Migration auf ein Nachfolgepaket) ein Folge-Issue bekommen, rein informative Punkte aber im PR-Body verbleiben?
5. **`THIRD_PARTY_LICENSES.md`-Bestandskorrektur:** Die Datei ist bereits unabhängig von neuen Updates veraltet. Soll die Korrektur im selben PR erfolgen (fachlich naheliegend, gleiche Erhebungsmethode) oder als separater Befund/Folge-Issue?
6. **Persistenz des Prüfberichts:** Soll das Prüfergebnis nur im PR-Body/Issue leben (flüchtig, wie bei den anderen Review-Workflows üblich) oder zusätzlich im Repo abgelegt werden (z. B. unter `docs/`)? Annahme: nur PR-Body — die wöchentliche Kadenz würde Repo-Artefakte schnell veralten lassen.
7. **Mehrere vs. ein PR:** Die Anforderung spricht von „einem Pull Request" (Singular) — Annahme: alle unbedenklichen Updates gebündelt in einem PR, keine Aufteilung nach Paket/Risiko.
8. **EF-Core-Updates und Migrationen:** Bei einem EF-Core-Paket-Update ist zu prüfen, ob Verhaltensänderungen eine neue Migration oder Anpassungen am `SoftwareschmiededDbContext` erfordern. Fällt die Beurteilung dieser Folgeänderungen noch unter „mittleres Risiko" oder macht sie das Update automatisch risikoreich?
