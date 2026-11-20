using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Softwareschmiede.App.Services.Testing;

/// <summary>
/// Statische E2E-Testkonfiguration für die kontrollierte Update-Umgebung. Wird aus der JSON-Datei
/// gelesen, deren Pfad in der Prozess-Umgebungsvariable <see cref="UmgebungsVariable"/> steht.
/// Aktiv ist die Konfiguration nur, wenn zusätzlich <c>SOFTWARESCHMIEDE_TEST_DB_PATH</c> gesetzt ist
/// (siehe <c>App.ConfigureServices</c>).
/// </summary>
public sealed class UpdateE2ETestConfiguration
{
    /// <summary>Name der Prozess-Umgebungsvariable mit dem Pfad zur Testkonfigurations-JSON.</summary>
    public const string UmgebungsVariable = "SOFTWARESCHMIEDE_UPDATE_TEST_CONFIG";

    /// <summary>Name der Prozess-Umgebungsvariable mit dem Pfad der Test-SQLite-Datenbank.</summary>
    public const string TestDatenbankUmgebungsVariable = Infrastructure.Terminal.TerminalSessionService.TestDatabasePathVariable;

    /// <summary>Wurzelverzeichnis des Tests; alle Fixture-Dateien und geschriebenen Update-Artefakte liegen darunter.</summary>
    [JsonPropertyName("testRoot")]
    public string Testwurzel { get; init; } = string.Empty;

    /// <summary>Pfad der Szenariodatei (JSON), die pro Request/Lesebefehl neu gelesen wird.</summary>
    [JsonPropertyName("scenarioFile")]
    public string SzenarioDateiPfad { get; init; } = string.Empty;

    /// <summary>Pfad der JSONL-Protokolldatei, in die alle beobachteten Ereignisse geschrieben werden.</summary>
    [JsonPropertyName("protocolFile")]
    public string ProtokollDateiPfad { get; init; } = string.Empty;

    /// <summary>Eindeutige Szenario-ID, die in jeden Protokolleintrag geschrieben wird.</summary>
    [JsonPropertyName("scenarioId")]
    public string SzenarioId { get; init; } = string.Empty;

    /// <summary>Verzeichnis der Freigabe-/Abbruch-Gate-Dateien. Standard: <c>{Testwurzel}/gates</c>.</summary>
    [JsonPropertyName("gatesDirectory")]
    public string? GatesVerzeichnis { get; init; }

    /// <summary>Das simulierte Installationsverzeichnis: <c>{Testwurzel}/installed</c> mit der <c>version.json</c>.</summary>
    public string InstallationsVerzeichnis => Path.Combine(Testwurzel, "installed");

    /// <summary>Effektives Gate-Verzeichnis (konfiguriert oder Standard unterhalb der Testwurzel).</summary>
    public string EffektivesGatesVerzeichnis
        => string.IsNullOrWhiteSpace(GatesVerzeichnis) ? Path.Combine(Testwurzel, "gates") : GatesVerzeichnis;

    /// <summary>
    /// Lädt die Testkonfiguration aus der per <see cref="UmgebungsVariable"/> referenzierten JSON-Datei.
    /// Gibt <c>null</c> zurück, wenn die Variable nicht gesetzt ist. Wirft bei gesetzter, aber fehlender
    /// oder ungültiger Konfiguration eine <see cref="UpdateE2ETestKonfigurationException"/> — es wird
    /// niemals stillschweigend auf die reale Update-Quelle oder den echten Updater zurückgefallen.
    /// </summary>
    /// <param name="testDatenbankPfad">Wert von <c>SOFTWARESCHMIEDE_TEST_DB_PATH</c> (erforderlich für die Aktivierung).</param>
    /// <returns>Die geladene und validierte Konfiguration, oder <c>null</c>, wenn die Variable nicht gesetzt ist.</returns>
    public static UpdateE2ETestConfiguration? LadeAusUmgebung(string? testDatenbankPfad)
    {
        var path = Environment.GetEnvironmentVariable(UmgebungsVariable);
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (string.IsNullOrWhiteSpace(testDatenbankPfad))
        {
            throw new UpdateE2ETestKonfigurationException(
                $"{UmgebungsVariable} ist gesetzt, aber {TestDatenbankUmgebungsVariable} fehlt. " +
                "Die Update-E2E-Umgebung wird nur mit beiden Variablen aktiviert.");
        }

        var configuration = Lade(path);
        Validiere(configuration, path);
        return configuration;
    }

    /// <summary>Lädt und parst die Testkonfigurations-JSON aus <paramref name="path"/>.</summary>
    /// <param name="path">Pfad der Konfigurationsdatei.</param>
    /// <returns>Die deserialisierte Konfiguration.</returns>
    /// <exception cref="UpdateE2ETestKonfigurationException">Die Datei fehlt, ist unlesbar oder enthält ungültiges JSON.</exception>
    public static UpdateE2ETestConfiguration Lade(string path)
    {
        if (!File.Exists(path))
        {
            throw new UpdateE2ETestKonfigurationException(
                $"Update-E2E-Testkonfiguration wurde nicht gefunden: {path}");
        }

        try
        {
            var configuration = JsonSerializer.Deserialize<UpdateE2ETestConfiguration>(File.ReadAllText(path));
            return configuration ?? throw new UpdateE2ETestKonfigurationException(
                $"Update-E2E-Testkonfiguration ist leer: {path}");
        }
        catch (JsonException ex)
        {
            throw new UpdateE2ETestKonfigurationException(
                $"Update-E2E-Testkonfiguration enthält ungültiges JSON: {path}", ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new UpdateE2ETestKonfigurationException(
                $"Update-E2E-Testkonfiguration konnte nicht gelesen werden: {path}", ex);
        }
    }

    private static void Validiere(UpdateE2ETestConfiguration configuration, string path)
    {
        if (string.IsNullOrWhiteSpace(configuration.Testwurzel))
        {
            throw new UpdateE2ETestKonfigurationException(
                $"Update-E2E-Testkonfiguration ohne testRoot: {path}");
        }

        if (!Directory.Exists(configuration.Testwurzel))
        {
            throw new UpdateE2ETestKonfigurationException(
                $"Update-E2E-Testwurzel existiert nicht: {configuration.Testwurzel}");
        }

        if (!File.Exists(configuration.InstallationsVersionsDateiPfad()))
        {
            throw new UpdateE2ETestKonfigurationException(
                $"Update-E2E-Testwurzel enthält keine installierte version.json: " +
                $"{configuration.InstallationsVersionsDateiPfad()}");
        }

        if (string.IsNullOrWhiteSpace(configuration.SzenarioDateiPfad)
            || !File.Exists(configuration.SzenarioDateiPfad))
        {
            throw new UpdateE2ETestKonfigurationException(
                $"Update-E2E-Szenariodatei fehlt: {configuration.SzenarioDateiPfad}");
        }

        if (string.IsNullOrWhiteSpace(configuration.ProtokollDateiPfad))
        {
            throw new UpdateE2ETestKonfigurationException(
                $"Update-E2E-Testkonfiguration ohne protocolFile: {path}");
        }

        // Sicherstellen, dass kein Pfad aus der Testwurzel ausbricht.
        var wurzel = Path.GetFullPath(configuration.Testwurzel);
        PruefeInnerhalbDerWurzel(configuration.SzenarioDateiPfad, wurzel, "Szenariodatei");
        PruefeInnerhalbDerWurzel(configuration.ProtokollDateiPfad, wurzel, "Protokolldatei");
        PruefeInnerhalbDerWurzel(configuration.EffektivesGatesVerzeichnis, wurzel, "Gate-Verzeichnis");
    }

    /// <summary>Pfad der installierten <c>version.json</c> unterhalb der Testwurzel.</summary>
    public string InstallationsVersionsDateiPfad() => Path.Combine(InstallationsVerzeichnis, "version.json");

    private static void PruefeInnerhalbDerWurzel(string path, string wurzel, string description)
    {
        var full = Path.GetFullPath(path);
        var relativ = Path.GetRelativePath(wurzel, full);
        if (relativ == ".."
            || relativ.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || relativ.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal)
            || Path.IsPathRooted(relativ))
        {
            throw new UpdateE2ETestKonfigurationException(
                $"Update-E2E-{description} liegt außerhalb der Testwurzel: {full}");
        }
    }
}

/// <summary>Fehler beim Laden oder Validieren der Update-E2E-Testkonfiguration.</summary>
public sealed class UpdateE2ETestKonfigurationException : Exception
{
    /// <inheritdoc cref="UpdateE2ETestKonfigurationException"/>
    public UpdateE2ETestKonfigurationException(string message) : base(message)
    {
    }

    /// <inheritdoc cref="UpdateE2ETestKonfigurationException"/>
    public UpdateE2ETestKonfigurationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
