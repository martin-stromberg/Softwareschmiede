using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Interfaces;

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
    /// <param name="localRepoPath">Pfad zum lokalen Repository-Verzeichnis (Root), wie er z. B. beim Klon angegeben wurde.</param>
    /// <param name="startConfig">Optionale Startkonfiguration mit dem relativen Arbeitsverzeichnis-Pfad.</param>
    /// <param name="gitPlugin">
    /// Optionales Git-Plugin, das zum Klonen des Repositories verwendet wurde. Wird genutzt, um
    /// <paramref name="localRepoPath"/> vor der Kombination mit dem relativen Arbeitsverzeichnis-Pfad über
    /// <see cref="IGitPlugin.ResolveEffectiveRepositoryPathAsync"/> in den tatsächlichen Repository-Pfad
    /// aufzulösen (relevant z. B. für <c>LocalDirectoryPlugin</c> im <c>InSourceDirectory</c>-Modus, wo
    /// <paramref name="localRepoPath"/> nur eine Pointer-Datei enthält). Bleibt <paramref name="gitPlugin"/>
    /// <c>null</c>, verhält sich die Methode wie zuvor und verwendet <paramref name="localRepoPath"/> unverändert.
    /// </param>
    /// <param name="ct">Cancellation Token.</param>
    /// <returns>Das effektive Arbeitsverzeichnis (Repository-Root, falls kein Unterverzeichnis konfiguriert ist).</returns>
    public static async Task<string> DetermineEffectiveWorkingDirectoryAsync(
        string localRepoPath,
        RepositoryStartKonfiguration? startConfig,
        IGitPlugin? gitPlugin = null,
        CancellationToken ct = default)
    {
        // Fällt auf localRepoPath zurück, wenn kein Plugin übergeben wurde oder das Plugin (z. B. ein in Tests
        // nicht vollständig konfiguriertes Mock-Objekt ohne CallBase) null/leer liefert. Ein produktives Plugin
        // ohne Override liefert dank der Default-Implementierung in IGitPlugin ohnehin localPath unverändert
        // zurück; dieser Fallback macht die Auflösung zusätzlich robust gegen unvollständige Test-Doubles.
        var resolvedByPlugin = gitPlugin is null
            ? null
            : await gitPlugin.ResolveEffectiveRepositoryPathAsync(localRepoPath, ct).ConfigureAwait(false);
        var repositoryRoot = string.IsNullOrWhiteSpace(resolvedByPlugin) ? localRepoPath : resolvedByPlugin;

        if (startConfig?.WorkingDirectoryRelativePath is null)
        {
            return repositoryRoot;
        }

        var effectiveWorkdir = ResolveEffectiveWorkingDirectory(repositoryRoot, startConfig.WorkingDirectoryRelativePath);
        ValidateWorkingDirectory(effectiveWorkdir, repositoryRoot);
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
