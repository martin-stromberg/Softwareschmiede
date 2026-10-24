using Softwareschmiede.Application.Services;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Range boundaries, token strictness and catalog honesty of the prompt-format profiles.</summary>
public sealed class ScmCliPromptProfileTests
{
    /// <summary>Same Major.Minor with any patch is supported; other Minor/Major releases are not.</summary>
    [Theory]
    [InlineData("Softwareschmiede.Codex", "OpenAI Codex (v0.114.0)", true)]
    [InlineData("Softwareschmiede.Codex", "OpenAI Codex (v0.114.7)", true)]
    [InlineData("Softwareschmiede.Codex", "OpenAI Codex (v0.115.0)", false)]
    [InlineData("Softwareschmiede.Codex", "OpenAI Codex (v0.113.9)", false)]
    [InlineData("Softwareschmiede.ClaudeCli", "Claude Code v2.1.0", true)]
    [InlineData("Softwareschmiede.ClaudeCli", "Claude Code v2.1.15", true)]
    [InlineData("Softwareschmiede.ClaudeCli", "Claude Code v2.2.0", false)]
    [InlineData("Softwareschmiede.Devin", "Devin CLI\nv3000.11.0", true)]
    [InlineData("Softwareschmiede.Devin", "Devin CLI\nv3000.11.205", true)]
    [InlineData("Softwareschmiede.Devin", "Devin CLI\nv3000.12.0", false)]
    [InlineData("Softwareschmiede.Devin", "Devin CLI\nv2999.9.9", false)]
    public void TryMatch_ClassifiesRangeBoundaries(string prefix, string text, bool supported)
    {
        var match = ScmCliPromptProfile.TryMatch(prefix, text);
        Assert.NotNull(match);
        Assert.Equal(supported, match.IsSupported);
        Assert.NotNull(match.Version);
    }

    /// <summary>Missing banner anchors and prefixes without a profile never produce a match.</summary>
    [Theory]
    [InlineData("Softwareschmiede.GitHubCopilot", "GitHub Copilot CLI v1.0.0")]
    [InlineData("Softwareschmiede.Codex", "irrelevant startup output")]
    [InlineData("Softwareschmiede.Devin", "v3000.11.92 alone is not the banner")]
    public void TryMatch_WithoutAnchorOrProfile_ReturnsNull(string prefix, string text)
        => Assert.Null(ScmCliPromptProfile.TryMatch(prefix, text));

    /// <summary>Suffixes or missing segments are honestly unparseable, never guessed.</summary>
    [Theory]
    [InlineData("Softwareschmiede.Codex", "OpenAI Codex (v0.114.0-beta)")]
    [InlineData("Softwareschmiede.ClaudeCli", "Claude Code v2.1.15-beta")]
    [InlineData("Softwareschmiede.ClaudeCli", "Claude Code v2.1")]
    [InlineData("Softwareschmiede.Devin", "Devin CLI\nbeta")]
    public void TryMatch_UnparseableToken_IsUnsupportedWithoutVersion(string prefix, string text)
    {
        var match = ScmCliPromptProfile.TryMatch(prefix, text);
        Assert.NotNull(match);
        Assert.False(match.IsSupported);
        Assert.Null(match.Version);
    }

    /// <summary>The unversioned simulator contract matches on its anchor alone.</summary>
    [Fact]
    public void TryMatch_SimulatorContract_MatchesWithoutVersion()
    {
        var match = ScmCliPromptProfile.TryMatch("Softwareschmiede.KiSimulator", "SCM_SIMULATOR_PROTOCOL_V1");
        Assert.NotNull(match);
        Assert.True(match.IsSupported);
        Assert.Null(match.Version);
        Assert.Equal("KI-Simulator", match.Profile.DisplayName);
    }

    /// <summary>Only reasons carrying the shared confirmation suffix offer the confirm action.</summary>
    [Theory]
    [InlineData("Eingabebereitschaft unbekannt. Für die angezeigte CLI-Sitzung bestätigen.", true)]
    [InlineData("Devin CLI v3000.12.0 wird nicht unterstützt; unterstützt ist v3000.11.x. Für die angezeigte CLI-Sitzung bestätigen.", true)]
    [InlineData("Das Promptformat der gestarteten CLI ist unbekannt; Eingabebereitschaft und Antwortkanal können nicht geprüft werden.", false)]
    [InlineData("Eingabebereitschaft unbekannt.", false)]
    [InlineData(null, false)]
    public void IsConfirmable_MatchesOnlyTheMarkerSuffix(string? reason, bool expected)
        => Assert.Equal(expected, ScmCliWaitReason.IsConfirmable(reason));

    /// <summary>An out-of-range version names CLI, detected version and supported range, and stays confirmable.</summary>
    [Fact]
    public void Describe_UnsupportedVersion_NamesCliVersionAndRange()
    {
        var reason = ScmCliWaitReason.Describe(new(ScmCliRecognitionState.Unsupported, "Devin CLI", new Version(3000, 12, 0), "v3000.11.x"));
        Assert.Contains("Devin CLI", reason);
        Assert.Contains("v3000.12.0", reason);
        Assert.Contains("v3000.11.x", reason);
        Assert.True(ScmCliWaitReason.IsConfirmable(reason));
    }

    /// <summary>A recognized anchor without a bound channel reports the start-contract reason,
    /// never a missing banner, and stays non-confirmable.</summary>
    [Fact]
    public void Describe_AnchorWithoutChannel_ReportsAccessReason()
    {
        var reason = ScmCliWaitReason.Describe(new(ScmCliRecognitionState.Unknown, "KI-Simulator", null, null)
        { ChannelReason = "Diese Simulator-Sitzung ist eine Shell und kein KI-Eingabekanal." });
        Assert.Contains("KI-Simulator", reason);
        Assert.Contains("Shell", reason);
        Assert.DoesNotContain("Versionsbanner", reason);
        Assert.False(ScmCliWaitReason.IsConfirmable(reason));
    }

    /// <summary>An unknown format never offers confirmation, with and without a known CLI name.</summary>
    [Fact]
    public void Describe_Unknown_NeverConfirmable()
    {
        Assert.False(ScmCliWaitReason.IsConfirmable(ScmCliWaitReason.Describe(new(ScmCliRecognitionState.Unknown, "KI-Simulator", null, null))));
        var profileless = ScmCliWaitReason.Describe(new(ScmCliRecognitionState.Unknown, null, null, null));
        Assert.Contains("unbekannt", profileless);
        Assert.False(ScmCliWaitReason.IsConfirmable(profileless));
    }
}
