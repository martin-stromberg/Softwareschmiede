using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Domain.Interfaces;

/// <summary>Optionale Fähigkeit für abgeglichene Antworten am gespeicherten Ereignisursprung.</summary>
public interface IScmEventResponder
{
    /// <summary>Prüft alle relevanten Seiten. Null bedeutet sicher nicht gefunden; Fehler werfen eine typisierte Ausnahme.</summary>
    Task<ScmResponseResult?> ReconcileResponseAsync(ScmResponseRequest request, CancellationToken ct = default);

    /// <summary>Gleicht vor dem Schreiben ab. Bei unbekanntem Schreibergebnis ist OutcomeUncertain gesetzt.</summary>
    Task<ScmResponseResult> ExecuteResponseAsync(ScmResponseRequest request, CancellationToken ct = default);
}
