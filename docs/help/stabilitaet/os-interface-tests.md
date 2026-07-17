# OS-Schnittstellen-Tests

Regulaere Tests sind deterministische Unit-, Service- und ViewModel-Tests ohne echte Betriebssystem-Schnittstellen. Sie duerfen keine echten ConPTY-Sitzungen, Prozessstarts, Clipboard-Zugriffe oder lockanfaellige Dateisystem-Szenarien benoetigen.

OS-Schnittstellen-Tests pruefen genau diese Umgebungskontakte. Sie bleiben erhalten, werden aber getrennt markiert und ausgewertet.

## Kategorie

OS-nahe Tests tragen xUnit-kompatibel:

```csharp
[Trait("Category", "OsInterface")]
```

Im Testprojekt `Softwareschmiede.Tests` stehen dafuer zentrale Attribute bereit:

```csharp
[OsInterface]
[OsInterfaceFact]
[OsInterfaceTheory]
```

Vorhandene Spezialkategorien wie `E2E` und `ConPTY` bleiben bestehen. `OsInterface` wird zusaetzlich gesetzt, damit alte lokale Filter weiter nutzbar bleiben.

## Lokale Befehle

```powershell
# Nur regulaere Tests
dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"

# Nur OS-Schnittstellen-Tests
dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category=OsInterface"

# Einzeltest-Skript, regulaerer Standardlauf
./scripts/Run-AllTestsIndividually.ps1

# Einzeltest-Skript fuer OS-Schnittstellen-Tests
./scripts/Run-AllTestsIndividually.ps1 -TestSet OsInterface
```

Die Kategorie-Konvention gilt auch fuer `Softwareschmiede.IntegrationTests`.

## CI-Verhalten

Der Workflow `.github/workflows/test.yml` baut und testet sowohl `Softwareschmiede.Tests` als auch `Softwareschmiede.IntegrationTests`. Beide Projekte laufen in getrennten Regular- und OS-Schnittstellen-Lanes:

- `Test regular tests`: blockierend, `Softwareschmiede.Tests`, `Category!=OsInterface`
- `Test regular integration`: blockierend, `Softwareschmiede.IntegrationTests`, `Category!=OsInterface`
- `Test OS interfaces tests`: best-effort, `Softwareschmiede.Tests`, `Category=OsInterface`, `continue-on-error: true`
- `Test OS interfaces integration`: best-effort, `Softwareschmiede.IntegrationTests`, `Category=OsInterface`, `continue-on-error: true`
- TRX-Artefakte werden getrennt als `test-results-regular` und `test-results-os-interface` hochgeladen.

Regulaere Fehler gelten als Regressionsfehler. OS-Schnittstellen-Fehler werden sichtbar gemacht, blockieren PRs aber nicht automatisch, solange die Runner-Umgebung fuer Desktop, ConPTY oder Clipboard nicht stabil genug ist.

## Bekannte Fehlerbilder

- ConPTY ist in Sandbox-, Agenten- oder nicht interaktiven Desktop-Umgebungen nicht immer verfuegbar.
- Clipboard-Zugriffe koennen transient mit `CLIPBRD_E_CANT_OPEN` fehlschlagen.
- Dateisystem-Cleanup kann durch Locks oder Read-only-Attribute scheitern.
- E2E-Tests koennen fehlschlagen, wenn parallel Build-Artefakte veraendert werden.
- Testhost- oder Build-Artefakt-Rennen sind Infrastrukturfehler und werden separat von Assertion-Fehlern bewertet.

## Env-Flags und Retry

`SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` bleibt fuer automatisierte Sandbox-Laeufe erhalten, in denen echte ConPTY-Isolation nicht funktioniert.

Retry ist nicht zur Stabilisierung regulaerer Tests vorgesehen. Das Einzeltest-Skript wiederholt nur erkannte Infrastruktur-/Ausfuehrungsfehler nach Rebuild-Recovery. Fachliche Assertion-Fehler regulaerer Tests werden nicht kaschiert. OS-Schnittstellen-Fehler duerfen separat dokumentiert und bei bekannter Umgebungsflakiness getrennt bewertet werden.

## Externe Runner

Fuer `/run-tests` gilt die erwartete Auswertung:

- regulaere Fehler bilden die blockierende Fehlermenge
- OS-Schnittstellen-Fehler werden separat gelistet
- Infrastruktur-/Ausfuehrungsfehler werden getrennt ausgewiesen

Fuer `/lifecycle` gilt entsprechend: automatische Iterationsentscheidungen sollen regulaere Fehlschlaege beruecksichtigen. OS-Schnittstellen-Fehler werden dokumentiert, aber nicht als regulaerer Regressionsfehler gezaehlt.
