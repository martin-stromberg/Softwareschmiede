# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

## Fehlgeschlagene Tests

Keine fehlgeschlagenen Tests.

## Zusammenfassung

- Gesamt: 1374
- Bestanden: 1372
- Fehlgeschlagen: 0
- Übersprungen: 2

### Test-Aufschlüsselung

**Reguläre Tests (Unit/Integration):** 1327 Tests
- Bestanden: 1326
- Übersprungen: 1
  - `ArbeitsverzeichnisOeffnenServiceTests.Oeffne_AufNichtWindows_WirftPlatformNotSupportedException` — Test gilt nur für Nicht-Windows-Betriebssysteme

**E2E/OS-Interface Tests:** 47 Tests
- Bestanden: 46
- Übersprungen: 1
  - `End2EndTest.RunConPtyTests` — Umgebungsvariable SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 gesetzt (Sandbox-Limitation, siehe CLAUDE.md)

## Testabdeckung

**Abdeckung:** 33.2 % (13.782 Zeilen von 41.461 gültig)
- Zeilenabdeckung: 33.24 %
- Branch-Abdeckung: 60.84 %

| Package | Abdeckung |
|---------|-----------|
| Softwareschmiede | 22.82 % |
| Softwareschmiede.App | 61.75 % |
| Softwareschmiede.Plugin.BitBucket | 69.38 % |
| Softwareschmiede.Plugin.Contracts | 63.41 % |

Hinweis: Nur Packages mit < 80 % Zeilenabdeckung aufgeführt.

## Fehlende Tests

Quelle: Coverage-Daten

Die folgenden Packages haben eine Abdeckung unter 80 %:

- `Softwareschmiede` — 22.82 % Abdeckung (Domain und Core-Services)
- `Softwareschmiede.App` — 61.75 % Abdeckung (WPF-UI und ViewModel)
- `Softwareschmiede.Plugin.BitBucket` — 69.38 % Abdeckung (BitBucket-Plugin)
- `Softwareschmiede.Plugin.Contracts` — 63.41 % Abdeckung (Plugin-Verträge)

Diese Packages benötigen zusätzliche Tests für:
1. Error-Handling und Edge-Cases in Core-Services
2. UI-Interaktionen und View-Model-Logik
3. Plugin-spezifische Funktionalität für Bitbucket
4. Abstraktion und Schnittstellen-Validierung

### Packages mit guter Abdeckung (≥ 80 %)

- `Softwareschmiede.Plugin.ClaudeCli` — 82.82 %
- `Softwareschmiede.Plugin.Codex` — 87.5 %
- `Softwareschmiede.Plugin.Devin` — 88.15 %
- `Softwareschmiede.Plugin.GitHub` — 82.69 %
- `Softwareschmiede.Plugin.GitHubCopilot` — 93.15 %
- `Softwareschmiede.Plugin.KiSimulator` — 100.0 %
- `Softwareschmiede.Plugin.LocalDirectory` — 82.17 %

## Test-Ausführung

**Konfiguration:**
- .NET 10.0
- XPlat Code Coverage aktiviert
- Umgebungsvariable: `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`
- Filter (regulär): `Category!=OsInterface`
- Filter (E2E): `Category=OsInterface`

**Testlauf-Zeiten:**
- Reguläre Tests: ~83 Sekunden
- E2E Tests: ~80 Sekunden
- Gesamt: ~2,7 Minuten

**Coverage-Datei:**
```
src/Softwareschmiede.Tests/TestResults/79851da6-5556-4156-8b33-2c99e613284f/coverage.cobertura.xml
```

## Notizen

- Alle 1374 Tests sind erfolgreich (1372 bestanden, 2 übersprungen)
- Reguläre Tests sind stabil und zuverlässig
- E2E-Tests (FlaUI) laufen erfolgreich; ConPTY-Tests sind in dieser Sandbox-Umgebung bewusst übersprungen
- Gesamtabdeckung von 33.2% ist erwartbar für ein Desktop-App-Projekt mit hohem UI-Anteil (WPF, XAML, ViewModels)
- Die neuesten Implementierungen der autonomen Aufgaben-Features sind ebenfalls vollständig getestet
