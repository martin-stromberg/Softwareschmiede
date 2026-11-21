using System.Text.Json;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Wire-Kompatibilität persistierter SCM-Ereignis-DTOs (ObservationJson/RequestJson).</summary>
public sealed class ScmEventWireCompatibilityTests
{
    /// <summary>Der persistierte Feldname „konto" bleibt nach der Umbenennung von
    /// <see cref="ScmEventRepository.Account"/> stabil.</summary>
    [Fact]
    public void ScmEventRepository_Account_SerializesAsAccount()
    {
        var repository = new ScmEventRepository("test", "host", "R_1", "owner", "repo",
            "42", "viewer", "login", true, ScmAccountMode.SeparateAccount);

        var json = JsonSerializer.Serialize(repository, ScmEventStore.JsonOptions);

        Assert.Contains("\"konto\":1", json);
        Assert.DoesNotContain("\"account\"", json);
    }

    /// <summary>Altbestand-Zeilen mit „konto" lesen den Kontomodus korrekt zurück.</summary>
    [Fact]
    public void ScmEventRepository_LegacyAccount_DeserializesAsAccount()
    {
        const string json = """
            {"providerPrefix":"test","host":"host","repositoryId":"R_1","owner":"owner",
             "name":"repo","ownerId":"42","viewerId":"v","viewerLogin":"login","canWrite":true,
             "konto":1,"numericRepositoryId":null}
            """;

        var repository = JsonSerializer.Deserialize<ScmEventRepository>(json, ScmEventStore.JsonOptions)!;

        Assert.Equal(ScmAccountMode.SeparateAccount, repository.Account);
    }

    /// <summary>Fehlendes „konto" fällt auf das Benutzerkonto zurück (Default).</summary>
    [Fact]
    public void ScmEventRepository_MissingAccount_DefaultsToUserAccount()
    {
        const string json = """
            {"providerPrefix":"test","host":"host","repositoryId":"R_1","owner":"owner",
             "name":"repo","ownerId":"42","viewerId":"v","viewerLogin":"login","canWrite":true}
            """;

        var repository = JsonSerializer.Deserialize<ScmEventRepository>(json, ScmEventStore.JsonOptions)!;

        Assert.Equal(ScmAccountMode.UserAccount, repository.Account);
    }
}
