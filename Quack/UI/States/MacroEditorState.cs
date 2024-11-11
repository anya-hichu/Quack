using Quack.Utils;
using System.Collections.Generic;

namespace Quack.UI.States;

public class MacroEditorState(HashSet<TreeNode<string>> pathNodes, string? selectedPath, string filter)
{
    public HashSet<TreeNode<string>> PathNodes { get; init; } = pathNodes;
    public string? SelectedPath { get; set; } = selectedPath;
    public string Filter { get; set; } = filter;
}
