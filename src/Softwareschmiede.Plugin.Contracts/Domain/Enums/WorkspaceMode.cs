namespace Softwareschmiede.Domain.Enums;

/// <summary>Arbeitsmodus für lokale Verzeichnis-Workspaces.</summary>
public enum WorkspaceMode
{
    /// <summary>Arbeitet direkt im Quellverzeichnis.</summary>
    InSourceDirectory,

    /// <summary>Arbeitet in einer separaten Arbeitskopie.</summary>
    SeparateWorkingDirectory
}
