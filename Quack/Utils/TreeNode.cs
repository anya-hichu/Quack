using System.Collections.Generic;

namespace Quack.Utils;

public class TreeNode<T>(T node)
{
    public T Node { get; init; } = node;
    public HashSet<TreeNode<T>> ChildNodes { get; init; } = new(0, TreeNodeComparer<T>.INSTANCE);
}
