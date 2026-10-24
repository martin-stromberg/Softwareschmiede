using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Softwareschmiede.Application.Services;

/// <summary>Halboffener Versionsbereich <c>[Min, MaxExclusive)</c>: <see cref="Min"/> einschließlich, <see cref="MaxExclusive"/> ausschließlich.</summary>
public sealed record ScmCliVersionRange(Version Min, Version MaxExclusive)
{
    /// <summary>Numerischer Vergleich der Versionssegmente, kein Zeichenfolgenvergleich.</summary>
    public bool Contains(Version version) => version >= Min && version < MaxExclusive;
}

/// <summary>Erkennungszustand des gebundenen CLI-Laufs, unabhängig von der dynamischen Prompt-Bereitschaft.</summary>
public enum ScmCliRecognitionState
{
    /// <summary>Noch kein unterstütztes Banner erkannt oder kein Promptformat-Profil für die gestartete CLI.</summary>
    Unknown,
    /// <summary>CLI erkannt, aber die erkannte oder nicht ermittelbare Version liegt außerhalb aller Bereiche.</summary>
    Unsupported,
    /// <summary>Promptformat und Version werden unterstützt.</summary>
    Supported,
}

/// <summary>Statische Sitzungsidentität: erkannter CLI-Typ und Versionszustand des gebundenen Laufs.
/// <see cref="CliName"/> <c>null</c> kodiert „kein Profil / unbekannte CLI“.</summary>
public sealed record ScmCliRecognition(ScmCliRecognitionState State, string? CliName, Version? Version, string? SupportedVersionsText)
{
    /// <summary>Zugriffsgrund des Startvertrags bei erkanntem Banner-Anker ohne gebundenen Antwortkanal
    /// (z. B. Simulator-Shell ohne SCM-Modus); <c>null</c>, solange Banner und Vertrag zusammenpassen
    /// oder der Anker noch nicht gesehen wurde.</summary>
    public string? ChannelReason { get; init; }
}

/// <summary>Anchor-Treffer eines Promptformat-Profils in der Startausgabe; <see cref="Version"/> bleibt
/// <c>null</c>, wenn das Token nicht zum dokumentierten Bannerformat passt.</summary>
public sealed record ScmCliPromptMatch(ScmCliPromptProfile Profile, Version? Version, bool IsSupported);

/// <summary>Katalog der unterstützten Promptformate je <see cref="PluginPrefix"/> mit Banner-Anker,
/// Versionsextraktion und zulässigen Versionsbereichen. Der Simulator-Vertrag ist unversioniert und
/// trägt deshalb weder <see cref="VersionPattern"/> noch <see cref="SupportedRanges"/>.</summary>
public sealed record ScmCliPromptProfile(string PluginPrefix, string BannerAnchor, Regex? VersionPattern,
    IReadOnlyList<ScmCliVersionRange> SupportedRanges, string DisplayName, string? SupportedVersionsText)
{
    private static readonly IReadOnlyDictionary<string, ScmCliPromptProfile> Profiles = new ScmCliPromptProfile[]
    {
        new("Softwareschmiede.Codex", "OpenAI Codex",
            new Regex(@"OpenAI Codex \(v(?<version>\d+\.\d+\.\d+)\)", RegexOptions.CultureInvariant),
            [new ScmCliVersionRange(new Version(0, 114, 0), new Version(0, 115, 0))], "Codex CLI", "v0.114.x"),
        new("Softwareschmiede.ClaudeCli", "Claude Code",
            new Regex(@"Claude Code v(?<version>\d+\.\d+\.\d+)(?=\s|$)", RegexOptions.CultureInvariant),
            [new ScmCliVersionRange(new Version(2, 1, 0), new Version(2, 2, 0))], "Claude Code", "v2.1.x"),
        new("Softwareschmiede.Devin", "Devin CLI",
            new Regex(@"Devin CLI\s+v(?<version>\d+\.\d+\.\d+)(?=\s|$)", RegexOptions.CultureInvariant),
            [new ScmCliVersionRange(new Version(3000, 11, 0), new Version(3000, 12, 0))], "Devin CLI", "v3000.11.x"),
        new("Softwareschmiede.KiSimulator", "SCM_SIMULATOR_PROTOCOL_V1", null, [], "KI-Simulator", null),
    }.ToDictionary(profile => profile.PluginPrefix, StringComparer.Ordinal);

    /// <summary>Liefert das Promptformat-Profil eines PluginPrefix, sofern eines dokumentiert ist.</summary>
    public static bool TryGet(string? pluginPrefix, [NotNullWhen(true)] out ScmCliPromptProfile? profile)
    {
        if (pluginPrefix is null) { profile = null; return false; }
        return Profiles.TryGetValue(pluginPrefix, out profile);
    }

    /// <summary>Sucht den Banner-Anker des Profils in der Startausgabe und extrahiert die Version.
    /// Kein Profil oder kein Anker liefert <c>null</c> — der Erkennungszustand bleibt unverändert,
    /// weil das Banner noch erscheinen kann. Nur das dokumentierte Bannerformat wird toleriert:
    /// Suffixe oder fehlende Segmente gelten als nicht ermittelbar, nicht als geraten.</summary>
    public static ScmCliPromptMatch? TryMatch(string? pluginPrefix, string text)
    {
        if (!TryGet(pluginPrefix, out var profile) || !text.Contains(profile.BannerAnchor, StringComparison.Ordinal))
            return null;
        if (profile.VersionPattern is null) return new(profile, null, true);
        var match = profile.VersionPattern.Match(text);
        if (!match.Success || !Version.TryParse(match.Groups["version"].Value, out var version))
            return new(profile, null, false);
        return new(profile, version, profile.SupportedRanges.Any(range => range.Contains(version)));
    }
}
