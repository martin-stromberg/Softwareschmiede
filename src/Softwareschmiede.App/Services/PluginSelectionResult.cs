namespace Softwareschmiede.App.Services;

/// <summary>Ergebnis des Plugin-Auswahl-Dialogs.</summary>
/// <param name="SelectedPluginPrefix">Vom Benutzer gewähltes Plugin-Prefix, oder null bei Abbruch.</param>
/// <param name="SaveAsProjectDefault">Gibt an, ob das Plugin als Projekt-Standard gespeichert werden soll.</param>
public sealed record PluginSelectionResult(string? SelectedPluginPrefix, bool SaveAsProjectDefault);
