using Quack.Macros;
using System.Collections.Generic;

namespace Quack.Windows.States;

public class GeneratorConfigState
{
    public HashSet<Macro> GeneratedMacros { get; set; } = new(0, MacroComparer.INSTANCE);
    public HashSet<Macro> SelectedGeneratedMacros { get; set; } = new(0, MacroComparer.INSTANCE);
    public string GeneratedMacrosFilter { get; set; } = string.Empty;
    public HashSet<Macro> FilteredGeneratedMacros { get; set; } = new(0, MacroComparer.INSTANCE);
}
