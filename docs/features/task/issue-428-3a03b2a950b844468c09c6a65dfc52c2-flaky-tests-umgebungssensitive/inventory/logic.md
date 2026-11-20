# Logik- und Infrastrukturklassen

Nur für die Anforderung relevante Klassen (Test-Tagging, OS-nahe Test-Helfer, Skip-/Retry-Mechanik). Klassen- und Methodennamen im Original.

## `OsInterfaceAttribute`

Datei: `src/Softwareschmiede.Tests/Infrastructure/Testing/OsInterfaceAttribute.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetTraits` | `public` | Liefert `Category = TestCategories.OsInterface` (`ITraitAttribute`-Implementierung, xUnit v3) |

`[AttributeUsage(Method | Class)]` — für Methoden- und Klassen-Tagging geeignet. Namespace `Softwareschmiede.Tests.Infrastructure.Testing`; im Integrationsprojekt **nicht** verfügbar (keine Referenz).

## `OsInterfaceFactAttribute` / `OsInterfaceTheoryAttribute`

Dateien: `src/Softwareschmiede.Tests/Infrastructure/Testing/OsInterfaceFactAttribute.cs`, `OsInterfaceTheoryAttribute.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| Konstruktor | `public` | Reicht `CallerFilePath`/`CallerLineNumber` an `FactAttribute`/`TheoryAttribute` (xUnit v3) weiter |
| `GetTraits` | `public` | Liefert `Category = OsInterface` |

Ersetzen `[Fact]`/`[Theory]` direkt — Bestehendes Muster, z. B. in `KiAusfuehrungsServiceTests` (~20 Methoden) und `TerminalControlTests.*`.

## `TestCategories`

Datei: `src/Softwareschmiede.Tests/Infrastructure/Testing/TestCategories.cs`

| Member | Typ | Kurzbeschreibung |
|--------|-----|------------------|
| `OsInterface` | `public const string = "OsInterface"` | Zentraler Kategoriename für `--filter "Category[=!]=OsInterface"` |

## `ScmRestrictedWindowsProcess`

Datei: `src/Softwareschmiede.IntegrationTests/Services/ScmRestrictedWindowsProcess.cs` (`internal static`, `[SupportedOSPlatform("windows")]`)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Start` | `internal static` | Erzeugt via `CreateRestrictedToken` + `CreateProcessAsUser` einen echten eingeschränkten `powershell.exe -EncodedCommand`-Prozess mit dedizierter Capability-SID; wirft `PlatformNotSupportedException` außerhalb Windows; kein unbeschränkter Fallback |
| `SetDefaultDacl` | `private static` | Setzt Token-Default-DACL auf Logon-SID + `S-1-3-4` |
| `GetLogonSid` | `private static` | Ermittelt Logon-SID aus Token-Gruppen; wirft ohne echte Logon-Identität |

Wird aufgerufen von `ScmEventCliExchangeAccessTests.RestrictedWindowsProcess_*` (Zeilen 118, 235).

## `ScmEventCliExchangeAccessTests` (partial) — Hilfs- und Cleanup-Methoden

Dateien: `src/Softwareschmiede.IntegrationTests/Services/ScmEventCliExchangeAccessTests{,.RestrictedProcess,.PendingProbe}.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Dispose` | `public` | Zeile 329: `Directory.Delete(_root, recursive: true)` — ohne Prozess-Kill, ohne Retry, wirft direkt |
| Konstruktor | `public` | Legt `_root` unter `Path.GetTempPath()` an (Feld Zeile 19) |
| `Spec` | `private async` | Erzeugt echte Plugin-Instanzen (`CodexPlugin`/`ClaudeCliPlugin`/`DevinPlugin`) mit gemocktem `ICredentialStore` und deren `TerminalSessionStartSpec` |
| `Session` | `private static` | `Mock<ITerminalSession>` mit `TerminalBuffer`-Banner und `Process.GetCurrentProcess()` |
| `RestrictDirectory` | `private static` | Setzt `DirectorySecurity`-ACLs (Owner = aktueller User, optionale Capability-SID mit/ohne Vererbung) |
| `GitInit` | `private static` | Echter `git init --quiet`-Prozessstart |
| `SetScreen` | `private static` | Ersetzt Buffer + feuert `BufferChanged` (PendingProbe-Partial) |
| `GrantingArguments`/`Banner`/`PromptGlyph` | `private static` | Pro-Provider-Konstanten der Startverträge |

## `ScmDispatchFixture`

Datei: `src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs` (`internal sealed`, `IAsyncDisposable`; per `<Compile Include>` ins Integrationsprojekt verlinkt)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| Konstruktor | `internal` | Baut echten DI-`ServiceProvider` (SQLite `events.db` im Temp-`Root`); `ITerminalSessionFactory`-Mock startet pro Session echten `cmd.exe /c ping -n 300`-Prozess (Zeile 136) und registriert ihn in `_processes` |
| `InitializeAsync` | `internal` | `EnsureCreatedAsync` + `ScmEventStartupGate.Ready()` |
| `AddAsync` | `internal` | Legt Projekt/Repository/Aufgabe + SCM-Event an; optional `InitGit` im Klonpfad |
| `CompleteProbeAsync` | `internal static` | Dateitool-Kanalprobe rein per Datei-I/O (challenge → temporaryProof → proof) |
| `DispatchAsync`/`EventAsync`/`SetSettingAsync`/`SetKontoModusAsync` | `internal` | Scoped-Service-Aufrufe gegen die Fixture-DI |
| `InitGit` | `private static` | Echter `git init --quiet`-Prozessstart (Zeilen 321–329) |
| `DisposeAsync` | `public` | Killt `_processes` mit `Kill(entireProcessTree: true)` + `WaitForExitAsync` (fängt `InvalidOperationException`), disposed Services, `Directory.Delete(Root, true)` ohne Retry (Zeile 340) |

## `WpfTestBase`

Datei: `src/Softwareschmiede.Tests/E2E/WpfTestBase.cs` (`public abstract`, `IDisposable`)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `LaunchApp` | `protected` | Zentraler Skip- und Startpunkt aller FlaUI-E2E-Tests: `SkipWennWpfE2ENichtVerfuegbar()`, dann `FlaUI.Core.Application.Launch` der echten App (Zeilen 235–283) |
| `LaunchAppAndGetMainWindow` | `protected` | `LaunchApp` + Hauptfenster-Ermittlung |
| `RestartAppPreservingDatabase` | `protected` | App-Neustart über letztes `LaunchApp`-Setup |
| `SkipWennConPtyNichtVerfuegbar` | `protected static` | `Assert.SkipWhen(!ConPtyEnvironmentProbe.IsAvailable, …)` (Zeile 989) |
| `SkipWennWpfE2ENichtVerfuegbar` | `protected static` | `Assert.SkipWhen(!WpfE2EEnvironmentProbe.IsAvailable, …)` (Zeile ~1004) |

## `ConPtyEnvironmentProbe` / `WpfE2EEnvironmentProbe`

Dateien: `src/Softwareschmiede.Tests/E2E/ConPtyEnvironmentProbe.cs`, `WpfE2EEnvironmentProbe.cs` (je `internal static`)

| Member | Typ | Kurzbeschreibung |
|--------|-----|------------------|
| `IsAvailable` | `internal static bool` | `Environment.GetEnvironmentVariable(SkipEnvVar) != "1"` — reine Env-Flag-Auswertung, bewusst **keine** Auto-Detection (dokumentierte Entscheidung, Issue 114) |
| `UnavailableReason` | `internal const string` | Diagnosetext für den Skip-Grund |

`SkipEnvVar`: `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS` bzw. `SOFTWARESCHMIEDE_SKIP_WPF_E2E_TESTS`.

## `ClipboardTestLock` / `ClipboardTestHelfer`

Dateien: `src/Softwareschmiede.Tests/Infrastructure/Testing/ClipboardTestLock.cs`, `ClipboardTestHelfer.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Halten` (`ClipboardTestLock`) | `public static` | Prozessweites Lock gegen parallele Clipboard-Belegung durch andere Test-Collections |
| `SetTextMitRetry`/`GetTextMitRetry`/`LeerenMitRetry` (`ClipboardTestHelfer`) | `public static` | Clipboard-Zugriffe mit Retry — bestehende Retry-Konvention für ein bekanntes Flake-Bild (`CLIPBRD_E_CANT_OPEN`) |

## `Run-AllTestsIndividually.ps1` — relevante Funktionen

Datei: `scripts/Run-AllTestsIndividually.ps1`

| Funktion | Kurzbeschreibung |
|----------|------------------|
| `Get-CategoryFilterExpression` (Z. 113–119) | Mappt `-TestSet` auf `Category!=OsInterface` (Regular, Default), `Category=OsInterface` oder keinen Filter (All) |
| `Get-TestTargets` (Z. 146–179) | `--list-tests`-Auswertung; Theory-Datenzeilen methodenbasiert gruppiert |
| `Test-LooksLikeRuntimeError` (Z. 233–241) | Marker-Liste für Infrastruktur-/Ausführungsfehler (Testhost, Datei-Locks) statt fachlicher Assertion |
| `Invoke-SingleTest` (Z. 243–266) | Einzelner `dotnet test --filter FullyQualifiedName=…`-Aufruf mit TRX-Auswertung |
| Retry-Schleife (Z. 306–316) | Genau **ein** Rebuild+Retry nur bei `IsRuntimeError` — die einzige existierende Retry-Konvention; bewusst nicht bei Assertion-Fehlschlägen |
