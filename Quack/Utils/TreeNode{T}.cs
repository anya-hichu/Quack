using System.Collections.Generic;

namespace Quack.Utils;

public class TreeNode<T>(T item)
{
    public HashSet<TreeNode<T>> Children { get; init; } = new(0, new TreeNodeComparer<T>());

    public T Item { get; init; } = item;

    public TreeNode<T> AddChild(T item)
    {
        var nodeItem = new TreeNode<T>(item);
        Children.Add(nodeItem);
        return nodeItem;
    }
}
