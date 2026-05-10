using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Components.Pages;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Tests.Components.Pages;

public sealed class EinstellungenBaseArbeitsverzeichnisTests
{
    [Fact]
    public async Task OnInitializedAsync_ShouldLoadSavedWorkdirAndFallbackHint_WhenResolverUsesFallback()
    {
        await using var db = CreateDb();
        var settingsService = new ArbeitsverzeichnisSettingsService(db, NullLogger<ArbeitsverzeichnisSettingsService>.Instance);
        var savedPath = Path.Combine(Path.GetTempPath(), $"ui-workdir-{Guid.NewGuid():N}");
        await settingsService.SaveArbeitsverzeichnisAsync(savedPath);

        var resolverMock = new Mock<IArbeitsverzeichnisResolver>();
        resolverMock.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ArbeitsverzeichnisResolutionResult(Path.GetTempPath(), true, "no-configured-path", null));

        var sut = CreateSut(settingsService, resolverMock.Object);

        await sut.InvokeOnInitializedAsync();

        sut.ArbeitsverzeichnisInput.Should().Be(Path.GetFullPath(savedPath));
        sut.ArbeitsverzeichnisFallbackHinweis.Should().Contain("Fallback verwendet");
    }

    [Fact]
    public async Task ArbeitsverzeichnisSpeichernAsync_ShouldSetValidationError_WhenInputIsInvalid()
    {
        await using var db = CreateDb();
        var settingsService = new ArbeitsverzeichnisSettingsService(db, NullLogger<ArbeitsverzeichnisSettingsService>.Instance);
        var resolverMock = new Mock<IArbeitsverzeichnisResolver>();
        resolverMock.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ArbeitsverzeichnisResolutionResult(Path.GetTempPath(), false, "configured", null));
        var sut = CreateSut(settingsService, resolverMock.Object);

        sut.InvokeArbeitsverzeichnisInputChanged("relative\\invalid");

        await sut.InvokeArbeitsverzeichnisSpeichernAsync();

        sut.ArbeitsverzeichnisValidationError.Should().NotBeNullOrEmpty();
        sut.ArbeitsverzeichnisStatusIsError.Should().BeTrue();
        sut.ArbeitsverzeichnisStatusMessage.Should().Be("Arbeitsverzeichnis konnte nicht gespeichert werden.");
    }

    [Fact]
    public async Task ArbeitsverzeichnisZuruecksetzenAsync_ShouldClearStoredWorkdir()
    {
        await using var db = CreateDb();
        var settingsService = new ArbeitsverzeichnisSettingsService(db, NullLogger<ArbeitsverzeichnisSettingsService>.Instance);
        var configuredPath = Path.Combine(Path.GetTempPath(), $"ui-reset-{Guid.NewGuid():N}");
        await settingsService.SaveArbeitsverzeichnisAsync(configuredPath);

        var resolverMock = new Mock<IArbeitsverzeichnisResolver>();
        resolverMock.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ArbeitsverzeichnisResolutionResult(Path.GetTempPath(), false, "configured", null));
        var sut = CreateSut(settingsService, resolverMock.Object);
        await sut.InvokeOnInitializedAsync();

        await sut.InvokeArbeitsverzeichnisZuruecksetzenAsync();

        var reloaded = await settingsService.GetArbeitsverzeichnisAsync();
        reloaded.Should().BeNull();
        sut.ArbeitsverzeichnisInput.Should().BeEmpty();
        sut.ArbeitsverzeichnisStatusIsError.Should().BeFalse();
    }

    private static TestEinstellungenPage CreateSut(
        ArbeitsverzeichnisSettingsService arbeitsverzeichnisSettings,
        IArbeitsverzeichnisResolver arbeitsverzeichnisResolver)
    {
        var credentialStoreMock = new Mock<ICredentialStore>();
        var pluginSettings = new PluginSettingsService(credentialStoreMock.Object, NullLogger<PluginSettingsService>.Instance);
        var sut = new TestEinstellungenPage();
        SetInjectedProperty(sut, "GitPlugins", Array.Empty<IGitPlugin>());
        SetInjectedProperty(sut, "KiPlugins", Array.Empty<IKiPlugin>());
        SetInjectedProperty(sut, "PluginSettings", pluginSettings);
        SetInjectedProperty(sut, "ArbeitsverzeichnisSettings", arbeitsverzeichnisSettings);
        SetInjectedProperty(sut, "ArbeitsverzeichnisResolver", arbeitsverzeichnisResolver);
        SetInjectedProperty(sut, "Logger", NullLogger<EinstellungenBase>.Instance);
        return sut;
    }

    private static SoftwareschmiededDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<SoftwareschmiededDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new SoftwareschmiededDbContext(options);
    }

    private static void SetInjectedProperty(object target, string propertyName, object value)
    {
        var property = typeof(EinstellungenBase).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic);
        property.Should().NotBeNull($"Property {propertyName} should exist for test setup.");
        property!.SetValue(target, value);
    }

    private sealed class TestEinstellungenPage : EinstellungenBase
    {
        public string ArbeitsverzeichnisInput => _arbeitsverzeichnisInput;
        public string? ArbeitsverzeichnisValidationError => _arbeitsverzeichnisValidationError;
        public string? ArbeitsverzeichnisStatusMessage => _arbeitsverzeichnisStatusMessage;
        public bool ArbeitsverzeichnisStatusIsError => _arbeitsverzeichnisStatusIsError;
        public string? ArbeitsverzeichnisFallbackHinweis => _arbeitsverzeichnisFallbackHinweis;

        public Task InvokeOnInitializedAsync() => OnInitializedAsync();
        public Task InvokeArbeitsverzeichnisSpeichernAsync() => ArbeitsverzeichnisSpeichernAsync();
        public void InvokeArbeitsverzeichnisInputChanged(string value) => ArbeitsverzeichnisInputChanged(value);
        public Task InvokeArbeitsverzeichnisZuruecksetzenAsync() => ArbeitsverzeichnisZuruecksetzenAsync();
    }
}
