namespace Softwareschmiede.Application.Services;

/// <summary>Validiert Branch-Namen gegen die von Git akzeptierten Regeln (öffentlich, da sowohl vom Service als auch vom Dialog-ViewModel benötigt).</summary>
public static class GitBranchNameValidator
{
    /// <summary>Prüft, ob <paramref name="branchName"/> ein gültiger Git-Branch-Name ist.</summary>
    /// <param name="branchName">Der zu prüfende Branch-Name.</param>
    /// <returns><see langword="true"/>, wenn <paramref name="branchName"/> ein gültiger Git-Branch-Name ist.</returns>
    public static bool IstGueltig(string branchName)
    {
        if (branchName.StartsWith('/') || branchName.EndsWith('/') || branchName.EndsWith('.'))
        {
            return false;
        }

        return !branchName.Contains("..", StringComparison.Ordinal)
            && !branchName.Any(c => c is ' ' or '~' or '^' or ':' or '?' or '*' or '[' or '\\');
    }
}
