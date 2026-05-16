using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Application.Services;

/// <summary>Reserviert lokale Ports, damit sie nicht zwischen Prüfung und Start kollidieren.</summary>
public sealed class PortReservationService
{
    private readonly ILogger<PortReservationService> _logger;

    /// <inheritdoc cref="PortReservationService"/>
    public PortReservationService(ILogger<PortReservationService> logger)
    {
        _logger = logger;
    }

    /// <summary>Reserviert einen freien Port gemäß Portmodus.</summary>
    public Task<PortReservation> ReserveAsync(
        RepositoryStartPortModus portModus,
        int? portBereichVon,
        int? portBereichBis,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        return Task.FromResult(ReserveInternal(portModus, portBereichVon, portBereichBis));
    }

    private PortReservation ReserveInternal(RepositoryStartPortModus portModus, int? portBereichVon, int? portBereichBis)
    {
        if (portModus == RepositoryStartPortModus.Fest)
        {
            if (portBereichVon is null)
            {
                throw new InvalidOperationException("Für den festen Portmodus ist ein Port erforderlich.");
            }

            return ReserveSpecific(portBereichVon.Value);
        }

        if (portBereichVon.HasValue || portBereichBis.HasValue)
        {
            if (!portBereichVon.HasValue || !portBereichBis.HasValue)
            {
                throw new InvalidOperationException("Ein Portbereich muss beide Grenzen enthalten.");
            }

            if (portBereichVon > portBereichBis)
            {
                throw new InvalidOperationException("Der Portbereich ist ungültig.");
            }

            return ReserveFromRange(portBereichVon.Value, portBereichBis.Value);
        }

        return ReserveEphemeral();
    }

    private PortReservation ReserveSpecific(int port)
    {
        ValidatePort(port);
        return TryReserve(port) ?? throw new InvalidOperationException($"Der Port {port} ist bereits belegt.");
    }

    private PortReservation ReserveFromRange(int from, int to)
    {
        ValidatePort(from);
        ValidatePort(to);

        for (var port = from; port <= to; port++)
        {
            var reservation = TryReserve(port);
            if (reservation is not null)
            {
                return reservation;
            }
        }

        throw new InvalidOperationException($"Im Portbereich {from}-{to} konnte kein freier Port reserviert werden.");
    }

    private PortReservation ReserveEphemeral()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        _logger.LogInformation("Freien Port reserviert: {Port}", port);
        return new PortReservation(port, listener);
    }

    private PortReservation? TryReserve(int port)
    {
        try
        {
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            _logger.LogInformation("Freien Port reserviert: {Port}", port);
            return new PortReservation(port, listener);
        }
        catch (SocketException)
        {
            return null;
        }
    }

    private static void ValidatePort(int port)
    {
        if (port is < IPEndPoint.MinPort or > IPEndPoint.MaxPort)
        {
            throw new InvalidOperationException($"Port {port} ist ungültig.");
        }
    }
}

/// <summary>Hält eine Portreservierung bis zur Freigabe fest.</summary>
public sealed class PortReservation : IDisposable
{
    private readonly TcpListener _listener;

    /// <summary>Der reservierte Port.</summary>
    public int Port { get; }

    /// <summary>Erstellt eine neue Portreservierung.</summary>
    public PortReservation(int port, TcpListener listener)
    {
        Port = port;
        _listener = listener;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _listener.Stop();
    }
}
