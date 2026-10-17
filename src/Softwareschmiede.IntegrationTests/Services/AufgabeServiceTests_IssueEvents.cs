using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.IntegrationTests.Infrastructure;

namespace Softwareschmiede.IntegrationTests.Services;

/// <summary>Tests für <see cref="AufgabeService.CreateFromIssueEventAsync"/>: SourceKey-Dedup der ereignisgesteuerten Anlage.</summary>
public sealed class AufgabeServiceTests_IssueEvents
{
    /// <summary>Die Anlage persistiert Aufgabe und IssueReferenz inklusive SourceKey.</summary>
    [Fact]
    public async Task CreateFromIssueEventAsync_ShouldPersistSourceKey()
    {
        await using var db = await DatabaseFixture.CreateAsync();
        var (projektId, repositoryId) = await CreateProjektMitRepositoryAsync(db);
        var service = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));
        var issue = new Issue(9, "Issue Titel", "Beschreibung", ["bug"], null, "https://github.com/owner/repo/issues/9");

        var created = await service.CreateFromIssueEventAsync(projektId, issue, repositoryId, "test-scm/github.com/R_1/Issue/9");

        await using var db2 = db.CreateNewContext();
        var loaded = await db2.Aufgaben.Include(x => x.IssueReferenz).SingleAsync(x => x.Id == created.Id);
        loaded.Titel.Should().Be("Issue Titel");
        loaded.GitRepositoryId.Should().Be(repositoryId);
        loaded.IssueReferenz!.IssueNummer.Should().Be(9);
        loaded.IssueReferenz.SourceKey.Should().Be("test-scm/github.com/R_1/Issue/9");
    }

    /// <summary>Ein zweiter Aufruf mit demselben SourceKey liefert die bestehende Aufgabe statt eines Duplikats.</summary>
    [Fact]
    public async Task CreateFromIssueEventAsync_ShouldReuseExisting_WhenSourceKeyExists()
    {
        await using var db = await DatabaseFixture.CreateAsync();
        var (projektId, repositoryId) = await CreateProjektMitRepositoryAsync(db);
        var service = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));
        var issue = new Issue(9, "Issue Titel", "Beschreibung", [], null, null);

        var first = await service.CreateFromIssueEventAsync(projektId, issue, repositoryId, "test-scm/github.com/R_1/Issue/9");
        var second = await service.CreateFromIssueEventAsync(projektId, issue, repositoryId, "test-scm/github.com/R_1/Issue/9");

        second.Id.Should().Be(first.Id);
        (await db.Context.Aufgaben.CountAsync()).Should().Be(1);
        (await db.Context.IssueReferenzen.CountAsync()).Should().Be(1);
    }

    /// <summary>Ein abweichender SourceKey erzeugt eine eigene Aufgabe.</summary>
    [Fact]
    public async Task CreateFromIssueEventAsync_ShouldCreateSeparateTask_ForDifferentSourceKey()
    {
        await using var db = await DatabaseFixture.CreateAsync();
        var (projektId, repositoryId) = await CreateProjektMitRepositoryAsync(db);
        var service = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));
        var issue = new Issue(9, "Issue Titel", "Beschreibung", [], null, null);

        var first = await service.CreateFromIssueEventAsync(projektId, issue, repositoryId, "test-scm/github.com/R_1/Issue/9");
        var second = await service.CreateFromIssueEventAsync(projektId, issue, repositoryId, "test-scm/github.com/R_1/Issue/10");

        second.Id.Should().NotBe(first.Id);
        (await db.Context.Aufgaben.CountAsync()).Should().Be(2);
    }

    private static async Task<(Guid ProjektId, Guid RepositoryId)> CreateProjektMitRepositoryAsync(DatabaseFixture db)
    {
        var project = new Projekt { Id = Guid.NewGuid(), Name = "IssueEvents" };
        var repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            Projekt = project,
            RepositoryUrl = "https://github.com/owner/repo",
            RepositoryName = "repo",
            PluginTyp = "test-scm"
        };
        db.Context.AddRange(project, repository);
        await db.Context.SaveChangesAsync();
        return (project.Id, repository.Id);
    }
}
