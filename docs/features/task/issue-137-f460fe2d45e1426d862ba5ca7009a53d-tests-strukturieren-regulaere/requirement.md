# Kundenanforderung - Tests strukturieren: regulaere vs. OS-Schnittstellen-Tests trennen, Mocking ausweiten, Flakiness in den Griff bekommen

## Fachliche Zusammenfassung

Die bestehende Testsuite vermischt deterministische Unit-/Logik-Tests mit Tests, die echte Betriebssystem-Schnittstellen verwenden. Dazu gehoeren insbesondere ConPTY- und Prozessstart-Tests, Zwischenablage-Zugriffe sowie Dateisystem-Operationen mit potenziellen Locks.

Diese Vermischung fuehrt dazu, dass Testlaeufe sporadisch fehlschlagen koennen, obwohl der eigentlich getestete Anwendungscode nicht fehlerhaft ist. Jeder Fehlschlag muss aktuell manuell eingeordnet werden: echter Regressionsfehler oder umgebungsbedingte Flakiness.

Ziel ist eine klare strukturelle Trennung:

1. Regulaere, deterministische Tests muessen separat ausfuehrbar und zuverlaessig gruen sein.
2. Tests mit echter OS-Schnittstellen-Beruehrung muessen explizit gekennzeichnet, separat auswertbar und separat behandelbar sein.
3. Unnoetige echte OS-Zugriffe in Unit-/Logik-Tests sollen durch injizierbare Abstraktionen und Mocking ersetzt werden.

## Betroffene Klassen und Komponenten

### Teststruktur und Testausfuehrung
- xUnit-Testprojekte und Testklassen
- Testfilterung ueber `Trait`/Kategorie-Attribute
- `dotnet test`-Aufrufe, insbesondere Filter wie `Category!=OsInterface`
- `/run-tests`-Auswertung
- `/lifecycle`-Iterationslogik und Interpretation von Testergebnissen
- CI-Konfiguration fuer Pflicht- und optionale Testlaeufe

### OS-Schnittstellen-Tests
- ConPTY-basierte CLI-Tests
- Tests mit echtem Prozessstart
- Tests mit Zugriff auf die Zwischenablage
- Tests mit Dateisystem-Operationen, Cleanup und Locks
- E2E-Tests mit empfindlichem Umgebungszustand

### Mocking und Test-Doubles
- `TaskDetailViewModelTestFactory`
- `KiAusfuehrungsService`
- ViewModel-Unit-Tests mit Prozessausfuehrung
- Abstraktionen fuer Prozessstart, ConPTY, Clipboard und Dateisystemzugriffe

### Bekannte Problemfelder
- ConPTY-Tests scheitern in Sandbox-/Agenten-Umgebungen ohne isoliertes Pseudo-Terminal.
- Zwischenablage-Zugriffe koennen mit `CLIPBRD_E_CANT_OPEN` fehlschlagen.
- Cleanup-Operationen koennen mit `UnauthorizedAccessException` bei `Directory.Delete` scheitern.
- Echte Prozessstarts in ViewModel-Unit-Tests verursachen Race-Conditions bei `IsRunning`.
- E2E-Tests reagieren empfindlich darauf, wenn parallel das Kompilat veraendert wird.

## Gewuenschtes Verhalten

### Regulaere Tests

Regulaere Tests sind deterministische Unit-/Logik-Tests ohne echte OS-Schnittstellen-Beruehrung. Sie muessen ohne besondere Umgebungsannahmen laufen und duerfen keine echten Prozessstarts, ConPTY-Sitzungen, Clipboard-Zugriffe oder instabilen Dateisystem-Lock-Szenarien ausloesen.

Der folgende Befehl muss nur regulaere Tests ausfuehren und zuverlaessig gruen sein:

```powershell
dotnet test --filter Category!=OsInterface
```

### OS-Schnittstellen-Tests

Tests, die bewusst echte Betriebssystem-Schnittstellen verwenden, muessen explizit als OS-Schnittstellen-Tests markiert werden. Diese Tests duerfen separat ausgefuehrt, separat ausgewertet und bei bekannten Umgebungsproblemen gesondert behandelt werden.

Vorgesehene Kategorie:

```csharp
[Trait("Category", "OsInterface")]
```

Alternativ kann ein projektspezifisches Attribut eingefuehrt werden, sofern es xUnit-kompatibel als `Category=OsInterface` filterbar ist.

## Implementierungsansatz

### Testkategorien einfuehren

Alle Tests sollen in zwei Gruppen eingeteilt werden:

- **Regulaere Tests:** Standardfall, deterministisch, ohne echte OS-Schnittstellen.
- **OS-Schnittstellen-Tests:** Tests mit echter Interaktion mit ConPTY, Prozessstart, Clipboard, Dateisystem-Locks oder vergleichbaren Umgebungsabhaengigkeiten.

OS-Schnittstellen-Tests erhalten ein zentrales Kategorie-Attribut. Die regulaeren Tests muessen durch Ausschluss dieser Kategorie filterbar bleiben.

### Mocking ausweiten

Unit- und ViewModel-Tests sollen keine echten OS-Funktionen verwenden, wenn diese nicht ausdruecklich Teil des Testziels sind. Stattdessen sollen bestehende oder neue injizierbare Abstraktionen verwendet werden.

Konkret relevant:

- `TaskDetailViewModelTestFactory` darf fuer regulaere ViewModel-Unit-Tests keinen echten `KiAusfuehrungsService` mit echtem Prozessstart verwenden.
- Prozessausfuehrung, ConPTY-Zugriff, Clipboard-Zugriff und kritische Dateisystem-Operationen sollen ueber Interfaces oder bereits vorhandene Services injizierbar sein.
- Regulaere Tests verwenden Mocks, Fakes oder deterministische In-Memory-Implementierungen.
- Nur explizite OS-Schnittstellen-Tests verwenden die echten Implementierungen.

### Retry-Strategie begrenzen

Retry-Mechanismen sollen nicht pauschal fuer alle Tests eingesetzt werden. Retry ist nur fuer OS-Schnittstellen-Tests zulaessig, bei denen bekannte externe Flakiness auftreten kann.

Regulaere Tests sollen ohne Retry stabil sein. Ein regulaerer Testfehler gilt als echter Fehler und soll in `/run-tests`, `/lifecycle` und CI als blockierend behandelt werden.

### Testauswertung trennen

Die Testauswertung soll regulaere Testfehler und OS-Schnittstellen-Fehler getrennt darstellen.

Erwartetes Verhalten:

- `/run-tests` weist regulaere Fehler als eigentliche Fehlermenge aus.
- OS-Schnittstellen-Fehler werden separat gelistet und nicht mit regulaeren Regressionsfehlern vermischt.
- `/lifecycle` laesst nur regulaere Fehlschlaege in die Iterationslogik einfliessen.
- OS-Schnittstellen-Fehler werden dokumentiert, aber separat bewertet.

### CI zweiteilen

Die CI soll zwei logisch getrennte Testlaeufe erhalten:

1. **Pflicht-Lauf:** Regulaere Tests mit Ausschluss von `Category=OsInterface`. Dieser Lauf muss stabil gruen sein und blockiert bei Fehlern.
2. **Optionaler oder Best-Effort-Lauf:** OS-Schnittstellen-Tests. Dieser Lauf darf separat reportet werden und kann je nach Umgebung optional, nicht-blockierend oder mit Retry laufen.

## Technische Umsetzung

### Test-Attribut

Es soll ein zentral verwendbares Attribut fuer OS-Schnittstellen-Tests eingefuehrt oder etabliert werden, zum Beispiel:

```csharp
public sealed class OsInterfaceFactAttribute : FactAttribute
{
}
```

Falls ein reines Attribut nicht ausreicht, um xUnit-Traits zu setzen, soll eine xUnit-kompatible Trait-Implementierung oder ein bestehendes Muster im Projekt verwendet werden. Entscheidend ist, dass der Filter `Category=OsInterface` funktioniert.

### Kategorisierung bestehender Tests

Mindestens folgende Testarten sind zu pruefen und bei echter OS-Beruehrung als `OsInterface` zu markieren:

- ConPTY-basierte CLI-Tests
- Tests, die echte Prozesse starten
- Tests, die auf die Zwischenablage zugreifen
- Tests, die Dateisystem-Locks oder Cleanup-Rennen ausloesen koennen
- E2E-Tests, die vom Zustand des gebauten Kompilats oder laufenden Prozessen abhaengen

### Entkopplung regulaerer Tests

Bestehende regulaere Tests, die unbeabsichtigt echte OS-Funktionen verwenden, sollen umgestellt werden:

- echte Services durch Mocks ersetzen
- Test-Factories so anpassen, dass sie standardmaessig Test-Doubles verwenden
- echte Implementierungen nur noch in expliziten OS-Schnittstellen-Tests einsetzen
- Race-Conditions durch deterministische Steuerung von asynchronem Verhalten vermeiden

### Dokumentation bekannter Sonderfaelle

Es soll eine zentrale Dokumentation geben, die bekannte OS-Schnittstellen-Sonderfaelle beschreibt:

- Welche Tests gehoeren zur Kategorie `OsInterface`?
- Welche Umgebungsabhaengigkeiten haben sie?
- Welche bekannten Fehlerbilder sind umgebungsbedingt?
- Welche Workarounds oder Env-Flags existieren, zum Beispiel `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`?
- Welche Retry-Regeln gelten?

## Akzeptanzkriterien

1. Alle Tests mit echter OS-Schnittstellen-Beruehrung sind als `Category=OsInterface` markiert oder anderweitig xUnit-kompatibel in dieser Kategorie filterbar.
2. `dotnet test --filter Category!=OsInterface` fuehrt nur regulaere Tests aus.
3. Der regulaere Testlauf ist deterministisch und benoetigt keine echten ConPTY-, Clipboard- oder Prozessstart-Zugriffe.
4. ViewModel-Unit-Tests verwenden keine echten Prozessstarts mehr, sofern der Prozessstart nicht ausdruecklich Testgegenstand ist.
5. `TaskDetailViewModelTestFactory` verwendet fuer regulaere Tests keinen echten `KiAusfuehrungsService`, wenn dadurch ConPTY- oder Prozessstart-Verhalten ausgeloest wird.
6. Retry-Logik wird nur fuer OS-Schnittstellen-Tests verwendet.
7. `/run-tests` unterscheidet regulaere Testfehler von OS-Schnittstellen-Fehlern.
8. `/lifecycle` beruecksichtigt fuer automatische Iterationsentscheidungen nur regulaere Testfehler.
9. CI enthaelt einen blockierenden regulaeren Testlauf und einen separaten optionalen oder best-effort OS-Schnittstellen-Testlauf.
10. Bekannte Sonderfaelle und Umgebungsabhaengigkeiten sind zentral dokumentiert.

## Nicht-Ziele

- OS-Schnittstellen-Tests sollen nicht entfernt werden.
- Echte ConPTY-, Clipboard-, Prozessstart- oder Dateisystem-Tests sollen nicht pauschal deaktiviert werden.
- Flaky OS-Schnittstellen-Tests sollen nicht als regulaere Testfehler kaschiert werden.
- Retry soll nicht zur Stabilisierung regulaerer Unit-/Logik-Tests eingesetzt werden.

## Verwandte Issues

- #125: Echter Prozess-Start in ViewModel-Unit-Tests fuehrt unter Last zu Race-Conditions bei `IsRunning`.
- #114: E2E-Tests reagieren empfindlich auf Umgebungszustand, insbesondere wenn parallel das Kompilat veraendert wird.

## Offene Fragen

1. Soll das Projekt ein eigenes Attribut wie `OsInterfaceFact`/`OsInterfaceTheory` einfuehren oder direkt xUnit-`Trait` verwenden?
2. Soll der OS-Schnittstellen-Lauf in CI nicht-blockierend sein oder nur fuer bestimmte Umgebungen/Runner aktiviert werden?
3. Welche bestehenden Env-Flags neben `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` sollen weiterhin unterstuetzt oder ersetzt werden?
4. Wo soll die zentrale Dokumentation der Sonderfaelle abgelegt werden?
5. Soll `/run-tests` OS-Schnittstellen-Tests standardmaessig ausfuehren und separat reporten oder standardmaessig nur regulaere Tests ausfuehren?
