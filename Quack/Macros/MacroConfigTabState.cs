using Quack.Collections;
using Quack.UI;
using Quack.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quack.Macros;

public class MacroConfigTabState : IDisposable
{
    private static readonly char PATH_SEPARATOR = '/';

    private HashSet<Macro> CachedMacros { get; init; }
    private MacroTable MacroTable { get; init; }
    private UIEvents UIEvents { get; init; }

    public string? MaybeConflictPath { get; set; }
    public HashSet<TreeNode<string>> PathNodes { get; private set; } = new(0, TreeNodeComparer<string>.INSTANCE);
    public HashSet<Macro> SelectedMacros { get; set; } = new(0, MacroComparer.INSTANCE);
    public string Filter { get; set; } = string.Empty;
    public CollectionConfig? MaybeCollectionConfig { get; set; }

    public MacroConfigTabState(HashSet<Macro> cachedMacros, MacroTable macroTable, UIEvents uiEvents) {
        CachedMacros = cachedMacros;
        MacroTable = macroTable;
        UIEvents = uiEvents;

        Update();
        MacroTable.OnChange += Update;
        UIEvents.OnMacroEditRequest += OnMacroEditRequest;
        UIEvents.OnCollectionConfigTagsUpdate += OnCollectionConfigTagsUpdate;
    }

    public void Dispose()
    {
        UIEvents.OnCollectionConfigTagsUpdate -= OnCollectionConfigTagsUpdate;
        UIEvents.OnMacroEditRequest -= OnMacroEditRequest;
        MacroTable.OnChange -= Update;
    }

    public void Update()
    {
        var pathNodes = new HashSet<TreeNode<string>>(0, TreeNodeComparer<string>.INSTANCE);
        var macros = MacroSearch.Lookup(CachedMacros, Filter);

        if (MaybeCollectionConfig != null)
        {
            macros = new(macros.Where(MaybeCollectionConfig.Matches));
        }

        foreach (var macro in macros)
        {
            var current = pathNodes;
            var parts = macro.Path.Split(PATH_SEPARATOR);
            for (var take = 1; take <= parts.Length; take++)
            {
                var newNode = new TreeNode<string>(string.Join(PATH_SEPARATOR, parts.Take(take)));
                if (current.TryGetValue(newNode, out var existingNode))
                {
                    current = existingNode.ChildNodes;
                }
                else
                {
                    current.Add(newNode);
                    current = newNode.ChildNodes;
                }
            }
        }
        PathNodes = pathNodes;
    }

    private void OnMacroEditRequest(Macro macro)
    {
        SelectedMacros = new([macro], MacroComparer.INSTANCE);
    }

    private void OnCollectionConfigTagsUpdate(CollectionConfig collectionConfig)
    {
        if (collectionConfig == MaybeCollectionConfig)
        {
            Update();
        }
    }
}
