using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Domain.Interfaces;

/// <summary>Löst das Basis-Arbeitsverzeichnis zur Laufzeit auf.</summary>
public interface IArbeitsverzeichnisResolver
{
    /// <summary>Löst das effektive Basis-Arbeitsverzeichnis auf.</summary>
    Task<ArbeitsverzeichnisResolutionResult> ResolveAsync(CancellationToken ct = default);
}
