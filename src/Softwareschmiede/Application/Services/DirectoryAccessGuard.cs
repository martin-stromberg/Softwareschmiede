using Softwareschmiede.Domain.Exceptions;

namespace Softwareschmiede.Application.Services;

/// <summary>Gemeinsame Hilfsmethode, um Datei-/Verzeichnisoperationen gegen <see cref="IOException"/> und <see cref="UnauthorizedAccessException"/> abzusichern und als <see cref="DirectoryAccessException"/> weiterzuwerfen.</summary>
internal static class DirectoryAccessGuard
{
    /// <summary>Führt <paramref name="action"/> aus und wandelt <see cref="IOException"/>/<see cref="UnauthorizedAccessException"/> in eine <see cref="DirectoryAccessException"/> für <paramref name="path"/> um.</summary>
    public static async Task ExecuteAsync(string path, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new DirectoryAccessException(path, ex);
        }
    }
}
