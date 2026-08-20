namespace Softwareschmiede.Application.Services;

/// <summary>Wendet die aus dem Setting <c>plugins.ide.order</c> gelesene, komma-getrennte Prioritäts-Reihenfolge auf eine Liste von IDE-Plugin-Prefixen an. Wird sowohl von der IDE-Plugin-Auflösung als auch von der Settings-UI genutzt, damit beide dieselbe Reihenfolge ermitteln.</summary>
public static class IdePluginOrderResolver
{
    /// <summary>Sortiert <paramref name="discoveryOrder"/> gemäß <paramref name="orderSetting"/>. Prefixe aus <paramref name="orderSetting"/>, die nicht in <paramref name="discoveryOrder"/> enthalten sind, werden ignoriert; Prefixe aus <paramref name="discoveryOrder"/>, die nicht in <paramref name="orderSetting"/> vorkommen, werden in ihrer ursprünglichen Reihenfolge angehängt.</summary>
    /// <param name="discoveryOrder">Die Plugin-Prefixe in Entdeckungsreihenfolge.</param>
    /// <param name="orderSetting">Die komma-getrennte, konfigurierte Prioritäts-Reihenfolge, oder <c>null</c>/leer.</param>
    /// <returns>Die sortierten Plugin-Prefixe. Fehlt <paramref name="orderSetting"/>, wird <paramref name="discoveryOrder"/> unverändert zurückgegeben.</returns>
    public static List<string> Apply(IReadOnlyList<string> discoveryOrder, string? orderSetting)
    {
        if (string.IsNullOrWhiteSpace(orderSetting))
            return discoveryOrder.ToList();

        var configured = orderSetting
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(prefix => discoveryOrder.Contains(prefix, StringComparer.OrdinalIgnoreCase))
            .ToList();

        var fehlende = discoveryOrder.Where(prefix => !configured.Contains(prefix, StringComparer.OrdinalIgnoreCase));
        configured.AddRange(fehlende);

        return configured;
    }
}
