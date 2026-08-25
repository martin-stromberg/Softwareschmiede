# Bestandsaufnahme: Verbesserung der E2E-Tests — View-Pattern

Diese Bestandsaufnahme analysiert die bestehende Codebase in Hinblick auf die in `requirement.md` geforderte Einführung eines View-Patterns für E2E-Tests.

## Zusammenfassung

**Bereich:** E2E-Test-Infrastruktur (FlaUI-basierte UI-Automation)

**Wesentliche Befunde:**

- `WpfTestBase` existiert als zentrale Test-Basisklasse mit umfangreichen Hilfsmethoden für Navigation, Element-Suche und UI-Interaktion
- FlaUI-Infrastruktur (UIA3Automation, Application, Window, AutomationElement) ist vollständig vorhanden
- **Noch nicht vorhanden:** Der geplante `Softwareschmiede.Tests.E2E.Views`-Namespace mit `BaseWindowView`, `MenuView` und view-spezifischen Subklassen
- **Noch nicht vorhanden:** Extension-Methode `Window.CurrentView()` zur automatischen View-Erkennung
- Existierende App-Views in `src/Softwareschmiede.App/Views/` definieren die verfügbaren Anwendungsansichten, deren UI-Strukturen das View-Pattern später abbilden soll
- E2E-Tests verwenden derzeit direkt FlaUI-Aufrufe über `WpfTestBase`-Hilfsmethoden, ohne ein strukturiertes View-Pattern

## Details

- [Test-Infrastruktur und WpfTestBase](inventory/test-infrastructure.md)
- [Bestehende App-Views](inventory/app-views.md)
- [FlaUI-Integration](inventory/flaui-integration.md)
