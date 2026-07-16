# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

Der Plan ist ein Governance-/Dokumentations-/CI-Vorhaben ohne neue produktive Code-Klassen,
Datenmodelle, Migrationen, Validierungsregeln oder neue Tests. Alle im Plan und in der Tasks-Datei
aufgeführten Planelemente sind im Repository auffindbar und vollständig umgesetzt.

## Umgesetzte Planelemente

### Neue Dateien (Governance/Dokumentation/CI)
- [x] `LICENSE` — angelegt (MIT, `Copyright (c) 2026 martin-stromberg`)
- [x] `SECURITY.md` — angelegt (Deutsch; GitHub Private Security Advisories; unterstützte Versionen; Responsible Disclosure mit 5-Werktage-Reaktionszeit; Maintainer `martin-stromberg`)
- [x] `THIRD_PARTY_LICENSES.md` — angelegt (Abschnitt „Ausgelieferte Abhängigkeiten" mit SPDX; Abschnitt „Nur Test/Build – nicht ausgeliefert" inkl. FlaUI GPL-3.0-or-later; eigener FlaUI-Einordnungsabschnitt)
- [x] `.github/workflows/security-scan.yml` — angelegt (Trigger `pull_request`, `push:main`, `schedule` wöchentlich; Scan-Schritt `dotnet list package --vulnerable --include-transitive` mit Ausgabe-Auswertung und `exit 1` bei Funden; Scan-Log als Artefakt)
- [x] `docs/.../pruefbericht-secrets-pii.md` — interner Prüfbericht (Secrets-/Pfad-/PII-Scan Arbeitsbaum + Historie; Abschluss-Verifikation; menschliche Nachfolgeaufgaben)

### Geänderte Dateien (quellnah)
- [x] `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj` — `FlaUI.Core`/`FlaUI.UIA3` um `<PrivateAssets>all</PrivateAssets>` und `<IncludeAssets>` ergänzt (mit `compile`, damit die E2E-Testkompilierung nicht bricht)
- [x] `.claude/hooks/log_token_usage.py` — `LOG_FILE` auf repo-externen Pfad (`~/.softwareschmiede/token-usage-log.csv`, per `SOFTWARESCHMIEDE_TOKEN_LOG_FILE` überschreibbar); `commit_log_file()` samt Aufruf entfernt; Modul-Docstring an repo-externe Persistenz angepasst
- [x] `.gitignore` — Eintrag `docs/token-usage-log.csv` (Zeile 383)
- [x] `docs/token-usage-log.csv` — per `git rm` aus dem Arbeitsbaum entfernt (`git status`: `D`); Inhalt nach `~/.softwareschmiede/token-usage-log.csv` migriert
- [x] `README.md` — Lizenz-Badge auf `License-MIT` (Zeile 11); Sektion „📄 Lizenz" verweist auf `LICENSE` + `THIRD_PARTY_LICENSES.md` + `SECURITY.md`; Sektion „📬 Kontakt"/Maintainer auf `martin-stromberg`
- [x] `CONTRIBUTING.md` — erweitert um Setup/Voraussetzungen (.NET 10 Desktop-Workload), Coding-Standards (async/await, `ILogger<T>`, XML-Doku CS1591), Test-Anforderungen (E2E/ConPTY, lokaler E2E-Lauf), Pull-Request-Prozess, Community-Standards, Maintainer

### Verifikationsschritte (laut Prüfbericht)
- [x] Build nach FlaUI-Änderung — 0 Fehler
- [x] `dotnet publish` von `Softwareschmiede.App` enthält keine `FlaUI.*.dll`
- [x] Nicht-E2E-Testlauf (`Category!=E2E&Category!=ConPTY`) — 942/942 bestanden
- [x] Hook schreibt am neuen Pfad, kein Git-Commit mehr (externe Logdatei wächst weiter)
- [x] Repository-Settings (Public, Branch-Protection, Private Security Advisories, E2E-Lauf) als menschliche Nachfolgeaufgabe vermerkt

### Bestätigt „Keine"-Elemente aus dem Plan
- [x] Neue Klassen / Enums / Interfaces — keine (plankonform)
- [x] Datenbankmigrationen — keine (plankonform)
- [x] Validierungsregeln — keine (plankonform)
- [x] `appsettings*.json`-Konfigurationsänderungen — keine (plankonform)
- [x] Neue Tests — keine (plankonform, Verifikation über bestehende Suite)
- [x] `PackageLicenseExpression` — bewusst nicht ergänzt (kein NuGet-Vertrieb)

## Offene Aufgaben

Keine. Alle 29 Aufgaben der Tasks-Datei sind umgesetzt.

## Hinweise

- **E2E-Suite (`Category=E2E`) im Sandbox nicht ausgeführt:** Der Prüfbericht dokumentiert transparent,
  dass der vollständige lokale E2E-Lauf wegen des Self-Hosting-Risikos (`CLAUDE.md`) nicht im
  Agenten-Kontext gelaufen ist und als menschliche Nachfolgeaufgabe verbleibt. Da die FlaUI-Änderung
  ausschließlich `PrivateAssets`/`IncludeAssets` betrifft (keine Verhaltensänderung des Testcodes)
  und Kompilierung + alle Nicht-E2E-Tests grün sind, ist das Restrisiko gering — der Plan selbst
  fordert für diesen Punkt ohnehin nur einen Verifikationslauf ohne Codeänderung.
- **Reine GitHub-Repository-Einstellungen** (Public schalten, Branch-Protection, Aktivierung Private
  Security Advisories) sind nicht per Commit umsetzbar und im Prüfbericht als menschliche
  Nachfolgeaufgaben festgehalten — planvorgesehen (Schritt 11).
- **Testnachweise:** Für dieses überwiegend dokumentarische Vorhaben existieren keine fachlichen
  Anwendungstests; die Tasks-Datei führt daher „Kein direkter Test" mit Verweis auf das jeweils
  belegende Artefakt (Prüfbericht, Datei-/Zeilennachweis). Einzige buildseitig abgesicherte Aufgabe
  ist die FlaUI-`.csproj`-Änderung (Nachweis: unveränderte Kompilierung der E2E-Testklassen).
