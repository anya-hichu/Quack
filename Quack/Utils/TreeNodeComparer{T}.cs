using System.Collections.Generic;

namespace Quack.Utils;

public class TreeNodeComparer<T> : IEqualityComparer<TreeNode<T>>
{
    public bool Equals(TreeNode<T>? lfs, TreeNode<T>? rhs)
    {
        if (lfs != null && rhs != null)
        {
            if (lfs.Item != null && rhs.Item != null)
            {
                return lfs.Item.Equals(rhs.Item);
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
            if (obj.Item != null)
            {
                return obj.Item.GetHashCode();
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
