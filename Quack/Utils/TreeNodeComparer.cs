using System.Collections.Generic;

namespace Quack.Utils;

public class TreeNodeComparer<T> : IEqualityComparer<TreeNode<T>>
{
    public static readonly TreeNodeComparer<T> INSTANCE = new();
    private TreeNodeComparer()
    {
    }

    public bool Equals(TreeNode<T>? lfs, TreeNode<T>? rhs)
    {
        if (lfs != null && rhs != null)
        {
            if (lfs.Node != null && rhs.Node != null)
            {
                return lfs.Node.Equals(rhs.Node);
            }
            else
            {
                return true;
            }
        }
        else
        {
            return lfs == rhs;
        }
    }

    public int GetHashCode(TreeNode<T>? obj)
    {
        if (obj != null)
        {
            if (obj.Node != null)
            {
                return obj.Node.GetHashCode();
            }
            else
            {
                return obj.GetHashCode();
            }
        }
        else
        {
            return 0;
        }
    }
}
