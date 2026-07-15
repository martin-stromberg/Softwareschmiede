using System.Text.RegularExpressions;

namespace Softwareschmiede.Application.Services.Updates;

/// <summary>Vergleicht stabile semantische Versionswerte mit optionalem führendem <c>v</c>.</summary>
public static partial class UpdateVersionComparer
{
    /// <summary>Versucht, einen Versionsstring in eine <see cref="Version"/> umzuwandeln.</summary>
    public static bool TryParse(string? value, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
            normalized = normalized[1..];

        var match = SemVerRegex().Match(normalized);
        if (!match.Success)
            return false;

        var versionText = $"{match.Groups["major"].Value}.{match.Groups["minor"].Value}.{match.Groups["patch"].Value}";
        return Version.TryParse(versionText, out version!);
    }

    /// <summary>Gibt zurück, ob <paramref name="candidateVersion"/> neuer als <paramref name="installedVersion"/> ist.</summary>
    public static bool IsNewer(string installedVersion, string candidateVersion)
    {
        return TryParse(installedVersion, out var installed)
            && TryParse(candidateVersion, out var candidate)
            && candidate.CompareTo(installed) > 0;
    }

    /// <summary>Normalisiert eine gültige Version auf <c>X.Y.Z</c>.</summary>
    public static string Normalize(string value)
    {
        if (!TryParse(value, out var version))
            throw new FormatException($"Ungültige Versionsangabe: {value}");

        return $"{version.Major}.{version.Minor}.{version.Build}";
    }

    [GeneratedRegex(@"^(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)(?:\+[0-9A-Za-z.-]+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex SemVerRegex();
}
