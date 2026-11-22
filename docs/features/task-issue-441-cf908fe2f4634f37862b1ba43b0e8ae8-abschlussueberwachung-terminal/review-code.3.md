# Code-Review (Nachfass zum Continue-Lauf)

Erstellt am: 2026-11-22

## Ergebnis

**Status:** Keine Befunde

Dieses Review prüft ausschließlich die Änderungen des Continue-Laufs, mit dem der einzige
verbliebene Befund aus `review-code.md` (Iteration 3) behoben wurde. Es ist eine reine
Test-Refaktorisierung — kein Produktivcode wurde geändert.

## Behandelter Befund aus `review-code.md`

- **Doppelter Code (behoben):** Die deterministische Test-Zeitquelle existiert jetzt einmalig
  als `TestTimeProvider` (setzbares `Now` + `Advance`) in
  `src/Softwareschmiede.Tests/Helpers/TestTimeProvider.cs` (Namespace `Softwareschmiede.TestSupport`,
  neben `ScmDispatchFixture`) und wird wie die bestehende Shared-Fixture per
  `<Compile Include ... Link="Infrastructure\TestTimeProvider.cs" />` in
  `Softwareschmiede.IntegrationTests.csproj` eingebunden. Die private `TestClock` in
  `ScmEventDispatchTests_Reconcile.cs` und die private `TestTimeProvider` in
  `ScmTaskCompletionMonitoringServiceTests.cs` sind entfernt; beide Klassen nutzen die
  gemeinsame Quelle.
- **Wortgleiches `Json`-Feld (behoben):** Die identischen
  `private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)`-Felder
  sind durch die gemeinsame `TestJson.Options`-Hilfsstelle in derselben TestSupport-Datei
  ersetzt. `ScmEventStore.JsonOptions` wurde bewusst nicht verwendet, damit Test-Code nicht
  an eine produktive Serialisierungsstelle für einen anderen Zweck (Store-Payloads statt
  Kanaldateien) koppelt.

## Neue Befunde durch die Änderung

Keine. Sichtprüfung des Diffs: keine Produktivcode-Änderung, kein Verhalten der Tests
geändert (gleiche Optionswerte, gleiche Zeitquellen-Semantik), keine neuen Warnungen.

**Hinweis (kein Befund, außerhalb des Branch-Diffs):** Vier weitere, inhaltlich identische
private `TestClock`-Kopien existieren in Dateien, die dieser Branch nicht anfasst
(`ScmEventDispatchTests_Cancel.cs`, `ScmEventDispatchTests_HostRetry.cs`,
`ScmEventDispatchTests_IssueBodyUpdate.cs` in IntegrationTests sowie
`ScmEventCancelTests.cs` in Softwareschmiede.Tests). Sie stammen aus der Zeit vor diesem
Branch und können bei Gelegenheit ebenfalls auf `TestTimeProvider` umgestellt werden —
bewusst nicht Teil dieses fachlich abgegrenzten Fix-Schritts.

## Verifikation

- `dotnet build Softwareschmiede.slnx`: erfolgreich, 0 Warnungen, 0 Fehler.
- `dotnet test src/Softwareschmiede.IntegrationTests/... --filter "Category!=OsInterface&(FullyQualifiedName~ScmEventDispatchTests_Reconcile|FullyQualifiedName~ScmTaskCompletionMonitoringServiceTests)"` (mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`): 32/32 bestanden, 0 Fehler, 0 übersprungen.
- `dotnet format Softwareschmiede.slnx --verify-no-changes`: sauber (Exit 0).

## Geprüfte Dateien (Diff dieses Laufs)

- `src/Softwareschmiede.Tests/Helpers/TestTimeProvider.cs` (neu: `TestTimeProvider`, `TestJson`)
- `src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj` (Compile-Link)
- `src/Softwareschmiede.IntegrationTests/Services/ScmEventDispatchTests_Reconcile.cs`
- `src/Softwareschmiede.IntegrationTests/Services/ScmTaskCompletionMonitoringServiceTests.cs`
