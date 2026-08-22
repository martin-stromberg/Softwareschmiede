using Softwareschmiede.Domain.Exceptions;

namespace Softwareschmiede.Application.Services;

/// <summary>Gemeinsame Hilfsmethode, um Datei-/Verzeichnisoperationen gegen <see cref="IOException"/> und <see cref="UnauthorizedAccessException"/> abzusichern und als <see cref="DirectoryAccessException"/> weiterzuwerfen.</summary>
internal static class DirectoryAccessGuard
{
    /// <summary>Führt <paramref name="aktion"/> aus und wandelt <see cref="IOException"/>/<see cref="UnauthorizedAccessException"/> in eine <see cref="DirectoryAccessException"/> für <paramref name="pfad"/> um.</summary>
    public static async Task AusfuehrenAsync(string pfad, Func<Task> aktion)
    {
        try
        {
            await aktion();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new DirectoryAccessException(pfad, ex);
        }
    }
}
