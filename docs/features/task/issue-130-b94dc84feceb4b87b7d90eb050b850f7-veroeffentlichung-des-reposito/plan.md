# Umsetzungsplan: Veröffentlichung des Repositories

## Übersicht

Das Softwareschmiede-Repository wird für die öffentliche Veröffentlichung vorbereitet. Dies ist ein Governance-, Compliance- und Dokumentationsvorhaben: Es werden fehlende Community-Dokumente (`LICENSE`, `SECURITY.md`, `THIRD_PARTY_LICENSES.md`) ergänzt, bestehende Dokumente (`README.md`, `CONTRIBUTING.md`) für externe Nutzer aufbereitet, eine Sicherheits-/Secrets-Prüfung abgeschlossen und dokumentiert sowie die Lizenzkompatibilität (insbesondere die GPL-3.0-Testabhängigkeit FlaUI) sauber abgegrenzt. Zusätzlich wird das interne Token-Verbrauchsprotokoll aus dem öffentlichen Tracking-Bereich herausgelöst (Hook schreibt künftig außerhalb des Repos) und ein dauerhafter Vulnerability-Scan in die CI/CD-Pipeline integriert. Es entstehen **keine** neuen produktiven Code-Klassen, Datenmodelle, Migrationen oder Konfigurationseinträge; die einzigen quellnahen Änderungen sind die Lizenz-Hygiene an der Test-`.csproj`, die Anpassung des Logging-Hooks und ein neuer CI-Workflow-Schritt.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Lizenzwahl Hauptprojekt | **MIT-Lizenz** für `Softwareschmiede`, `Softwareschmiede.App`, `Softwareschmiede.Plugin.Contracts` | Alle ausgelieferten produktiven Abhängigkeiten sind MIT/Apache-2.0/BSD-3 (permissiv). MIT ist maximal reuse-freundlich und passt zu einem Werkzeug-/Workflow-Projekt. Die einzige Copyleft-Abhängigkeit (FlaUI, GPL-3.0) ist reine Test-/Entwicklungsabhängigkeit und wird nicht ausgeliefert (siehe Lizenz-Hygiene). Bestätigt. |
| Copyright-Zeile in `LICENSE` | Rechteinhaber **`martin-stromberg`**, Jahr **`2026`** | Entspricht dem GitHub-Repo-Owner. Bestätigt. |
| Behandlung FlaUI (GPL-3.0) | FlaUI-`PackageReference` in `Softwareschmiede.Tests.csproj` auf `<PrivateAssets>all</PrivateAssets>` setzen; in `THIRD_PARTY_LICENSES.md` als **Test-/Dev-only, nicht ausgeliefert** kennzeichnen | FlaUI-Assemblies gelangen nie in `release.zip` (nur `Softwareschmiede.App` wird published). `PrivateAssets=all` verhindert jegliche transitive Propagation und dokumentiert die Test-Only-Absicht im Buildsystem. Kein Ersatz von FlaUI nötig. |
| `THIRD_PARTY_LICENSES.md` – Umfang | Nur **ausgelieferte** direkte + transitive Laufzeit-Abhängigkeiten der published Projekte; Test-/Build-only-Pakete separat als „nicht ausgeliefert" gelistet | Die Datei dient der Compliance für die verteilte Software. Test-Werkzeuge (FlaUI, xunit, Moq, coverlet) sind für Endnutzer irrelevant, werden aber transparenzhalber in einem eigenen Abschnitt geführt. |
| Erzeugung der Lizenzliste | Ableitung über `dotnet list package --include-transitive` je Projekt, manuell mit SPDX-Kennungen angereichert (Transaction-Script-artiges einmaliges Vorgehen, kein dauerhaftes Tooling) | Anforderung verlangt eine statische Übersichtsdatei, kein wiederkehrender Build-Schritt. Ein Tool-Zwang (z. B. `nuget-license`) würde eine neue Abhängigkeit einführen, die die Anforderung nicht fordert. |
| Sprache der neuen Dokumente (`SECURITY.md` u. a.) | **Deutsch**, konsistent zu README/CONTRIBUTING/CHANGELOG | Bestehende Projektdokumentation ist durchgängig deutsch; Einheitlichkeit vor Internationalisierung (Lokalisierung ist laut Inventory ausdrücklich kein Bestandteil). Bestätigt. |
| Sicherheits-Meldeweg (`SECURITY.md`) | **GitHub Private Security Advisories** als primärer Kanal | Vermeidet die Veröffentlichung einer privaten E-Mail-Adresse und nutzt den nativen GitHub-Reporting-Flow (`Security` → `Report a vulnerability`). Bestätigt. |
| Maintainer-Struktur | **`martin-stromberg` als alleiniger Maintainer** | In `CONTRIBUTING.md` und README-Kontaktsektion vermerkt. Bestätigt. |
| Release-Channel | **Nur GitHub Releases** (bestehender `release.zip`-Flow), kein NuGet-Push | Kein NuGet-Vertrieb → `PackageLicenseExpression` in den `.csproj` ist **nicht** erforderlich und entfällt. Bestätigt. |
| Initiale öffentliche Version | Beim bestehenden **Semantic-Release-Schema bleiben** (aktuell v1.12.0), kein Reset/Sprung | Historie und Tags sind konsistent (v1.0.0–v1.12.0); Semantic Release ist etabliert. Die Doku-Commits (`docs:`) lösen ohnehin keinen Versionssprung aus. Bestätigt. |
| Git-Historie | **Keine Bereinigung, kein Force-Push** — unabhängig vom Token-Usage-Log | Es wurden keine echten Secrets gefunden; das Token-Usage-Log enthält reine Workflow-/Prozessmetriken (keine Secrets). Das Belassen der bestehenden Log-Commits in der Historie ist explizit akzeptiert. Bestätigt. |
| Umgang mit `docs/token-usage-log.csv` | **Hook-`LOG_FILE` auf repo-externen, globalen Pfad umstellen**; bestehenden Inhalt dorthin migrieren; Datei per `git rm` aus dem Arbeitsbaum entfernen und in `.gitignore` aufnehmen | Der aktive Hook `.claude/hooks/log_token_usage.py` schreibt und committet die Datei laufend. Ein reines `git rm` würde beim nächsten `Stop`-Event sofort wieder ein Tracking erzeugen. Ein repo-externer Zielpfad hält die Protokollierung für interne Auswertungen unverändert am Laufen, entkoppelt sie aber vollständig vom öffentlichen Repo und löst zugleich das im Hook dokumentierte Verlustrisiko beim Löschen eines Worktrees. |
| Vulnerability-Scanning | **Dauerhafter CI-Schritt** (`dotnet list package --vulnerable --include-transitive`) als eigener Workflow `.github/workflows/security-scan.yml` mit Triggern `pull_request`, `push` auf `main` und wöchentlichem `schedule`; Fehlschlag bei gefundenen Schwachstellen | Abweichend von einem einmaligen manuellen Lauf soll der Scan bei jedem Build und zusätzlich zeitgesteuert (fängt neue Advisories auf unveränderten Abhängigkeiten ab) laufen. Ein eigener Workflow hält die Belange getrennt von der `test.yml`-Laufzeit und erlaubt den `schedule`-Trigger ohne Einfluss auf das PR-Test-Gate. |

## Programmabläufe

Dieses Vorhaben führt keine neuen Laufzeit-Abläufe in der Anwendung ein. Die „Abläufe" sind Governance-/Build-Prozesse.

### Lizenz-Hygiene der Test-Abhängigkeit (FlaUI)

1. In `Softwareschmiede.Tests.csproj` werden die `PackageReference`-Einträge `FlaUI.Core` und `FlaUI.UIA3` um `<PrivateAssets>all</PrivateAssets>` und passende `<IncludeAssets>` ergänzt (analog zu `coverlet.collector`/`xunit.runner.visualstudio`).
2. Ein vollständiger `dotnet build` bestätigt, dass die Testkompilierung unverändert erfolgreich ist (FlaUI wird weiterhin im Testprojekt selbst genutzt, nur transitive Weitergabe wird unterbunden).
3. Das published Artefakt wird gegen die FlaUI-Assemblies geprüft: `dotnet publish` von `Softwareschmiede.App` darf keine `FlaUI.*.dll` enthalten. Ergebnis wird im internen Prüfbericht festgehalten.

Beteiligte Klassen/Komponenten: `Softwareschmiede.Tests.csproj`, `.github/actions/build-and-package/action.yml` (nur Verifikation, keine Änderung erwartet)

### Umstellung des Token-Usage-Loggings (repo-externe Persistenz)

1. Der bisherige Inhalt von `docs/token-usage-log.csv` wird an den neuen, repo-externen Zielpfad kopiert (einmalige Migration, damit keine historischen Zeilen verloren gehen).
2. In `.claude/hooks/log_token_usage.py` wird `LOG_FILE` von `os.path.join("docs", "token-usage-log.csv")` auf einen **repo-unabhängigen** Pfad umgestellt (z. B. `os.path.join(os.path.expanduser("~"), ".softwareschmiede", "token-usage-log.csv")`, optional per Umgebungsvariable überschreibbar). Das bestehende `os.makedirs(os.path.dirname(LOG_FILE), exist_ok=True)` legt das Zielverzeichnis weiterhin an.
3. Die Funktion `commit_log_file()` und ihr Aufruf im `Stop`-Zweig von `main()` entfallen, da außerhalb des Git-Baums nichts mehr zu committen ist. Die Protokollierung selbst (`sum_usage`, `writer.writerow`) bleibt unverändert.
4. `docs/token-usage-log.csv` wird per `git rm` aus dem Arbeitsbaum entfernt und als Eintrag in `.gitignore` aufgenommen, sodass ein versehentliches erneutes Tracking ausgeschlossen ist.
5. Die bestehenden Commits mit dem bisherigen Log-Inhalt verbleiben unverändert in der Historie (keine Bereinigung, siehe Designentscheidung Git-Historie).

Beteiligte Klassen/Komponenten: `.claude/hooks/log_token_usage.py`, `.gitignore`, `docs/token-usage-log.csv` (Entfernung)

### CI-Vulnerability-Scan

1. Ein neuer Workflow `.github/workflows/security-scan.yml` wird angelegt. Trigger: `pull_request` auf `main`, `push` auf `main`, `schedule` (wöchentlicher Cron).
2. Job auf `windows-latest`: Checkout, `actions/setup-dotnet@v4` (.NET 10), `dotnet restore`.
3. Scan-Schritt führt `dotnet list package --vulnerable --include-transitive` aus. Da das Kommando bei Funden **keinen** Nicht-Null-Exit-Code liefert, wird die Ausgabe ausgewertet (Muster `has the following vulnerable packages` bzw. Vorhandensein von Zeilen mit Severity) und der Schritt bei Treffern gezielt zum Scheitern gebracht (`exit 1`), sodass der Workflow rot wird.
4. Optional wird die Scan-Ausgabe als Workflow-Log/Artefakt sichtbar gemacht.

Beteiligte Klassen/Komponenten: `.github/workflows/security-scan.yml` (neu)

### Secrets- und Interna-Prüfung (Abschluss der Bestandsaufnahme)

1. Repository-weiter Musterscan auf Secrets (`key=`, `token=`, `password=`, `secret=`, Connection Strings, `.pem`/`.pfx`/`.key`) über Arbeitsbaum **und** Git-Historie (`git log -S`).
2. Scan auf hartcodierte interne Pfade (`C:\Users\`, `C:\Entwicklung\`, UNC `\\server\`) und interne Kommentarmarker (`INTERNAL:`, `CONFIDENTIAL:`, `VERTRAULICH:`) — für `src/*.cs` bereits als trefferfrei verifiziert; Scan wird auf `.ps1`, `.py`, `.json`, `.md`, `.xaml` ausgeweitet.
3. Prüfung der Testdaten in `Softwareschmiede.Tests` auf echte PII (E-Mail, Telefon, Namen).
4. Ergebnisse werden in einem **internen** Prüfbericht dokumentiert (nicht Teil des öffentlichen Repos, abgelegt unter dem Feature-Ordner). Werden im Arbeitsbaum Funde gemacht, werden sie entfernt/maskiert; eine Historien-Bereinigung erfolgt **nicht** (siehe Designentscheidung Git-Historie — es werden keine echten Secrets erwartet, nur Prozessmetriken).

Beteiligte Klassen/Komponenten: gesamter Arbeitsbaum, `.git`-Historie (nur lesend im Scan)

## Neue Klassen

Keine. Es werden ausschließlich Dokumentations-, Lizenz- und CI-/Hook-Dateien erstellt bzw. geändert. Keine `class`/`enum`/`interface`/Datenmodellklasse.

## Änderungen an bestehenden Klassen

Keine Änderungen an C#-Klassen. Die quellnahen Änderungen betreffen eine Projektdatei und ein Hook-Skript (keine Typen):

### `Softwareschmiede.Tests.csproj` (MSBuild-Projektdatei)

- **Geänderte Einträge:** `PackageReference Include="FlaUI.Core"` und `PackageReference Include="FlaUI.UIA3"` — Ergänzung um `<PrivateAssets>all</PrivateAssets>` und `<IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>`, damit die GPL-3.0-Assemblies nicht transitiv propagieren und die Test-Only-Absicht im Buildsystem festgeschrieben ist.

### `.claude/hooks/log_token_usage.py` (Python-Hook)

- **Geänderte Konstante:** `LOG_FILE` zeigt künftig auf einen repo-externen, globalen Pfad statt `docs/token-usage-log.csv`.
- **Entfernte Funktion/Aufruf:** `commit_log_file()` und dessen Aufruf im `Stop`-Zweig von `main()` entfallen (kein Git-Commit außerhalb des Repos).
- **Modulkommentar:** Der einleitende Docstring wird an die neue, repo-externe Persistenz angepasst (Beschreibung des Auto-Commits ist dann obsolet).

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine.

## Konfigurationsänderungen

Keine an `appsettings*.json` oder Konfigurationsklassen. Die Änderungen betreffen Build-/CI-/Ignore-Metadaten:

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `docs/token-usage-log.csv` in `.gitignore` | `.gitignore`-Eintrag | — | Verhindert erneutes Tracking des künftig repo-extern geführten Token-Verbrauchsprotokolls. |
| `LOG_FILE` in `.claude/hooks/log_token_usage.py` | Python-Konstante | repo-externer Pfad (z. B. `~/.softwareschmiede/token-usage-log.csv`) | Protokollierung läuft für interne Auswertungen weiter, jedoch außerhalb des öffentlichen Repos. |
| `.github/workflows/security-scan.yml` | GitHub-Actions-Workflow (neu) | Trigger: `pull_request`, `push:main`, `schedule` (wöchentlich) | Dauerhafte automatisierte Prüfung auf verwundbare NuGet-Pakete via `dotnet list package --vulnerable`. |

`PackageLicenseExpression` wird **nicht** ergänzt, da kein NuGet-Vertrieb erfolgt (Release-Channel: nur GitHub Releases).

## Seiteneffekte und Risiken

- **README-Badge:** Der Badge `License-zu%20definieren` (Zeile 11) und die Sektion „📄 Lizenz" (ab Zeile 1721) verweisen aktuell auf einen Platzhalter. Nach Ergänzung der MIT-Lizenz müssen Badge und Sektion synchron aktualisiert werden, sonst entsteht ein Widerspruch zur `LICENSE`-Datei.
- **Semantic Release / CHANGELOG:** Neue Doku-Dateien werden als `docs:`-Commits eingebracht → **kein** Versionssprung. Die Hook-Änderung (`chore:`) und der CI-Workflow (`ci:`/`chore:`) lösen ebenfalls keinen Feature-/Fix-Release aus. `.releaserc.json` bleibt unverändert.
- **FlaUI-Buildänderung:** `PrivateAssets=all` verhindert nur die *transitive Weitergabe*; die Nutzung von FlaUI *innerhalb* von `Softwareschmiede.Tests` bleibt bestehen. Risiko eines gebrochenen E2E-Builds ist gering, muss aber durch vollständigen Build + E2E-Testlauf verifiziert werden.
- **Token-Usage-Hook:** Nach der Umstellung schreibt der Hook außerhalb des Repos und committet nichts mehr. Positiver Nebeneffekt: keine `chore: Token-Verbrauchsprotokoll`-Commits mehr im Verlauf. Risiko: Läuft die Migration (Schritt: Kopie des Alt-Inhalts) nicht vor der Hook-Umstellung, entsteht am neuen Ort eine frische Datei ohne Historie — daher Reihenfolge einhalten (erst kopieren, dann `git rm`).
- **CI-Vulnerability-Scan:** Ein neuer, dauerhaft rot werdender Gate-Schritt kann PRs bei künftig gemeldeten Schwachstellen blockieren. Das ist beabsichtigt; bei Bedarf lässt sich die Fehlschlag-Semantik später auf „nur Warnung" lockern. Der `schedule`-Trigger erzeugt regelmäßige Läufe unabhängig von Commits.
- **Bestehende Log-Commits in der Historie:** Die bisherigen `chore: Token-Verbrauchsprotokoll`-Commits bleiben öffentlich sichtbar. Explizit akzeptiert (keine Secrets, nur Prozessmetriken).
- **Keine Auswirkung auf Laufzeitverhalten:** App-Start, Plugin-Discovery, DB-Zugriffe und UI bleiben funktional unberührt.

## Umsetzungsreihenfolge

1. **Secrets-, Pfad- und PII-Scan abschließen und intern dokumentieren**
   - Voraussetzungen: Keine (Arbeitsbaum + Git-Historie vorhanden).
   - Beschreibung: Musterscan auf Secrets/Connection-Strings/Key-Dateien über Arbeitsbaum und `git log -S`; Scan auf interne Pfade und Kommentarmarker über `.ps1`/`.py`/`.json`/`.md`/`.xaml` (für `src/*.cs` bereits verifiziert); PII-Prüfung der Testdaten. Ergebnis als internen Prüfbericht unter dem Feature-Ordner ablegen. Etwaige Funde im Arbeitsbaum bereinigen (keine Historien-Bereinigung).

2. **`LICENSE` anlegen (MIT)**
   - Voraussetzungen: Schritt 1 (keine verbleibenden Secrets, die eine Lizenzierung blockieren würden).
   - Beschreibung: MIT-Lizenztext mit Copyright-Zeile `Copyright (c) 2026 martin-stromberg` als `LICENSE` im Root ablegen.

3. **`THIRD_PARTY_LICENSES.md` erstellen**
   - Voraussetzungen: `LICENSE` (Schritt 2) als Referenz der Hauptlizenz; Abhängigkeitsliste via `dotnet list package --include-transitive` je Projekt.
   - Beschreibung: Übersicht aller ausgelieferten direkten + transitiven Laufzeit-Abhängigkeiten mit SPDX-Kennung erstellen; separater Abschnitt „Nur Test/Build – nicht ausgeliefert" (FlaUI GPL-3.0, xunit, Moq, coverlet, bunit, FluentAssertions, EFCore.InMemory, TimeProvider.Testing, NET.Test.Sdk, SkippableFact).

4. **Lizenz-Hygiene FlaUI in `Softwareschmiede.Tests.csproj`**
   - Voraussetzungen: `THIRD_PARTY_LICENSES.md` (Schritt 3) als Nachweis der Test-Only-Einstufung.
   - Beschreibung: `PrivateAssets=all` + `IncludeAssets` an `FlaUI.Core`/`FlaUI.UIA3` ergänzen. Anschließend vollständigen Build + Verifikation, dass `dotnet publish` von `Softwareschmiede.App` keine `FlaUI.*.dll` enthält.

5. **`SECURITY.md` erstellen (Deutsch, GitHub Private Security Advisories)**
   - Voraussetzungen: Keine (Meldeweg entschieden).
   - Beschreibung: Deutschsprachige Policy mit GitHub Private Security Advisories als primärem Meldeweg (`Security` → `Report a vulnerability`), Responsible-Disclosure-Hinweisen, unterstützten Versionen und angestrebten Reaktionszeiten. Maintainer: `martin-stromberg`.

6. **`CONTRIBUTING.md` für externe Beiträge erweitern**
   - Voraussetzungen: `SECURITY.md` (Schritt 5, Querverweis für Security-Meldungen); bestehende Commit-Konventionen bereits vorhanden.
   - Beschreibung: Bestehenden Commit-Konventions-Teil beibehalten und ergänzen: Setup/Voraussetzungen (.NET 10 Desktop-Workload), Coding-Standards (async/await, `ILogger<T>`, XML-Doku CS1591), Test-Anforderungen (Kategorien E2E/ConPTY, lokaler E2E-Lauf), Pull-Request-Prozess, Verhaltensregeln/Community-Standards, Maintainer (`martin-stromberg` als alleiniger Maintainer).

7. **`README.md` lizenz- und öffentlichkeitsreif machen**
   - Voraussetzungen: `LICENSE` (Schritt 2), `SECURITY.md` (Schritt 5), `THIRD_PARTY_LICENSES.md` (Schritt 3).
   - Beschreibung: Lizenz-Badge (Zeile 11) auf **MIT** aktualisieren; Sektion „📄 Lizenz" (ab Zeile 1721) mit Verweis auf `LICENSE` + `THIRD_PARTY_LICENSES.md` füllen; Querverweis auf `SECURITY.md`; Sektion „📬 Kontakt"/Maintainer auf `martin-stromberg` (alleiniger Maintainer) anpassen.

8. **Token-Usage-Log migrieren und Hook umstellen**
   - Voraussetzungen: Keiner der vorherigen Schritte technisch nötig; muss aber **vor** dem `git rm` in genau dieser Teilreihenfolge erfolgen.
   - Beschreibung: (a) Bestehenden Inhalt von `docs/token-usage-log.csv` an den neuen repo-externen Pfad kopieren. (b) In `.claude/hooks/log_token_usage.py` `LOG_FILE` auf den repo-externen Pfad umstellen und `commit_log_file()` samt Aufruf entfernen, Docstring anpassen. (c) `docs/token-usage-log.csv` per `git rm` entfernen und in `.gitignore` aufnehmen.

9. **Dauerhaften CI-Vulnerability-Scan integrieren**
   - Voraussetzungen: Bestehende Workflows unter `.github/workflows/` (als Vorlage für Setup-Schritte); .NET-10-Setup-Muster aus `test.yml`.
   - Beschreibung: `.github/workflows/security-scan.yml` anlegen mit Triggern `pull_request`/`push:main`/`schedule`; Schritt `dotnet list package --vulnerable --include-transitive` mit Ausgabe-Auswertung und gezieltem `exit 1` bei Funden.

10. **Interna in `docs/` und Root sichten und `.gitignore`-Verifikation**
    - Voraussetzungen: Schritt 1 (Prüfkriterien/Fundliste liegen vor), Schritt 8 (Token-Log-Ignore-Eintrag gesetzt).
    - Beschreibung: `docs/CI_CD.md`, `docs/features/`, `docs/help/` auf Interna prüfen. `docs/features/` vor „Public" sichten; im Zweifel via `.gitignore` ausschließen. `.github/workflows/*.yml` erneut auf ausschließlich `secrets.GITHUB_TOKEN` bestätigen; `.gitignore` um in Schritt 1 identifizierte interne Pfade ergänzen.

11. **Abschluss-Verifikation (Build + Tests)**
    - Voraussetzungen: Alle vorherigen Schritte.
    - Beschreibung: Vollständiger `dotnet build` und Testlauf (`SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`, ohne Backgrounding) bestätigen, dass die FlaUI-Änderung nichts bricht. Prüfen, dass der Hook nach Umstellung sauber am neuen Ort schreibt und keinen Git-Commit mehr erzeugt. Internen Prüfbericht finalisieren; Repository-Settings (Public, Branch-Protection, Aktivierung von Private Security Advisories) als menschliche Nachfolgeaufgabe vermerken.

## Tests

Dieses Vorhaben ändert keinen produktiven Anwendungscode und führt keine neue Benutzerinteraktion ein. Es sind daher **keine neuen fachlichen Unit-/Integration-/BUnit-Tests** erforderlich. Die relevante technische Verifikation ist der unveränderte Grün-Status der bestehenden Suite nach der FlaUI-`.csproj`-Änderung sowie die manuelle Verifikation von Hook- und CI-Änderung.

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| Keine | — | Keine neue testbare Programmlogik. Verifikation über bestehende Suite, manuelle Publish-Artefaktprüfung (keine `FlaUI.*.dll` im Publish), manuelle Hook-Prüfung (Schreiben am neuen Pfad, kein Commit) und CI-Workflow-Lauf. |

### Betroffene bestehende Tests

Keine inhaltliche Anpassung. Die gesamte E2E-Suite (`Category="E2E"`) ist von der FlaUI-`PrivateAssets`-Änderung potenziell betroffen und muss zur Absicherung einmal vollständig laufen — jedoch **ohne Codeänderung** an den Testklassen.

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| Keine (nur Verifikationslauf) | FlaUI-`PrivateAssets`-Änderung darf Testkompilierung/-ausführung nicht beeinträchtigen; Nachweis durch einmaligen vollständigen E2E-Lauf, keine Signaturänderung. |

### E2E-Tests (Pflicht)

Es entsteht keine neue oder geänderte Benutzerinteraktion (rein dokumentarisches/organisatorisches Vorhaben). Ein neuer E2E-Test ist daher fachlich nicht ableitbar; die Pflicht greift mangels neuem Happy-Path nicht.

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Keine neue Benutzerinteraktion | — | Statt E2E: vollständiger Build + bestehender E2E-Lauf grün nach FlaUI-Änderung; Publish enthält keine GPL-Assembly. |

Betroffene bestehende E2E-Tests: Keine (nur Verifikationslauf, keine Codeänderung).

## Offene Punkte

Keine. Alle zuvor offenen Punkte (Lizenzwahl, Copyright-Zeile, initiale Version, Git-Historie, Security-Meldeweg, Maintainer-Struktur, Release-Channel, Umgang mit `docs/token-usage-log.csv` und `docs/features/`, Sprache `SECURITY.md`, Vulnerability-Scanning) sind geklärt und im Plan eingearbeitet.
