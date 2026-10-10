using Xunit;

// WPF- und E2E-Tests greifen auf prozessübergreifende Windows-Ressourcen wie die
// Zwischenablage, UI-Automation und den Credential Store zu. Parallel ausgeführte
// Testklassen würden deshalb keine unabhängigen Ergebnisse liefern.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
