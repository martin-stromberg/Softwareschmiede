using System.Diagnostics.CodeAnalysis;

namespace Softwareschmiede.Application.Services.Updates;

/// <summary>Vergleicht semantische Versionswerte (SemVer 2.0) mit optionalem führendem <c>v</c>.</summary>
public static class UpdateVersionComparer
{
    /// <summary>
    /// Versucht, einen Versionsstring in eine <see cref="SemanticUpdateVersion"/> umzuwandeln.
    /// Leerzeichen am Rand und ein führendes <c>v</c>/<c>V</c> werden toleriert; der Rest muss
    /// eine vollständige SemVer-Version <c>X.Y.Z[-prerelease][+metadaten]</c> sein.
    /// </summary>
    public static bool TryParse(string? value, [NotNullWhen(true)] out SemanticUpdateVersion? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
            normalized = normalized[1..];

        return SemanticUpdateVersion.TryParse(normalized, out version);
    }

    /// <summary>Gibt zurück, ob <paramref name="candidateVersion"/> nach SemVer-Präzedenz neuer als <paramref name="installedVersion"/> ist.</summary>
    public static bool IsNewer(string installedVersion, string candidateVersion)
    {
        return TryParse(installedVersion, out var installed)
            && TryParse(candidateVersion, out var candidate)
            && candidate.CompareTo(installed) > 0;
    }

    /// <summary>
    /// Normalisiert eine gültige Version auf <c>X.Y.Z[-prerelease][+metadaten]</c> ohne Leerzeichen am Rand
    /// und ohne führendes <c>v</c>. Prerelease-Suffix und Build-Metadaten bleiben erhalten.
    /// </summary>
    /// <exception cref="FormatException">Die Versionsangabe ist keine gültige vollständige SemVer-Version.</exception>
    public static string Normalize(string value)
    {
        if (!TryParse(value, out var version))
            throw new FormatException($"Ungültige Versionsangabe: {value}");

        return version.ToString();
    }
}
