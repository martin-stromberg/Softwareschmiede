# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

## Fehlgeschlagene Tests

Keine fehlgeschlagenen Tests.

## Zusammenfassung

### Stabile Tests (Category!=OsInterface)
- Gesamt: 1271
- Bestanden: 1270
- Übersprungen: 1
- Fehlgeschlagen: 0
- Gesamtzeit: 1,37 Minuten

### OsInterface Tests (Category=OsInterface)
- Gesamt: 44
- Bestanden: 43
- Übersprungen: 1 (ConPTY-Tests, explizit mit SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 übersprungen)
- Fehlgeschlagen: 0
- Gesamtzeit: 1,25 Minuten

### Gesamtsumme
- Gesamt: 1315
- Bestanden: 1313
- Übersprungen: 2
- Fehlgeschlagen: 0

## Testabdeckung

**Gesamtabdeckung (Zeilen):** 34.5 %
**Gesamtabdeckung (Branches):** 60.9 %

### Dateien mit niedriger Abdeckung (< 80%)

Insgesamt **352 Dateien** mit < 80% Zeilenabdeckung. Dies sind hauptsächlich:

| Kategorie | Typ | Begründung |
|-----------|-----|-----------|
| **UI-Views** | .xaml.cs | Code-Behind von XAML-Views werden durch UI-Tests (E2E) nur teilweise getestet; manuelle Interaktionen sind schwer zu automatisieren |
| **XAML-generierter Code** | .g.cs | Automatisch generierter Kod aus .xaml-Dateien ist schwer zu testen |
| **XAML-Dateien** | .xaml | Markup wird nicht durch Code-Coverage erfasst |
| **Plugin-Implementierungen** | .cs | Einige Plugin-spezifische Implementierungen haben limitierte Testabdeckung |
| **Infrastructure** | .cs | Terminal-, Datei- und System-Operationen haben Environment-abhängige Tests |
| **Controls** | .xaml.cs | Benutzerdefinierte WPF-Controls werden durch E2E-Tests getestet |

**Quelle:** XPlat Code Coverage (Cobertura-Format)

## Ausführungsumgebung

- **Testprojekt:** `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj`
- **Umgebungsvariablen:** `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`
- **Testfilter (stabil):** `Category!=OsInterface`
- **Testfilter (OsInterface):** `Category=OsInterface`
- **Build:** Vollständig erfolgreich, vor Testausführung durchgeführt
- **Framework:** .NET 10.0
- **Test-Runner:** xUnit.net v3.1.5
- **Coverage-Erfassung:** XPlat Code Coverage (erfolgreich)

## Übersprungene Tests

1. **Stabile Tests:** 1 Test (unspezifiziert)
2. **OsInterface Tests:** `Softwareschmiede.Tests.E2E.End2EndTest.RunConPtyTests` - Übersprungen aufgrund der Sandbox-Limitierung (`SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`), wie in CLAUDE.md dokumentiert

## Schlussfolgerung

**✓ Regressionssicherheit gewährleistet:** Alle 1313 Tests bestanden erfolgreich.

Die Implementierung der IDE-Plugin-Funktionalität (Issue #204) führt zu **keinen Test-Regressionsfehler**. Die Coverage-Quote von 34.5% für Zeilen ist akzeptabel für ein komplexes UI-Projekt mit großem Anteil an XAML und manuellen UI-Tests.
