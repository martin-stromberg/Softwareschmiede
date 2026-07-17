# Testauswertung, Skripte und CI

## CI

Datei: `.github/workflows/test.yml`

Der Workflow:

1. laeuft auf `windows-latest`
2. restored die Solution
3. baut `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj`
4. fuehrt Tests mit Ausschluss von `E2E` und `ConPTY` aus
5. laedt TRX-Ergebnisse hoch

Aktueller Testbefehl:

```powershell
dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --no-build -c Debug --filter "Category!=E2E&Category!=ConPTY" --logger "trx;LogFileName=test-results.trx" --logger "console;verbosity=normal"
```

Fuer die Anforderung muss dieser Pflicht-Lauf auf `Category!=OsInterface` umgestellt werden, sobald alle OS-nahen Tests korrekt markiert sind.

Ein separater OS-Schnittstellen-Lauf existiert noch nicht.

## Lokales Einzeltest-Skript

Datei: `scripts/Run-AllTestsIndividually.ps1`

Das Skript:

- findet alle Testprojekte ueber `Microsoft.NET.Test.Sdk`
- baut jedes Testprojekt einzeln
- listet Tests per `dotnet test --list-tests`
- fuehrt jeden Test einzeln per `FullyQualifiedName` aus
- wertet TRX aus
- unterscheidet `Bestanden`, `Fehlgeschlagen`, `Ausfuehrungsfehler`, `Unbekannt`
- wiederholt Infrastruktur-/Ausfuehrungsfehler nach Rebuild genau einmal

Die Runtime-Error-Erkennung sucht unter anderem nach:

- fehlendem `hostpolicy.dll`
- fehlendem App-Build-Artefakt
- nicht startbarem Testhost
- Datei wird von anderem Prozess verwendet

Diese Retry-Logik ist aktuell nicht auf OS-Schnittstellen-Tests begrenzt. Nach der Anforderung sollte Retry entweder auf OS-Schnittstellen-Laeufe begrenzt oder klar als Infrastruktur-Recovery ausserhalb regulaerer Testfehler ausgewiesen werden.

## `/run-tests` und `/lifecycle`

Im Repository ist keine direkte Implementierung von `/run-tests` oder `/lifecycle` als Projektdatei sichtbar. Die Lifecycle-Anleitung liegt ausserhalb des Repos als Codex-Skill. Daraus folgt:

- Anpassungen an `/run-tests` koennen nicht allein durch Quellcodeaenderungen im Repo erfolgen, sofern das Kommando extern bereitgestellt wird.
- Fuer diese Codebasis koennen aber Ergebnisformate, Dokumentation und ggf. Skripte so vorbereitet werden, dass regulaere und OS-Schnittstellen-Fehler getrennt berichtet werden.
- Die Lifecycle-Iterationslogik muss externe OS-Schnittstellen-Fehler ignorieren oder separat dokumentieren; im Repo selbst ist dafuer kein zentraler Hook sichtbar.

## Erwartete neue Auswertung

Sinnvolle Zielstruktur fuer Testberichte:

- regulaerer Pflichtlauf: `dotnet test --filter "Category!=OsInterface"`
- OS-Schnittstellen-Lauf: `dotnet test --filter "Category=OsInterface"`
- regulaere Fehler: blockierend
- OS-Schnittstellen-Fehler: separater Abschnitt, je nach CI-Entscheidung optional oder best-effort
- Retry: nur im OS-Schnittstellen-Lauf oder nur fuer explizit als Infrastrukturfehler klassifizierte OS-Schnittstellen-Faelle

## Kompatibilitaetsrisiken

- Bestehende Dokumentation und CI-Kommentare nennen `E2E` und `ConPTY`; diese muessen mit der neuen Kategorie konsistent werden.
- Entwickler koennten lokale Filter `Category!=E2E` oder `Category=E2E` weiter verwenden. Wenn diese Kategorien erhalten bleiben, kann `OsInterface` als zusaetzliche Kategorie eingefuehrt werden.
- `dotnet test`-Filter fuer mehrere Kategorien muessen exakt formuliert werden, z. B. `Category!=OsInterface` oder `Category=OsInterface`.
