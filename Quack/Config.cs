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

    public int MaxMatches { get; set; } = 50;
    public string CommandFormat { get; set; } = "/echo {0}";

    public List<GeneratorConfig> GeneratorConfigs { get; set; } = [];

    public HashSet<Macro> Macros { get; set; } = new(0, new MacroComparer());

    public event Action? OnSave;

    public Config() { }

    public Config(List<GeneratorConfig> generatorConfigs)
    {
        GeneratorConfigs = generatorConfigs;
    }

    public void Save()
    {
        // Force removal of conflicting macro paths on save
        Macros = new(Macros, new MacroComparer());

        Plugin.PluginInterface.SavePluginConfig(this);
        OnSave?.Invoke();
    }
}
