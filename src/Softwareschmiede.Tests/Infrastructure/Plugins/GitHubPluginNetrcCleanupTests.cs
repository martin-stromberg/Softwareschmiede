using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Infrastructure.Plugins;

namespace Softwareschmiede.Tests.Infrastructure.Plugins;

/// <summary>
/// Tests für die einmalige <c>_netrc</c>-Legacy-Bereinigung im <see cref="GitHubPlugin"/>-Konstruktor
/// (Issue #376): Altversionen schrieben <c>machine github.com</c>/<c>machine api.github.com</c>-Stanzas
/// mit Klartext-Token — diese werden entfernt, Fremd-Einträge (z. B. des BitBucket-Plugins) bleiben
/// unverändert.
/// </summary>
public sealed class GitHubPluginNetrcCleanupTests : IDisposable
{
    private readonly string _tempDir;

    /// <summary>GitHubPluginNetrcCleanupTests.</summary>
    public GitHubPluginNetrcCleanupTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"sws-netrc-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    /// <summary>GitHub- und Fremd-Stanzas in einer Datei: nur die GitHub-Einträge werden entfernt,
    /// der restliche Inhalt bleibt byteidentisch erhalten.</summary>
    [Fact]
    public void Ctor_BereinigtGithubStanzas_UndBehalteFremdeintraege()
    {
        const string bitbucketBlock = "machine bitbucket.org\nlogin bitbucket-user\npassword bb-pass\n";
        const string selfHostedBlock = "machine git.example.com\nlogin self-user\npassword sh-pass\n";
        var content = "machine github.com\nlogin oauth2\npassword ghp_secret\n"
            + bitbucketBlock
            + "machine api.github.com\nlogin oauth2\npassword ghp_secret\n"
            + selfHostedBlock;
        var netrcPath = CreateNetrc(content);

        CreatePlugin(netrcPath);

        File.ReadAllText(netrcPath).Should().Be(bitbucketBlock + selfHostedBlock);
    }

    /// <summary>Bestand die _netrc ausschließlich aus GitHub-Stanzas, wird die Datei gelöscht.</summary>
    [Fact]
    public void Ctor_LoeschtNetrc_WennNurGithubStanzas()
    {
        var netrcPath = CreateNetrc(
            "machine github.com\nlogin oauth2\npassword ghp_secret\n"
            + "machine api.github.com\nlogin oauth2\npassword ghp_secret\n");

        CreatePlugin(netrcPath);

        File.Exists(netrcPath).Should().BeFalse();
    }

    /// <summary>Eine _netrc ohne GitHub-Einträge wird nicht angefasst (Inhalt byteidentisch, kein Rewrite).</summary>
    [Fact]
    public void Ctor_LaesstNetrcUnveraendert_OhneGithubStanzas()
    {
        const string content = "machine bitbucket.org\nlogin bitbucket-user\npassword bb-pass\n";
        var netrcPath = CreateNetrc(content);

        CreatePlugin(netrcPath);

        File.ReadAllText(netrcPath).Should().Be(content);
    }

    /// <summary>Existiert keine _netrc, wird keine angelegt.</summary>
    [Fact]
    public void Ctor_LegtKeineNetrcAn_WennFehlend()
    {
        var netrcPath = Path.Combine(_tempDir, "nicht-vorhanden", "_netrc");

        CreatePlugin(netrcPath);

        File.Exists(netrcPath).Should().BeFalse();
    }

    /// <summary>Die machine-Erkennung tokenisiert die Zeilen: Tabulator- und
    /// Mehrfach-Leerzeichen-Trenner werden genauso entfernt wie das Einzel-Leerzeichen-Format.</summary>
    [Fact]
    public void Ctor_BereinigtGithubStanzas_MitBeliebigenWhitespaceTrennern()
    {
        const string bitbucketBlock = "machine bitbucket.org\nlogin bitbucket-user\npassword bb-pass\n";
        var netrcPath = CreateNetrc(
            "machine\tgithub.com\nlogin oauth2\npassword ghp_tab\n"
            + bitbucketBlock
            + "machine  api.github.com\nlogin oauth2\npassword ghp_spaces\n");

        CreatePlugin(netrcPath);

        File.ReadAllText(netrcPath).Should().Be(bitbucketBlock);
    }

    /// <summary>Eine Stanza mit Inline-Tokens (alle Angaben in einer Zeile) wird komplett entfernt.</summary>
    [Fact]
    public void Ctor_BereinigtGithubStanzas_MitInlineTokens()
    {
        const string bitbucketBlock = "machine bitbucket.org\nlogin bitbucket-user\npassword bb-pass\n";
        var netrcPath = CreateNetrc(
            "machine github.com login oauth2 password ghp_inline\n"
            + bitbucketBlock);

        CreatePlugin(netrcPath);

        File.ReadAllText(netrcPath).Should().Be(bitbucketBlock);
    }

    /// <summary>Eine <c>default</c>-Stanza ist eine Blockgrenze: Sie beendet eine zu
    /// entfernende GitHub-Stanza und bleibt samt Folgezeilen erhalten.</summary>
    [Fact]
    public void Ctor_BehalteDefaultStanza_AlsBlockgrenze()
    {
        const string defaultBlock = "default\nlogin anonymous\npassword anon-pass\n";
        const string bitbucketBlock = "machine bitbucket.org\npassword bb-pass\n";
        var netrcPath = CreateNetrc(
            "machine github.com\nlogin oauth2\npassword ghp_secret\n"
            + defaultBlock
            + bitbucketBlock);

        CreatePlugin(netrcPath);

        File.ReadAllText(netrcPath).Should().Be(defaultBlock + bitbucketBlock);
    }

    /// <summary>Die finale Zeilenschaltung des verbleibenden Inhalts bleibt erhalten,
    /// wenn die letzte Stanza der Datei entfernt wird.</summary>
    [Fact]
    public void Ctor_BehalteFinaleZeilenschaltung_DesRestinhalts()
    {
        const string bitbucketBlock = "machine bitbucket.org\nlogin bitbucket-user\npassword bb-pass\n";
        var netrcPath = CreateNetrc(
            bitbucketBlock
            + "machine github.com\nlogin oauth2\npassword ghp_secret\n");

        CreatePlugin(netrcPath);

        File.ReadAllText(netrcPath).Should().Be(bitbucketBlock);
    }

    /// <summary>Der dokumentierte Umgebungsvariablen-Override
    /// (<see cref="GitHubPlugin.NetrcPathEnvironmentVariable"/>) greift, wenn kein expliziter
    /// <c>netrcPath</c> übergeben wird — die Seam für Instanziierungen, bei denen der
    /// Konstruktorparameter nicht erreichbar ist (Plugin-Discovery via ActivatorUtilities).</summary>
    [Fact]
    public void Ctor_NutztUmgebungsvariablenOverride_WennKeinExpliziterNetrcPath()
    {
        const string bitbucketBlock = "machine bitbucket.org\nlogin bitbucket-user\npassword bb-pass\n";
        var netrcPath = CreateNetrc("machine github.com\nlogin oauth2\npassword ghp_secret\n" + bitbucketBlock);
        var previous = Environment.GetEnvironmentVariable(GitHubPlugin.NetrcPathEnvironmentVariable);
        Environment.SetEnvironmentVariable(GitHubPlugin.NetrcPathEnvironmentVariable, netrcPath);
        try
        {
            CreatePlugin(null);
        }
        finally
        {
            Environment.SetEnvironmentVariable(GitHubPlugin.NetrcPathEnvironmentVariable, previous);
        }

        File.ReadAllText(netrcPath).Should().Be(bitbucketBlock);
    }

    /// <summary>Der explizite <c>netrcPath</c>-Konstruktorparameter hat Vorrang vor der
    /// Umgebungsvariable: Die Datei am Env-Pfad bleibt unverändert.</summary>
    [Fact]
    public void Ctor_ExpliziterNetrcPath_HatVorrangVorUmgebungsvariable()
    {
        const string githubBlock = "machine github.com\nlogin oauth2\npassword ghp_secret\n";
        var envNetrcPath = Path.Combine(_tempDir, "env_netrc");
        File.WriteAllText(envNetrcPath, githubBlock);
        var explicitNetrcPath = CreateNetrc(githubBlock);
        var previous = Environment.GetEnvironmentVariable(GitHubPlugin.NetrcPathEnvironmentVariable);
        Environment.SetEnvironmentVariable(GitHubPlugin.NetrcPathEnvironmentVariable, envNetrcPath);
        try
        {
            CreatePlugin(explicitNetrcPath);
        }
        finally
        {
            Environment.SetEnvironmentVariable(GitHubPlugin.NetrcPathEnvironmentVariable, previous);
        }

        File.Exists(explicitNetrcPath).Should().BeFalse();
        File.ReadAllText(envNetrcPath).Should().Be(githubBlock);
    }

    /// <summary>Bereinigungsfehler (hier: netrcPath ist ein Verzeichnis) werden nur als
    /// Warning geloggt und lassen die Plugin-Instanziierung nicht fehlschlagen.</summary>
    [Fact]
    public void Ctor_Bereinigungsfehler_WirftNicht()
    {
        var directoryAsNetrcPath = Path.Combine(_tempDir, "netrc-als-verzeichnis");
        Directory.CreateDirectory(directoryAsNetrcPath);
        var loggerMock = new Mock<ILogger<GitHubPlugin>>();

        var act = () => CreatePlugin(directoryAsNetrcPath, loggerMock);

        act.Should().NotThrow();
        loggerMock.Verify(l => l.Log(
            It.Is<LogLevel>(level => level == LogLevel.Warning),
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce);
    }

    private string CreateNetrc(string content)
    {
        var netrcPath = Path.Combine(_tempDir, "_netrc");
        File.WriteAllText(netrcPath, content);
        return netrcPath;
    }

    private static GitHubPlugin CreatePlugin(string? netrcPath, Mock<ILogger<GitHubPlugin>>? loggerMock = null)
        => new(
            Mock.Of<ICliRunner>(),
            Mock.Of<ICredentialStore>(),
            (loggerMock ?? new Mock<ILogger<GitHubPlugin>>()).Object,
            null,
            null,
            netrcPath);
}
