# Bestandsaufnahme

- GitHub Actions verwenden `.github/workflows/test.yml`.
- Der Testjob baut `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj`.
- Vor der Aenderung fuehrte der Workflow die komplette Testsuite aus und setzte nur `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`.
- FlaUI-/WPF-E2E-Tests sind ueber `Trait("Category", "E2E")` markiert.
- Ein echter ConPTY-Integrationstest ist ueber `Trait("Category", "ConPTY")` markiert.

Relevante Dateien:
- `.github/workflows/test.yml`
- `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj`
- `src/Softwareschmiede.Tests/E2E/*`
- `src/Softwareschmiede.Tests/ServiceIntegration/CliEmbeddingServiceIntegrationTests.cs`
