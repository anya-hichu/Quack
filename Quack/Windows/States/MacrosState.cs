using Quack.Macros;
using Quack.Utils;
using System.Collections.Generic;
using System.Linq;
namespace Quack.Windows.States;

public class MacrosState
{
    public HashSet<TreeNode<string>> Nodes { get; init; } = new(0, new TreeNodeComparer<string>());
    public TreeNode<string> SelectedNode { get; init; } = null!;

    public MacrosState(IEnumerable<Macro> macros)
    {

        foreach (var macro in macros)
        {
            var currentSet = Nodes;
            var sep = '/';
            var parts = macro.Path.Split(sep);
            for (var take = 1; take <= parts.Length; take++)
            {
                var node = new TreeNode<string>(string.Join(sep, parts.Take(take)));
                if (currentSet.Add(node))
                {
                    currentSet = node.Children;
                }
                else
                {
                    currentSet.TryGetValue(node, out var existingNode);
                    currentSet = existingNode!.Children;
                }
            }
        }
    }
}
