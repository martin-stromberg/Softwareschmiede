namespace Softwareschmiede.Domain.Interfaces;

/// <summary>Verwaltet Discovery und Zugriff auf geladene Plugins.</summary>
public interface IPluginManager
{
    /// <summary>Gibt alle geladenen SCM-Plugins zurück.</summary>
    IReadOnlyList<IGitPlugin> GetSourceCodeManagementPlugins();

    /// <summary>Gibt alle geladenen Development-Automation-Plugins zurück.</summary>
    IReadOnlyList<IKiPlugin> GetDevelopmentAutomationPlugins();

    /// <summary>Gibt das erste verfügbare SCM-Plugin zurück.</summary>
    IGitPlugin GetDefaultSourceCodeManagementPlugin();

    /// <summary>Gibt das priorisierte Development-Automation-Plugin zurück.</summary>
    IKiPlugin GetDefaultDevelopmentAutomationPlugin();

    /// <summary>Gibt alle geladenen IDE-Plugins zurück.</summary>
    IReadOnlyList<IIdePlugin> GetIdePlugins();

    /// <summary>Gibt das erste verfügbare IDE-Plugin zurück.</summary>
    IIdePlugin GetDefaultIdePlugin();
}
