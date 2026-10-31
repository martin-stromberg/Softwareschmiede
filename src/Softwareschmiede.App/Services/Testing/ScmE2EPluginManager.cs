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
        ICliRunner cliRunner, ILogger<GitHubPlugin> logger, ICredentialStore credentialStore)
    {
        _inner = inner;
        _configuration = configuration;
        // Der Fixture-Benutzer-Token steht von Anfang an im Store; das AgentToken bleibt
        // absichtlich leer, damit die UI-Pflichtvalidierung greifen kann.
        credentialStore.SetCredential("Softwareschmiede.GitHub.Token", "isolated-fixture-token");
        // Zwei getrennte Fixture-Identitäten: ein falsches Client-Routing (Benutzer- statt
        // Agent-Client) liefe gegen scm-owner und fiele beim scm-bot-Abgleich sofort auf.
        _gitHub = new GitHubPlugin(new ScmE2EGitRunner(cliRunner, configuration), credentialStore, logger,
            new ScmE2EApiClient(configuration), new ScmE2EApiClient(configuration, "scm-bot", "BOT"));
    }

    public IReadOnlyList<IGitPlugin> GetSourceCodeManagementPlugins() => [_gitHub];
    public IReadOnlyList<IKiPlugin> GetDevelopmentAutomationPlugins()
        => _configuration.ReadScenario().PluginAvailable
            ? _inner.GetDevelopmentAutomationPlugins().Where(x => x.PluginPrefix == "Softwareschmiede.KiSimulator").ToArray() : [];
    public IGitPlugin GetDefaultSourceCodeManagementPlugin() => _gitHub;
    public IKiPlugin GetDefaultDevelopmentAutomationPlugin() => GetDevelopmentAutomationPlugins().First();
    public IReadOnlyList<IIdePlugin> GetIdePlugins() => _inner.GetIdePlugins();
    public IIdePlugin GetDefaultIdePlugin() => _inner.GetDefaultIdePlugin();
}
