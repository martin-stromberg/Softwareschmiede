namespace Softwareschmiede.App.ViewModels;

/// <summary>Anzeigemodus des Dateiexplorers in der Aufgabendetailansicht.</summary>
public enum DateibrowserAnsichtsmodus
{
    /// <summary>Zeigt den vollständigen Arbeitsbaum des geklonten Repositories.</summary>
    Standard,

    /// <summary>Zeigt nur im Branch geänderte Dateien, gruppiert nach Commits.</summary>
    Vergleich
}
