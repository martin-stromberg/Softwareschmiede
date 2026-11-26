namespace Softwareschmiede.StatusContracts;

/// <summary>Handshake-Anfrage einer App-Instanz am Relay-Hub (<c>RegisterInstance</c>).</summary>
/// <param name="InstanceId">Die persistente, eindeutige Instanz-ID der sendenden Installation.</param>
/// <param name="InstanceName">Optionaler Anzeigename der Instanz für die Status-Seite.</param>
/// <param name="ProtocolVersion">Die vom Client verwendete Protokollversion (<see cref="RelayProtocolVersion.Current"/>).</param>
public sealed record RegisterInstanceRequest(
    string InstanceId,
    string? InstanceName,
    int ProtocolVersion);

/// <summary>Ergebnis des <c>RegisterInstance</c>-Handshakes.</summary>
/// <param name="Accepted"><see langword="true"/>, wenn die Instanz registriert wurde.</param>
/// <param name="RejectionReason">Ablehnungsgrund für die Anzeige in der App (bei <paramref name="Accepted"/> = <see langword="false"/>).</param>
public sealed record RegisterInstanceResult(bool Accepted, string? RejectionReason)
{
    /// <summary>Erfolgreiche Registrierung.</summary>
    public static RegisterInstanceResult Ok { get; } = new(true, null);

    /// <summary>Erzeugt ein Ablehnungsergebnis mit verständlichem Grund.</summary>
    /// <param name="reason">Der Ablehnungsgrund (z. B. Protokollversion inkompatibel).</param>
    /// <returns>Das Ablehnungsergebnis.</returns>
    public static RegisterInstanceResult Rejected(string reason) => new(false, reason);
}
