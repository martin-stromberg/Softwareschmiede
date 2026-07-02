using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Softwareschmiede.App.Extensions;

/// <summary>Erweiterungsmethoden für <see cref="ObservableCollection{T}"/>.</summary>
public static class ObservableCollectionExtensions
{
    /// <summary>Ersetzt den gesamten Inhalt der Collection durch die übergebenen Elemente.</summary>
    /// <typeparam name="T">Elementtyp der Collection.</typeparam>
    /// <param name="collection">Die zu ersetzende Collection.</param>
    /// <param name="items">Die neuen Elemente.</param>
    public static void ReplaceAll<T>(this ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();
        foreach (var item in items)
            collection.Add(item);
    }
}
