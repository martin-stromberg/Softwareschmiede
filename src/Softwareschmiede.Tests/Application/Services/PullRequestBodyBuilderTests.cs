using Softwareschmiede.Application.Services;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Extraktion der Closing-Direktiven aus Pull-Request-Bodys.</summary>
public sealed class PullRequestBodyBuilderTests
{
    /// <summary>Alle unterstützten Closing-Keywords werden unabhängig von der Schreibweise erkannt.</summary>
    [Theory]
    [InlineData("Closes #42")]
    [InlineData("close #42")]
    [InlineData("closed #42")]
    [InlineData("CLOSES #42")]
    [InlineData("Fix #42")]
    [InlineData("fixes #42")]
    [InlineData("fixed #42")]
    [InlineData("Resolve #42")]
    [InlineData("resolves #42")]
    [InlineData("resolved #42")]
    public void ExtractClosingIssueNumbers_WithClosingDirective_ReturnsIssueNumber(string body)
    {
        Assert.Equal([42], PullRequestBodyBuilder.ExtractClosingIssueNumbers(body));
    }

    /// <summary>Mehrere Direktiven im selben Body liefern alle referenzierten Issue-Nummern.</summary>
    [Fact]
    public void ExtractClosingIssueNumbers_WithMultipleDirectives_ReturnsAllIssueNumbers()
    {
        Assert.Equal([7, 12], PullRequestBodyBuilder.ExtractClosingIssueNumbers(
            "Closes #7 und fixes #12. Siehe auch #99 ohne Direktive.").OrderBy(x => x));
    }

    /// <summary>Ohne Repository-Filter zählt eine qualifizierte Direktive ungefiltert.</summary>
    [Fact]
    public void ExtractClosingIssueNumbers_WithQualifiedDirectiveWithoutRepositoryId_ReturnsIssueNumber()
    {
        Assert.Equal([42], PullRequestBodyBuilder.ExtractClosingIssueNumbers("Closes owner/repo#42"));
    }

    /// <summary>Eine qualifizierte Direktive zählt bei Übereinstimmung mit der Repository-ID.</summary>
    [Theory]
    [InlineData("Closes owner/repo#42", "owner/repo")]
    [InlineData("Closes OWNER/REPO#42", "owner/repo")]
    public void ExtractClosingIssueNumbers_WithMatchingQualifiedDirective_ReturnsIssueNumber(string body, string repositoryId)
    {
        Assert.Equal([42], PullRequestBodyBuilder.ExtractClosingIssueNumbers(body, repositoryId));
    }

    /// <summary>Eine auf ein fremdes Repository qualifizierte Direktive zählt nicht.</summary>
    [Fact]
    public void ExtractClosingIssueNumbers_WithForeignQualifiedDirective_ReturnsEmpty()
    {
        Assert.Empty(PullRequestBodyBuilder.ExtractClosingIssueNumbers("Closes other/repo#42", "owner/repo"));
    }

    /// <summary>Unqualifizierte Direktiven zählen auch mit gesetztem Repository-Filter.</summary>
    [Fact]
    public void ExtractClosingIssueNumbers_WithUnqualifiedDirectiveAndRepositoryId_ReturnsIssueNumber()
    {
        Assert.Equal([42], PullRequestBodyBuilder.ExtractClosingIssueNumbers("Closes #42", "owner/repo"));
    }

    /// <summary>Ohne Direktive oder ohne Text gibt es keine Treffer.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Nur Text ohne Direktive")]
    [InlineData("Closes issue 42")]
    [InlineData("Verweis auf #42")]
    public void ExtractClosingIssueNumbers_WithoutDirective_ReturnsEmpty(string? body)
    {
        Assert.Empty(PullRequestBodyBuilder.ExtractClosingIssueNumbers(body));
    }
}
