namespace Softwareschmiede.Domain.Interfaces;

using Softwareschmiede.Domain.ValueObjects;

/// <summary>Service zur Verwaltung von Agentenpaketen, Verzeichnissen und Dateien im Dateisystem.</summary>
public interface IAgentPackageFileService
{
    // Pakete
    /// <summary>Erstellt ein neues Agentenpaket (Verzeichnis).</summary>
    /// <param name="name">Name des neuen Pakets.</param>
    /// <param name="ct">Cancellation Token.</param>
    /// <returns>Die Informationen des neu erstellten Pakets.</returns>
    Task<AgentPackageInfo> CreatePackageAsync(string name, CancellationToken ct = default);

    /// <summary>Benennt ein Agentenpaket um.</summary>
    /// <param name="oldName">Aktueller Name des Pakets.</param>
    /// <param name="newName">Neuer Name des Pakets.</param>
    /// <param name="ct">Cancellation Token.</param>
    Task RenamePackageAsync(string oldName, string newName, CancellationToken ct = default);

    /// <summary>Löscht ein Agentenpaket inklusive aller Inhalte.</summary>
    /// <param name="name">Name des zu löschenden Pakets.</param>
    /// <param name="ct">Cancellation Token.</param>
    Task DeletePackageAsync(string name, CancellationToken ct = default);

    /// <summary>Erstellt den vollständigen Dateibaum eines Agentenpakets.</summary>
    /// <param name="packageName">Name des Pakets.</param>
    /// <param name="ct">Cancellation Token.</param>
    /// <returns>Der Root-Knoten des Dateibaums.</returns>
    Task<FileTreeNode> BuildPackageTreeAsync(string packageName, CancellationToken ct = default);

    // Verzeichnisse
    /// <summary>Erstellt ein neues Verzeichnis innerhalb eines Pakets.</summary>
    /// <param name="packageName">Name des Pakets.</param>
    /// <param name="relativePath">Pfad relativ zum Paket-Root (z.B. "skills" oder "skills/advanced").</param>
    /// <param name="ct">Cancellation Token.</param>
    Task CreateDirectoryAsync(string packageName, string relativePath, CancellationToken ct = default);

    /// <summary>Benennt ein Verzeichnis innerhalb eines Pakets um.</summary>
    /// <param name="packageName">Name des Pakets.</param>
    /// <param name="relativeOldPath">Aktueller Pfad relativ zum Paket-Root.</param>
    /// <param name="newName">Neuer Name (nur der letzte Pfadteil).</param>
    /// <param name="ct">Cancellation Token.</param>
    Task RenameDirectoryAsync(string packageName, string relativeOldPath, string newName, CancellationToken ct = default);

    /// <summary>Löscht ein Verzeichnis inkl. aller Inhalte.</summary>
    /// <param name="packageName">Name des Pakets.</param>
    /// <param name="relativePath">Pfad relativ zum Paket-Root.</param>
    /// <param name="ct">Cancellation Token.</param>
    Task DeleteDirectoryAsync(string packageName, string relativePath, CancellationToken ct = default);

    // Dateien
    /// <summary>Liest den Textinhalt einer Datei.</summary>
    /// <param name="packageName">Name des Pakets.</param>
    /// <param name="relativeFilePath">Pfad relativ zum Paket-Root.</param>
    /// <param name="ct">Cancellation Token.</param>
    /// <returns>Der Textinhalt der Datei.</returns>
    Task<string> ReadFileAsync(string packageName, string relativeFilePath, CancellationToken ct = default);

    /// <summary>Schreibt den Textinhalt einer Datei (überschreibt bestehenden Inhalt).</summary>
    /// <param name="packageName">Name des Pakets.</param>
    /// <param name="relativeFilePath">Pfad relativ zum Paket-Root.</param>
    /// <param name="content">Neuer Inhalt.</param>
    /// <param name="ct">Cancellation Token.</param>
    Task WriteFileAsync(string packageName, string relativeFilePath, string content, CancellationToken ct = default);

    /// <summary>Erstellt eine leere Datei.</summary>
    /// <param name="packageName">Name des Pakets.</param>
    /// <param name="relativeFilePath">Pfad relativ zum Paket-Root.</param>
    /// <param name="ct">Cancellation Token.</param>
    Task CreateEmptyFileAsync(string packageName, string relativeFilePath, CancellationToken ct = default);

    /// <summary>Lädt eine Datei in ein Paket-Verzeichnis hoch.</summary>
    /// <param name="packageName">Name des Pakets.</param>
    /// <param name="relativeDirectory">Zielverzeichnis relativ zum Paket-Root (leer für Root).</param>
    /// <param name="fileName">Dateiname.</param>
    /// <param name="content">Dateiinhalt als Stream.</param>
    /// <param name="ct">Cancellation Token.</param>
    Task UploadFileAsync(string packageName, string relativeDirectory, string fileName, Stream content, CancellationToken ct = default);

    /// <summary>Benennt eine Datei um.</summary>
    /// <param name="packageName">Name des Pakets.</param>
    /// <param name="relativeOldPath">Aktueller Pfad relativ zum Paket-Root.</param>
    /// <param name="newName">Neuer Dateiname (nur der letzte Pfadteil).</param>
    /// <param name="ct">Cancellation Token.</param>
    Task RenameFileAsync(string packageName, string relativeOldPath, string newName, CancellationToken ct = default);

    /// <summary>Löscht eine Datei.</summary>
    /// <param name="packageName">Name des Pakets.</param>
    /// <param name="relativeFilePath">Pfad relativ zum Paket-Root.</param>
    /// <param name="ct">Cancellation Token.</param>
    Task DeleteFileAsync(string packageName, string relativeFilePath, CancellationToken ct = default);
}
