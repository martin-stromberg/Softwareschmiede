using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.App.Services;

/// <summary>Ergebnis des Dialogs zur Issue-Anlage.</summary>
public sealed record IssueCreateDialogResult(Issue Issue, bool UpdateTaskDescription, string? LocalBody);
