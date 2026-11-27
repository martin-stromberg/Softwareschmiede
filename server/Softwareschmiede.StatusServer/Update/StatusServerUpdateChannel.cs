namespace Softwareschmiede.StatusServer.Update;

/// <summary>Der Release-Kanal, aus dem der Status-Server Updates bezieht
/// (persistiert als <c>stable</c>/<c>prerelease</c> in der
/// <c>ServerSettings</c>-Tabelle).</summary>
public enum StatusServerUpdateChannel
{
    /// <summary>Nur stabile Releases (<c>vX.Y.Z</c>).</summary>
    Stable,

    /// <summary>Stabile Releases und Vorabversionen (<c>vX.Y.Z-rc.N</c>,
    /// GitHub-<c>prerelease</c>-Flag).</summary>
    ReleaseCandidate,
}
