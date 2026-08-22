using FluentAssertions;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Tests.Infrastructure.Data;

/// <summary>Tests für <see cref="DatenbankPfadResolver"/>.</summary>
public sealed class DatenbankPfadResolverTests
{
    /// <summary>Der Test-Override hat höchste Priorität und wird unverändert zurückgegeben.</summary>
    [Fact]
    public void ErmittlePfad_ShouldReturnOverride_WhenTestDbPathOverrideIsSet()
    {
        using var temp = new TempDirectory();
        var overridePfad = Path.Combine(temp.Path, "custom.db");

        var result = DatenbankPfadResolver.ErmittlePfad(temp.Path, overridePfad);

        result.Should().Be(overridePfad);
    }

    /// <summary>Der Test-Override hat auch dann Vorrang, wenn eine produktive version.json vorliegt.</summary>
    [Fact]
    public void ErmittlePfad_ShouldReturnOverride_EvenWhenVersionJsonIndicatesProduktiv()
    {
        using var temp = new TempDirectory();
        SchreibeVersionJson(temp.Path, "v1.2.3");
        var overridePfad = Path.Combine(temp.Path, "custom.db");

        var result = DatenbankPfadResolver.ErmittlePfad(temp.Path, overridePfad);

        result.Should().Be(overridePfad);
    }

    /// <summary>Ohne version.json (z. B. Ausführung unter Visual Studio) wird das lokale Programmverzeichnis genutzt.</summary>
    [Fact]
    public void ErmittlePfad_ShouldUseBaseDirectory_WhenVersionJsonIsMissing()
    {
        using var temp = new TempDirectory();

        var result = DatenbankPfadResolver.ErmittlePfad(temp.Path, testDbPathOverride: null);

        result.Should().Be(Path.Combine(temp.Path, "softwareschmiede.db"));
    }

    /// <summary>Ein RC-Tag (enthält "-rc") führt zum lokalen Programmverzeichnis.</summary>
    /// <param name="tagName">Der zu testende <c>tagName</c>-Wert aus der version.json.</param>
    [Theory]
    [InlineData("v1.2.3-rc.1")]
    [InlineData("V1.2.3-RC.1")]
    public void ErmittlePfad_ShouldUseBaseDirectory_WhenTagNameIsReleaseCandidate(string tagName)
    {
        using var temp = new TempDirectory();
        SchreibeVersionJson(temp.Path, tagName);

        var result = DatenbankPfadResolver.ErmittlePfad(temp.Path, testDbPathOverride: null);

        result.Should().Be(Path.Combine(temp.Path, "softwareschmiede.db"));
    }

    /// <summary>Ein produktiver Tag (kein "-rc"-Infix) führt weiterhin zum %LocalAppData%-Pfad.</summary>
    [Fact]
    public void ErmittlePfad_ShouldUseLocalApplicationData_WhenTagNameIsProduktiv()
    {
        using var temp = new TempDirectory();
        SchreibeVersionJson(temp.Path, "v1.2.3");

        var result = DatenbankPfadResolver.ErmittlePfad(temp.Path, testDbPathOverride: null);

        var erwarteterPfad = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Softwareschmiede",
            "softwareschmiede.db");
        result.Should().Be(erwarteterPfad);
    }

    /// <summary>Eine leere oder syntaktisch ungültige version.json führt zum konservativen Fallback (versionslos).</summary>
    /// <param name="json">Der zu testende, ungültige Dateiinhalt der version.json.</param>
    [Theory]
    [InlineData("")]
    [InlineData("{ nicht valides json")]
    public void ErmittlePfad_ShouldFallBackToBaseDirectory_WhenVersionJsonIsBroken(string json)
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "version.json"), json);

        var result = DatenbankPfadResolver.ErmittlePfad(temp.Path, testDbPathOverride: null);

        result.Should().Be(Path.Combine(temp.Path, "softwareschmiede.db"));
    }

    /// <summary>Eine version.json ohne tagName-Feld führt ebenfalls zum konservativen Fallback (versionslos).</summary>
    [Fact]
    public void ErmittlePfad_ShouldFallBackToBaseDirectory_WhenVersionJsonHasNoTagName()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "version.json"), """{ "version": "1.2.3" }""");

        var result = DatenbankPfadResolver.ErmittlePfad(temp.Path, testDbPathOverride: null);

        result.Should().Be(Path.Combine(temp.Path, "softwareschmiede.db"));
    }

    private static void SchreibeVersionJson(string verzeichnis, string tagName)
    {
        File.WriteAllText(
            Path.Combine(verzeichnis, "version.json"),
            $$"""
            {
              "version": "1.2.3",
              "tagName": "{{tagName}}",
              "commit": "abc",
              "createdAtUtc": "2026-07-14T00:00:00Z"
            }
            """);
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        public TempDirectory() => Directory.CreateDirectory(Path);

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
