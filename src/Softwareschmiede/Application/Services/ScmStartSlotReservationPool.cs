namespace Softwareschmiede.Application.Services;

/// <summary>
/// Bestand offener Startplatz-Reservierungen der SCM-Ausführungsbegrenzung.
/// </summary>
/// <remarks>
/// Der Bestand liegt in einem DI-Singleton statt in statischen Feldern des
/// <see cref="ScmEventExecutionLimitService"/>: Der Service selbst ist scoped, die
/// Zulassung muss aber über alle DI-Scopes und Dispatch-Pfade hinweg denselben
/// Platzbestand sehen. Pro Service-Provider gibt es genau einen Pool — produktiv
/// deckt das den ganzen Prozess ab, in Tests erhält jede Fixture ihren eigenen
/// Container und damit vollständig isolierte Reservierungen.
/// <see cref="AdmissionGate"/> serialisiert den gesamten Check-und-Claim-Abschnitt
/// über die Belegungsabfrage hinweg; die Sperre um die Map selbst bleibt kurz und
/// non-blocking, damit die Freigabe in Dispose kein Warten erfordert.
/// </remarks>
public sealed class ScmStartSlotReservationPool
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, StartSlotReservation> _reservations = new();

    /// <summary>Serialisiert die atomare Zulassungsprüfung samt Platzbelegung
    /// (Belegungsabfrage eingeschlossen) für alle Zulassungsversuche dieses Pools.</summary>
    internal SemaphoreSlim AdmissionGate { get; } = new(1, 1);

    /// <summary>Legt eine neue Reservierung an und trägt sie in den Bestand ein.</summary>
    /// <param name="pluginPrefix">KI-Plugin-Prefix des reservierten Starts.</param>
    /// <param name="validUntilUtcMs">Verfallzeitpunkt (Unix-Millisekunden UTC).</param>
    /// <returns>Die eingetragene Reservierung; <see cref="IDisposable.Dispose"/> gibt den Platz frei.</returns>
    internal StartSlotReservation Reserve(string pluginPrefix, long validUntilUtcMs)
    {
        var reservation = new StartSlotReservation(this, pluginPrefix, validUntilUtcMs);
        lock (_sync)
            _reservations[reservation.Id] = reservation;
        return reservation;
    }

    /// <summary>Liefert die noch gültigen Reservierungen; verfallene Einträge werden
    /// dabei aus dem Bestand entfernt.</summary>
    /// <param name="nowUtcMs">Aktueller Zeitpunkt (Unix-Millisekunden UTC).</param>
    internal List<StartSlotReservation> ActiveReservations(long nowUtcMs)
    {
        lock (_sync)
        {
            foreach (var id in _reservations.Where(x => x.Value.ValidUntilUtcMs <= nowUtcMs).Select(x => x.Key).ToList())
                _reservations.Remove(id);
            return _reservations.Values.ToList();
        }
    }

    /// <summary>Reservierter Startplatz; die Freigabe über <see cref="Dispose"/> entfernt den
    /// Platz aus der Zählung, sobald die echte Laufzählung ihn übernommen hat oder der Start
    /// fehlgeschlagen ist. Die Freigabe ist bewusst non-blocking (kurzer Lock, kein Warten).</summary>
    internal sealed class StartSlotReservation(ScmStartSlotReservationPool pool, string pluginPrefix, long validUntilUtcMs) : IDisposable
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string PluginPrefix { get; } = pluginPrefix;
        public long ValidUntilUtcMs { get; } = validUntilUtcMs;

        public void Dispose()
        {
            lock (pool._sync)
                pool._reservations.Remove(Id);
        }
    }
}
