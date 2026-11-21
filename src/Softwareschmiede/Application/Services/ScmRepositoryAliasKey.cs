using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Application.Services;

/// <summary>Bildet den kanonischen Gruppierungs-/Checkpoint-Schlüssel eines lokalen Repository-Alias.</summary>
internal static class ScmRepositoryAliasKey
{
    /// <summary>Liefert die kanonische Form des Alias, sofern die Quelle
    /// <see cref="IScmRepositoryAliasNormalizer"/> implementiert und die Normalisierung gelingt;
    /// andernfalls (Interface fehlt, Parse-Fehler, leeres Ergebnis) den unveränderten Roh-Alias.
    /// Der Roh-Fallback erhält das bisherige Gruppierungs- und Checkpoint-Verhalten für Quellen
    /// ohne Normalisierer sowie für nicht parsebare Alias-Werte.</summary>
    /// <param name="source">Die Ereignisquelle des Plugins.</param>
    /// <param name="alias">Lokaler Repository-Alias (z. B. Repository-URL oder <c>owner/name</c>).</param>
    /// <param name="logger">Optionaler Logger für Normalisierungsfehler, die kein
    /// Parse-Fehler (<see cref="ScmProviderException"/>) sind — solche Implementierungsfehler
    /// degradieren die Gruppierung sonst ohne jedes Fehlersignal.</param>
    internal static string CanonicalOrRaw(IScmEventSource source, string alias, ILogger? logger = null)
    {
        if (source is not IScmRepositoryAliasNormalizer normalizer)
            return alias;
        try
        {
            var canonical = normalizer.NormalizeRepositoryAlias(alias);
            return string.IsNullOrWhiteSpace(canonical) ? alias : canonical;
        }
        catch (ScmProviderException)
        {
            // Dokumentierter Parse-Fehler des Normalisierers → Roh-Alias-Fallback.
            return alias;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Implementierungsfehler im Normalisierer eines Plugins: sichtbar machen statt
            // still zu degradieren; die Gruppierung bleibt über den Roh-Alias nutzbar.
            logger?.LogWarning(ex, "Repository-Alias {Alias} konnte nicht kanonisiert werden; der Roh-Alias wird verwendet.", alias);
            return alias;
        }
    }
}
