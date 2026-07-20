# Code-Review

Status: Keine Befunde

## Prüfung

- Der Race-Befund aus `review-code.3.md` ist behoben. `IssueAnlegenAsync` verwendet nach der externen Anlage nicht mehr `UpdateIssueReferenzAsync`, sondern den neuen Servicepfad `TryAssignIssueReferenzIfNoneAsync`. Dieser prüft vor dem Insert auf vorhandene Referenzen und behandelt einen parallelen Insert über den bestehenden eindeutigen `IssueReferenzen.AufgabeId`-Index als `false`, statt eine vorhandene Referenz zu überschreiben.
- Die bestehende `Issue zuweisen`-Semantik bleibt getrennt davon erhalten. `UpdateIssueReferenzAsync` wurde nicht auf die neue Einmaligkeitsregel umgestellt und überschreibt weiterhin vorhandene Referenzen; der ergänzte Test `UpdateIssueReferenzAsync_ShouldOverwriteExistingReference_WhenReferenceExists` deckt das explizit ab.
- Die Iteration-2-Befunde sind weiterhin adressiert: reale Codex-/Claude-Implementierungen für `IIssueTemplateTextGenerator` sind vorhanden, und GitHub lädt nur noch Markdown-Issue-Templates statt YAML-Issue-Forms oder `config.yml`.

## Verifikation

- Code-Review der aktuellen uncommitted Änderungen durchgeführt.
- `git diff --check` ohne Whitespace-Fehler; nur bestehende LF/CRLF-Hinweise ausgegeben.
- `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --no-build --nologo --filter "FullyQualifiedName~AufgabeServiceTests|FullyQualifiedName~TaskDetailViewModelTests|FullyQualifiedName~IssueCreateDialogViewModelTests|FullyQualifiedName~GitHubPluginTests|FullyQualifiedName~BitbucketPluginTests|FullyQualifiedName~GitPluginBaseTests|FullyQualifiedName~LocalDirectoryPluginTests|FullyQualifiedName~ClaudeCliPluginTests|FullyQualifiedName~CodexPluginTests"` erfolgreich: 284 bestanden, 0 fehlgeschlagen.
- `dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj --no-build --nologo --filter "FullyQualifiedName~AufgabeServiceTests.TryAssignIssueReferenzIfNoneAsync_ShouldNotOverwrite_WhenReferenceIsAssignedAfterCallerCheck"` erfolgreich: 1 bestanden, 0 fehlgeschlagen.
