using System.Collections.Generic;

namespace Quack.Macros;

public class MacroComparer: IEqualityComparer<Macro>
{
    public bool Equals(Macro? lfs, Macro? rhs)
    {
        if (lfs != null && rhs != null)
        {
            if (lfs.Path != null && rhs.Path != null)
            {
                return lfs.Path.Equals(rhs.Path);
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

    public int GetHashCode(Macro? obj)
    {
        if (obj != null)
        {
            if (obj.Path != null)
            {
                return obj.Path.GetHashCode();
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
