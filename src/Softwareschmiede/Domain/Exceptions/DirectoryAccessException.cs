namespace Softwareschmiede.Domain.Exceptions;

/// <summary>Wird ausgelöst, wenn ein Arbeitsverzeichnis nicht erstellt oder darauf nicht zugegriffen werden kann.</summary>
public sealed class DirectoryAccessException : SoftwareschmiedeException
{
    /// <summary>Erstellt eine neue Instanz der <see cref="DirectoryAccessException"/>.</summary>
    /// <param name="path">Pfad des betroffenen Verzeichnisses.</param>
    /// <param name="innerException">Ursprüngliche Ausnahme.</param>
    public DirectoryAccessException(string path, Exception innerException)
        : base($"Verzeichnis '{path}' konnte nicht erstellt werden oder ist nicht zugänglich.", innerException)
    {
        DirectoryPath = path;
    }

    /// <summary>Pfad des betroffenen Verzeichnisses.</summary>
    public string DirectoryPath { get; }
}
