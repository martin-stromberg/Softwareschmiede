using FluentAssertions;
using Softwareschmiede.Application.Services;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Unit-Tests für den festen Analyse-Prompt der Issue-Aufbereitung:
/// Titel/Beschreibung-Einbettung, alle Qualitätskriterien und der Ausgabevertrag.</summary>
public sealed class ScmIssueAufbereitungPromptTests
{
    /// <summary>Titel und Beschreibung werden vollständig in den Prompt eingebettet.</summary>
    [Fact]
    public void Build_BettetTitelUndBeschreibungEin()
    {
        var prompt = ScmIssueAufbereitungPrompt.Build("Mein Titel", "Meine Beschreibung");

        prompt.Should().Contain("Mein Titel");
        prompt.Should().Contain("Meine Beschreibung");
    }

    /// <summary>Alle geforderten Qualitätskriterien sind Teil des Prompts.</summary>
    [Fact]
    public void Build_EnthältAlleQualitätskriterien()
    {
        var prompt = ScmIssueAufbereitungPrompt.Build("T", "B");

        prompt.Should().Contain("Verständlichkeit");
        prompt.Should().Contain("sprachliche Qualität");
        prompt.Should().Contain("Vollständigkeit");
        prompt.Should().Contain("fehlende Informationen");
        prompt.Should().Contain("offene Fragen");
        prompt.Should().Contain("Risiken");
        prompt.Should().Contain("Zielkonflikte");
        prompt.Should().Contain("Lösungsansätze");
        prompt.Should().Contain("Fehlinterpretationen");
    }

    /// <summary>Der Ausgabevertrag verlangt die issueBodyUpdate- und comment-Aktion und
    /// sichert ausdrücklich, dass der Titel unverändert bleibt.</summary>
    [Fact]
    public void Build_DefiniertAusgabevertrag()
    {
        var prompt = ScmIssueAufbereitungPrompt.Build("T", "B");

        prompt.Should().Contain("issueBodyUpdate");
        prompt.Should().Contain("comment");
        prompt.Should().Contain("Titel wird nicht geändert");
    }

    /// <summary>Der Prompt ist deterministisch (Pflicht für die bitgleiche
    /// request.json-Rekonstruktion beim Wiederanlauf).</summary>
    [Fact]
    public void Build_IstDeterministisch()
    {
        ScmIssueAufbereitungPrompt.Build("T", "B").Should().Be(ScmIssueAufbereitungPrompt.Build("T", "B"));
        ScmIssueAufbereitungPrompt.Build(null, null).Should().Be(ScmIssueAufbereitungPrompt.Build(null, null));
    }

    /// <summary>Fehlende Werte degradieren ehrlich statt zu stürzen.</summary>
    [Fact]
    public void Build_LeereWerte_LiefertPlatzhalter()
    {
        var prompt = ScmIssueAufbereitungPrompt.Build(null, "  ");

        prompt.Should().Contain("(kein Titel)");
        prompt.Should().Contain("(leer)");
    }
}
