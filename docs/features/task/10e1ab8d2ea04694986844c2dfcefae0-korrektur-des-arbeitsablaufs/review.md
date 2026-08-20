# Plan-Review: Korrektur des Arbeitsablaufs (CLI-Panel-Sichtbarkeit)

## Ergebnis

**Status:** Vollständig umgesetzt

---

## Umgesetzte Planelemente

### Code-Änderungen

- [x] **Methode `SollCliAnzeigen` in `AufgabeAusfuehrungsStatusExtensions`** — Bedingung erweitert
  - Datei: `src/Softwareschmiede/Domain/Enums/AufgabeAusfuehrungsStatusExtensions.cs`
  - Änderung: `ausfuehrungsStatus == AufgabeAusfuehrungsStatus.Aktiv` → `ausfuehrungsStatus is (AufgabeAusfuehrungsStatus.Aktiv or AufgabeAusfuehrungsStatus.Beendet)`
  - XML-Dokumentation aktualisiert: Explizit erwähnt, dass CLI-Ansicht in beiden Zuständen (`Aktiv` und `Beendet`) angezeigt wird

### Unit-Tests

- [x] **Neue Testklasse `AufgabeAusfuehrungsStatusExtensionsTests`** — angelegt
  - Datei: `src/Softwareschmiede.Tests/Domain/Enums/AufgabeAusfuehrungsStatusExtensionsTests.cs`
  - Testmethoden vorhanden:
    - `SollCliAnzeigen_WhenAusfuehrungsStatusIsBeendet_AndAufgabeStatusIsGestartet_ReturnsTrue` ✓
    - `SollCliAnzeigen_WhenAusfuehrungsStatusIsBeendet_AndAufgabeStatusIsWartend_ReturnsTrue` ✓
    - `SollCliAnzeigen_WhenAusfuehrungsStatusIsBeendet_AndAufgabeStatusIsBeendet_ReturnsFalse` ✓
    - `SollCliAnzeigen_WhenAusfuehrungsStatusIsBeendet_AndAufgabeStatusIsArchiviert_ReturnsFalse` ✓
    - `SollCliAnzeigen_WhenAusfuehrungsStatusIsAktiv_AndAufgabeStatusIsGestartet_ReturnsTrue` ✓
    - `SollCliAnzeigen_WhenAusfuehrungsStatusIsNichtGestartet_AndAufgabeStatusIsGestartet_ReturnsFalse` ✓

- [x] **Anpassung bestehender Unit-Tests in `TaskDetailViewModelTests`**
  - Datei: `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`
  - Testmethode `ShowCliPanel_IsTrue_WhenAusfuehrungBeendetIst` (vorher: `ShowCliPanel_IsFalse_WhenAusfuehrungBeendetIst`)
  - Erwartung angepasst: `sut.ShowCliPanel.Should().BeTrue()` (vorher: `.BeFalse()`)
  - Testdokumentation aktualisiert

### E2E-Tests

- [x] **Neue E2E-Testklasse `E2E_CliPanelVisibility`** — angelegt
  - Datei: `src/Softwareschmiede.Tests/E2E/E2E_CliPanelVisibility.cs`
  - Testmethode: `CliPanel_BleibtSichtbarNachBeendigung_E2E`
  - Szenario: Aufgabe starten → CLI läuft → CLI stoppen → Überprüfe, dass CLI-Panel noch sichtbar ist und "Neustarten"-Button vorhanden ist
  - Integration in `MainTest.cs` durchgeführt

- [x] **Erweiterung bestehender E2E-Test `PluginAendernBeiLaufenderCli_StopptUndStartetMitNeuemPlugin_E2E`**
  - Datei: `src/Softwareschmiede.Tests/E2E/E2E_PluginAuswahlUndWechsel.cs`
  - Zusätzliche Assertions:
    - CLI-Panel-Sichtbarkeit vor dem Plugin-Wechsel überprüft
    - CLI-Panel-Sichtbarkeit während des gesamten Wechsels überprüft (Regressionstest für Zwischenstand)
  - Testdokumentation erweitert: Explizit auf die Korrektur des Arbeitsablaufs hingewiesen

---

## Planelemente im Detail

### 1. Korrektur von `AufgabeAusfuehrungsStatusExtensions.SollCliAnzeigen`
- **Geplant:** Bedingung von `== Aktiv` zu `is (Aktiv or Beendet)` erweitern
- **Umgesetzt:** ✓ Vollständig
- **Testnachweis:** `AufgabeAusfuehrungsStatusExtensionsTests.SollCliAnzeigen_WhenAusfuehrungsStatusIsBeendet_AndAufgabeStatusIsGestartet_ReturnsTrue`

### 2. Unit-Tests erweitern für `SollCliAnzeigen` mit `Beendet`-Status
- **Geplant:** Neue Testmethoden für `Beendet`-Status (mit aktiven und beendeten Aufgabenstatus)
- **Umgesetzt:** ✓ Vollständig (6 Testmethoden, alle vorhanden)
- **Testnachweis:** Alle 6 Tests in `AufgabeAusfuehrungsStatusExtensionsTests`

### 3. Überprüfung und Anpassung bestehender Unit-Tests
- **Geplant:** Tests durchsuchen und anpassen, die `ShowCliPanel` mit `Beendet`-Status überprüfen
- **Umgesetzt:** ✓ Vollständig
- **Betroffene Tests:**
  - `TaskDetailViewModelTests.ShowCliPanel_IsTrue_WhenAusfuehrungBeendetIst` — erwartete Rückgabe von `false` zu `true` angepasst
- **Testnachweis:** Geänderter Test selbst

### 4. E2E-Test für CLI-Panel-Sichtbarkeit nach Beendigung
- **Geplant:** Neuer E2E-Test, Szenario: Aufgabe starten → Ausführung beenden → CLI-Panel sichtbar + Neustarten möglich
- **Umgesetzt:** ✓ Vollständig
- **Datei:** `E2E_CliPanelVisibility.cs` (neu)
- **Testmethode:** `CliPanel_BleibtSichtbarNachBeendigung_E2E`
- **Abgedeckte Aspekte:**
  - CLI läuft, Panel ist sichtbar
  - CLI wird manuell gestoppt (AusfuehrungsStatus → Beendet)
  - Panel bleibt sichtbar (ShowCliPanel == true)
  - Letzte CLI-Ausgabe bleibt sichtbar
  - "Neustarten"-Button ist verfügbar (KannCliNeuStarten == true)
  - Aufgabenstatus bleibt unverändert

### 5. E2E-Test für Plugin-Wechsel mit CLI-Panel-Kontinuität
- **Geplant:** E2E-Test überprüft, dass CLI-Panel während Plugin-Wechsel nicht verschwindet
- **Umgesetzt:** ✓ Vollständig (Erweiterung bestehenden Tests)
- **Datei:** `E2E_PluginAuswahlUndWechsel.cs`
- **Testmethode:** `PluginAendernBeiLaufenderCli_StopptUndStartetMitNeuemPlugin_E2E` (erweitert)
- **Abgedeckte Aspekte:**
  - CLI-Panel sichtbar vor Plugin-Wechsel
  - Plugin wird gewechselt
  - Stoppen-Button ist nach Wechsel wieder vorhanden (CLI läuft mit neuem Plugin)
  - CLI-Panel bleibt sichtbar während des Wechsels (Regressionstest für Zwischenzustand mit AusfuehrungsStatus=Beendet)

---

## Verifikation der Implementierung

### Code-Struktur
- ✓ Methode `SollCliAnzeigen` korrekt geändert
- ✓ XML-Dokumentation präzise aktualisiert
- ✓ Änderung folgt bestehendem Pattern (auch `DarfAusfuehrungStarten` nutzt bereits `is (... or ...)`-Pattern)

### Test-Abdeckung
- ✓ 6 neue Unit-Tests für Erweiterungsmethode vorhanden
- ✓ 1 bestehender Unit-Test angepasst (Erwartung korrigiert)
- ✓ 1 neuer E2E-Test für Basis-Szenario
- ✓ 1 bestehender E2E-Test erweitert um Regressionstest
- ✓ Alle kritischen Grenzfälle abgedeckt:
  - `Beendet` + `Gestartet` → `true`
  - `Beendet` + `Wartend` → `true`
  - `Beendet` + `Beendet` → `false`
  - `Beendet` + `Archiviert` → `false`
  - `NichtGestartet` + `Gestartet` → `false`

### Konsistenz
- ✓ Alle geplanten Schritte vollständig durchgeführt
- ✓ Keine geplanten Elemente fehlend
- ✓ Keine zusätzlichen, nicht geplanten Elemente (Review nur auf Plan bezogen)

---

## Hinweise

### Abhängigkeiten und Zusammenhang
- Die Änderung an `SollCliAnzeigen` beeinflusst auch `KannCliNeuStarten` (nutzt ebenfalls `SollCliAnzeigen`)
- Plugin-Wechsel-Test deckt die subtile Timing-Abhängigkeit ab (AusfuehrungsStatus wechselt kurzzeitig auf Beendet)
- Tests verwenden Softwareschmiede.KiSimulator für Reproduzierbarkeit

### Architektur-Konformität
- ✓ Keine neuen Klassen/Typen eingeführt (reine Erweiterung bestehender Logik)
- ✓ Keine Datenbankmigrationen erforderlich
- ✓ Keine Validierungsregeln oder Konfigurationsänderungen erforderlich
- ✓ Änderung ist rückwärtskompatibel (nur Erweiterung der True-Bedingung)

### Testabsicherung
- Tests prüfen sowohl positive Szenarien (Panel bleibt sichtbar) als auch negative Szenarien (Panel verschwindet korrekt bei beendeter Aufgabe)
- E2E-Tests decken UI-Automation-Ebene ab (FlaUI)
- Unit-Tests isolieren die Erweiterungsmethode

---

**Review durchgeführt:** 2026-08-20  
**Reviewer:** Claude Code Plan-Review Agent  
**Ergebnis:** ALLE PLANELEMENTE VOLLSTÄNDIG UMGESETZT
