namespace Softwareschmiede.Domain.Exceptions;

/// <summary>Wird ausgelöst, wenn ein Arbeitsverzeichnis nicht erstellt oder darauf nicht zugegriffen werden kann.</summary>
public sealed class DirectoryAccessException : Exception
{
    /// <summary>Erstellt eine neue Instanz der <see cref="DirectoryAccessException"/>.</summary>
    /// <param name="pfad">Pfad des betroffenen Verzeichnisses.</param>
    /// <param name="innerException">Ursprüngliche Ausnahme.</param>
    public DirectoryAccessException(string pfad, Exception innerException)
        : base($"Verzeichnis '{pfad}' konnte nicht erstellt werden oder ist nicht zugänglich.", innerException)
    {
        Pfad = pfad;
    }

    /// <summary>Pfad des betroffenen Verzeichnisses.</summary>
    public string Pfad { get; }
}
