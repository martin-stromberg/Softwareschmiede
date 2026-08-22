namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Ressourcenlimits einer Autonomen Aufgabe (Token-Budget und Laufzeitbegrenzung).</summary>
/// <param name="TokenBudget">Token-Budget für die Gesamtaufgabe.</param>
/// <param name="TokenBudgetErweitert">Optionales erweitertes Token-Budget.</param>
/// <param name="LaufzeitLimitMinuten">Nettozeit-Limit in Minuten.</param>
public sealed record RessourcenLimits(
    int TokenBudget,
    int? TokenBudgetErweitert,
    int LaufzeitLimitMinuten
);
