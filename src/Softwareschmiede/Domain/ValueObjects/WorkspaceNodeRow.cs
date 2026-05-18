namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Zeile für die gerenderte Workspace-Liste.</summary>
/// <param name="Node">Der zugehörige Knoten.</param>
/// <param name="Depth">Einrückungstiefe im Baum.</param>
public sealed record WorkspaceNodeRow(WorkspaceFileNode Node, int Depth);
