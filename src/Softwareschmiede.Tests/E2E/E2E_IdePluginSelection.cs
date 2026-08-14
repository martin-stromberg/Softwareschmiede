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
/// <see cref="PluginSelectionService"/>, <see cref="IdeOeffnenService.OpenRepositoryInIdeAsync"/>).
///
/// Diese Szenarien werden bewusst zusätzlich zur FlaUI-Abdeckung in <c>E2E_VerzeichnisAktionen</c> (Ribbon-
/// Button "IDE öffnen", der inzwischen ebenfalls über <see cref="PluginSelectionService.ResolveIdePluginAsync"/>
/// auflöst) als reine Objektgraph-Tests gehalten, um die Plugin-Auswahl-Logik selbst schnell und ohne
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

        var (ideOeffnenService, prozessStarter) = CreateStack();

        await ideOeffnenService.OpenRepositoryInIdeAsync(repository);

        prozessStarter.Aufrufe.Should().ContainSingle();
        prozessStarter.Aufrufe[0].DateiName.Should().Be(Path.Combine(repository, "Loesung.sln"));
        prozessStarter.Aufrufe[0].ShellAusfuehren.Should().BeTrue();
    }

    /// <summary>Ein Repository ohne .sln-Datei wird automatisch mit Visual Studio Code geöffnet (Fallback).</summary>
    [Fact]
    public async Task E2E_IdePluginSelection_RepositoryWithoutSln()
    {
        var repository = _tempDirectoryFixture.CreateTempDirectory("e2e_ide_selection_ohne_sln");

        var (ideOeffnenService, prozessStarter) = CreateStack();

        await ideOeffnenService.OpenRepositoryInIdeAsync(repository);

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

        var (ideOeffnenService, prozessStarter, pluginActivationService) = CreateStackWithActivationService();
        await pluginActivationService.SetPluginEnabledAsync("Softwareschmiede.VisualStudio", false);

        await ideOeffnenService.OpenRepositoryInIdeAsync(repository);

        prozessStarter.Aufrufe.Should().ContainSingle();
        prozessStarter.Aufrufe[0].DateiName.Should().Be("code.cmd");
        prozessStarter.Aufrufe[0].ShellAusfuehren.Should().BeFalse();
    }

    private (IdeOeffnenService IdeOeffnenService, RecordingProzessStarter ProzessStarter) CreateStack()
    {
        var (ideOeffnenService, prozessStarter, _) = CreateStackWithActivationService();
        return (ideOeffnenService, prozessStarter);
    }

    private (IdeOeffnenService IdeOeffnenService, RecordingProzessStarter ProzessStarter, PluginActivationService PluginActivationService) CreateStackWithActivationService()
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

        var ideOeffnenService = new IdeOeffnenService(prozessStarter, pluginSelectionService);

        return (ideOeffnenService, prozessStarter, pluginActivationService);
    }

    private sealed class RecordingProzessStarter : IProzessStarter
    {
        public List<ProzessStartAnfrage> Aufrufe { get; } = [];

        public void Starten(ProzessStartAnfrage anfrage) => Aufrufe.Add(anfrage);
    }
}
