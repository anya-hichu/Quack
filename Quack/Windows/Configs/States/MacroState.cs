using Quack.Macros;
using Quack.Utils;
using System.Collections.Generic;
using System.Linq;

namespace Quack.Windows.Configs.States;

public class MacroState(HashSet<TreeNode<string>> pathNodes, string? selectedPath, string filter)
{
    public HashSet<TreeNode<string>> PathNodes { get; init; } = pathNodes;
    public string? SelectedPath { get; set; } = selectedPath;
    public string Filter { get; set; } = filter;

    public static HashSet<TreeNode<string>> GeneratePathNodes(IEnumerable<Macro> macros)
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
