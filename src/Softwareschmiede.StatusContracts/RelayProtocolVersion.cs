namespace Softwareschmiede.StatusContracts;

/// <summary>
/// Versionskonstante des Relay-Protokolls zwischen Anwendung und Status-Server.
/// Wird im <c>RegisterInstance</c>-Handshake geprüft; Server und App können sich
/// unabhängig voneinander aktualisieren, solange die Version übereinstimmt.
/// </summary>
public static class RelayProtocolVersion
{
    /// <summary>Die aktuell unterstützte Protokollversion.</summary>
    public const int Current = 1;
}
