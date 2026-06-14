# README aktualisieren

Erstellt oder aktualisiert die `README.md` im Projektstammverzeichnis. Grundlage ist der aktuelle Stand des Codes — es wird nur dokumentiert, was tatsächlich vorhanden ist.

**Ziel:** Eine README erzeugen, die Entwickler schnell orientiert: Was ist das Projekt, wie richtet man es ein, wie arbeitet man damit.

---

## Schritt 1: Projekt verstehen

Verschaffe dir einen Überblick über das Projekt:

- Lies `docs/features/{branchname}/requirement.md` und `docs/features/{branchname}/plan.md`, falls vorhanden.
- Lies vorhandene Dokumentation unter `docs/help/` (insbesondere `index.md`-Dateien und `beschreibung.md`-Dateien).
- Ermittle alle im aktuellen Branch geänderten und neu erstellten Dateien:
  ```
  git diff --name-only --diff-filter=AM $(git merge-base HEAD main)
  ```
- Lies die wesentlichen Quelldateien. Achte auf:
  - Projektname (aus `*.csproj`, `package.json`, `pyproject.toml` o. ä.)
  - Technologien und Frameworks (Abhängigkeiten, Imports)
  - Einstiegspunkte (`Program.cs`, `index.ts`, `main.py` etc.)
  - Konfigurationsparameter und Umgebungsvariablen
  - API-Endpunkte (Controller, Router, Endpoints)
  - Datenbankentitäten
  - Test-Setup (Testprojekte, Testframeworks)
  - CI/CD-Konfiguration (`.github/workflows/`, `Dockerfile` etc.)
  - Ermittle dabei den **GitHub-Owner und Repository-Namen** (`git remote get-url origin`) — wird für Badge-URLs benötigt.

## Schritt 2: Bestehende README lesen

Lies `README.md` im Projektstammverzeichnis, falls vorhanden. Integriere neue Inhalte in die bestehende Struktur — lösche keine Abschnitte, die noch gültig sind.

## Schritt 3: Relevante Abschnitte bestimmen

Prüfe für jeden der folgenden Abschnitte, ob er für dieses Projekt sinnvoll ist:

| Abschnitt | Sinnvoll wenn... |
|-----------|-----------------|
| Shields.io-Badges | immer — mindestens 1 sinnvolles Badge vorhanden |
| Projektname & Kurzbeschreibung | immer |
| Features / Highlights | immer |
| Installation / Setup | Installationsschritte, Abhängigkeiten oder Build-Prozess vorhanden |
| Usage / Beispiele | Einstiegspunkte, CLI-Befehle oder typische Anwendungsfälle erkennbar |
| Konfiguration | Umgebungsvariablen, Konfigurationsdateien oder Parameter vorhanden |
| Architektur / Projektstruktur | Mehr als 5 relevante Verzeichnisse oder nicht-triviale Modulstruktur |
| API-Dokumentation | Öffentliche HTTP-Endpunkte oder Events exponiert |
| Tests | Testprojekte oder Testframeworks vorhanden |
| Deployment / CI/CD | Dockerfile, Workflow-Dateien oder Deployment-Konfiguration vorhanden |
| Contribution Guide | Branch-Strategie oder PR-Regeln im Projekt definiert |
| Roadmap | Geplante Features aus `plan.md` oder Anforderungen bekannt |
| Changelog | `changes.log` im Projektverzeichnis vorhanden |
| Lizenz | `LICENSE`-Datei vorhanden oder Lizenz in Projektdatei angegeben |
| Kontakt / Maintainer | Bekannte Maintainer-Informationen vorhanden |

## Schritt 4: README erstellen oder aktualisieren

Erstelle oder aktualisiere `README.md` im Projektstammverzeichnis mit den in Schritt 3 ausgewählten Abschnitten.

**Regeln:**
- Nichts erfinden — nur dokumentieren, was im Code tatsächlich vorhanden ist.
- Bezeichnungen aus dem Code übernehmen (Projektname, Klassen, Konfigurationsschlüssel).
- Codeblöcke für Befehle, Beispiele und Konfiguration.
- Leere Abschnitte weglassen — kein Platzhaltertext.
- Kein Abschnitt ohne konkreten Inhalt.

---

### Shields.io-Badges

Badges stehen direkt unter dem Projekttitel, vor der Kurzbeschreibung. Füge **nur** Badges ein, für die du konkrete Werte aus dem Code ermitteln konntest — kein Platzhalter-Badge.

**Entscheidungslogik je Badge-Typ:**

| Badge | Einfügen wenn... | URL-Muster |
|-------|-----------------|------------|
| GitHub Actions (CI) | `.github/workflows/`-Datei vorhanden — Workflow-Name aus `name:`-Feld ermitteln | `https://img.shields.io/github/actions/workflow/status/{owner}/{repo}/{dateiname}.yml?label={Workflow-Name}` |
| Lizenz | `LICENSE`-Datei oder Lizenzangabe in Projektdatei vorhanden | `https://img.shields.io/github/license/{owner}/{repo}` |
| .NET-Version | `.csproj` mit `<TargetFramework>` vorhanden — Version aus Datei lesen | `https://img.shields.io/badge/.NET-{version}-512BD4?logo=dotnet` |
| Node.js-Version | `package.json` mit `engines.node` oder `.nvmrc` vorhanden | `https://img.shields.io/badge/node-%3E%3D{version}-339933?logo=nodedotjs` |
| Python-Version | `pyproject.toml` mit `requires-python` oder `.python-version` vorhanden | `https://img.shields.io/badge/python-{version}-3776AB?logo=python` |
| npm-Paketversion | `package.json` mit `name` und `version`, Paket öffentlich auf npm | `https://img.shields.io/npm/v/{paketname}` |
| NuGet-Version | `.csproj` mit `<PackageId>`, Paket öffentlich auf NuGet | `https://img.shields.io/nuget/v/{paketname}` |
| Docker-Image | `Dockerfile` vorhanden und Image auf Docker Hub veröffentlicht | `https://img.shields.io/docker/v/{owner}/{image}?logo=docker` |
| Code-Coverage | Coverage-Report-Datei (Codecov, Coveralls) oder CI-Upload erkennbar | `https://img.shields.io/codecov/c/github/{owner}/{repo}` |
| Letzte GitHub-Release | GitHub-Releases im Repo vorhanden (prüfen mit `git tag`) | `https://img.shields.io/github/v/release/{owner}/{repo}` |

**Stil-Vorgaben:**
- Standard-Stil (`flat` ist Shield.io-Default — nicht explizit angeben)
- Pro Zeile maximal 5–6 Badges, danach Zeilenumbruch
- Badges als Markdown-Image-Links mit sinnvollem `alt`-Text:  
  `[![CI](badge-url)](link-zum-workflow-oder-ziel)`

**Beispiel-Block (nur zur Orientierung — konkrete Werte aus dem Code ableiten):**

```markdown
[![CI](https://img.shields.io/github/actions/workflow/status/acme/myapp/ci.yml?label=CI)](https://github.com/acme/myapp/actions)
[![License](https://img.shields.io/github/license/acme/myapp)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com)
```

---

### Projektname & Kurzbeschreibung

Prägnanter Titel. 1–3 Sätze: Was macht das Projekt, welches Problem löst es?

### Features / Highlights

Auflistung der wichtigsten Funktionen. Projektstatus (Alpha / Beta / Stable), soweit aus Code und Dokumentation ersichtlich.

### Installation / Setup

Voraussetzungen (Laufzeitversionen, externe Dienste). Schritt-für-Schritt-Anleitung mit Beispielbefehlen.

### Usage / Beispiele

Start-Befehl, CLI-Befehle oder Beispielcode. Typische Anwendungsfälle in Kurzform.

### Konfiguration

Tabelle der Umgebungsvariablen oder Konfigurationsparameter mit Typ, Standardwert und Beschreibung.

| Parameter | Typ | Standardwert | Beschreibung |
|-----------|-----|--------------|--------------|
| ...       | ... | ...          | ...          |

### Architektur / Projektstruktur

Vereinfachter Verzeichnisbaum mit kurzen Erläuterungen. Technologien und Frameworks auflisten.

### API-Dokumentation

Wichtigste Endpunkte mit Methode, Pfad und Kurzbeschreibung. Bei umfangreicher API Verweis auf `docs/help/` für Details.

### Tests

Befehl zum Ausführen der Tests. Genutzte Frameworks.

### Deployment / CI/CD

Hinweise zu Docker, Workflows oder Deployment-Prozess.

### Contribution Guide

Branch-Strategie, PR-Regeln, Code-Style-Hinweise — nur wenn konkrete Informationen vorliegen.

### Roadmap

Geplante Features aus `plan.md` oder bekannten Anforderungen, soweit noch nicht umgesetzt.

### Changelog

Verweis auf `changes.log`, falls vorhanden.

### Lizenz

Lizenztyp und Verweis auf `LICENSE`-Datei.

### Kontakt / Maintainer

Bekannte Ansprechpartner und Kontaktmöglichkeiten.
