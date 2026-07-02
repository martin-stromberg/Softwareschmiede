namespace Softwareschmiede.Domain.Enums;

/// <summary>Erweiterungsmethoden für <see cref="AufgabeStatus"/>.</summary>
public static class AufgabeStatusExtensions
{
    /// <summary>Die Stati, die eine Aufgabe als aktiv (laufend) oder wartend kennzeichnen.</summary>
    public static readonly AufgabeStatus[] AktivOderWartendStatus = [AufgabeStatus.Gestartet, AufgabeStatus.Wartend];

    /// <summary>Gibt an, ob der Status eine aktive oder wartende Aufgabe kennzeichnet (Gestartet oder Wartend).</summary>
    /// <param name="status">Der zu prüfende Status.</param>
    /// <returns><c>true</c>, wenn der Status <see cref="AufgabeStatus.Gestartet"/> oder <see cref="AufgabeStatus.Wartend"/> ist; andernfalls <c>false</c>.</returns>
    public static bool IstAktivOderWartend(this AufgabeStatus status)
        => AktivOderWartendStatus.Contains(status);
}
