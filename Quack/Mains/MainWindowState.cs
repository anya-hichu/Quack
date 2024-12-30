using Dalamud.Utility;
using Quack.Collections;
using Quack.Macros;
using Quack.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quack.Mains;

public class MainWindowState : IDisposable
{
    private MacroTable MacroTable { get; init; }
    private UIEvents UIEvents { get; init; }

    public string Query { get; set; } = string.Empty;
    public CollectionConfig? MaybeCollectionConfig { get; set; }
    public HashSet<Macro> FilteredMacros { get; set; } = [];

    public MainWindowState(MacroTable macroTable, UIEvents uiEvents)
    {
        MacroTable = macroTable;
        UIEvents = uiEvents;

        Update();
        MacroTable.OnChange += Update;
        UIEvents.OnCollectionConfigTagsEdit += OnTagsEdit;
    }

    public void Dispose()
    {
        UIEvents.OnCollectionConfigTagsEdit -= OnTagsEdit;
        MacroTable.OnChange -= Update;
    }

    public void Update()
    {
        if (MaybeCollectionConfig == null)
        {
            FilteredMacros = MacroTable.Search(Query);
        } 
        else
        {
            var collectionFilters = MaybeCollectionConfig.Tags.Select(tag => $"tags:{tag}");
            var AllFilters = Query.IsNullOrWhitespace() ? collectionFilters : collectionFilters.Prepend(Query);
            var query = string.Join(" AND ", AllFilters);
            var macros = MacroTable.Search(query);

            // Refilter to exclude partial matches (done in 2 steps for performance reasons)
            FilteredMacros = new(macros.Where(MaybeCollectionConfig.Matches), MacroComparer.INSTANCE);
        }
    }

    private void OnTagsEdit(CollectionConfig collectionConfig)
    {
        if (collectionConfig == MaybeCollectionConfig)
        {
            Update();
        }
    }
}
