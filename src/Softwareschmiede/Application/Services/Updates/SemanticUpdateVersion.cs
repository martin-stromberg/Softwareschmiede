using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Softwareschmiede.Application.Services.Updates;

/// <summary>
/// Vollständige semantische Version (SemVer 2.0) für Update-Vergleiche: Kernversion <c>X.Y.Z</c>,
/// geordnete Prerelease-Identifier und optionale Build-Metadaten. Build-Metadaten haben keinen
/// Einfluss auf Rangfolge und Gleichheit.
/// </summary>
public sealed class SemanticUpdateVersion : IComparable<SemanticUpdateVersion>, IEquatable<SemanticUpdateVersion>
{
    private SemanticUpdateVersion(int major, int minor, int patch, IReadOnlyList<string> prereleaseIdentifiers, string? buildMetadata)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        PrereleaseIdentifiers = prereleaseIdentifiers;
        BuildMetadata = buildMetadata;
    }

    /// <summary>Major-Komponente der Kernversion.</summary>
    public int Major { get; }

    /// <summary>Minor-Komponente der Kernversion.</summary>
    public int Minor { get; }

    /// <summary>Patch-Komponente der Kernversion.</summary>
    public int Patch { get; }

    /// <summary>Geordnete Prerelease-Identifier; leer bei stabilen Versionen.</summary>
    public IReadOnlyList<string> PrereleaseIdentifiers { get; }

    /// <summary>Optionale Build-Metadaten ohne führendes <c>+</c>; ohne Einfluss auf die Rangfolge.</summary>
    public string? BuildMetadata { get; }

    /// <summary>Gibt an, ob die Version ein SemVer-Prerelease-Suffix trägt.</summary>
    public bool IsPrerelease => PrereleaseIdentifiers.Count > 0;

    /// <summary>
    /// Versucht, einen strengen SemVer-String der Form <c>X.Y.Z[-prerelease][+metadaten]</c> zu parsen.
    /// </summary>
    /// <param name="value">Der zu parsende Versionsstring.</param>
    /// <param name="version">Die geparste Version, wenn erfolgreich.</param>
    /// <returns><see langword="true"/>, wenn der String eine gültige vollständige SemVer-Version ist.</returns>
    public static bool TryParse(string? value, [NotNullWhen(true)] out SemanticUpdateVersion? version)
    {
        version = null;
        if (string.IsNullOrEmpty(value))
            return false;

        var core = value;
        string? buildMetadata = null;
        var plusIndex = value.IndexOf('+');
        if (plusIndex >= 0)
        {
            buildMetadata = value[(plusIndex + 1)..];
            core = value[..plusIndex];
            if (!IsValidIdentifierList(buildMetadata, numericIdentifiersNeedNoLeadingZero: false))
                return false;
        }

        var prereleaseIdentifiers = Array.Empty<string>();
        var hyphenIndex = core.IndexOf('-');
        if (hyphenIndex >= 0)
        {
            var prerelease = core[(hyphenIndex + 1)..];
            core = core[..hyphenIndex];
            if (!IsValidIdentifierList(prerelease, numericIdentifiersNeedNoLeadingZero: true))
                return false;

            prereleaseIdentifiers = prerelease.Split('.');
        }

        var coreParts = core.Split('.');
        if (coreParts.Length != 3
            || !TryParseCoreNumber(coreParts[0], out var major)
            || !TryParseCoreNumber(coreParts[1], out var minor)
            || !TryParseCoreNumber(coreParts[2], out var patch))
        {
            return false;
        }

        version = new SemanticUpdateVersion(major, minor, patch, prereleaseIdentifiers, buildMetadata);
        return true;
    }

    /// <summary>
    /// Vergleicht nach SemVer-Präzedenz: erst Kernversion, dann Prerelease-Identifier
    /// (numerische Identifier numerisch und kleiner als nichtnumerische, nichtnumerische ordinal
    /// und case-sensitiv, kürzere identische Identifierfolge kleiner als deren Verlängerung;
    /// stabil höher als eigene Prereleases). Build-Metadaten bleiben ohne Einfluss.
    /// </summary>
    public int CompareTo(SemanticUpdateVersion? other)
    {
        if (other is null)
            return 1;

        var result = Major.CompareTo(other.Major);
        if (result != 0)
            return result;

        result = Minor.CompareTo(other.Minor);
        if (result != 0)
            return result;

        result = Patch.CompareTo(other.Patch);
        if (result != 0)
            return result;

        if (PrereleaseIdentifiers.Count == 0)
            return other.PrereleaseIdentifiers.Count == 0 ? 0 : 1;

        if (other.PrereleaseIdentifiers.Count == 0)
            return -1;

        var sharedCount = Math.Min(PrereleaseIdentifiers.Count, other.PrereleaseIdentifiers.Count);
        for (var i = 0; i < sharedCount; i++)
        {
            result = ComparePrereleaseIdentifier(PrereleaseIdentifiers[i], other.PrereleaseIdentifiers[i]);
            if (result != 0)
                return result;
        }

        return PrereleaseIdentifiers.Count.CompareTo(other.PrereleaseIdentifiers.Count);
    }

    /// <summary>Präzedenzbasierte Gleichheit; Build-Metadaten bleiben ohne Einfluss.</summary>
    public bool Equals(SemanticUpdateVersion? other) => other is not null && CompareTo(other) == 0;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is SemanticUpdateVersion other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Major);
        hash.Add(Minor);
        hash.Add(Patch);
        foreach (var identifier in PrereleaseIdentifiers)
            hash.Add(identifier, StringComparer.Ordinal);

        return hash.ToHashCode();
    }

    /// <summary>Gibt die kanonische Form <c>X.Y.Z[-prerelease][+metadaten]</c> zurück.</summary>
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(Major).Append('.').Append(Minor).Append('.').Append(Patch);
        if (PrereleaseIdentifiers.Count > 0)
            builder.Append('-').AppendJoin('.', PrereleaseIdentifiers);
        if (BuildMetadata is not null)
            builder.Append('+').Append(BuildMetadata);

        return builder.ToString();
    }

    private static int ComparePrereleaseIdentifier(string left, string right)
    {
        var leftNumeric = IsAllDigits(left);
        var rightNumeric = IsAllDigits(right);

        if (leftNumeric && rightNumeric)
        {
            // Ohne führende Nullen entspricht die längere Ziffernfolge dem größeren numerischen Wert.
            var lengthCompare = left.Length.CompareTo(right.Length);
            return lengthCompare != 0 ? lengthCompare : string.CompareOrdinal(left, right);
        }

        if (leftNumeric)
            return -1;

        if (rightNumeric)
            return 1;

        return string.CompareOrdinal(left, right);
    }

    private static bool TryParseCoreNumber(string part, out int number)
    {
        number = 0;
        if (part.Length == 0 || !IsAllDigits(part))
            return false;

        if (part.Length > 1 && part[0] == '0')
            return false;

        return int.TryParse(part, out number);
    }

    private static bool IsValidIdentifierList(string value, bool numericIdentifiersNeedNoLeadingZero)
    {
        if (value.Length == 0)
            return false;

        foreach (var identifier in value.Split('.'))
        {
            if (identifier.Length == 0)
                return false;

            var numeric = true;
            foreach (var c in identifier)
            {
                if (c is (>= '0' and <= '9') or (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '-')
                {
                    numeric &= c is >= '0' and <= '9';
                }
                else
                {
                    return false;
                }
            }

            if (numericIdentifiersNeedNoLeadingZero && numeric && identifier.Length > 1 && identifier[0] == '0')
                return false;
        }

        return true;
    }

    private static bool IsAllDigits(string value)
    {
        foreach (var c in value)
        {
            if (c is < '0' or > '9')
                return false;
        }

        return value.Length > 0;
    }
}
