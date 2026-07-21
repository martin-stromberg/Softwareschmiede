# IDE-Service und Prozessstart

## IdeOeffnenService

`IdeOeffnenService` ist aktuell klein und fokussiert:

- `FindeSolutions(string? arbeitsverzeichnis)` prueft null/leer und `Directory.Exists()`.
- Es sucht nur `*.sln` auf oberster Ebene (`SearchOption.TopDirectoryOnly`).
- Es sortiert alphabetisch mit `StringComparer.OrdinalIgnoreCase`.
- `OeffneSolution(string solutionPfad)` validiert den Pfad und ruft `IProzessStarter.Starten()` mit `ShellAusfuehren=true` auf.

Fundstellen:

- `src/Softwareschmiede/Application/Services/IdeOeffnenService.cs:13`
- `src/Softwareschmiede/Application/Services/IdeOeffnenService.cs:18`
- `src/Softwareschmiede/Application/Services/IdeOeffnenService.cs:25`
- `src/Softwareschmiede/Application/Services/IdeOeffnenService.cs:29`

## Fehlende VS-Code-Faehigkeiten

Der Service hat noch keine Methoden fuer:

- Erkennen, ob Visual Studio Code verfuegbar ist.
- Aufloesen eines geeigneten Befehls (`code`, `code.cmd`, absoluter Installationspfad).
- Oeffnen eines Arbeitsverzeichnisses mit VS Code.
- Differenzierte Rueckgabe, warum kein Fallback moeglich ist.

Die Anforderung passt fachlich gut in oder neben `IdeOeffnenService`, weil dieser bereits die IDE-Aktion buendelt. Wenn die VS-Code-Erkennung umfangreicher wird, kann eine separate Hilfsklasse oder ein kleiner Resolver sinnvoll sein, solange `TaskDetailViewModel` nicht direkt PATH und Installationspfade kennen muss.

## Prozessstart-Abstraktion

`IProzessStarter` kapselt den eigentlichen OS-Prozessstart:

```csharp
void Starten(ProzessStartAnfrage anfrage);
```

`ProzessStartAnfrage` enthaelt:

- `DateiName`
- `Argumente`
- `ShellAusfuehren`

`SystemProzessStarter` mappt diese Felder auf `ProcessStartInfo.FileName`, `Arguments` und `UseShellExecute`, loggt den Start und ruft `Process.Start()` auf.

Fundstellen:

- `src/Softwareschmiede/Domain/Interfaces/IProzessStarter.cs:6`
- `src/Softwareschmiede/Domain/ValueObjects/ProzessStartAnfrage.cs:7`
- `src/Softwareschmiede/Infrastructure/Services/SystemProzessStarter.cs:12`
- `src/Softwareschmiede/Infrastructure/Services/SystemProzessStarter.cs:22`
- `src/Softwareschmiede/Infrastructure/Services/SystemProzessStarter.cs:29`

## Testmodus fuer Prozessstarts

Im Testmodus registriert `App.xaml.cs` statt `SystemProzessStarter` einen `AufzeichnenderProzessStarter`. Dieser schreibt jede Anfrage in `prozess-starts.log`, ohne echte Prozesse zu starten.

Das ist fuer den VS-Code-Fallback hilfreich: E2E-Tests koennen pruefen, ob `code`/`code.cmd` mit dem Arbeitsverzeichnis als Argument aufgezeichnet wurde.

Fundstellen:

- `src/Softwareschmiede.App/App.xaml.cs:204`
- `src/Softwareschmiede.App/App.xaml.cs:207`
- `src/Softwareschmiede.App/App.xaml.cs:214`
- `src/Softwareschmiede/Infrastructure/Services/AufzeichnenderProzessStarter.cs:28`
- `src/Softwareschmiede/Infrastructure/Services/AufzeichnenderProzessStarter.cs:32`

## DI-Registrierung

`IdeOeffnenService` ist scoped registriert. `IProzessStarter` ist singleton registriert, im Testmodus als aufzeichnende Variante und sonst als `SystemProzessStarter`.

Fundstellen:

- `src/Softwareschmiede.App/App.xaml.cs:197`
- `src/Softwareschmiede.App/App.xaml.cs:198`
- `src/Softwareschmiede.App/App.xaml.cs:207`
- `src/Softwareschmiede.App/App.xaml.cs:214`

## Umsetzungskonsequenzen

- Fuer `code "<arbeitsverzeichnis>"` kann `ProzessStartAnfrage(DateiName = resolvedCodeCommand, Argumente = Quote(arbeitsverzeichnis), ShellAusfuehren = false oder true)` verwendet werden. Die Wahl von `ShellAusfuehren` haengt davon ab, ob ein absoluter Pfad oder PATH-Aufloesung genutzt wird.
- Da `ProzessStartAnfrage` kein `WorkingDirectory` hat, sollte der Ordner als Argument uebergeben werden, wie in der Anforderung beschrieben.
- Eine robuste Verfuegbarkeitspruefung sollte nicht ueber einen echten Startversuch erfolgen, weil das Tests und Fehlermeldungen unklar macht. Besser ist ein Resolver, der Dateiexistenz und PATH-Suche prueft.
