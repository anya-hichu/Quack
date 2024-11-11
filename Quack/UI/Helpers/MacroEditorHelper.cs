
using Quack.Macros;
using Quack.Utils;
using System.Collections.Generic;
using System.Linq;

namespace Quack.UI.Helpers;

public static class MacroEditorHelper
{
    public static HashSet<TreeNode<string>> BuildPathNodes(IEnumerable<Macro> macros)
    {
        var pathNodes = new HashSet<TreeNode<string>>(0, new TreeNodeComparer<string>());
        foreach (var macro in macros)
        {
            var current = pathNodes;
            var sep = '/';
            var parts = macro.Path.Split(sep);
            for (var take = 1; take <= parts.Length; take++)
            {
                var node = new TreeNode<string>(string.Join(sep, parts.Take(take)));
                if (current.Add(node))
                {
                    current = node.Children;
                }
                else
                {
                    current.TryGetValue(node, out var existingNode);
                    current = existingNode!.Children;
                }
            }
        }
        return pathNodes;
    }
}
