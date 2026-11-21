using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Domain.Interfaces;

/// <summary>Löst das Basis-Arbeitsverzeichnis zur Laufzeit auf.</summary>
public interface IWorkingDirectoryResolver
{
    /// <summary>Löst das effektive Basis-Arbeitsverzeichnis auf.</summary>
    Task<WorkingDirectoryResolutionResult> ResolveAsync(CancellationToken ct = default);
}
