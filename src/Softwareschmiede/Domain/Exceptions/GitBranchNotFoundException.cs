namespace Softwareschmiede.Domain.Exceptions;

/// <summary>Wird ausgelöst, wenn ein konfigurierter Basis-Branch nicht im Remote-Repository existiert.</summary>
public sealed class GitBranchNotFoundException : InvalidOperationException
{
    /// <summary>Erstellt eine neue Instanz der <see cref="GitBranchNotFoundException"/>.</summary>
    /// <param name="branchName">Name des nicht gefundenen Branches.</param>
    public GitBranchNotFoundException(string branchName)
        : base($"Branch '{branchName}' existiert nicht im Repository.")
    {
        BranchName = branchName;
    }

    /// <summary>Name des nicht gefundenen Branches.</summary>
    public string BranchName { get; }
}
