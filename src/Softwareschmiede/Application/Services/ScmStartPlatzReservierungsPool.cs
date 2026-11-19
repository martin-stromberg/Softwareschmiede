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
/// <see cref="ZulassungsGate"/> serialisiert den gesamten Check-und-Claim-Abschnitt
/// über die Belegungsabfrage hinweg; die Sperre um die Map selbst bleibt kurz und
/// non-blocking, damit die Freigabe in Dispose kein Warten erfordert.
/// </remarks>
public sealed class ScmStartPlatzReservierungsPool
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, StartPlatzReservierung> _reservierungen = new();

    /// <summary>Serialisiert die atomare Zulassungsprüfung samt Platzbelegung
    /// (Belegungsabfrage eingeschlossen) für alle Zulassungsversuche dieses Pools.</summary>
    internal SemaphoreSlim ZulassungsGate { get; } = new(1, 1);

    /// <summary>Legt eine neue Reservierung an und trägt sie in den Bestand ein.</summary>
    /// <param name="pluginPrefix">KI-Plugin-Prefix des reservierten Starts.</param>
    /// <param name="gueltigBisUtcMs">Verfallzeitpunkt (Unix-Millisekunden UTC).</param>
    /// <returns>Die eingetragene Reservierung; <see cref="IDisposable.Dispose"/> gibt den Platz frei.</returns>
    internal StartPlatzReservierung Reservieren(string pluginPrefix, long gueltigBisUtcMs)
    {
        var reservierung = new StartPlatzReservierung(this, pluginPrefix, gueltigBisUtcMs);
        lock (_sync)
            _reservierungen[reservierung.Id] = reservierung;
        return reservierung;
    }

    /// <summary>Liefert die noch gültigen Reservierungen; verfallene Einträge werden
    /// dabei aus dem Bestand entfernt.</summary>
    /// <param name="jetztUtcMs">Aktueller Zeitpunkt (Unix-Millisekunden UTC).</param>
    internal List<StartPlatzReservierung> AktiveReservierungen(long jetztUtcMs)
    {
        lock (_sync)
        {
            foreach (var id in _reservierungen.Where(x => x.Value.GueltigBisUtcMs <= jetztUtcMs).Select(x => x.Key).ToList())
                _reservierungen.Remove(id);
            return _reservierungen.Values.ToList();
        }
    }

    /// <summary>Reservierter Startplatz; die Freigabe über <see cref="Dispose"/> entfernt den
    /// Platz aus der Zählung, sobald die echte Laufzählung ihn übernommen hat oder der Start
    /// fehlgeschlagen ist. Die Freigabe ist bewusst non-blocking (kurzer Lock, kein Warten).</summary>
    internal sealed class StartPlatzReservierung(ScmStartPlatzReservierungsPool pool, string pluginPrefix, long gueltigBisUtcMs) : IDisposable
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string PluginPrefix { get; } = pluginPrefix;
        public long GueltigBisUtcMs { get; } = gueltigBisUtcMs;

        public void Dispose()
        {
            lock (pool._sync)
                pool._reservierungen.Remove(Id);
        }
    }
}
