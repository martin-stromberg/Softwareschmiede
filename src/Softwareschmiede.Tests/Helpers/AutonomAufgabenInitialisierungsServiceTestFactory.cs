using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Tests.Helpers;

/// <summary>Erstellt die für AutonomAufgabenInitialisierungsService-/AutonomAufgabeInitialisierungsDialogViewModel-Tests benötigten Testdaten und Mocks (ICliRunner für "git branch", IGitPlugin mit Klon-Callback, Service, Projekt/Aufgabe).</summary>
internal static class AutonomAufgabenInitialisierungsServiceTestFactory
{
    /// <summary>Erstellt einen ICliRunner-Mock, der jeden "git"-Aufruf (insbesondere "git branch" in ErstelleProjektbranchAsync) erfolgreich simuliert.</summary>
    /// <returns>Ein Mock, der bei jedem "git"-Aufruf einen erfolgreichen CliResult zurückgibt.</returns>
    public static Mock<ICliRunner> CreateCliRunnerMockMitErfolgreicherGitAusfuehrung()
    {
        var cliRunnerMock = new Mock<ICliRunner>();
        cliRunnerMock
            .Setup(r => r.RunAsync(
                "git",
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string?>(),
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));
        return cliRunnerMock;
    }

    /// <summary>Erstellt einen IGitPlugin-Mock, dessen CloneRepositoryAsync das Zielverzeichnis inklusive Marker-Datei anlegt, dessen GetRemoteBranchesAsync standardmäßig eine leere Liste liefert (Branch existiert nicht remote) und dessen ResolveEffectiveRepositoryPathAsync den übergebenen Pfad unverändert zurückgibt.</summary>
    /// <returns>Ein Mock, der einen erfolgreichen Klon sowie die für ErstelleProjektbranchAsync benötigten Standardantworten simuliert.</returns>
    public static Mock<IGitPlugin> CreateGitPluginMockMitErfolgreichemKlon()
    {
        var gitPluginMock = new Mock<IGitPlugin>();
        gitPluginMock
            .Setup(p => p.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, zielPfad, _) =>
            {
                Directory.CreateDirectory(zielPfad);
                File.WriteAllText(Path.Combine(zielPfad, ".git-marker"), "cloned");
            })
            .Returns(Task.CompletedTask);
        gitPluginMock
            .Setup(p => p.GetRemoteBranchesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());
        gitPluginMock.SetupPassthroughResolveEffectiveRepositoryPath();
        return gitPluginMock;
    }

    /// <summary>Erstellt einen AutonomAufgabenInitialisierungsService mit Standard-Options für Tests.</summary>
    /// <param name="db">Der zu verwendende Datenbankkontext.</param>
    /// <param name="cliRunner">Der zu verwendende ICliRunner.</param>
    /// <param name="gitPlugin">Der zu verwendende IGitPlugin.</param>
    /// <returns>Ein einsatzbereiter AutonomAufgabenInitialisierungsService.</returns>
    public static AutonomAufgabenInitialisierungsService CreateService(SoftwareschmiededDbContext db, ICliRunner cliRunner, IGitPlugin gitPlugin)
        => new(db, cliRunner, gitPlugin, Options.Create(new AutonomAufgabenOptions()), NullLogger<AutonomAufgabenInitialisierungsService>.Instance);

    /// <summary>Erstellt ein Projekt und fügt es dem Datenbankkontext hinzu, ohne zu speichern.</summary>
    /// <param name="db">Der zu verwendende Datenbankkontext.</param>
    /// <returns>Die Id des neu angelegten Projekts.</returns>
    public static Guid ErstelleProjekt(SoftwareschmiededDbContext db)
    {
        var projektId = Guid.NewGuid();
        db.Projekte.Add(new Projekt
        {
            Id = projektId,
            Name = "Testprojekt",
            ErstellungsDatum = DateTimeOffset.UtcNow,
            Status = ProjektStatus.Aktiv
        });
        return projektId;
    }

    /// <summary>Erstellt eine Aufgabe mit lokalem Klon-Verzeichnis (wird auf der Festplatte angelegt) und einem verknüpften GitRepository (RepositoryUrl zeigt auf dasselbe Verzeichnis) und fügt sie dem Datenbankkontext hinzu, ohne zu speichern.</summary>
    /// <param name="db">Der zu verwendende Datenbankkontext.</param>
    /// <param name="projektId">Die Id des Projekts, dem die Aufgabe zugeordnet wird.</param>
    /// <param name="testRoot">Das Arbeitsverzeichnis, aus dessen Pfad das lokale Klon-Quellverzeichnis (testRoot + "-quelle") abgeleitet wird.</param>
    /// <param name="titel">Der Titel der Aufgabe.</param>
    /// <param name="branchName">Der BranchName der Aufgabe, oder null.</param>
    /// <returns>Die neu angelegte, noch nicht gespeicherte Aufgabe.</returns>
    public static Aufgabe ErstelleAufgabeMitLokalemKlon(
        SoftwareschmiededDbContext db, Guid projektId, string testRoot, string titel, string? branchName = null)
    {
        var quellRepo = testRoot + "-quelle";
        Directory.CreateDirectory(quellRepo);

        var aufgabe = new Aufgabe
        {
            Id = Guid.NewGuid(),
            ProjektId = projektId,
            Titel = titel,
            Status = AufgabeStatus.Neu,
            AusfuehrungsStatus = AufgabeAusfuehrungsStatus.NichtGestartet,
            ErstellungsDatum = DateTimeOffset.UtcNow,
            LokalerKlonPfad = quellRepo,
            BranchName = branchName,
            GitRepository = new GitRepository
            {
                Id = Guid.NewGuid(),
                ProjektId = projektId,
                PluginTyp = "TestGitPlugin",
                RepositoryUrl = quellRepo,
                RepositoryName = "quelle"
            }
        };
        db.Aufgaben.Add(aufgabe);

        return aufgabe;
    }
}
