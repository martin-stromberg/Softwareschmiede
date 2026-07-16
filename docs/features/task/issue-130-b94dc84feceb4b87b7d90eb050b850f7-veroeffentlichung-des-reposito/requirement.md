# Kundenanforderung – Veröffentlichung eines Software-Repositories

## Fachliche Zusammenfassung

Das Projekt Softwareschmiede soll zur öffentlichen Nutzung vorbereitet und veröffentlicht werden. Hierfür ist eine umfassende Sicherheits- und Compliance-Prüfung erforderlich, um sicherzustellen, dass keine vertraulichen Daten (Secrets, API-Keys, interne Systempfade), keine personenbezogenen Daten in Testdaten und keine rechtlichen Risiken (Lizenzkompatibilität, proprietäre Komponenten) in das öffentliche Repository gelangen. Parallel wird die Repository-Dokumentation für externe Nutzer aufbereitet und ein klares Release-Management etabliert.

## Betroffene Klassen und Komponenten

### Infrastruktur und Konfiguration
- **.gitignore und Exclusion-Mechanismen:** Überprüfung und Erweiterung, um Secrets-Dateien auszuschließen
- **CI/CD-Konfigurationen:** `.github/workflows/*.yml` auf interne Systeme und Secrets-Referenzen prüfen
- **Build-Skripte:** `build.ps1`, `.claude/hooks/*.py` auf versteckte Zugangsdaten prüfen
- **Konfigurationsdateien:** `appsettings.json`, `settings.json`, `CLAUDE.md` auf sensible Inhalte prüfen

### Dokumentation (zu erstellen/zu aktualisieren)
- **README.md:** Zweck, Features-Übersicht, Installation, Quick-Start, Lizenz
- **CONTRIBUTING.md:** Guidelines für externe Beiträge, Coding-Standards, PR-Prozess
- **SECURITY.md:** Vulnerability-Meldewege, Security-Policies
- **LICENSE.md / LICENSE:** Open-Source-Lizenz (z. B. MIT, Apache 2.0, GPL)
- **CHANGELOG.md:** History von Versionsänderungen
- **docs/installation.md, docs/usage.md:** Detaillierte Dokumentation für Nutzer
- **Interne Dokumente:** Entfernung oder Markierung von Dateien, die nicht für die Öffentlichkeit bestimmt sind

### Quellcode und Testdaten
- **Kommentare:** Entfernen von internen Kommentaren mit vertraulichen Informationen
- **Systempfade:** Entfernung von hardcodierten internen Pfaden (z. B. `C:\Entwicklung\...`)
- **Debug-/Diagnose-Endpunkte:** Überprüfung auf nicht-produktive Test-Interfaces
- **Testdaten:** Säuberung personenbezogener oder geschützter Daten aus `*.Tests`-Projekten

### Abhängigkeitsmanagement
- **NuGet-Abhängigkeiten:** `*.csproj`, `*.sln` und `packages.config` (falls vorhanden) auf proprietäre Komponenten prüfen
- **Lizenz-Kompatibilität:** Überprüfung aller NuGet-Pakete auf Open-Source-Lizenzen (SPDX)
- **Abhängigkeiten-Liste:** Dokumentation aller direkten und transitiven Abhängigkeiten mit Lizenzen

### Versionierung und Release
- **Git-History:** Optional: Bereinigung des Commit-Verlaufs, falls Secrets entfernt wurden (z. B. `git filter-repo` oder ähnliches)
- **Version-String:** Festlegung auf Semantic Versioning (SemVer)
- **Git-Tags:** Tagging eines initialen Releases (z. B. `v1.0.0`)
- **.version oder VERSION-Datei:** Optional: Zentralisierte Versionsverwaltung

### Lizenzierung
- **LICENSE-Datei:** Auswahl und Integration einer Open-Source-Lizenz
- **License-Header in Quellcode:** Optional: Hinzufügen von Copyright-Notices in Quelldateien
- **THIRD_PARTY_LICENSES.md:** Übersicht aller Abhängigkeiten und deren Lizenzen

## Implementierungsansatz

### Phase 1: Sicherheits- und Secrets-Prüfung

1. **Secrets-Scanning:** Durchsuche das gesamte Repository nach:
   - Muster für API-Keys, Tokens, Passwörter (z. B. `key=`, `token=`, `password=`, `secret=`)
   - Connection Strings und Datenbank-Credentials
   - Private SSH-Keys, Zertifikate (.pem, .pfx, .key)
   - Azure-Verbindungszeichenfolgen, AWS-Keys, etc.

2. **Tools zur Prüfung:**
   - `git log -S <pattern>` oder ähnliche Befehle, um Secrets im History zu finden
   - `TruffleHog`, `detect-secrets` oder ähnliche Secrets-Scanning-Tools
   - Manuelle Überprüfung von `.github/workflows/*.yml` auf `secrets.*` Referenzen
   - Überprüfung von `appsettings.json`, `appsettings.*.json` auf hartcodierte Werte

3. **Handling gefundener Secrets:**
   - Entfernen oder Maskieren in der aktuellen Branch
   - Optional: Bereinigung aus der Git-Historie (mit Vorsicht, da dies einen Force-Push erfordert)
   - Dokumentation aller entfernten Secrets in einem internen Report

### Phase 2: Code- und Architekturqualität

1. **Interne Kommentare:** Durchsuche auf Muster wie `INTERNAL:`, `CONFIDENTIAL:`, `TODO:` mit sensiblem Kontext
2. **Systempfade:** Suche nach hartcodierten Windows-Pfaden (z. B. `C:\Users\`, `C:\Entwicklung\`, `\\servername\`)
3. **Debug-Endpunkte:** Überprüfung auf spezielle Test-Routen, die nur in der Entwicklung vorhanden sein sollten
4. **Testdaten:** Überprüfung von `*.Tests`-Projekten auf echte E-Mail-Adressen, Telefonnummern oder PII

### Phase 3: Lizenzierung und Abhängigkeiten

1. **Lizenz-Auswahl:** Bestimmung einer geeigneten Open-Source-Lizenz basierend auf Projektanforderungen
   - Empfehlung: `MIT`, `Apache 2.0`, oder `GPL v3` (je nach Anforderung)
2. **NuGet-Abhängigkeiten prüfen:** Alle `*.csproj`-Dateien durchsuchen nach `<PackageReference>`
3. **Lizenz-Kompatibilität:** Überprüfung jeder Abhängigkeit auf SPDX-Lizenz-Kompatibilität
4. **Third-Party-Dokumentation:** Erstellung einer `THIRD_PARTY_LICENSES.md` mit allen Abhängigkeiten und Lizenzen

### Phase 4: Dokumentation

1. **README.md:**
   - Kurzbeschreibung: Was ist Softwareschmiede?
   - Features-Highlight
   - Installation (Anforderungen, Installation steps)
   - Quick-Start-Beispiel
   - Lizenz-Verweis

2. **CONTRIBUTING.md:**
   - Wie kann man zum Projekt beitragen?
   - Code-Style und Konventionen
   - Test-Anforderungen
   - Pull-Request-Prozess
   - Community-Standards

3. **SECURITY.md:**
   - Wie können Sicherheitsprobleme gemeldet werden?
   - Meldewege (E-Mail, Responsible Disclosure Policy)
   - Versprechen für Response-Zeiten

4. **CHANGELOG.md:**
   - Versionsgeschichte mit Highlights jeder Version
   - Datum der Releases
   - Backward-Compatibility-Hinweise

5. **Interne Dokumente:**
   - Überprüfung aller Dateien unter `docs/` auf interne Referenzen
   - Entfernung oder Umbennung von Dateien mit internem Charakter (z. B. `docs/internal/`, `docs/TODO.md`)

### Phase 5: Versionierung und Release

1. **Versionierung:** Festlegung auf Semantic Versioning (Major.Minor.Patch)
2. **Git-Tags:** Tagging der aktuellen Version mit `git tag v<version>`
3. **Release-Notes:** Erstellung von Release-Notes für das erste öffentliche Release
4. **Commit-Historie:** Optional: Überprüfung, ob die Historie bereinigt werden muss (wird nur bei kritischen Secrets empfohlen)

### Phase 6: CI/CD-Anpassungen

1. **Workflow-Überprüfung:** `.github/workflows/*.yml` auf interne Systeme, Secrets-Referenzen prüfen
2. **Deployment-Ziele:** Sicherstellen, dass öffentliche Builds keine internen Infrastruktur-Ressourcen nutzen
3. **Build-Artefakte:** Überprüfung, dass öffentliche Builds nur öffentliche NuGet-Feeds nutzen

## Konfiguration

Diese Veröffentlichung erfordert keine neuen Code-Konfigurationen (Classes, Services), sondern ist primär ein Governance- und Dokumentations-Prozess. Die Konfiguration besteht aus:

1. **Repository-Settings:** Sicherstellen, dass sensible Branches (z. B. mit Credentials) nicht öffentlich sind
2. **Branch-Protection:** Optional: Aktivierung von Branch-Protection für `main` und `develop`
3. **Secrets-Management in GitHub:** Verwaltung von CI/CD-Secrets über GitHub's native Secret-Management (nicht im Quellcode)
4. **LICENSE-File:** Integration einer `LICENSE`-Datei im Root-Verzeichnis

## Offene Fragen

1. **Lizenz-Wahl:** Welche Open-Source-Lizenz soll verwendet werden? (MIT, Apache 2.0, GPL v3, oder andere?)

2. **Versionssprung:** Mit welcher initiale Version soll das Projekt starten? (v0.1.0, v1.0.0, oder aktuelle interne Version?)

3. **Git-Historie:** Sollen sensible Commits aus der Historie entfernt werden (Force-Push), oder nur von Secrets bereinigt?

4. **Interne Dokumentation:** Welche bestehenden Dokumente unter `docs/` sollen entfernt oder als "internal" markiert werden?

5. **Maintainer-Struktur:** Wer ist verantwortlich für die Wartung und Review von externen Contributions?

6. **Security-Disclosure-Richtlinie:** Welche Responsibilty-Disclosure-Policy soll in `SECURITY.md` definiert werden? (z. B. Meldewege, Response-Zeiten)

7. **Public Release-Channel:** Soll das Projekt auf NuGet, GitHub Releases, oder anderswo veröffentlicht werden?

8. **Abhängigkeitsfreigabe:** Müssen propriäre Abhängigkeiten entfernt oder durch Open-Source-Alternativen ersetzt werden?

9. **Branding und Marketing:** Sollen neben den technischen Dokumenten auch Marketing-Unterlagen oder ein Website/Wiki erstellt werden? (Hinweis: Dies ist laut Abgrenzung nicht Bestandteil dieser Anforderung, könnte aber parallel laufen.)

10. **Testing nach Veröffentlichung:** Sollen automatisierte Tests vor der Veröffentlichung durchgeführt werden (z. B. Build, Unit-Tests, Integration-Tests auf öffentlicher Konfiguration)?
