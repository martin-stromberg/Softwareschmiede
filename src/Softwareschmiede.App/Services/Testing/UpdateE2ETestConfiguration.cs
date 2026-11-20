using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Softwareschmiede.App.Services.Testing;

/// <summary>
/// Statische E2E-Testkonfiguration für die kontrollierte Update-Umgebung. Wird aus der JSON-Datei
/// gelesen, deren Pfad in der Prozess-Umgebungsvariable <see cref="EnvironmentVariable"/> steht.
/// Aktiv ist die Konfiguration nur, wenn zusätzlich <c>SOFTWARESCHMIEDE_TEST_DB_PATH</c> gesetzt ist
/// (siehe <c>App.ConfigureServices</c>).
/// </summary>
public sealed class UpdateE2ETestConfiguration
{
    /// <summary>Name der Prozess-Umgebungsvariable mit dem Pfad zur Testkonfigurations-JSON.</summary>
    public const string EnvironmentVariable = "SOFTWARESCHMIEDE_UPDATE_TEST_CONFIG";

    /// <summary>Name der Prozess-Umgebungsvariable mit dem Pfad der Test-SQLite-Datenbank.</summary>
    public const string TestDatabaseEnvironmentVariable = Infrastructure.Terminal.TerminalSessionService.TestDatabasePathVariable;

    /// <summary>Wurzelverzeichnis des Tests; alle Fixture-Dateien und geschriebenen Update-Artefakte liegen darunter.</summary>
    [JsonPropertyName("testRoot")]
    public string TestRoot { get; init; } = string.Empty;

    /// <summary>Pfad der Szenariodatei (JSON), die pro Request/Lesebefehl neu gelesen wird.</summary>
    [JsonPropertyName("scenarioFile")]
    public string ScenarioFilePath { get; init; } = string.Empty;

    /// <summary>Pfad der JSONL-Protokolldatei, in die alle beobachteten Ereignisse geschrieben werden.</summary>
    [JsonPropertyName("protocolFile")]
    public string LogFilePath { get; init; } = string.Empty;

    /// <summary>Eindeutige Szenario-ID, die in jeden Protokolleintrag geschrieben wird.</summary>
    [JsonPropertyName("scenarioId")]
    public string ScenarioId { get; init; } = string.Empty;

    /// <summary>Verzeichnis der Freigabe-/Abbruch-Gate-Dateien. Standard: <c>{TestRoot}/gates</c>.</summary>
    [JsonPropertyName("gatesDirectory")]
    public string? GatesDirectory { get; init; }

    /// <summary>Das simulierte Installationsverzeichnis: <c>{TestRoot}/installed</c> mit der <c>version.json</c>.</summary>
    public string InstallationDirectory => Path.Combine(TestRoot, "installed");

    /// <summary>Effektives Gate-Verzeichnis (konfiguriert oder Standard unterhalb der TestRoot).</summary>
    public string EffectiveGatesDirectory
        => string.IsNullOrWhiteSpace(GatesDirectory) ? Path.Combine(TestRoot, "gates") : GatesDirectory;

    /// <summary>
    /// Lädt die Testkonfiguration aus der per <see cref="EnvironmentVariable"/> referenzierten JSON-Datei.
    /// Gibt <c>null</c> zurück, wenn die Variable nicht gesetzt ist. Wirft bei gesetzter, aber fehlender
    /// oder ungültiger Konfiguration eine <see cref="UpdateE2ETestConfigurationException"/> — es wird
    /// niemals stillschweigend auf die reale Update-Quelle oder den echten Updater zurückgefallen.
    /// </summary>
    /// <param name="testDatabasePath">Wert von <c>SOFTWARESCHMIEDE_TEST_DB_PATH</c> (erforderlich für die Aktivierung).</param>
    /// <returns>Die geladene und validierte Konfiguration, oder <c>null</c>, wenn die Variable nicht gesetzt ist.</returns>
    public static UpdateE2ETestConfiguration? LoadFromEnvironment(string? testDatabasePath)
    {
        var path = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (string.IsNullOrWhiteSpace(testDatabasePath))
        {
            throw new UpdateE2ETestConfigurationException(
                $"{EnvironmentVariable} ist gesetzt, aber {TestDatabaseEnvironmentVariable} fehlt. " +
                "Die Update-E2E-Umgebung wird nur mit beiden Variablen aktiviert.");
        }

        var configuration = Load(path);
        Validate(configuration, path);
        return configuration;
    }

    /// <summary>Lädt und parst die Testkonfigurations-JSON aus <paramref name="path"/>.</summary>
    /// <param name="path">Pfad der Konfigurationsdatei.</param>
    /// <returns>Die deserialisierte Konfiguration.</returns>
    /// <exception cref="UpdateE2ETestConfigurationException">Die Datei fehlt, ist unlesbar oder enthält ungültiges JSON.</exception>
    public static UpdateE2ETestConfiguration Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new UpdateE2ETestConfigurationException(
                $"Update-E2E-Testkonfiguration wurde nicht gefunden: {path}");
        }

        try
        {
            var configuration = JsonSerializer.Deserialize<UpdateE2ETestConfiguration>(File.ReadAllText(path));
            return configuration ?? throw new UpdateE2ETestConfigurationException(
                $"Update-E2E-Testkonfiguration ist leer: {path}");
        }
        catch (JsonException ex)
        {
            throw new UpdateE2ETestConfigurationException(
                $"Update-E2E-Testkonfiguration enthält ungültiges JSON: {path}", ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new UpdateE2ETestConfigurationException(
                $"Update-E2E-Testkonfiguration konnte nicht gelesen werden: {path}", ex);
        }
    }

    private static void Validate(UpdateE2ETestConfiguration configuration, string path)
    {
        if (string.IsNullOrWhiteSpace(configuration.TestRoot))
        {
            throw new UpdateE2ETestConfigurationException(
                $"Update-E2E-Testkonfiguration ohne testRoot: {path}");
        }

        if (!Directory.Exists(configuration.TestRoot))
        {
            throw new UpdateE2ETestConfigurationException(
                $"Update-E2E-TestRoot existiert nicht: {configuration.TestRoot}");
        }

        if (!File.Exists(configuration.InstallationVersionsFilePath()))
        {
            throw new UpdateE2ETestConfigurationException(
                $"Update-E2E-TestRoot enthält keine installierte version.json: " +
                $"{configuration.InstallationVersionsFilePath()}");
        }

        if (string.IsNullOrWhiteSpace(configuration.ScenarioFilePath)
            || !File.Exists(configuration.ScenarioFilePath))
        {
            throw new UpdateE2ETestConfigurationException(
                $"Update-E2E-Szenariodatei fehlt: {configuration.ScenarioFilePath}");
        }

        if (string.IsNullOrWhiteSpace(configuration.LogFilePath))
        {
            throw new UpdateE2ETestConfigurationException(
                $"Update-E2E-Testkonfiguration ohne protocolFile: {path}");
        }

        // Sicherstellen, dass kein Pfad aus der TestRoot ausbricht.
        var root = Path.GetFullPath(configuration.TestRoot);
        CheckInnerhalbDerRoot(configuration.ScenarioFilePath, root, "Szenariodatei");
        CheckInnerhalbDerRoot(configuration.LogFilePath, root, "Protokolldatei");
        CheckInnerhalbDerRoot(configuration.EffectiveGatesDirectory, root, "Gate-Verzeichnis");
    }

    /// <summary>Pfad der installierten <c>version.json</c> unterhalb der TestRoot.</summary>
    public string InstallationVersionsFilePath() => Path.Combine(InstallationDirectory, "version.json");

    private static void CheckInnerhalbDerRoot(string path, string root, string description)
    {
        var full = Path.GetFullPath(path);
        var relativ = Path.GetRelativePath(root, full);
        if (relativ == ".."
            || relativ.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || relativ.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal)
            || Path.IsPathRooted(relativ))
        {
            throw new UpdateE2ETestConfigurationException(
                $"Update-E2E-{description} liegt außerhalb der TestRoot: {full}");
        }
    }
}

/// <summary>Fehler beim Laden oder Validieren der Update-E2E-Testkonfiguration.</summary>
public sealed class UpdateE2ETestConfigurationException : Exception
{
    /// <inheritdoc cref="UpdateE2ETestConfigurationException"/>
    public UpdateE2ETestConfigurationException(string message) : base(message)
    {
    }

    /// <inheritdoc cref="UpdateE2ETestConfigurationException"/>
    public UpdateE2ETestConfigurationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
