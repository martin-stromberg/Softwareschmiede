using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;

namespace Softwareschmiede.IntegrationTests.Scripts;

/// <summary>
/// Integration tests for the repository root start.ps1 behavior.
/// </summary>
public sealed class StartPs1IntegrationTests : IDisposable
{
    private readonly List<string> _temporaryDirectories = [];

    /// <summary>
    /// Validates autonomous discovery and update of multiple projects without parameters.
    /// </summary>
    [Fact]
    public async Task StartScript_ShouldDiscoverAndUpdateMultipleProjects_WhenRunWithoutParameters()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // Arrange
        var tempRepositoryRoot = CreateTemporaryRepository();
        CreateLaunchSettings(tempRepositoryRoot, Path.Combine("src", "AppOne", "Properties"), "http://127.0.0.1:5001");
        CreateLaunchSettings(tempRepositoryRoot, Path.Combine("src", "AppTwo", "Properties"), "https://localhost:7001;http://softwareschmiede.dev.localhost:5002");
        CreateLaunchSettings(tempRepositoryRoot, Path.Combine("src", "AppThree", "Properties"), "https://localhost:7002");
        CreateLaunchSettings(tempRepositoryRoot, Path.Combine("src", "Ignored", "bin", "Debug", "Properties"), "http://localhost:5999");

        // Act
        var result = await RunStartScriptAsync(tempRepositoryRoot);

        // Assert
        result.ExitCode.Should().Be(0);

        var appOneUrl = ReadHttpApplicationUrl(Path.Combine(tempRepositoryRoot, "src", "AppOne", "Properties", "launchSettings.json"));
        var appTwoUrl = ReadHttpApplicationUrl(Path.Combine(tempRepositoryRoot, "src", "AppTwo", "Properties", "launchSettings.json"));
        var appThreeUrl = ReadHttpApplicationUrl(Path.Combine(tempRepositoryRoot, "src", "AppThree", "Properties", "launchSettings.json"));
        var ignoredUrl = ReadHttpApplicationUrl(Path.Combine(tempRepositoryRoot, "src", "Ignored", "bin", "Debug", "Properties", "launchSettings.json"));

        var appOneUri = new Uri(appOneUrl);
        var appTwoUri = new Uri(appTwoUrl);
        var appThreeUri = new Uri(appThreeUrl);

        appOneUri.Host.Should().Be("127.0.0.1");
        appTwoUri.Host.Should().Be("localhost");
        appThreeUri.Host.Should().Be("localhost");
        appOneUri.Port.Should().NotBe(appTwoUri.Port);
        appOneUri.Port.Should().NotBe(appThreeUri.Port);
        appTwoUri.Port.Should().NotBe(appThreeUri.Port);
        ignoredUrl.Should().Be("http://localhost:5999");
    }

    /// <summary>
    /// Validates clear error handling when no launchSettings target exists.
    /// </summary>
    [Fact]
    public async Task StartScript_ShouldFailWithExit10_WhenNoTargetLaunchSettingsExists()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // Arrange
        var tempRepositoryRoot = CreateTemporaryRepository();

        // Act
        var result = await RunStartScriptAsync(tempRepositoryRoot);

        // Assert
        result.ExitCode.Should().Be(10);
        result.StandardOutput.Should().Contain("[CODE:10]").And.Contain(tempRepositoryRoot);
    }

    /// <summary>
    /// Validates clear error handling when launchSettings contains invalid JSON.
    /// </summary>
    [Fact]
    public async Task StartScript_ShouldFailWithExit11_WhenLaunchSettingsContainsInvalidJson()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // Arrange
        var tempRepositoryRoot = CreateTemporaryRepository();
        var launchSettingsPath = Path.Combine(tempRepositoryRoot, "src", "AppOne", "Properties", "launchSettings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(launchSettingsPath)!);
        File.WriteAllText(launchSettingsPath, "{ invalid ");

        // Act
        var result = await RunStartScriptAsync(tempRepositoryRoot);

        // Assert
        result.ExitCode.Should().Be(11);
        result.StandardOutput.Should().Contain("[CODE:11]").And.Contain("invalid JSON");
    }

    /// <summary>
    /// Validates fallback to a project profile when no http profile exists.
    /// </summary>
    [Fact]
    public async Task StartScript_ShouldUseProjectProfile_WhenHttpProfileIsMissing()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // Arrange
        var tempRepositoryRoot = CreateTemporaryRepository();
        var launchSettingsPath = Path.Combine(tempRepositoryRoot, "src", "AppOne", "Properties", "launchSettings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(launchSettingsPath)!);
        File.WriteAllText(launchSettingsPath, """
{
  "profiles": {
    "https": {
      "commandName": "Project",
      "applicationUrl": "https://localhost:7001"
    }
  }
}
""");

        // Act
        var result = await RunStartScriptAsync(tempRepositoryRoot);

        // Assert
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Contain("Updated 'https' profile");

        using var jsonDocument = JsonDocument.Parse(File.ReadAllText(launchSettingsPath));
        var profileUrl = jsonDocument.RootElement
            .GetProperty("profiles")
            .GetProperty("https")
            .GetProperty("applicationUrl")
            .GetString();
        profileUrl.Should().NotBeNullOrWhiteSpace();
        profileUrl.Should().MatchRegex("^http://localhost:[0-9]+$");
    }

    /// <summary>
    /// Validates controlled failure with cleanup when launchSettings write fails.
    /// </summary>
    [Fact]
    public async Task StartScript_ShouldFailWithExit13AndCleanupTempFile_WhenLaunchSettingsWriteFails()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // Arrange
        var tempRepositoryRoot = CreateTemporaryRepository();
        var launchSettingsPath = CreateLaunchSettings(tempRepositoryRoot, Path.Combine("src", "AppOne", "Properties"), "http://localhost:5000");
        File.SetAttributes(launchSettingsPath, File.GetAttributes(launchSettingsPath) | FileAttributes.ReadOnly);

        try
        {
            // Act
            var result = await RunStartScriptAsync(tempRepositoryRoot);

            // Assert
            result.ExitCode.Should().Be(13);
            result.StandardOutput.Should().Contain("[CODE:13]");
            File.Exists($"{launchSettingsPath}.tmp").Should().BeFalse();
        }
        finally
        {
            File.SetAttributes(launchSettingsPath, FileAttributes.Normal);
        }
    }

    /// <summary>
    /// Validates that exit code aggregation keeps processing and prefers higher-priority errors.
    /// </summary>
    [Fact]
    public async Task StartScript_ShouldAggregateExitCodePriority_WhenProjectsHaveMixedResults()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // Arrange
        var tempRepositoryRoot = CreateTemporaryRepository();
        var writablePath = CreateLaunchSettings(tempRepositoryRoot, Path.Combine("src", "AppOne", "Properties"), "http://localhost:5001");
        var readOnlyPath = CreateLaunchSettings(tempRepositoryRoot, Path.Combine("src", "AppTwo", "Properties"), "http://localhost:5002");
        var invalidPath = Path.Combine(tempRepositoryRoot, "src", "AppThree", "Properties", "launchSettings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(invalidPath)!);
        File.WriteAllText(invalidPath, "{ invalid ");
        File.SetAttributes(readOnlyPath, File.GetAttributes(readOnlyPath) | FileAttributes.ReadOnly);

        try
        {
            // Act
            var result = await RunStartScriptAsync(tempRepositoryRoot);

            // Assert
            result.ExitCode.Should().Be(13);
            result.StandardOutput.Should().Contain("[CODE:13]").And.Contain("[CODE:11]");
            ReadHttpApplicationUrl(writablePath).Should().MatchRegex("^http://localhost:[0-9]+$");
        }
        finally
        {
            File.SetAttributes(readOnlyPath, FileAttributes.Normal);
        }
    }

    /// <summary>
    /// Validates diagnostics include correlation fields per processed project.
    /// </summary>
    [Fact]
    public async Task StartScript_ShouldWriteCorrelatableDiagnostics_WhenProcessingProjects()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // Arrange
        var tempRepositoryRoot = CreateTemporaryRepository();
        CreateLaunchSettings(tempRepositoryRoot, Path.Combine("src", "AppOne", "Properties"), "http://localhost:5000");

        // Act
        var result = await RunStartScriptAsync(tempRepositoryRoot);

        // Assert
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Contain("[RUN:");
        result.StandardOutput.Should().Contain("[PROJECT:");
        result.StandardOutput.Should().Contain("[FILE:");
        result.StandardOutput.Should().Contain("[PORT:");
    }

    /// <summary>
    /// Validates malformed profiles.http (non-object) still fails with exit 11 if no usable profile exists.
    /// </summary>
    [Fact]
    public async Task StartScript_ShouldReturnExit11_PerTarget_WhenHttpProfileIsNotObject()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // Arrange
        var tempRepositoryRoot = CreateTemporaryRepository();
        var validLaunchSettingsPath = CreateLaunchSettings(tempRepositoryRoot, Path.Combine("src", "ValidApp", "Properties"), "http://localhost:5001");
        var invalidLaunchSettingsPath = Path.Combine(tempRepositoryRoot, "src", "InvalidApp", "Properties", "launchSettings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(invalidLaunchSettingsPath)!);
        File.WriteAllText(invalidLaunchSettingsPath, """
{
  "profiles": {
    "http": "not-an-object"
  }
}
""");

        // Act
        var result = await RunStartScriptAsync(tempRepositoryRoot);

        // Assert
        result.ExitCode.Should().Be(11);
        result.StandardOutput.Should().Contain("[CODE:11]").And.Contain("No usable profile was found");
        result.StandardOutput.Should().NotContain("[CODE:99]");
        ReadHttpApplicationUrl(validLaunchSettingsPath).Should().MatchRegex("^http://localhost:[0-9]+$");
    }

    /// <summary>
    /// Validates malformed profiles.http (non-object) falls back to a usable project profile.
    /// </summary>
    [Fact]
    public async Task StartScript_ShouldUseProjectProfile_WhenHttpProfileIsInvalidButProjectProfileExists()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // Arrange
        var tempRepositoryRoot = CreateTemporaryRepository();
        var launchSettingsPath = Path.Combine(tempRepositoryRoot, "src", "AppOne", "Properties", "launchSettings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(launchSettingsPath)!);
        File.WriteAllText(launchSettingsPath, """
{
  "profiles": {
    "http": "not-an-object",
    "https": {
      "commandName": "Project",
      "applicationUrl": "https://localhost:7001"
    }
  }
}
""");

        // Act
        var result = await RunStartScriptAsync(tempRepositoryRoot);

        // Assert
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Contain("Updated 'https' profile");

        using var jsonDocument = JsonDocument.Parse(File.ReadAllText(launchSettingsPath));
        var profileUrl = jsonDocument.RootElement
            .GetProperty("profiles")
            .GetProperty("https")
            .GetProperty("applicationUrl")
            .GetString();
        profileUrl.Should().NotBeNullOrWhiteSpace();
        profileUrl.Should().MatchRegex("^http://localhost:[0-9]+$");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var directory in _temporaryDirectories.Where(Directory.Exists))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string ResolveRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var scriptPath = Path.Combine(current.FullName, "start.ps1");
            var projectFilePath = Path.Combine(current.FullName, "src", "Softwareschmiede", "Softwareschmiede.csproj");

            if (File.Exists(scriptPath) && File.Exists(projectFilePath))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root could not be resolved.");
    }

    private static string ReadHttpApplicationUrl(string launchSettingsPath)
    {
        using var jsonDocument = JsonDocument.Parse(File.ReadAllText(launchSettingsPath));
        return jsonDocument.RootElement
            .GetProperty("profiles")
            .GetProperty("http")
            .GetProperty("applicationUrl")
            .GetString() ?? string.Empty;
    }

    private string CreateTemporaryRepository()
    {
        var repositoryRoot = Path.Combine(Path.GetTempPath(), $"startps1-it-{Guid.NewGuid():N}");
        _temporaryDirectories.Add(repositoryRoot);
        Directory.CreateDirectory(repositoryRoot);

        var sourceScriptPath = Path.Combine(ResolveRepositoryRoot(), "start.ps1");
        File.Copy(sourceScriptPath, Path.Combine(repositoryRoot, "start.ps1"));
        return repositoryRoot;
    }

    private static string CreateLaunchSettings(string repositoryRoot, string relativePropertiesPath, string applicationUrl)
    {
        var launchSettingsPath = Path.Combine(repositoryRoot, relativePropertiesPath, "launchSettings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(launchSettingsPath)!);
        File.WriteAllText(launchSettingsPath, $$"""
{
  "profiles": {
    "http": {
      "commandName": "Project",
      "applicationUrl": "{{applicationUrl}}",
      "dotnetRunMessages": true
    }
  }
}
""");

        return launchSettingsPath;
    }

    private static async Task<ScriptExecutionResult> RunStartScriptAsync(string repositoryPath, IEnumerable<string>? additionalArguments = null)
    {
        var scriptPath = Path.Combine(repositoryPath, "start.ps1");
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            WorkingDirectory = repositoryPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(scriptPath);
        if (additionalArguments is not null)
        {
            foreach (var argument in additionalArguments)
            {
                startInfo.ArgumentList.Add(argument);
            }
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start powershell.exe.");
        var standardOutput = await process.StandardOutput.ReadToEndAsync();
        var standardError = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return new ScriptExecutionResult(process.ExitCode, standardOutput, standardError);
    }

    private sealed record ScriptExecutionResult(int ExitCode, string StandardOutput, string StandardError);
}
