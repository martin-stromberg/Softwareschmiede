namespace Softwareschmiede.Domain.Exceptions;

/// <summary>Gemeinsame Basisklasse aller domänenspezifischen Ausnahmen der Softwareschmiede.</summary>
public abstract class SoftwareschmiedeException : Exception
{
    /// <inheritdoc cref="SoftwareschmiedeException"/>
    protected SoftwareschmiedeException(string message)
        : base(message)
    {
    }

    /// <inheritdoc cref="SoftwareschmiedeException"/>
    protected SoftwareschmiedeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
