using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace Softwareschmiede.Application.Services;

/// <summary>Gemeinsame Lese-/Schreib-Hilfsmethoden für state.json im Arbeitsverzeichnis einer Autonomen Aufgabe.</summary>
internal static class StateJsonHelper
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    /// <summary>Liest state.json als <see cref="JsonObject"/>. Gibt <see langword="null"/> zurück, wenn die Datei nicht existiert, und fängt Parse-Fehler ab (protokolliert eine Warnung statt die Exception zu propagieren).</summary>
    public static async Task<JsonObject?> LeseAsync(string stateJsonPfad, ILogger logger, CancellationToken ct)
    {
        if (!File.Exists(stateJsonPfad))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(stateJsonPfad, ct);
        try
        {
            return JsonNode.Parse(json) as JsonObject ?? new JsonObject();
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "state.json unter '{StateJsonPfad}' konnte nicht geparst werden; Aktualisierung wird übersprungen.", stateJsonPfad);
            return null;
        }
    }

    /// <summary>Schreibt <paramref name="node"/> formatiert nach <paramref name="stateJsonPfad"/>.</summary>
    public static Task SchreibeAsync(string stateJsonPfad, JsonObject node, CancellationToken ct)
        => File.WriteAllTextAsync(stateJsonPfad, node.ToJsonString(WriteOptions), ct);
}
