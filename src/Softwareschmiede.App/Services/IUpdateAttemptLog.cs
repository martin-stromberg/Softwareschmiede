namespace Softwareschmiede.App.Services;

/// <summary>
/// Instrumentierung der Updateversuche des Hauptfensters für kontrollierte Ende-zu-Ende-Prüfungen.
/// Die produktive Implementierung ist ein No-op-fähiger Vertrag; konkrete Protokollierung stellt
/// ausschließlich die E2E-Testumgebung bereit.
/// </summary>
public interface IUpdateAttemptLog
{
    /// <summary>Protokolliert die erreichte Fensterbereitschaft vor dem ersten Updateversuch.</summary>
    void LogWindowReady();

    /// <summary>Meldet den Beginn eines Updateversuchs.</summary>
    /// <param name="kind">Die Versuchsart (<c>startup</c>, <c>pruefen</c>, <c>starten</c>).</param>
    void BeginAttempt(string kind);

    /// <summary>Meldet den Abschluss eines Updateversuchs.</summary>
    /// <param name="kind">Die Versuchsart.</param>
    /// <param name="updateAvailable">Ob nach dem Versuch ein Updateangebot sichtbar ist.</param>
    /// <param name="hint">Der aktuell angezeigte Update-Hinweis, falls vorhanden.</param>
    void EndAttempt(string kind, bool updateAvailable, string? hint);
}
