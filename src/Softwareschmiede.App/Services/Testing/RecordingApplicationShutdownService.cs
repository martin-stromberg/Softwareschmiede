using Softwareschmiede.Application.Services.Updates;

namespace Softwareschmiede.App.Services.Testing;

/// <summary>
/// E2E-Testadapter für <see cref="IApplicationShutdownService"/>: protokolliert den
/// <see cref="UpdateE2EEreignisse.ShutdownRequested"/>-Aufruf und lässt die Test-App geöffnet,
/// damit der Runner das eigene Testfenster danach regulär schließen kann.
/// </summary>
public sealed class RecordingApplicationShutdownService : IApplicationShutdownService
{
    private readonly UpdateE2ETestKontext _kontext;

    /// <inheritdoc cref="RecordingApplicationShutdownService"/>
    /// <param name="kontext">Der Laufzeitkontext der Update-E2E-Umgebung.</param>
    public RecordingApplicationShutdownService(UpdateE2ETestKontext kontext)
    {
        _kontext = kontext;
    }

    /// <inheritdoc/>
    public void Shutdown()
    {
        _kontext.Protokoll.Schreibe(UpdateE2EEreignisse.ShutdownRequested);
    }
}
