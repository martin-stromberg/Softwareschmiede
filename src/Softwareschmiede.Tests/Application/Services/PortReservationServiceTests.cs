using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Tests.Application.Services;

public sealed class PortReservationServiceTests
{
    private readonly PortReservationService _sut = new(NullLogger<PortReservationService>.Instance);

    [Fact]
    public async Task ReserveAsync_ShouldReserveEphemeralPort_WhenNoRangeIsProvided()
    {
        using var reservation = await _sut.ReserveAsync(RepositoryStartPortModus.Auto, null, null);

        reservation.Port.Should().BeGreaterThan(0);

        var act = () =>
        {
            using var conflictingListener = new TcpListener(IPAddress.Loopback, reservation.Port);
            conflictingListener.Start();
        };

        act.Should().Throw<SocketException>();
    }

    [Fact]
    public async Task ReserveAsync_ShouldThrow_WhenFestModeHasNoPort()
    {
        var act = () => _sut.ReserveAsync(RepositoryStartPortModus.Fest, null, null);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Port erforderlich*");
    }

    [Fact]
    public async Task ReserveAsync_ShouldReserveRequestedPort_WhenFestModePortIsFree()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var freePort = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();

        using var reservation = await _sut.ReserveAsync(RepositoryStartPortModus.Fest, freePort, freePort);

        reservation.Port.Should().Be(freePort);
    }

    [Fact]
    public async Task ReserveAsync_ShouldThrow_WhenRequestedFestPortIsAlreadyInUse()
    {
        using var occupiedListener = new TcpListener(IPAddress.Loopback, 0);
        occupiedListener.Start();
        var occupiedPort = ((IPEndPoint)occupiedListener.LocalEndpoint).Port;

        var act = () => _sut.ReserveAsync(RepositoryStartPortModus.Fest, occupiedPort, occupiedPort);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{occupiedPort}*belegt*");
    }

    [Fact]
    public async Task ReserveAsync_ShouldReserveSecondPortFromRange_WhenFirstPortIsOccupied()
    {
        using var occupiedListener = new TcpListener(IPAddress.Loopback, 0);
        occupiedListener.Start();
        var firstPort = ((IPEndPoint)occupiedListener.LocalEndpoint).Port;
        var secondPort = FindFreePortInRange(firstPort + 1, firstPort + 30);

        using var reservation = await _sut.ReserveAsync(RepositoryStartPortModus.Auto, firstPort, secondPort);

        reservation.Port.Should().Be(secondPort);
    }

    [Fact]
    public async Task ReserveAsync_ShouldThrow_WhenPortIsOutsideValidRange()
    {
        var act = () => _sut.ReserveAsync(RepositoryStartPortModus.Fest, 70000, 70000);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ungültig*");
    }

    [Fact]
    public async Task ReserveAsync_ShouldThrow_WhenRangeIsInvalid()
    {
        var act = () => _sut.ReserveAsync(RepositoryStartPortModus.Auto, 6000, 5000);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Portbereich ist ungültig*");
    }

    [Fact]
    public async Task ReserveAsync_ShouldThrow_WhenCancellationWasRequested()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => _sut.ReserveAsync(RepositoryStartPortModus.Auto, null, null, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static int FindFreePortInRange(int from, int to)
    {
        for (var port = from; port <= to; port++)
        {
            try
            {
                using var probe = new TcpListener(IPAddress.Loopback, port);
                probe.Start();
                return port;
            }
            catch (SocketException)
            {
                // try next
            }
        }

        throw new InvalidOperationException($"No free port found in range {from}-{to}.");
    }
}
