namespace Softwareschmiede.Domain.Enums;

/// <summary>Wird ausgelöst, wenn ein ungültiger Statusübergang versucht wird.</summary>
public sealed class InvalidStatusTransitionException : InvalidOperationException
{
    /// <summary>Erstellt eine neue Instanz der <see cref="InvalidStatusTransitionException"/>.</summary>
    public InvalidStatusTransitionException(AufgabeStatus von, AufgabeStatus nach)
        : base($"Statusübergang von '{von}' nach '{nach}' ist nicht erlaubt.")
    {
        Von = von;
        Nach = nach;
    }

    /// <summary>Ausgangsstatus.</summary>
    public AufgabeStatus Von { get; }

    /// <summary>Zielstatus.</summary>
    public AufgabeStatus Nach { get; }
}
