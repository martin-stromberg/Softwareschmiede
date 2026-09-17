namespace Softwareschmiede.App.Services;

/// <summary>
/// Instrumentierung der Updateversuche des Hauptfensters für kontrollierte Ende-zu-Ende-Prüfungen.
/// Die produktive Implementierung ist ein No-op-fähiger Vertrag; konkrete Protokollierung stellt
/// ausschließlich die E2E-Testumgebung bereit.
/// </summary>
public interface IUpdateVersuchProtokoll
{
    /// <summary>Protokolliert die erreichte Fensterbereitschaft vor dem ersten Updateversuch.</summary>
    void ProtokolliereWindowReady();

    /// <summary>Meldet den Beginn eines Updateversuchs.</summary>
    /// <param name="art">Die Versuchsart (<c>startup</c>, <c>pruefen</c>, <c>starten</c>).</param>
    void BeginneVersuch(string art);

    /// <summary>Meldet den Abschluss eines Updateversuchs.</summary>
    /// <param name="art">Die Versuchsart.</param>
    /// <param name="updateVerfuegbar">Ob nach dem Versuch ein Updateangebot sichtbar ist.</param>
    /// <param name="hinweis">Der aktuell angezeigte Update-Hinweis, falls vorhanden.</param>
    void BeendeVersuch(string art, bool updateVerfuegbar, string? hinweis);
}
