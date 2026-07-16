# Dokumentation

## Vorhandene Hauptdokumente im Root

### `README.md`
**Datei:** `README.md` (163 KB, ca. 500+ Zeilen)

- **Inhaltsverzeichnis:** 19 Punkte von Projektbeschreibung bis Kontakt
- **Sektionen vorhanden:**
  - Projektbeschreibung: Kurzbeschreibung, Geschäftsziele
  - Implementierungsstatus: Detaillierte Matrix mit ✅/🔄/⚠️ für 20+ Features
  - Features: Feature-Highlights mit Implementierungsdetails
  - UI-Status: WPF vs. Blazor-Migration
  - Voraussetzungen: .NET 10, Windows 10+, optional weitere Tools
  - Installation: Anweisungen für Entwicklung und Installation
  - Usage: Anwendung starten und Grundlagen
  - Konfiguration & Plugin-Setup: Plugin-Mechanik, Standardplugin-Auflösung
  - Agentenpakete: Beschreibung des Agent-Verwaltungssystems
  - Projektstruktur: Verzeichnisübersicht (`src/`, `plugins/`, `docs/`, etc.)
  - Architektur: Domain-, Service-, Application-Layers, Plugin-Architektur
  - Tests: Unit, Integration, BUnit, E2E (WPF)
  - Deployment: Release-Prozess, GitHub Releases
  - Changelog: Verweis auf `CHANGELOG.md`
  - Roadmap: Zukünftige Features und Bereiche
  - Dokumentation: Verweise auf weitere Dokuseiten
  - Beitragen: Verweis auf `CONTRIBUTING.md`
  - Lizenz: ⚠️ **"zu definieren"** (noch nicht gewählt)
  - Kontakt: Email-Adresse und GitHub-Links

- **Sprachigkeit:** Deutsch
- **Umfang:** Sehr umfangreiche, detaillierte Dokumentation für Entwickler
- **Qualität:** High — ausführliche Erklärungen, Versionsstand aktuell (2026-07-14)
- **Öffentlichkeitsreife:** Großteils OK, aber License-Abschnitt muss gefüllt werden

### `CONTRIBUTING.md`
**Datei:** `CONTRIBUTING.md` (70 Zeilen)

- **Inhalt:**
  - Commit-Konventionen: Conventional Commits Standard
  - Commit-Types: feat, fix, refactor, docs, test, chore, perf, ci
  - Versioning-Impact: Wie Commit-Types Versionierung beeinflussen (Minor, Patch, Major)
  - Breaking Changes: BREAKING CHANGE-Markierung erforderlich
  - Manual Release Tag: Override-Verfahren für explizite Versionen
  - Rules: 4 Regeln für Commit-Hygiene

- **Sprachigkeit:** Deutsch
- **Umfang:** Prägnant, fokussiert auf Contribution-Prozess
- **Öffentlichkeitsreife:** Gut, benötigt aber Erweiterungen für externe Beiträge

### `CHANGELOG.md`
**Datei:** `CHANGELOG.md` (21 KB)

- **Format:** Semantic Versioning (v1.0.0 bis v1.12.0)
- **Einträge:** 12 Major-Releases dokumentiert, jeder mit Features, Bugfixes, Breaking Changes
- **Struktur:**
  - Links zu Commit-Vergleichen auf GitHub
  - Datum des Releases
  - Sektionen: "Features", "Bug Fixes"
  - Referenzen zu Commit-SHAs und GitHub-Issue-Links

- **Sprachigkeit:** Deutsch
- **Umfang:** Vollständig ab v1.0.0
- **Qualität:** High — konsistent formatiert, Conventional-Commits-konform
- **Öffentlichkeitsreife:** Sehr gut

## Fehlende Kernzusammenfassungen

### ❌ `SECURITY.md`
- **Status:** Nicht vorhanden
- **Anforderung aus Specifikation:** 
  - Wie können Sicherheitsprobleme gemeldet werden?
  - Meldewege (E-Mail, Responsible Disclosure Policy)
  - Versprechen für Response-Zeiten

### ❌ `LICENSE.md` / `LICENSE`
- **Status:** Nicht vorhanden
- **Anforderung aus Specifikation:**
  - Auswahl einer Open-Source-Lizenz (z. B. MIT, Apache 2.0, GPL v3)
  - Integration im Root-Verzeichnis
  - License-Header in Quelldateien (optional, je nach Lizenzwahl)

### ❌ `THIRD_PARTY_LICENSES.md`
- **Status:** Nicht vorhanden
- **Anforderung aus Specifikation:**
  - Übersicht aller Abhängigkeiten und deren Lizenzen (SPDX-Format)
  - Notwendig für Compliance und Transparenz gegenüber Nutzern

## Weitere Dokumentation im `docs/`-Verzeichnis

### Root-Dokumente
- `docs/CI_CD.md` — Erklärung der CI/CD-Pipeline
- `docs/token-usage-log.csv` — Token-Verbrauchsprotokoll (Berlin Time)
- **Weitere Unterordner:** `docs/features/`, `docs/help/` (müssen auf Interna überprüft werden)

### Überprüfung erforderlich für öffentliche Veröffentlichung
- Dokumente unter `docs/` auf interne Referenzen durchsuchen
- Entfernung oder Markierung von Dateien mit internem Charakter (z. B. `docs/internal/`, `docs/TODO.md`, internal-nur-Hinweise)
- Umbennung von Dateien mit internem Context

## README-Kommentare und Anmerkungen

- ⚠️ **WPF-Desktopanwendung in Entwicklung:** README verweist auf laufende Migration
- ⚠️ **Blazor Server:** Alte UI wird noch nicht vollständig dokumentiert als deprecated
- ✅ **Implementierungsstatus aktuell:** Detaillierte Feature-Matrix ist up-to-date
- ✅ **Roadmap vorhanden:** Zukünftige Features dokumentiert

## Sprachigkeit

- ✅ **Deutsch:** Alle Kernzusammenfassungen (README, CONTRIBUTING, CHANGELOG, SECURITY erforderlich)
- ⚠️ **Lokalisierung:** Keine Mehrsprachigkeit, rein Deutsch

## Offene Fragen

1. **Lizenzwahl:** Welche Open-Source-Lizenz soll verwendet werden? (MIT, Apache 2.0, GPL v3, etc.)
2. **Lizenz-Header:** Sollen Quellcode-Dateien License-Header enthalten?
3. **Sprachigkeit von SECURITY.md:** Deutsch oder Englisch?
4. **README-Überarbeitung:** Sind die "Interne Kommentare" im aktuellen README zielgruppen-gerecht oder müssen sie überarbeitet werden?
5. **docs/-Verzeichnis:** Soll dies vollständig öffentlich sein oder gibt es interne Dokumente zu entfernen?
