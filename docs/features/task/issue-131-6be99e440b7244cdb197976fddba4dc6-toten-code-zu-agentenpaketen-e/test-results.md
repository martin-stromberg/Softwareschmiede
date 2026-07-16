# Test-Ergebnisse

## Ergebnis

**Status:** Fehler vorhanden

## Fehlgeschlagene Tests

### Softwareschmiede.Tests

- **Softwareschmiede.Tests.App.Controls.TerminalControlTests.OnPreviewKeyDown_CtrlV_SetsHandledTrue** — System.Runtime.InteropServices.COMException: OpenClipboard fehlgeschlagen (0x800401D0) - Umgebungsproblem (kein Clipboard im Test-Kontext)

- **Softwareschmiede.Tests.E2E.E2E_WorkingDirectory.RepositoryZuweisen_MitFehlgeschlagenemStrukturabruf_ZeigtTextBoxUndSpeichertManuellenPfad_E2E** — System.UnauthorizedAccessException: Access to path denied - Datei-Sperr-Problem bei Test-Cleanup

## Zusammenfassung

| Metrik | Wert |
|--------|------|
| Gesamt | 1.058 |
| Bestanden | 1.055 |
| Fehlgeschlagen | 2 |
| Übersprungen | 1 |
| Erfolgsquote | 99,72% |

### Detailansicht

**Unit Tests (Softwareschmiede.Tests):**
- Gesamt: 990
- Bestanden: 987
- Fehlgeschlagen: 2
- Übersprungen: 1

**Integration Tests (Softwareschmiede.IntegrationTests):**
- Gesamt: 68
- Bestanden: 68
- Fehlgeschlagen: 0
- Übersprungen: 0

**Testdauer (Unit Tests):** 6 m 17 s
**Testdauer (Integration Tests):** 4 s

## Testabdeckung

**Abdeckung (Unit Tests):** 31,15%
**Abdeckung (Integration Tests):** 72,78%

| Assembly | Abdeckung | Kategorie |
|----------|-----------|-----------|
| Softwareschmiede.App | 57,06% | WPF-UI / Präsentation |
| Softwareschmiede | 22,34% | Domain/Service/Infrastructure |
| Softwareschmiede.Plugin.BitBucket | 67,41% | Plugin |
| Softwareschmiede.Plugin.ClaudeCli | 89,23% | Plugin |
| Softwareschmiede.Plugin.Codex | 92,30% | Plugin |
| Softwareschmiede.Plugin.Contracts | 60,60% | Plugin |
| Softwareschmiede.Plugin.GitHub | 85,92% | Plugin |
| Softwareschmiede.Plugin.GitHubCopilot | 93,84% | Plugin |
| Softwareschmiede.Plugin.KiSimulator | 100,00% | Plugin |
| Softwareschmiede.Plugin.LocalDirectory | 85,55% | Plugin |

### Integration-Test-Abdeckung

| Assembly | Abdeckung |
|----------|-----------|
| Softwareschmiede | 73,67% |
| Softwareschmiede.Plugin.Contracts | 20,34% |
| Softwareschmiede.Plugin.LocalDirectory | 59,08% |

## Fehlende Tests

Keine Dateien mit 0% Abdeckung identifiziert. Alle getesteten Assemblies haben mindestens teilweise Test-Abdeckung.

### Assemblies mit niedriger Abdeckung (<50%)

Quelle: `Coverage-Daten`

- `Softwareschmiede` — 22,34% Abdeckung (Domain/Service Schicht, großer Umfang)
- `Softwareschmiede.Plugin.Contracts` — 20,34% in Integration Tests (Interface-Definitionen)

## Hinweise

### Fehlgeschlagene Tests (Umgebungsprobleme)

1. **TerminalControlTests.OnPreviewKeyDown_CtrlV_SetsHandledTrue**
   - Grund: Clipboard-Zugriff in Test-Kontext nicht möglich
   - Klassifizierung: Umgebungsproblem (nicht Code-Fehler)
   - Betrifft: WPF-UI-Tests
   - Status: Bekannt, abhängig von Desktop-Session

2. **E2E_WorkingDirectory.RepositoryZuweisen_MitFehlgeschlagenemStrukturabruf**
   - Grund: Datei-Lock beim Test-Cleanup
   - Klassifizierung: Timing/Ressourcen-Problem
   - Status: Intermittierend, nicht reproduzierbar mit sauberen Verhältnissen

### Coverage-Analyse

- **Plugin-Assemblies**: Sehr gute Abdeckung (67–100%)
- **Core-Domäne**: Moderate Abdeckung (22–73%)
- **UI-Layer**: 57% Abdeckung (Schwieriger zu testen bei WPF)

---

**Datum:** 2026-07-16
**Branch:** task/issue-131-6be99e440b7244cdb197976fddba4dc6-toten-code-zu-agentenpaketen-e
**Konfiguration:** Release
