using FluentAssertions;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Infrastructure.Data;

/// <summary>Tests für <see cref="DatabasePathResolver"/>.</summary>
public sealed class DatabasePathResolverTests
{
    /// <summary>Der Test-Override hat höchste Priorität und wird unverändert zurückgegeben.</summary>
    [Fact]
    public void ResolvePath_ShouldReturnOverride_WhenTestDbPathOverrideIsSet()
    {
        using var temp = new TempDirectory();
        var overridePath = Path.Combine(temp.Path, "custom.db");

        var result = DatabasePathResolver.ResolvePath(temp.Path, overridePath);

        result.Should().Be(overridePath);
    }

    /// <summary>Der Test-Override hat auch dann Vorrang, wenn eine produktive version.json vorliegt.</summary>
    [Fact]
    public void ResolvePath_ShouldReturnOverride_EvenWhenVersionJsonIndicatesProduction()
    {
        using var temp = new TempDirectory();
        WriteVersionJson(temp.Path, "v1.2.3");
        var overridePath = Path.Combine(temp.Path, "custom.db");

        var result = DatabasePathResolver.ResolvePath(temp.Path, overridePath);

        result.Should().Be(overridePath);
    }

    /// <summary>Ohne version.json (z. B. Ausführung unter Visual Studio) wird das lokale Programmverzeichnis genutzt.</summary>
    [Fact]
    public void ResolvePath_ShouldUseBaseDirectory_WhenVersionJsonIsMissing()
    {
        using var temp = new TempDirectory();

        var result = DatabasePathResolver.ResolvePath(temp.Path, testDbPathOverride: null);

        result.Should().Be(Path.Combine(temp.Path, "softwareschmiede.db"));
    }

    /// <summary>Ein RC-Tag (enthält "-rc") führt zum lokalen Programmverzeichnis.</summary>
    /// <param name="tagName">Der zu testende <c>tagName</c>-Wert aus der version.json.</param>
    [Theory]
    [InlineData("v1.2.3-rc.1")]
    [InlineData("V1.2.3-RC.1")]
    public void ResolvePath_ShouldUseBaseDirectory_WhenTagNameIsReleaseCandidate(string tagName)
    {
        using var temp = new TempDirectory();
        WriteVersionJson(temp.Path, tagName);

        var result = DatabasePathResolver.ResolvePath(temp.Path, testDbPathOverride: null);

        result.Should().Be(Path.Combine(temp.Path, "softwareschmiede.db"));
    }

    /// <summary>Ein produktiver Tag (kein "-rc"-Infix) führt weiterhin zum %LocalAppData%-Pfad.</summary>
    [Fact]
    public void ResolvePath_ShouldUseLocalApplicationData_WhenTagNameIsProduction()
    {
        using var temp = new TempDirectory();
        WriteVersionJson(temp.Path, "v1.2.3");

        var result = DatabasePathResolver.ResolvePath(temp.Path, testDbPathOverride: null);

        var expectedPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Softwareschmiede",
            "softwareschmiede.db");
        result.Should().Be(expectedPath);
    }

    /// <summary>Eine leere oder syntaktisch ungültige version.json führt zum konservativen Fallback (versionslos).</summary>
    /// <param name="json">Der zu testende, ungültige Dateiinhalt der version.json.</param>
    [Theory]
    [InlineData("")]
    [InlineData("{ nicht valides json")]
    public void ResolvePath_ShouldCaseBackToBaseDirectory_WhenVersionJsonIsBroken(string json)
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "version.json"), json);

        var result = DatabasePathResolver.ResolvePath(temp.Path, testDbPathOverride: null);

        result.Should().Be(Path.Combine(temp.Path, "softwareschmiede.db"));
    }

    /// <summary>Eine version.json ohne tagName-Feld führt ebenfalls zum konservativen Fallback (versionslos).</summary>
    [Fact]
    public void ResolvePath_ShouldCaseBackToBaseDirectory_WhenVersionJsonHasNoTagName()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "version.json"), """{ "version": "1.2.3" }""");

        var result = DatabasePathResolver.ResolvePath(temp.Path, testDbPathOverride: null);

        result.Should().Be(Path.Combine(temp.Path, "softwareschmiede.db"));
    }

    private static void WriteVersionJson(string directory, string tagName)
    {
        File.WriteAllText(
            Path.Combine(directory, "version.json"),
            $$"""
            {
              "version": "1.2.3",
              "tagName": "{{tagName}}",
              "commit": "abc",
              "createdAtUtc": "2026-07-14T00:00:00Z"
            }
            """);
    }
}
