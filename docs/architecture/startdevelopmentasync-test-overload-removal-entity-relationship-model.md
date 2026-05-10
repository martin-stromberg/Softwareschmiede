# ERM: Signaturkonsolidierung `StartDevelopmentAsync`

## 1. Modellfokus
Für dieses Feature wird kein Datenbankschema geändert. Modelliert werden Laufzeit- und Test-Entitäten für die Overload-Entfernung:
- `PluginContract`
- `PluginMethod`
- `InvocationPath`
- `ExecutionId`
- `TestCase`
- `Invariant`

## 2. Entitäten und Beziehungen
| Entität | Attribute (Auszug) | Beziehung |
|---|---|---|
| PluginContract | name, canonicalSignature | 1:n zu `PluginMethod` |
| PluginMethod | name, signatureType(short/canonical) | n:1 zu `PluginContract` |
| InvocationPath | source, callShape | n:1 zu `PluginMethod` |
| ExecutionId | rawValue, normalizedValue | 1:1 zum kanonischen `PluginMethod`-Pfad |
| TestCase | name, expectedBehavior | n:1 zu `InvocationPath` |
| Invariant | rule, status | n:n zu `PluginMethod`/`TestCase` |

## 3. Zustandsmodell
1. DualSignature (Ist)
2. TestMigrated (alle Tests auf kanonische Signatur)
3. SignatureConsolidated (kurzer Overload entfernt)
4. RegressionValidated (Tests grün)

Fehlerkanten:
- Test nicht migriert → Compile-/Testfehler
- Semantikabweichung `executionId` → Regression

## 4. Invarianten
- Es existiert langfristig nur eine öffentliche `StartDevelopmentAsync`-Signatur.
- `executionId == null` bleibt zulässig und semantisch unverändert.
- Cleanup-, `.gitignore`- und CLI-Verhalten bleiben durch Tests abgesichert.
- Testaufrufe sind konsistent auf die kanonische Signatur ausgerichtet.

## 5. Mapping auf Code
| Modellbaustein | Datei |
|---|---|
| PluginContract | `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IKiPlugin.cs` |
| PluginMethod | `plugins/Softwareschmiede.Plugin.GitHubCopilot/GitHubCopilotPlugin.cs` |
| InvocationPath (App) | `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs` |
| InvocationPath (Tests) | `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubCopilotPluginTests.cs` |
| ExecutionId-Semantik | `GitHubCopilotPlugin.NormalizeAndValidateExecutionId` |

## 6. Testrelevante Kantenfälle
- Aufrufe mit `executionId: null`
- GUID im D-Format (Normalisierung auf N)
- Ungültige `executionId` (Exception)
- Cancellation-Weitergabe mit korrekter Parameterposition
- Fehlerpfade inkl. Cleanup

## 7. Traceability
- Anforderungen: `../requirements/startdevelopmentasync-test-overload-removal-requirements-analysis.md`
- Architektur: `./startdevelopmentasync-test-overload-removal-architecture-blueprint.md`
- Review: `../improvements/startdevelopmentasync-test-overload-removal-architecture-review.md`

