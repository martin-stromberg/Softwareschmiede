using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests fuer Promptvorlagen-Services.</summary>
public sealed class PromptVorlagenServiceTests
{
    /// <summary>EnsureInitialPromptVorlagenAsync legt die drei Standardvorlagen an.</summary>
    [Fact]
    public async Task EnsureInitialPromptVorlagenAsync_LegtDreiStandardvorlagenAn()
    {
        using var db = TestDbContextFactory.Create();
        var sut = new PromptVorlagenService(db, NullLogger<PromptVorlagenService>.Instance);

        await sut.EnsureInitialPromptVorlagenAsync();

        var vorlagen = await sut.GetAllAsync();
        vorlagen.Should().HaveCount(3);
        vorlagen.Select(v => v.Name).Should().ContainInOrder(
            "Initialanforderung senden",
            "Weitermachen",
            "Pullrequest");
        vorlagen[0].Prompttext.Should().Be("Die Anforderung zum Thema '%TaskName%' ist in issue.md beschrieben.");
    }

    /// <summary>EnsureInitialPromptVorlagenAsync legt keine Duplikate an, wenn bereits Vorlagen existieren.</summary>
    [Fact]
    public async Task EnsureInitialPromptVorlagenAsync_MitBestehenderVorlage_LegtKeineDuplikateAn()
    {
        using var db = TestDbContextFactory.Create();
        db.PromptVorlagen.Add(new PromptVorlage
        {
            Name = "Benutzerdefiniert",
            Prompttext = "Eigener Prompt",
            Sortierung = 0
        });
        await db.SaveChangesAsync();
        var sut = new PromptVorlagenService(db, NullLogger<PromptVorlagenService>.Instance);

        await sut.EnsureInitialPromptVorlagenAsync();

        var vorlagen = await sut.GetAllAsync();
        vorlagen.Should().ContainSingle();
        vorlagen[0].Name.Should().Be("Benutzerdefiniert");
    }

    /// <summary>PromptVorlagenPlatzhalterService ersetzt bekannte Platzhalter und laesst unbekannte stehen.</summary>
    [Fact]
    public void PromptVorlagenPlatzhalterService_Resolve_ErsetztBekanntePlatzhalter()
    {
        var sut = new PromptVorlagenPlatzhalterService();
        var projekt = new Projekt { Id = Guid.NewGuid(), Name = "Projekt A", Status = ProjektStatus.Aktiv };
        var repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            ProjektId = projekt.Id,
            Projekt = projekt,
            RepositoryName = "Repo",
            RepositoryUrl = "https://example.test/repo.git",
            PluginTyp = "git"
        };
        var aufgabe = new Aufgabe
        {
            Id = Guid.NewGuid(),
            Titel = "Aufgabe B",
            Projekt = projekt,
            GitRepository = repository
        };

        var result = sut.Resolve("%ProjectName%/%TaskName%/%RepositoryUrl%/%Unknown%", aufgabe);

        result.Should().Be("Projekt A/Aufgabe B/https://example.test/repo.git/%Unknown%");
    }

    /// <summary>PromptVorlagenPlatzhalterService ersetzt fehlende RepositoryUrl durch leeren Text.</summary>
    [Fact]
    public void PromptVorlagenPlatzhalterService_Resolve_FehlendesRepositoryWirdLeer()
    {
        var sut = new PromptVorlagenPlatzhalterService();
        var aufgabe = new Aufgabe
        {
            Id = Guid.NewGuid(),
            Titel = "Aufgabe",
            Projekt = new Projekt { Id = Guid.NewGuid(), Name = "Projekt", Status = ProjektStatus.Aktiv }
        };

        var result = sut.Resolve("Repo=%RepositoryUrl%", aufgabe);

        result.Should().Be("Repo=");
    }
}
