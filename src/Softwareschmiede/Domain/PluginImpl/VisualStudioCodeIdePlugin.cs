using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Domain.PluginImpl;

/// <summary>IDE-Plugin für Visual Studio Code. Dient als universeller Rückfall für beliebige Repositories.</summary>
/// <param name="prozessStarter">Startet den Öffnen-Befehl für das Repository-Verzeichnis.</param>
/// <param name="visualStudioCodeLocator">Ermittelt den startbaren Visual-Studio-Code-Befehl.</param>
public sealed class VisualStudioCodeIdePlugin(
    IProzessStarter prozessStarter,
    IVisualStudioCodeLocator visualStudioCodeLocator) : IIdePlugin
{
    /// <inheritdoc/>
    public string PluginName => "Visual Studio Code";

    /// <inheritdoc/>
    public string PluginPrefix => "Softwareschmiede.VisualStudioCode";

    /// <inheritdoc/>
    public PluginType PluginType => PluginType.Ide;

    /// <inheritdoc/>
    public IReadOnlyList<PluginSettingGroup> GetSettingGroups() => [];

    /// <inheritdoc/>
    public Task<IdePluginCompatibility> CheckCompatibilityAsync(string repositoryPath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);

        return Task.FromResult(IdePluginCompatibility.Fallback);
    }

    /// <inheritdoc/>
    public Task OpenRepositoryAsync(string repositoryPath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);

        OpenDirectory(prozessStarter, visualStudioCodeLocator, repositoryPath);

        return Task.CompletedTask;
    }

    /// <summary>Öffnet das übergebene Verzeichnis in Visual Studio Code.</summary>
    /// <param name="prozessStarter">Startet den Öffnen-Befehl für das Verzeichnis.</param>
    /// <param name="visualStudioCodeLocator">Ermittelt den startbaren Visual-Studio-Code-Befehl.</param>
    /// <param name="path">Der zu öffnende Ordner.</param>
    internal static void OpenDirectory(IProzessStarter prozessStarter, IVisualStudioCodeLocator visualStudioCodeLocator, string path)
    {
        var availability = visualStudioCodeLocator.Locate();
        if (!availability.IsAvailable || string.IsNullOrWhiteSpace(availability.ExecutablePath))
            throw new InvalidOperationException("Visual Studio Code wurde nicht gefunden.");

        prozessStarter.Starten(new ProzessStartAnfrage(
            availability.ExecutablePath,
            QuoteArgument(path),
            ShellAusfuehren: false));
    }

    private static string QuoteArgument(string argument)
        => $"\"{argument.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";

    /// <inheritdoc/>
    public Task<IReadOnlyList<IdeEntryPoint>> FindEntryPointsAsync(string repositoryPath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);

        IReadOnlyList<IdeEntryPoint> entryPoints = [new IdeEntryPoint(repositoryPath, "Visual Studio Code")];

        return Task.FromResult(entryPoints);
    }

    /// <inheritdoc/>
    public Task OpenEntryPointAsync(IdeEntryPoint entryPoint, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entryPoint);

        OpenDirectory(prozessStarter, visualStudioCodeLocator, entryPoint.Path);

        return Task.CompletedTask;
    }
}
