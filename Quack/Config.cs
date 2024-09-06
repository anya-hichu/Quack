using Dalamud.Configuration;
using Quack.Generators;
using Quack.Macros;
using System;
using System.Collections.Generic;

namespace Quack;

[Serializable]
public class Config : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public int MaxSearchResults { get; set; } = 50;
    public string CommandFormat { get; set; } = "/echo {0}";

    public List<GeneratorConfig> GeneratorConfigs { get; set; } = [];

    public HashSet<Macro> Macros { get; set; } = new(0, new MacroComparer());

    public Config() { }

    public Config(List<GeneratorConfig> generatorConfigs)
    {
        GeneratorConfigs = generatorConfigs;
    }

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
