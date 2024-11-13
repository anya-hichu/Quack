using Quack.Utils;
using System.Collections.Generic;

namespace Quack.Macros;

public class MacroConfigTabState
{
    public HashSet<TreeNode<string>> PathNodes { get; set; } = [];
    public HashSet<Macro> SelectedMacros { get; set; } = [];
    public string Filter { get; set; } = string.Empty;
}
