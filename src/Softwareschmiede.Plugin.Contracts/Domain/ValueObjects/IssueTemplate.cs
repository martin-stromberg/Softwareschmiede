namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Provider-Issue-Template als editierbare Textgrundlage.</summary>
/// <param name="Name">Anzeigename des Templates.</param>
/// <param name="Body">Template-Inhalt.</param>
public sealed record IssueTemplate(string Name, string Body);
