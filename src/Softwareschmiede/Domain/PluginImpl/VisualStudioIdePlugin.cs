using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Domain.PluginImpl;

/// <summary>IDE-Plugin für Visual Studio. Prüft auf <c>.sln</c>/<c>.slnx</c>-Dateien im Repository-Root.</summary>
/// <param name="prozessStarter">Startet den Öffnen-Befehl für die gefundene Solution-Datei.</param>
public sealed class VisualStudioIdePlugin(IProzessStarter prozessStarter) : IIdePlugin
{
    /// <inheritdoc/>
    public string PluginName => "Visual Studio";

    /// <inheritdoc/>
    public string PluginPrefix => "Softwareschmiede.VisualStudio";

    /// <inheritdoc/>
    public PluginType PluginType => PluginType.Ide;

    /// <inheritdoc/>
    public IReadOnlyList<PluginSettingGroup> GetSettingGroups() => [];

    /// <inheritdoc/>
    public Task<IdePluginCompatibility> CheckCompatibilityAsync(string repositoryPath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);

        var compatibility = FindSolutionFiles(repositoryPath).Count > 0
            ? IdePluginCompatibility.Explicit
            : IdePluginCompatibility.Incompatible;

        return Task.FromResult(compatibility);
    }

    /// <inheritdoc/>
    public Task OpenRepositoryAsync(string repositoryPath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);

        var solutionPfad = FindSolutionFiles(repositoryPath).FirstOrDefault()
            ?? throw new FileNotFoundException($"Keine .sln/.slnx-Datei im Repository gefunden: {repositoryPath}");

        OpenSolutionFile(prozessStarter, solutionPfad);

        return Task.CompletedTask;
    }

    /// <summary>Ermittelt alle <c>*.sln</c>-/<c>*.slnx</c>-Dateien auf oberster Ebene des Verzeichnisses, alphabetisch sortiert.</summary>
    /// <param name="repositoryPath">Der zu durchsuchende Verzeichnispfad.</param>
    /// <returns>Die gefundenen Solution-Pfade, alphabetisch sortiert; leere Liste bei nicht existierendem Verzeichnis.</returns>
    internal static List<string> FindSolutionFiles(string repositoryPath)
    {
        if (!Directory.Exists(repositoryPath))
            return [];

        return Directory.EnumerateFiles(repositoryPath, "*.sln", SearchOption.TopDirectoryOnly)
            .Concat(Directory.EnumerateFiles(repositoryPath, "*.slnx", SearchOption.TopDirectoryOnly))
            .OrderBy(pfad => pfad, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Öffnet die übergebene Solution-Datei mit dem beim Betriebssystem registrierten Standardhandler.</summary>
    /// <param name="prozessStarter">Startet den Öffnen-Befehl für die Solution-Datei.</param>
    /// <param name="solutionPath">Der Pfad der zu öffnenden <c>*.sln</c>-Datei.</param>
    internal static void OpenSolutionFile(IProzessStarter prozessStarter, string solutionPath)
        => prozessStarter.Starten(new ProzessStartAnfrage(solutionPath, Argumente: null, ShellAusfuehren: true));

    /// <inheritdoc/>
    public Task<IReadOnlyList<IdeEntryPoint>> FindEntryPointsAsync(string repositoryPath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);

        IReadOnlyList<IdeEntryPoint> entryPoints = FindSolutionFiles(repositoryPath)
            .Select(solutionPfad => new IdeEntryPoint(solutionPfad))
            .ToList();

        return Task.FromResult(entryPoints);
    }

    /// <inheritdoc/>
    public Task OpenEntryPointAsync(IdeEntryPoint entryPoint, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entryPoint);

        OpenSolutionFile(prozessStarter, entryPoint.Path);

        return Task.CompletedTask;
    }
}
