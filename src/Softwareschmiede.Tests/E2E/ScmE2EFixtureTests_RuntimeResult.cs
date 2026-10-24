using System.Text.Json;

namespace Softwareschmiede.Tests.E2E;

/// <summary>Runtime fixture results are only consumed after their writer releases the file.</summary>
public sealed class ScmE2EFixtureTests_RuntimeResult
{
    /// <summary>A visible but exclusively locked result is retried before returning the complete run identifier.</summary>
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
                await JsonSerializer.SerializeAsync(writer, new { runId });
                await writer.FlushAsync();
                result = ScmE2EFixture.RuntimeResultAsync(path);
                await Task.Delay(100);
                Assert.False(result.IsCompleted, "The reader must wait while publication is in progress.");
            }
            Assert.Equal(runId, await result.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            if (result is not null) { try { await result; } catch (IOException) { } }
            File.Delete(path + ".result");
        }
    }
}
