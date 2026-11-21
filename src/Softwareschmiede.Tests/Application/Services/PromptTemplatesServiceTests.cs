using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests fuer Promptvorlagen-Services.</summary>
public sealed class PromptTemplatesServiceTests
{
    /// <summary>EnsureInitialPromptVorlagenAsync legt die drei Standardvorlagen an.</summary>
    [Fact]
    public async Task EnsureInitialPromptTemplatesAsync_CreatesThreeDefaulttemplatesAn()
    {
        using var db = TestDbContextFactory.Create();
        var sut = new PromptTemplateService(db, NullLogger<PromptTemplateService>.Instance);

        await sut.EnsureInitialPromptTemplatesAsync(TestContext.Current.CancellationToken);

        var templates = await sut.GetAllAsync(TestContext.Current.CancellationToken);
        templates.Should().HaveCount(3);
        templates.Select(v => v.Name).Should().ContainInOrder(
            "Initialanforderung senden",
            "Weitermachen",
            "Pullrequest");
        templates[0].PromptText.Should().Be("Die Anforderung zum Thema '%TaskName%' ist in issue.md beschrieben.");
    }

    /// <summary>EnsureInitialPromptVorlagenAsync legt keine Duplikate an, wenn bereits Vorlagen existieren.</summary>
    [Fact]
    public async Task EnsureInitialPromptTemplatesAsync_WithExistingTemplate_CreatesNoDuplicatesAn()
    {
        using var db = TestDbContextFactory.Create();
        db.PromptTemplates.Add(new PromptTemplate
        {
            Name = "Benutzerdefiniert",
            PromptText = "Eigener Prompt",
            SortOrder = 0
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var sut = new PromptTemplateService(db, NullLogger<PromptTemplateService>.Instance);

        await sut.EnsureInitialPromptTemplatesAsync(TestContext.Current.CancellationToken);

        var templates = await sut.GetAllAsync(TestContext.Current.CancellationToken);
        templates.Should().ContainSingle();
        templates[0].Name.Should().Be("Benutzerdefiniert");
    }

    /// <summary>PromptVorlagenPlatzhalterService ersetzt bekannte Platzhalter und laesst unbekannte stehen.</summary>
    [Fact]
    public void PromptTemplatesPlaceholderService_Resolve_ReplacesKnownPlaceholder()
    {
        var sut = new PromptTemplatePlaceholderService();
        var project = new Project { Id = Guid.NewGuid(), Name = "Projekt A", Status = ProjectStatus.Active };
        var repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Project = project,
            RepositoryName = "Repo",
            RepositoryUrl = "https://example.test/repo.git",
            PluginType = "git"
        };
        var workItem = new WorkItem
        {
            Id = Guid.NewGuid(),
            Title = "Aufgabe B",
            Project = project,
            GitRepository = repository
        };

        var result = sut.Resolve("%ProjectName%/%TaskName%/%RepositoryUrl%/%Unknown%", workItem);

        result.Should().Be("Projekt A/Aufgabe B/https://example.test/repo.git/%Unknown%");
    }

    /// <summary>PromptVorlagenPlatzhalterService ersetzt fehlende RepositoryUrl durch leeren Text.</summary>
    [Fact]
    public void PromptTemplatesPlaceholderService_Resolve_MissingRepositoryWillEmpty()
    {
        var sut = new PromptTemplatePlaceholderService();
        var workItem = new WorkItem
        {
            Id = Guid.NewGuid(),
            Title = "Aufgabe",
            Project = new Project { Id = Guid.NewGuid(), Name = "Projekt", Status = ProjectStatus.Active }
        };

        var result = sut.Resolve("Repo=%RepositoryUrl%", workItem);

        result.Should().Be("Repo=");
    }
}
