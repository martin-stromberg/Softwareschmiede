using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.Application.Services;

/// <summary>Ordnet ein lokales Projekt-Repository seinem SCM-Plugin zu.</summary>
internal static class ScmPluginMatcher
{
    /// <summary>Prüft, ob das Repository zum Plugin gehört — über Plugin-Prefix oder Anzeigename.</summary>
    internal static bool MatchesPlugin(IGitPlugin plugin, GitRepository repository)
        => string.Equals(plugin.PluginPrefix, repository.PluginTyp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(plugin.PluginName, repository.PluginTyp, StringComparison.OrdinalIgnoreCase);
}
