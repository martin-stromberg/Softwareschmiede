using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Domain.Interfaces;

/// <summary>Optionale Fähigkeit eines SCM-Plugins, die jüngste wirksame Zuweisung des
/// Viewer-Kontos auf einer Referenz samt Zuweisendem zu liefern. Plugins ohne diese Fähigkeit
/// melden assign-ausgelöste Referenzen ehrlich als „Zuweisender nicht bestimmbar".</summary>
public interface IScmAssignmentSource
{
    /// <summary>Liefert die jüngste wirksame Zuweisung auf das Viewer-Konto der Referenz oder
    /// <see langword="null"/>, wenn keine besteht (kein wirksames Zuweisungsereignis oder eine
    /// zwischenzeitlich aufgehobene Zuweisung).</summary>
    Task<ScmAssignment?> GetLatestViewerAssignmentAsync(ScmEventReference reference, CancellationToken ct = default);
}
