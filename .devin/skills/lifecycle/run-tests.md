# Tests ausführen

Führe alle automatisierten Tests des Projekts aus, miss die Testabdeckung und melde Dateien ohne Tests.

**Ziel:** Regressionssicherheit nach der Implementierung prüfen — keine Code-Analyse, keine inhaltliche Bewertung, nur Testergebnisse und Abdeckungslücken.

---

## Schritt 1: Test-Runner ermitteln

Prüfe die vorhandenen Projektdateien und leite daraus den passenden Befehl ab:

| Datei vorhanden | Test-Befehl | Coverage-Befehl |
|-----------------|-------------|-----------------|
| `*.sln` oder `*.csproj` | `dotnet test --no-build` | `dotnet test --no-build --collect:"XPlat Code Coverage"` |
| `package.json` mit `scripts.test` | `npm test -- --passWithNoTests` | `npm test -- --passWithNoTests --coverage` |
| `pytest.ini`, `pyproject.toml`, `setup.cfg` oder `conftest.py` | `pytest -v` | `pytest -v --cov --cov-report=term-missing` |
| `pom.xml` | `mvn test -q` | `mvn test jacoco:report -q` (nur wenn Jacoco konfiguriert) |
| `build.gradle` oder `build.gradle.kts` | `./gradlew test` | `./gradlew test jacocoTestReport` |
| `Cargo.toml` | `cargo test` | `cargo tarpaulin --out Stdout` (nur wenn installiert) |
| `go.mod` | `go test ./...` | `go test -cover ./...` |

Ist kein Test-Runner erkennbar, gib im Ergebnis Status `Kein Test-Runner gefunden` aus und brich ab.

## Schritt 2: Tests ausführen

Versuche zunächst den **Coverage-Befehl**. Schlägt dieser fehl (Tool nicht installiert, Plugin fehlt), führe stattdessen den normalen Test-Befehl aus und vermerke `Abdeckung: Nicht messbar`.

- Bei `dotnet test`: Verwende zusätzlich `--logger "console;verbosity=normal"` für strukturierte Ausgabe.
- Bei `pytest`: `--cov` erfordert `pytest-cov`; fehlt es, fällt der Befehl zurück auf `pytest -v`.
- Schlägt der Befehl mit einem Prozessfehler fehl (kein Kompilierfehler, sondern z. B. nicht installierte Abhängigkeiten), halte den Fehlertext im Ergebnis fest und setze Status `Ausführungsfehler`.

## Schritt 3: Fehlende Tests ermitteln

Werte die Coverage-Ausgabe aus. Falls keine Coverage-Daten vorhanden sind, suche stattdessen nach Quelldateien ohne korrespondierende Testdatei:

**Aus Coverage-Daten (bevorzugt):** Liste alle Quelldateien mit 0 % Zeilenabdeckung auf.

**Fallback ohne Coverage:** Für jede Quelldatei (z. B. `src/foo/bar.py`, `src/Foo/Bar.cs`, `internal/foo/bar.go`) prüfe, ob eine Testdatei mit passendem Namen existiert (z. B. `test_bar.py`, `BarTests.cs`, `bar_test.go`). Fehlt sie, gilt die Datei als ungetestet.

Ignoriere dabei generierte Dateien, Konfigurationsdateien und Einstiegspunkte (z. B. `Program.cs`, `main.go`, `__init__.py`).

## Schritt 4: Ergebnis ausgeben

Speichere das Ergebnis als `docs/features/{branchname}/test-results.md`:

```
# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler | Fehler vorhanden | Ausführungsfehler | Kein Test-Runner gefunden

## Fehlgeschlagene Tests

Nur ausfüllen, wenn Status „Fehler vorhanden".

### {TestSuite / Dateiname}

- **{Testname}** — {Fehlermeldung in einer Zeile}

## Zusammenfassung

- Gesamt: {n}
- Bestanden: {n}
- Fehlgeschlagen: {n}
- Übersprungen: {n}

## Testabdeckung

**Abdeckung:** {n} % | Nicht messbar

| Datei | Abdeckung |
|-------|-----------|
| {Dateiname} | {n} % |

## Fehlende Tests

Nur ausfüllen, wenn Dateien mit 0 % Abdeckung oder ohne Testdatei gefunden wurden.
Quelle: `Coverage-Daten` | `Dateinamen-Konvention`

- `{Quelldatei}` — {Grund: „0 % Abdeckung" oder „Keine Testdatei gefunden"}
```

---

## Hinweise

- Nur fehlgeschlagene Tests auflisten — bestandene Tests nicht einzeln aufführen.
- Fehlermeldungen auf eine Zeile kürzen; Stack-Traces weglassen.
- In der Abdeckungstabelle nur Dateien unter 80 % aufführen; Dateien mit 100 % weglassen.
- Das Ergebnis muss maschinell lesbar sein: Der Status in der ersten Zeile von „## Ergebnis" ist exakt einer von: `Keine Fehler`, `Fehler vorhanden`, `Ausführungsfehler`, `Kein Test-Runner gefunden`.
