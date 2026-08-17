using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Plugins;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// End-to-End-Abdeckung der automatischen IDE-Plugin-Auswahl (Anforderung Issue #204): Ein Repository
/// mit <c>.sln</c>-Datei wird von Visual Studio explizit übernommen, ein Repository ohne <c>.sln</c>
/// fällt auf Visual Studio Code zurück, und ein deaktiviertes Visual-Studio-Plugin führt ebenfalls zum
/// VS-Code-Fallback - jeweils über den vollständigen, unveränderten Produktions-Stack
/// (<see cref="PluginManager"/> mit eingebauten IDE-Plugins, <see cref="PluginActivationService"/>,
/// <see cref="PluginSelectionService.ResolveIdePluginAsync"/>, gefolgt von
/// <see cref="IIdePlugin.FindEntryPointsAsync"/>/<see cref="IIdePlugin.OpenEntryPointAsync"/> auf dem
/// aufgelösten Plugin — derselbe Resolve/Find/Open-Ablauf, den auch <c>TaskDetailViewModel</c> direkt nutzt).
///
/// Diese Szenarien werden bewusst zusätzlich zur FlaUI-Abdeckung in <c>E2E_VerzeichnisAktionen</c> (Ribbon-
/// Button "IDE öffnen", der denselben <see cref="PluginSelectionService.ResolveIdePluginAsync"/>-Pfad nutzt)
/// als reine Objektgraph-Tests gehalten, um die Plugin-Auswahl-Logik selbst schnell und ohne
/// App-Start/ConPTY-Abhängigkeit isoliert zu verifizieren; sie laufen deshalb in der regulären Testlane
/// statt unter Category=OsInterface.
///
/// Statt eines echten Prozessstarts wird <see cref="IProzessStarter"/> durch einen aufzeichnenden
/// Test-Double ersetzt (analog zu <see cref="Infrastructure.Services.AufzeichnenderProzessStarter"/>,
/// der auch für echte FlaUI-E2E-Tests im Testmodus verwendet wird).
/// </summary>
public sealed class E2E_IdePluginSelection : IDisposable
{
    private readonly TestTempDirectoryFixture _tempDirectoryFixture = new();

    /// <summary>Löscht alle temporären Testverzeichnisse.</summary>
    public void Dispose() => _tempDirectoryFixture.Dispose();

    /// <summary>Ein Repository mit .sln-Datei wird automatisch mit Visual Studio geöffnet (Explicit-Kompatibilität).</summary>
    [Fact]
    public async Task E2E_IdePluginSelection_RepositoryWithSln()
    {
        var repository = _tempDirectoryFixture.CreateTempDirectory("e2e_ide_selection_mit_sln");
        File.WriteAllText(Path.Combine(repository, "Loesung.sln"), string.Empty);

        var (pluginSelectionService, prozessStarter) = CreateStack();

        await OpenRepositoryInIdeAsync(pluginSelectionService, repository);

        prozessStarter.Aufrufe.Should().ContainSingle();
        prozessStarter.Aufrufe[0].DateiName.Should().Be(Path.Combine(repository, "Loesung.sln"));
        prozessStarter.Aufrufe[0].ShellAusfuehren.Should().BeTrue();
    }

    /// <summary>Ein Repository ohne .sln-Datei wird automatisch mit Visual Studio Code geöffnet (Fallback).</summary>
    [Fact]
    public async Task E2E_IdePluginSelection_RepositoryWithoutSln()
    {
        var repository = _tempDirectoryFixture.CreateTempDirectory("e2e_ide_selection_ohne_sln");

        var (pluginSelectionService, prozessStarter) = CreateStack();

        await OpenRepositoryInIdeAsync(pluginSelectionService, repository);

        prozessStarter.Aufrufe.Should().ContainSingle();
        prozessStarter.Aufrufe[0].DateiName.Should().Be("code.cmd");
        prozessStarter.Aufrufe[0].Argumente.Should().Be($"\"{repository}\"");
        prozessStarter.Aufrufe[0].ShellAusfuehren.Should().BeFalse();
    }

    /// <summary>
    /// Ist Visual Studio deaktiviert, wird trotz vorhandener .sln-Datei Visual Studio Code als
    /// verbleibendes aktives (Fallback-)Plugin verwendet.
    /// </summary>
    [Fact]
    public async Task E2E_IdePluginSelection_VisualStudioDisabled()
    {
        var repository = _tempDirectoryFixture.CreateTempDirectory("e2e_ide_selection_vs_deaktiviert");
        File.WriteAllText(Path.Combine(repository, "Loesung.sln"), string.Empty);

        var (pluginSelectionService, prozessStarter, pluginActivationService) = CreateStackWithActivationService();
        await pluginActivationService.SetPluginEnabledAsync("Softwareschmiede.VisualStudio", false);

        await OpenRepositoryInIdeAsync(pluginSelectionService, repository);

        prozessStarter.Aufrufe.Should().ContainSingle();
        prozessStarter.Aufrufe[0].DateiName.Should().Be("code.cmd");
        prozessStarter.Aufrufe[0].ShellAusfuehren.Should().BeFalse();
    }

    /// <summary>
    /// Bildet den Resolve/Find/Open-Ablauf nach, den auch <c>TaskDetailViewModel.OeffneIdeInternAsync</c>
    /// nutzt: Plugin über <see cref="PluginSelectionService.ResolveIdePluginAsync"/> auflösen, dessen
    /// Einstiegspunkte ermitteln und den ersten öffnen. Alle Szenarien dieser Klasse liefern genau einen
    /// Einstiegspunkt, daher wird hier bewusst keine Mehrfach-Auswahl-Verzweigung nachgebildet.
    /// </summary>
    /// <param name="pluginSelectionService">Löst das für das Repository zuständige IDE-Plugin auf.</param>
    /// <param name="repositoryPath">Pfad des zu öffnenden Repositories.</param>
    private static async Task OpenRepositoryInIdeAsync(PluginSelectionService pluginSelectionService, string repositoryPath)
    {
        var plugin = await pluginSelectionService.ResolveIdePluginAsync(repositoryPath);
        var entryPoints = await plugin.FindEntryPointsAsync(repositoryPath);
        await plugin.OpenEntryPointAsync(entryPoints[0]);
    }

    private (PluginSelectionService PluginSelectionService, RecordingProzessStarter ProzessStarter) CreateStack()
    {
        var (pluginSelectionService, prozessStarter, _) = CreateStackWithActivationService();
        return (pluginSelectionService, prozessStarter);
    }

    private (PluginSelectionService PluginSelectionService, RecordingProzessStarter ProzessStarter, PluginActivationService PluginActivationService) CreateStackWithActivationService()
    {
        var db = TestDbContextFactory.Create();
        var prozessStarter = new RecordingProzessStarter();
        var visualStudioCodeLocator = new TestVisualStudioCodeLocator(new VisualStudioCodeAvailability(true, "code.cmd"));

        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IProzessStarter>(prozessStarter)
            .AddSingleton<IVisualStudioCodeLocator>(visualStudioCodeLocator)
            .BuildServiceProvider();

        var pluginManager = new PluginManager(
            services,
            NullLogger<PluginManager>.Instance,
            Path.Combine(Path.GetTempPath(), $"e2e_ide_selection_keine_plugins_{Guid.NewGuid():N}"));

        var appEinstellungService = new AppEinstellungService(db, NullLogger<AppEinstellungService>.Instance);
        var pluginActivationService = new PluginActivationService(appEinstellungService, pluginManager, NullLogger<PluginActivationService>.Instance);
        var defaultSettingsService = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);
        var pluginSelectionService = new PluginSelectionService(
            pluginManager,
            defaultSettingsService,
            pluginActivationService,
            NullLogger<PluginSelectionService>.Instance,
            appEinstellungService);

        return (pluginSelectionService, prozessStarter, pluginActivationService);
    }

    private sealed class RecordingProzessStarter : IProzessStarter
    {
        public List<ProzessStartAnfrage> Aufrufe { get; } = [];

        public void Starten(ProzessStartAnfrage anfrage) => Aufrufe.Add(anfrage);
    }
}
