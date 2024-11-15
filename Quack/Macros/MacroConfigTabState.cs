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

    public string? MaybeConflictPath { get; set; }
    public HashSet<TreeNode<string>> PathNodes { get; private set; } = new(0, TreeNodeComparer<string>.INSTANCE);
    public HashSet<Macro> SelectedMacros { get; set; } = new(0, MacroComparer.INSTANCE);
    public string Filter { get; set; } = string.Empty;

    public MacroConfigTabState(HashSet<Macro> cachedMacros, MacroTable macroTable) {
        CachedMacros = cachedMacros;
        MacroTable = macroTable;

        Update();
        MacroTable.OnChange += Update;
    }

    public void Dispose()
    {
        MacroTable.OnChange -= Update;
    }

    public void Update()
    {
        var pathNodes = new HashSet<TreeNode<string>>(0, TreeNodeComparer<string>.INSTANCE);
        foreach (var macro in MacroSearch.Lookup(CachedMacros, Filter))
        {
            var current = pathNodes;
            var parts = macro.Path.Split(PATH_SEPARATOR, StringSplitOptions.RemoveEmptyEntries);
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
}
