using System.Text.Json;

namespace Softwareschmiede.Tests.E2E;

/// <summary>Laufzeit-Fixture-Ergebnisse werden erst konsumiert, nachdem ihr Writer die Datei freigegeben hat.</summary>
public sealed class ScmE2EFixtureTests_RuntimeResult
{
    /// <summary>Ein sichtbares, aber exklusiv gesperrtes Ergebnis wird wiederholt, bevor die vollständige Lauf-ID zurückgegeben wird.</summary>
    [Fact]
    public async Task RuntimeResultAsync_WriterStillOwnsResult_WaitsForCompletePublication()
    {
        var path = Path.Combine(Path.GetTempPath(), "scm-runtime-" + Guid.NewGuid().ToString("N") + ".command");
        var runId = Guid.NewGuid();
        Task<Guid>? result = null;
        try
        {
            await using (var writer = new FileStream(path + ".result", FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(writer, new { runId }, cancellationToken: TestContext.Current.CancellationToken);
                await writer.FlushAsync(TestContext.Current.CancellationToken);
                result = ScmE2EFixture.RuntimeResultAsync(path);
                await Task.Delay(100, TestContext.Current.CancellationToken);
                Assert.False(result.IsCompleted, "The reader must wait while publication is in progress.");
            }
            Assert.Equal(runId, await result.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        }
        finally
        {
            if (result is not null) { try { await result; } catch (IOException) { } }
            File.Delete(path + ".result");
        }
    }
}
