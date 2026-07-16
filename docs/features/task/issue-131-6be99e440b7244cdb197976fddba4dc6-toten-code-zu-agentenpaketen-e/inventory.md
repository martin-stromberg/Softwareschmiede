# Bestandsaufnahme: Toten Code zu Agentenpaketen entfernen

Diese Bestandsaufnahme dokumentiert den bestehenden Code zum Feature „Agentenpakete", das bereits aus der Dokumentation (README.md, Architektur-Diagramm) entfernt wurde. Der Code besteht aus sieben Dateien in drei Bereichen: Interfaces (Domain Layer), ValueObject (Domain Layer), Services (Infrastructure Layer) und Tests.

## Zusammenfassung

### Betroffene Komponenten
- **Interfaces:** 2 Schnittstellen (`IAgentPackageService`, `IAgentPackageFileService`)
- **ValueObject:** 1 Record (`AgentPackageInfo`)
- **Services:** 2 Implementierungen (`AgentPackageReader`, `AgentPackageFileService`)
- **Tests:** 2 Testklassen (Unit und Integration) mit insgesamt 19 Testmethoden

### Abhängigkeitsstruktur
- `AgentPackageInfo` wird referenziert von beiden Interfaces und Implementierungen
- `IAgentPackageService` wird von `AgentPackageReader` implementiert
- `IAgentPackageFileService` wird von `AgentPackageFileService` implementiert
- Keine Registrierung in `Program.cs` (bestätigt durch Anforderung)
- Keine weitere Verwendung außerhalb der Testklassen

### Erkannte Abhängigkeiten
- `AgentInfo` wird in `AgentPackageInfo.Agenten` verwendet, ist aber selbst in `Softwareschmiede.Plugin.Contracts` definiert und wird in `CliKiPluginBase.cs` benötigt (nicht zu löschen)
- `FileTreeNode` wird in `IAgentPackageFileService.BuildPackageTreeAsync` verwendet und muss ggf. überprüft werden

## Details

- [Interfaces](inventory/interfaces.md)
- [Datenmodell](inventory/models.md)
- [Logik / Services](inventory/logic.md)
- [Tests](inventory/tests.md)
