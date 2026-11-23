# Tasks: Wöchentliche NuGet-Paketprüfung – 2026-KW48 (Issue #469)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Pakete | `Serilog.Extensions.Hosting` 10.0.0 → 11.0.0 in `src/Softwareschmiede.App/Softwareschmiede.App.csproj` heben | Offen | — |
| 2 | Pakete | `Microsoft.Extensions.TimeProvider.Testing` 11.0.0 → 11.0.1 in `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj` heben | Offen | — |
| 3 | Verifikation | `dotnet restore Softwareschmiede.slnx` ausführen und transitive Hebungen prüfen (`Serilog` 4.4.0, `Serilog.Extensions.Logging` 11.0.0) | Offen | — |
| 4 | Verifikation | Vollen Build `dotnet build Softwareschmiede.slnx -c Debug -p:TreatWarningsAsErrors=true` mit 0 Warnungen/0 Fehlern | Offen | — |
| 5 | Verifikation | Stabile Lane `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"` (Soll: 2800/0/3) | Offen | — |
| 6 | Verifikation | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj` (Soll: 436/0/0) | Offen | — |
| 7 | Verifikation | `dotnet list Softwareschmiede.slnx package --vulnerable --include-transitive` ohne verwundbare Pakete | Offen | — |
| 8 | Dokumentation | `THIRD_PARTY_LICENSES.md`: Abschnitt „Ausgelieferte Abhängigkeiten" auf aufgelöste Versionen bringen (EF-Core/`Microsoft.Extensions.*` → 11.0.0, `SQLitePCLRaw.*` → 3.53.3/3.0.5, `SQLite` 3.53.4 ergänzen, Serilog → 4.4.0/11.0.0/11.0.0) | Offen | — |
| 9 | Dokumentation | `THIRD_PARTY_LICENSES.md`: Abschnitt „Nur Test/Build" korrigieren (`xunit` → `xunit.v3` 4.0.3, `xunit.runner.visualstudio` → 4.0.1, `Xunit.SkippableFact` entfernen, `Microsoft.NET.Test.Sdk` → 18.12.0, `coverlet.collector` → 10.1.1, `FluentAssertions` → 8.11.0, `EFCore.InMemory`/`.Design` → 11.0.0, `TimeProvider.Testing` → 11.0.1, `Moq` → 4.21.0; fehlende Transitiva wie `Interop.UIAutomationClient`, `Castle.Core` ergänzen) | Offen | — |
| 10 | Dokumentation | `THIRD_PARTY_LICENSES.md`: Stand-Datum im Kopf auf Umsetzungsdatum setzen | Offen | — |
| 11 | PR | Commit(s) mit `Closes #469` und PR gegen `staging` erstellen; Body mit Prüfungstabelle, Risikobewertung, aufgeschobenen Empfehlungen (kein stabiles `SQLitePCLRaw`-Update, transitive Major-/Dev-Time-Updates, Prereleases) | Offen | — |
| 12 | PR | PR-Checks mit `gh pr checks --watch --fail-fast` überwachen; bei `fail` sofort `gh run view --job <id> --log-failed` diagnostizieren | Offen | — |
