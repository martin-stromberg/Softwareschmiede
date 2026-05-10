namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>
/// Gruppiert mehrere <see cref="PluginSettingField"/>-Einträge unter einem gemeinsamen Anzeigenamen.
/// </summary>
/// <param name="GroupName">Anzeigename der Gruppe (z.B. "Authentifizierung").</param>
/// <param name="Fields">Felder dieser Gruppe.</param>
public sealed record PluginSettingGroup(
    string GroupName,
    IReadOnlyList<PluginSettingField> Fields);
