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

/// <summary>Erstellt die für AutonomAufgabenInitialisierungsService-/AutonomAufgabeInitialisierungsDialogViewModel-Tests benötigten Testdaten und Mocks (ICliRunner mit Git-Klon-Callback, Service, Projekt/Aufgabe).</summary>
internal static class AutonomAufgabenInitialisierungsServiceTestFactory
{
    /// <summary>Erstellt einen ICliRunner-Mock, der einen "git clone"-Aufruf simuliert, indem das Zielverzeichnis inklusive Marker-Datei angelegt wird.</summary>
    /// <returns>Ein Mock, der bei jedem "git clone"-Aufruf das Zielverzeichnis anlegt und einen erfolgreichen CliResult zurückgibt.</returns>
    public static Mock<ICliRunner> CreateCliRunnerMockMitErfolgreichemGitKlon()
    {
        var cliRunnerMock = new Mock<ICliRunner>();
        cliRunnerMock
            .Setup(r => r.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.Contains("clone")),
                It.IsAny<string?>(),
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IEnumerable<string>, string?, IDictionary<string, string>?, CancellationToken>((_, args, _, _, _) =>
            {
                var zielPfad = args.Last();
                Directory.CreateDirectory(zielPfad);
                File.WriteAllText(Path.Combine(zielPfad, ".git-marker"), "cloned");
            })
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));
        return cliRunnerMock;
    }

    /// <summary>Erstellt einen AutonomAufgabenInitialisierungsService mit Standard-Options für Tests.</summary>
    /// <param name="db">Der zu verwendende Datenbankkontext.</param>
    /// <param name="cliRunner">Der zu verwendende ICliRunner.</param>
    /// <returns>Ein einsatzbereiter AutonomAufgabenInitialisierungsService.</returns>
    public static AutonomAufgabenInitialisierungsService CreateService(SoftwareschmiededDbContext db, ICliRunner cliRunner)
        => new(db, cliRunner, Options.Create(new AutonomAufgabenOptions()), NullLogger<AutonomAufgabenInitialisierungsService>.Instance);

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

    /// <summary>Erstellt eine Aufgabe mit lokalem Klon-Verzeichnis (wird auf der Festplatte angelegt) und fügt sie dem Datenbankkontext hinzu, ohne zu speichern.</summary>
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
            BranchName = branchName
        };
        db.Aufgaben.Add(aufgabe);

        return aufgabe;
    }
}
