namespace Softwareschmiede.Domain.Options;

/// <summary>Technische Laufzeitoptionen des plugin-internen Auflösungscaches für
/// <see cref="Softwareschmiede.Domain.ValueObjects.ScmEventRepository"/>-Identitäten.
/// Gebunden an die Konfigurationssektion <c>ScmEvents</c> (kein UI-Schalter).</summary>
public sealed class ScmResolutionCacheOptions
{
    /// <summary>Untergrenze der akzeptierten Cache-Lebensdauer.</summary>
    public static readonly TimeSpan MinimumRepositoryResolutionCacheTtl = TimeSpan.FromMinutes(1);

    /// <summary>Obergrenze der akzeptierten Cache-Lebensdauer.</summary>
    public static readonly TimeSpan MaximumRepositoryResolutionCacheTtl = TimeSpan.FromHours(24);

    /// <summary>Standard-Lebensdauer eines gecachten Auflösungsergebnisses.</summary>
    public static readonly TimeSpan DefaultRepositoryResolutionCacheTtl = TimeSpan.FromMinutes(15);

    /// <summary>Lebensdauer eines erfolgreich aufgelösten <see cref="Softwareschmiede.Domain.ValueObjects.ScmEventRepository"/>
    /// im Plugin-Cache. Konfigurierte Werte außerhalb <c>[1 min, 24 h]</c> werden auf die Grenzen
    /// geklemmt (<see cref="EffectiveRepositoryResolutionCacheTtl"/>, angewendet bei der
    /// Optionsbindung); ungültige Werte (<c>&lt;= 0</c>) fallen auf den Default von 15 Minuten zurück.</summary>
    public TimeSpan RepositoryResolutionCacheTtl { get; set; } = DefaultRepositoryResolutionCacheTtl;

    /// <summary>Liefert die wirksame, auf die zulässigen Grenzen geklemmte Lebensdauer
    /// (Validierung konfigurierter Werte; direkt konstruierte Optionsobjekte — z. B. Test-Fixtures —
    /// können bewusst kürzere Laufzeiten übergeben).</summary>
    public TimeSpan EffectiveRepositoryResolutionCacheTtl()
        => RepositoryResolutionCacheTtl <= TimeSpan.Zero
            ? DefaultRepositoryResolutionCacheTtl
            : TimeSpan.FromTicks(Math.Clamp(RepositoryResolutionCacheTtl.Ticks,
                MinimumRepositoryResolutionCacheTtl.Ticks, MaximumRepositoryResolutionCacheTtl.Ticks));
}
