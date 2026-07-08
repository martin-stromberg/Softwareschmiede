using Softwareschmiede.Domain.Entities;

namespace Softwareschmiede.Application.Services;

/// <summary>
/// Löst das effektive Arbeitsverzeichnis eines Repositories anhand einer optionalen
/// <see cref="RepositoryStartKonfiguration"/> auf. Von <see cref="KiAusfuehrungsService"/> (CLI-Start) und
/// <see cref="GitOrchestrationService"/> (Validierung nach Klon) gemeinsam genutzt, damit keiner der beiden
/// Services in die jeweils andere, fachlich fremde Klasse greifen muss.
/// </summary>
public static class WorkingDirectoryResolver
{
    /// <summary>
    /// Ermittelt das effektive Arbeitsverzeichnis aus Repository-Root und optionaler Startkonfiguration,
    /// inklusive Path-Traversal-Prüfung und Existenz-Validierung, sofern ein Unterverzeichnis konfiguriert ist.
    /// </summary>
    /// <param name="localRepoPath">Pfad zum lokalen Repository-Verzeichnis (Root).</param>
    /// <param name="startConfig">Optionale Startkonfiguration mit dem relativen Arbeitsverzeichnis-Pfad.</param>
    /// <returns>Das effektive Arbeitsverzeichnis (Repository-Root, falls kein Unterverzeichnis konfiguriert ist).</returns>
    public static string DetermineEffectiveWorkingDirectory(string localRepoPath, RepositoryStartKonfiguration? startConfig)
    {
        if (startConfig?.WorkingDirectoryRelativePath is null)
        {
            return localRepoPath;
        }

        var effectiveWorkdir = ResolveEffectiveWorkingDirectory(localRepoPath, startConfig.WorkingDirectoryRelativePath);
        ValidateWorkingDirectory(effectiveWorkdir, localRepoPath);
        return effectiveWorkdir;
    }

    /// <summary>
    /// Kombiniert das Repository-Root-Verzeichnis mit einem relativen Arbeitsverzeichnis-Pfad und normalisiert
    /// das Ergebnis. Verhindert Path-Traversal-Angriffe (z. B. <c>"../../../etc"</c>) sowie das Escapen in ein
    /// Sibling-Verzeichnis mit gemeinsamem String-Präfix (z. B. <c>"task-1"</c> vs. <c>"task-12"</c>), indem
    /// geprüft wird, dass der normalisierte Pfad innerhalb des normalisierten Repository-Roots liegt.
    /// </summary>
    /// <param name="repositoryRoot">Wurzelverzeichnis des geklonten Repositories.</param>
    /// <param name="relativePath">Relativer Pfad zum Arbeitsverzeichnis, oder <c>null</c>/leer für das Repository-Root.</param>
    /// <returns>Den absoluten, normalisierten Pfad des effektiven Arbeitsverzeichnisses.</returns>
    public static string ResolveEffectiveWorkingDirectory(string repositoryRoot, string? relativePath)
    {
        var normalizedRoot = Path.GetFullPath(repositoryRoot);

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return normalizedRoot;
        }

        var combined = Path.Combine(repositoryRoot, relativePath);
        var normalized = Path.GetFullPath(combined);

        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var rootWithSeparator = normalizedRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var isRootItself = string.Equals(normalized, normalizedRoot, comparison);
        var isWithinRoot = normalized.StartsWith(rootWithSeparator, comparison);
        if (!isRootItself && !isWithinRoot)
        {
            throw new InvalidOperationException($"Pfad verlässt Repository-Verzeichnis: {relativePath}");
        }

        return normalized;
    }

    /// <summary>Prüft, ob das effektive Arbeitsverzeichnis existiert.</summary>
    /// <param name="effectiveWorkdir">Der zu prüfende, effektive Arbeitsverzeichnis-Pfad.</param>
    /// <param name="repositoryRoot">Wurzelverzeichnis des Repositories (für die Fehlermeldung).</param>
    public static void ValidateWorkingDirectory(string effectiveWorkdir, string repositoryRoot)
    {
        if (!Directory.Exists(effectiveWorkdir))
        {
            throw new DirectoryNotFoundException($"Arbeitsverzeichnis nicht gefunden: {effectiveWorkdir} (Repository-Root: {repositoryRoot})");
        }
    }
}
