using Dalamud.Plugin.Ipc.Internal;
using Dalamud.Plugin.Services;
using Quack.Macros;
using System.Collections.Generic;

namespace Quack.Generators;

public class GeneratorConfigTabState(GeneratorConfig generatorConfig, IPluginLog pluginLog)
{
    public Generator Generator { get; set; } = new(generatorConfig, pluginLog);
    public GeneratorException? MaybeGeneratorException { get; set; }
    public HashSet<Macro> GeneratedMacros { get; set; } = new(0, MacroComparer.INSTANCE);

    public HashSet<Macro> SelectedGeneratedMacros { get; set; } = new(0, MacroComparer.INSTANCE);
    public string GeneratedMacrosFilter { get; set; } = string.Empty;
    public bool ShowSelectedOnly { get; set; } = false;
    public HashSet<Macro> FilteredGeneratedMacros { get; set; } = new(0, MacroComparer.INSTANCE);
}
