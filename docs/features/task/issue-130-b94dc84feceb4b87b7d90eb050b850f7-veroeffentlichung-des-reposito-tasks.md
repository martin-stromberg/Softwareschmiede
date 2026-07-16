# Tasks: Veröffentlichung des Repositories

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Sicherheit | Secrets-Scan über Arbeitsbaum + Git-Historie (`git log -S`: `key=`, `token=`, `password=`, `secret=`, Connection Strings, `.pem`/`.pfx`/`.key`) durchführen | Offen | — |
| 2 | Sicherheit | Scan auf interne Pfade (`C:\Users\`, `C:\Entwicklung\`, UNC) und Marker (`INTERNAL:`/`CONFIDENTIAL:`/`VERTRAULICH:`) über `.ps1`/`.py`/`.json`/`.md`/`.xaml` | Offen | — |
| 3 | Sicherheit | Testdaten in `Softwareschmiede.Tests` auf echte PII (E-Mail, Telefon, Namen) prüfen | Offen | — |
| 4 | Sicherheit | Internen Prüfbericht (Secrets/Pfade/PII) unter dem Feature-Ordner ablegen; etwaige Funde im Arbeitsbaum bereinigen (keine Historien-Bereinigung) | Offen | — |
| 5 | Lizenzierung | `LICENSE` (MIT) mit Copyright-Zeile `Copyright (c) 2026 martin-stromberg` im Root anlegen | Offen | — |
| 6 | Lizenzierung | Abhängigkeitsliste je Projekt via `dotnet list package --include-transitive` erzeugen | Offen | — |
| 7 | Lizenzierung | `THIRD_PARTY_LICENSES.md` mit ausgelieferten Abhängigkeiten + SPDX-Kennungen erstellen | Offen | — |
| 8 | Lizenzierung | Abschnitt „Nur Test/Build – nicht ausgeliefert" (FlaUI GPL-3.0 u. a.) in `THIRD_PARTY_LICENSES.md` ergänzen | Offen | — |
| 9 | Build/Lizenz-Hygiene | `PrivateAssets=all` + `IncludeAssets` an `FlaUI.Core`/`FlaUI.UIA3` in `Softwareschmiede.Tests.csproj` ergänzen | Offen | — |
| 10 | Build/Lizenz-Hygiene | Verifizieren, dass `dotnet publish` von `Softwareschmiede.App` keine `FlaUI.*.dll` enthält | Offen | — |
| 11 | Dokumentation | `SECURITY.md` (Deutsch) mit GitHub Private Security Advisories als Meldeweg, Disclosure-Policy, unterstützten Versionen, Reaktionszeiten erstellen | Offen | — |
| 12 | Dokumentation | `CONTRIBUTING.md` um Setup/Voraussetzungen (.NET 10 Desktop-Workload) erweitern | Offen | — |
| 13 | Dokumentation | `CONTRIBUTING.md` um Coding-Standards (async/await, `ILogger<T>`, XML-Doku CS1591) erweitern | Offen | — |
| 14 | Dokumentation | `CONTRIBUTING.md` um Test-Anforderungen (Kategorien E2E/ConPTY, lokaler E2E-Lauf) erweitern | Offen | — |
| 15 | Dokumentation | `CONTRIBUTING.md` um PR-Prozess, Community-Standards und Maintainer (`martin-stromberg`, alleinig) erweitern | Offen | — |
| 16 | Dokumentation | README-Lizenz-Badge (Zeile 11) auf MIT aktualisieren | Offen | — |
| 17 | Dokumentation | README-Sektion „📄 Lizenz" mit Verweis auf `LICENSE` + `THIRD_PARTY_LICENSES.md` füllen | Offen | — |
| 18 | Dokumentation | README-Sektionen „📬 Kontakt"/Maintainer (`martin-stromberg`) + Querverweis auf `SECURITY.md` aktualisieren | Offen | — |
| 19 | Token-Usage-Log | Bestehenden Inhalt von `docs/token-usage-log.csv` an repo-externen Zielpfad kopieren (Migration vor Entfernung) | Offen | — |
| 20 | Token-Usage-Log | `LOG_FILE` in `.claude/hooks/log_token_usage.py` auf repo-externen Pfad umstellen, `commit_log_file()` + Aufruf entfernen, Docstring anpassen | Offen | — |
| 21 | Token-Usage-Log | `docs/token-usage-log.csv` per `git rm` entfernen und in `.gitignore` aufnehmen | Offen | — |
| 22 | CI/CD | `.github/workflows/security-scan.yml` (neu) mit Triggern `pull_request`/`push:main`/`schedule` anlegen | Offen | — |
| 23 | CI/CD | Scan-Schritt `dotnet list package --vulnerable --include-transitive` mit Ausgabe-Auswertung und `exit 1` bei Funden implementieren | Offen | — |
| 24 | Interna | `docs/CI_CD.md`, `docs/features/`, `docs/help/` auf Interna sichten; Entscheidung (entfernen/ignorieren) umsetzen | Offen | — |
| 25 | Interna | `.github/workflows/*.yml` erneut auf ausschließlich `secrets.GITHUB_TOKEN` bestätigen | Offen | — |
| 26 | Interna | `.gitignore` um in der Sichtung identifizierte interne Pfade ergänzen | Offen | — |
| 27 | Verifikation | Vollständigen Build + Testlauf (`SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`, ohne Backgrounding) nach FlaUI-Änderung grün bestätigen | Offen | — |
| 28 | Verifikation | Hook nach Umstellung prüfen: schreibt am neuen Pfad, erzeugt keinen Git-Commit mehr | Offen | — |
| 29 | Verifikation | Repository-Settings (Public, Branch-Protection, Aktivierung Private Security Advisories) als menschliche Nachfolgeaufgabe vermerken | Offen | — |
