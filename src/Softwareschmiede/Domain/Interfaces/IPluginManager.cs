namespace Softwareschmiede.Domain.Interfaces;

/// <summary>Verwaltet Discovery und Zugriff auf geladene Plugins.</summary>
public interface IPluginManager
{
    IReadOnlyList<IGitPlugin> GetSourceCodeManagementPlugins();
    IReadOnlyList<IKiPlugin> GetDevelopmentAutomationPlugins();
    IGitPlugin GetDefaultSourceCodeManagementPlugin();
    IKiPlugin GetDefaultDevelopmentAutomationPlugin();
}
