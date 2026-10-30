using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Infrastructure.Plugins;

namespace Softwareschmiede.App.Services.Testing;

// Keeps all production source parsing, response reconciliation and simulator behavior.
internal sealed class ScmE2EPluginManager : IPluginManager
{
    private readonly PluginManager _inner;
    private readonly ScmE2ETestConfiguration _configuration;
    private readonly IGitPlugin _gitHub;

    public ScmE2EPluginManager(PluginManager inner, ScmE2ETestConfiguration configuration,
        ICliRunner cliRunner, ILogger<GitHubPlugin> logger)
    {
        _inner = inner;
        _configuration = configuration;
        _gitHub = new GitHubPlugin(new ScmE2EGitRunner(cliRunner, configuration), new EmptyCredentialStore(), logger, new ScmE2EApiClient(configuration));
    }

    public IReadOnlyList<IGitPlugin> GetSourceCodeManagementPlugins() => [_gitHub];
    public IReadOnlyList<IKiPlugin> GetDevelopmentAutomationPlugins()
        => _configuration.ReadScenario().PluginAvailable
            ? _inner.GetDevelopmentAutomationPlugins().Where(x => x.PluginPrefix == "Softwareschmiede.KiSimulator").ToArray() : [];
    public IGitPlugin GetDefaultSourceCodeManagementPlugin() => _gitHub;
    public IKiPlugin GetDefaultDevelopmentAutomationPlugin() => GetDevelopmentAutomationPlugins().First();
    public IReadOnlyList<IIdePlugin> GetIdePlugins() => _inner.GetIdePlugins();
    public IIdePlugin GetDefaultIdePlugin() => _inner.GetDefaultIdePlugin();

    private sealed class EmptyCredentialStore : ICredentialStore
    {
        public string? GetCredential(string key) => key == "Softwareschmiede.GitHub.Token" ? "isolated-fixture-token" : null;
        public void SetCredential(string key, string value) => throw new InvalidOperationException("SCM-Fixture verwendet keine Benutzercredentials.");
        public void DeleteCredential(string key) { }
    }
}
