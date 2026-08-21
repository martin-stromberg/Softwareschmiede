# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### src/Softwareschmiede/Application/Services/ProjektleiterAgentService.cs (ProjektleiterAgentService)

- **Kopplung / Typmissbrauch (Inappropriate Intimacy an der Grenze zu Primitive Obsession)** — In `SteuereUnteragentAsync` (Zeile 70–74) wird eine `UnteragentSpezifikation`-Instanz (`arbeitsbereichsGrenze`) ausschließlich als Trägerobjekt für zwei Felder (`AgentId`, `AgentDirectory`) erzeugt, um sie an `UnteragentGovernanceService.VerifiziereBerechtigung` zu übergeben. `UnteragentSpezifikation` ist eine EF-Entität, die einen echten, persistierten Unteragenten repräsentiert (u. a. mit Navigationseigenschaft `AutonomAufgabe`); hier wird sie zweckentfremdet, um lediglich "erlaubter Basispfad" zu transportieren. Der Aufrufkontext prüft inhaltlich etwas anderes als der von `VerifiziereBerechtigung` dokumentierte Vertrag ("validiert, dass ein Unteragent nur in seinem eigenen Bereich arbeitet") — hier wird stattdessen geprüft, ob das neue Unteragenten-Arbeitsverzeichnis innerhalb des Arbeitsverzeichnisses der Autonomen Aufgabe liegt. Das ist eine andere Governance-Frage (Grenze der Aufgabe vs. Grenze eines bereits etablierten Unteragenten), die über dieselbe Methode und ein zweckentfremdetes Domänenobjekt abgebildet wird.

  Empfehlung: Eine dedizierte Überladung bzw. Methode in `UnteragentGovernanceService` ergänzen, die zwei reine Pfad-Strings entgegennimmt (z. B. `VerifiziereArbeitsbereichsGrenze(string erlaubterBasisPfad, string zielPfad, string agentIdFuerLogging)` oder ähnlich), statt eine fachliche Entität für einen rein technischen Pfadvergleich zu instanziieren. Alternativ: bestehende Methode so umbenennen/dokumentieren, dass der allgemeinere Zweck ("Pfad X muss unterhalb Pfad Y liegen") klar wird, und den Parametertyp auf `string erlaubterBasisPfad` statt `UnteragentSpezifikation` ändern.

### src/Softwareschmiede.Tests/Application/Services/ProjektleiterAgentServiceTests.cs (ProjektleiterAgentServiceTests)

- **Doppelter Code / unvollständige Extraktion** — Die im selben Änderungspaket neu geschaffene `ProjektleiterAgentServiceTestDatenFactory.ErstelleUnteragent(...)` (in `src/Softwareschmiede.Tests/Helpers/ProjektleiterAgentServiceTestDatenFactory.cs`) wurde explizit zur Vermeidung von Testcode-Duplikation extrahiert und bereits in `ProjektleiterAgentServiceTests_Fehlerfaelle.cs` eingesetzt. In `ProjektleiterAgentServiceTests.cs` selbst (die den Namespace `Softwareschmiede.Tests.Helpers` bereits importiert, siehe Zeile 9) wurde die Umstellung jedoch nicht vollzogen: Zeilen 100–111 (`SteuereUnteragentAsync_ErzeugtUnteragentSpezifikation`) sind eine 1:1-Kopie dessen, was `ProjektleiterAgentServiceTestDatenFactory.ErstelleUnteragent(_testRoot, konfiguration.Id)` (Default-Suffix "001") liefert. Zeilen 134–147 (`IntegriereErgebnisseAsync_AktualisieertPlanMdUndProgressMd`) duplizieren dieselbe Konstruktion mit Suffix "002" plus zwei zusätzlichen Property-Zuweisungen (`ErzeugungsDatum`, `Status`), die sich problemlos nach dem Fabrikaufruf ergänzen ließen. Damit bleibt genau die Duplikation bestehen, die Batch 2 laut Aufgabenbeschreibung beheben sollte.

  Empfehlung: Beide Stellen auf `ProjektleiterAgentServiceTestDatenFactory.ErstelleUnteragent(_testRoot, konfiguration.Id, "001"/"002")` umstellen; bei der zweiten Stelle `ErzeugungsDatum`/`Status` anschließend auf dem zurückgegebenen Objekt setzen (Objektinitialisierer entfällt dann, stattdessen Property-Zuweisung nach Fabrikaufruf oder `with`-Ausdruck, falls sinnvoll gekapselt).

## Geprüfte Dateien

- `src/Softwareschmiede/Application/Services/ProjektleiterAgentService.cs`
- `src/Softwareschmiede/Application/Services/UnteragentGovernanceService.cs` (Kontext, unverändert)
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTestsBase.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_PluginAktivierung.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_ZeitgesteuerterPrompt.cs`
- `src/Softwareschmiede.Tests/Application/Services/ProjektleiterAgentServiceTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/ProjektleiterAgentServiceTests_Fehlerfaelle.cs`
- `src/Softwareschmiede.Tests/Helpers/TaskDetailViewModelTestFactory.cs`
- `src/Softwareschmiede.Tests/Helpers/ProjektleiterAgentServiceTestDatenFactory.cs` (neu)
