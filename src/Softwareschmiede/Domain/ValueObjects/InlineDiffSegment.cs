namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Repräsentiert einen Teilabschnitt einer modifizierten Diff-Zeile.</summary>
/// <param name="Text">Textinhalt des Segments.</param>
/// <param name="IsChanged">Gibt an, ob dieser Teilabschnitt gegenüber der Vergleichszeile geändert wurde.</param>
/// <returns>Ein neues <see cref="InlineDiffSegment"/>.</returns>
public sealed record InlineDiffSegment(string Text, bool IsChanged);
