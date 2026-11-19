namespace Softwareschmiede.Tests.Infrastructure.Testing;

/// <summary>
/// Prozessweites Lock für alle Testzugriffe auf die Windows-Zwischenablage. Die Zwischenablage
/// ist eine systemweite Singleton-Ressource ohne Locking-Schutz: parallele xUnit-Collections
/// (z. B. <c>TerminalControlTests</c> mit echten <c>System.Windows.Clipboard</c>-Aufrufen und
/// <c>End2EndTest</c>, der eine echte App-Instanz mit Strg+Umschalt+C-Gesten fährt) konkurrieren
/// um <c>OpenClipboard</c> und erzeugen reproduzierbare <c>CLIPBRD_E_CANT_OPEN</c>-Fehler.
/// Auch die von einem E2E-Test gestartete App ist ein externer Konkurrent — deshalb muss das
/// Lock die komplette Interaktionsregion abdecken (Vorbereiten, die auslösende App-Geste und
/// das abschließende Lesen), nicht nur einzelne Clipboard-Aufrufe; Retries allein reichen nicht.
/// </summary>
public static class ClipboardTestLock
{
    private static readonly SemaphoreSlim Semaphore = new(1, 1);

    /// <summary>Belegt das Clipboard-Lock synchron und gibt es beim Disposen wieder frei.</summary>
    /// <returns>Ein Disposable, dessen <see cref="IDisposable.Dispose"/> das Lock freigibt.</returns>
    public static IDisposable Halten()
    {
        Semaphore.Wait();
        return new Releaser();
    }

    private sealed class Releaser : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
                Semaphore.Release();
        }
    }
}
