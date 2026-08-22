using System.Text.Json;
using System.Text.Json.Serialization;

namespace Softwareschmiede.Infrastructure.Data;

/// <summary>
/// Ermittelt synchron und ohne DI-Abhängigkeiten den Pfad der SQLite-Datenbankdatei beim
/// Programmstart. Wird bewusst separat von <c>ApplicationVersionProvider</c> gehalten, da die
/// Pfadauflösung in <c>App.xaml.cs</c>/<c>ConfigureServices</c> laufen muss — also bevor der
/// DI-Container/Host aufgebaut ist und bevor asynchroner Code ausgeführt werden kann.
/// </summary>
public static class DatenbankPfadResolver
{
    private const string DatenbankDateiname = "softwareschmiede.db";
    private const string VersionsDateiname = "version.json";
    private const string ReleaseCandidateMarker = "-rc";

    /// <summary>
    /// Ermittelt den zu verwendenden Datenbankpfad.
    /// </summary>
    /// <param name="baseDirectory">
    /// Das Basisverzeichnis der Anwendung (in Produktion <see cref="AppContext.BaseDirectory"/>),
    /// in dem sowohl eine ggf. vorhandene <c>version.json</c> als auch die Datenbankdatei für
    /// nicht-produktive Versionen abgelegt werden.
    /// </param>
    /// <param name="testDbPathOverride">
    /// Wert der Umgebungsvariable <c>SOFTWARESCHMIEDE_TEST_DB_PATH</c>, falls gesetzt. Hat höchste
    /// Priorität und wird unverändert zurückgegeben (bestehendes Testinfrastruktur-Verhalten).
    /// </param>
    /// <returns>
    /// Bei gesetztem <paramref name="testDbPathOverride"/> dessen Wert. Andernfalls: Bei einer
    /// produktiven Version (vorhandene <c>version.json</c> mit einem <c>tagName</c> ohne
    /// „-rc"-Infix) der bisherige Pfad unter <c>%LocalAppData%\Softwareschmiede\softwareschmiede.db</c>.
    /// Bei einer RC-Version (<c>tagName</c> enthält „-rc") oder einer versionslosen Version (keine
    /// oder keine gültige <c>version.json</c>) der Pfad <c>{baseDirectory}\softwareschmiede.db</c>.
    /// </returns>
    public static string ErmittlePfad(string baseDirectory, string? testDbPathOverride)
    {
        if (!string.IsNullOrEmpty(testDbPathOverride))
        {
            return testDbPathOverride;
        }

        return IstProduktiveVersion(baseDirectory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Softwareschmiede",
                DatenbankDateiname)
            : Path.Combine(baseDirectory, DatenbankDateiname);
    }

    private static bool IstProduktiveVersion(string baseDirectory)
    {
        var versionPfad = Path.Combine(baseDirectory, VersionsDateiname);
        if (!File.Exists(versionPfad))
        {
            // Versionslos (z. B. Ausführung unter Visual Studio) → nicht produktiv.
            return false;
        }

        try
        {
            var json = File.ReadAllText(versionPfad);
            var document = JsonSerializer.Deserialize<VersionJson>(json);
            var tagName = document?.TagName;

            if (string.IsNullOrWhiteSpace(tagName))
            {
                // version.json ohne verwertbares tagName → konservativ als nicht produktiv behandeln.
                return false;
            }

            // RC-Tags folgen dem Format "v{version}-rc.{n}" (siehe staging-ci.yml), produktive Tags
            // dem Format "v{version}" ohne "-rc"-Infix (siehe release.yml).
            return !tagName.Contains(ReleaseCandidateMarker, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or FormatException)
        {
            // Kaputte/unlesbare version.json darf den Programmstart nicht crashen lassen →
            // konservativ als nicht produktiv behandeln. Diese Klasse läuft synchron vor dem
            // Host-/DI-Aufbau (siehe App.xaml.cs/ConfigureServices) und hat daher bewusst keine
            // Logger-Abhängigkeit; der Aufrufer kann bei Bedarf über den Rückgabewert loggen.
            return false;
        }
    }

    private sealed class VersionJson
    {
        [JsonPropertyName("tagName")]
        public string? TagName { get; set; }
    }
}
