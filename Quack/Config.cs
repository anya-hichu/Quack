using Dalamud.Configuration;
using Quack.Generators;
using System;
using System.Collections.Generic;

namespace Quack;

[Serializable]
public class Config : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public int MaxSearchResults { get; set; } = 20;
    public string CommandFormat { get; set; } = "/echo {0}";

    public List<GeneratorConfig> GeneratorConfigs { get; set; } = [];

    public List<Macro> Macros { get; set; } = [];

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
