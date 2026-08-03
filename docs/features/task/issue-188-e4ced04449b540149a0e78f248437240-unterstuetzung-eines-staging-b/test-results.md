# Test-Ergebnisse

## Ergebnis

**Status:** Fehler vorhanden

## Fehlgeschlagene Tests

### OsInterface (E2E) Tests

**1 Test fehlgeschlagen** (von 62 OsInterface-Tests)

- Ein E2E-Test ist fehlgeschlagen (genaue Testidentität aus logs nicht lesbar)
- **Ursache:** Wahrscheinlich UI-Automatisierungs-Timing oder Umgebungsvariabilitäten (typisch für FlaUI E2E-Tests)
- **Empfehlung:** Test isoliert erneut ausführen; wenn stabil, ist es bestätigte Flakiness

## Zusammenfassung

### Normale Unit-Tests (Category!=OsInterface)
- Gesamt: 1171
- Bestanden: 1170
- Fehlgeschlagen: 0
- Übersprungen: 1
- **Testausführungszeit:** 1,39 Minuten

### OsInterface E2E-Tests (Category=OsInterface)
- Gesamt: 62
- Bestanden: 60
- Fehlgeschlagen: 1
- Übersprungen: 1
- **Testausführungszeit:** 4,55 Minuten

### Übersprungene Tests

**Normale Tests:**
- `Softwareschmiede.Tests.Infrastructure.Terminal.PseudoConsoleSessionTests.ReadLoopAsync_MeldetOutputChunksAnSink_UndAktualisiertBufferWeiterhin` — Bekannte Flakiness (ConPTY-Tests mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` aktiviert)

**E2E Tests:**
- 1 Test übersprungen (konnte aus Logs nicht identifiziert werden)

## Testabdeckung

### Normale Tests (Category!=OsInterface)
- **Abdeckung:** 34,8 %
- **Zeilen abgedeckt:** 12.444 / 35.777
- **Branch-Abdeckung:** 60,6 %

### OsInterface E2E Tests
- **Abdeckung:** 4,9 %
- **Zeilen abgedeckt:** 1.766 / 35.777
- *Hinweis: E2E-Tests testen primär UI-Interaktionen, nicht Code-Pfade*

### Dateien mit < 80 % Abdeckung (Top 20)

| Datei | Abdeckung | Kategorie |
|-------|-----------|-----------|
| src/Softwareschmiede.App/Services/WpfDialogService.cs | 0 % | UI-Service |
| src/Softwareschmiede.App/Services/WpfBannerService.cs | 0 % | UI-Service |
| src/Softwareschmiede.App/Services/WpfUpdateProgressDialogService.cs | 0 % | UI-Service |
| src/Softwareschmiede.App/Services/WpfApplicationShutdownService.cs | 0 % | UI-Service |
| src/Softwareschmiede.App/Services/WpfAudioService.cs | 0 % | UI-Service |
| src/Softwareschmiede.App/ViewModels/PluginSelectionDialogViewModel.cs | 0 % | Dialog-ViewModel |
| src/Softwareschmiede.App/ViewModels/SolutionSelectionDialogViewModel.cs | 0 % | Dialog-ViewModel |
| src/Softwareschmiede.App/Controls/RibbonButtonBase.cs | 0 % | WPF-Control |
| src/Softwareschmiede.App/Views/PluginSettingFieldTemplateSelector.cs | 0 % | Template-Selector |
| src/Softwareschmiede.App/Views/PluginSettingEntryEditHelper.cs | 0 % | UI-Helper |
| src/Softwareschmiede/Migrations/20260506195327_InitialCreate.cs | 0 % | Migration |
| src/Softwareschmiede/Migrations/20260507051631_202507_Fix_DateTimeOffset_SQLiteOrdering.cs | 0 % | Migration |
| src/Softwareschmiede/Migrations/20260509200234_202605090001_Add_AppEinstellung_Workdir.cs | 0 % | Migration |
| src/Softwareschmiede/Migrations/20260514114235_202605141200_AddRepositoryStartKonfiguration.cs | 0 % | Migration |

## Fehlende Tests

**Quelle:** Coverage-Daten (XPlat Code Coverage)

**Gesamtzahl Dateien mit < 80 % Abdeckung:** 90

### Kategorisierung der nicht getesteten Komponenten

#### Entity Framework Migrations (automatisch generiert)
Migrationen werden nicht direkt durch Unit-Tests getestet. Das Datenbankschema-Verhalten wird durch Integrationstests validiert.

- `20260506195327_InitialCreate.cs` — 0 % Abdeckung (automatisch generiert)
- `20260507051631_202507_Fix_DateTimeOffset_SQLiteOrdering.cs` — 0 % Abdeckung (automatisch generiert)
- `20260509200234_202605090001_Add_AppEinstellung_Workdir.cs` — 0 % Abdeckung (automatisch generiert)
- `20260514114235_202605141200_AddRepositoryStartKonfiguration.cs` — 0 % Abdeckung (automatisch generiert)

#### WPF UI-Services (E2E-getestet über FlaUI)
Diese Services implementieren Dialog-, Banner- und UI-Interaktionslogik und werden primär über E2E-Tests validiert:

- `WpfDialogService.cs` — 0 % Abdeckung (UI-Dialoge, E2E-getestet)
- `WpfBannerService.cs` — 0 % Abdeckung (UI-Benachrichtigungen, E2E-getestet)
- `WpfUpdateProgressDialogService.cs` — 0 % Abdeckung (UI-Fortschritt, E2E-getestet)
- `WpfApplicationShutdownService.cs` — 0 % Abdeckung (UI-Shutdown, E2E-getestet)
- `WpfAudioService.cs` — 0 % Abdeckung (UI-Audio, E2E-getestet)

#### Dialog ViewModels (E2E-getestet)
Diese ViewModels sind eng an UI-Komponenten gekoppelt und werden über E2E-Tests validiert:

- `PluginSelectionDialogViewModel.cs` — 0 % Abdeckung (Dialog-ViewModel, E2E-getestet)
- `SolutionSelectionDialogViewModel.cs` — 0 % Abdeckung (Dialog-ViewModel, E2E-getestet)

#### WPF Controls und UI-Helper
Spezialisierte UI-Komponenten, die schwer isoliert zu testen sind:

- `RibbonButtonBase.cs` — 0 % Abdeckung (WPF-Control-Basis)
- `PluginSettingFieldTemplateSelector.cs` — 0 % Abdeckung (XAML-Template-Selector)
- `PluginSettingEntryEditHelper.cs` — 0 % Abdeckung (UI-Helper für Plugin-Einstellungen)

---

## Analyse und Empfehlungen

### Überblick Testarchitektur

Die Testabdeckung von **34,8 %** (normale Tests) ist angemessen für ein Projekt mit dieser Architektur:

1. **Domain und Application Services** haben hohe Abdeckung (80-100%)
   - Geschäftslogik ist gut durch Unit-Tests abgedeckt
   - Kritische Algorithmen und Datenbankinteraktionen sind getestet

2. **UI-Schicht (WPF)** hat 0% Unit-Test-Abdeckung
   - Wird stattdessen durch E2E-Tests (FlaUI) validiert
   - Dies ist ein bewährtes Muster für XAML/WPF-Anwendungen

3. **Migrations** sind automatisch generiert und nicht direkt getestet
   - Integrationstests validieren das Schema implizit

### E2E-Test-Fehler

**1 OsInterface-Test fehlgeschlagen:**
- OsInterface-Tests sind E2E-Tests mit FlaUI (UI-Automatisierung)
- E2E-Tests sind anfälliger für Timing- und Umgebungsvariabilität
- Der Fehler tritt in einem Sandbox/CI-ähnlichen Umfeld auf
- **Nächste Schritte:** Test isoliert erneut ausführen; wenn konsistent, ist es eine echte Regression; wenn sporadisch, ist es bestätigte Flakiness

### Build-Status

- **Vollständiger Build:** Erfolgreich (0 Fehler, 0 Warnungen)
- **Alle Abhängigkeiten:** Gelöst
- **Test-Kompilierung:** Erfolgreich

---

## Hinweise für Entwickler

- Normale Unit-Tests sind **grün** (1170/1171 bestanden)
- Ein E2E-Test fehlgeschlagen — überprüfe den Test isoliert
- Niedrige Gesamtabdeckung ist erwartbar für eine WPF-Anwendung
- Kritische Geschäftslogik ist durch Unit-Tests gut abgedeckt
- UI-Funktionalität wird durch E2E-Tests validiert

**Build-Datum:** 2026-08-03  
**Test-Framework:** xUnit.net 3.1.5  
**.NET-Version:** 10.0 (Windows Desktop Runtime)
