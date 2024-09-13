using Dalamud.Configuration;
using Dalamud.Game.ClientState.Keys;
using Quack.Generators;
using Quack.Macros;
using System;
using System.Collections.Generic;

namespace Quack;

[Serializable]
public class Config : IPluginConfiguration
{
    public static readonly VirtualKey[] MODIFIER_KEYS = [VirtualKey.NO_KEY, VirtualKey.CONTROL, VirtualKey.SHIFT, VirtualKey.MENU];

    public int Version { get; set; } = 0;

    public VirtualKey KeyBind { get; set; } = VirtualKey.INSERT;
    public VirtualKey KeyBindExtraModifier { get; set; } = VirtualKey.NO_KEY;

    public int MaxMatches { get; set; } = 50;
    public string CommandFormat { get; set; } = "/echo {0}";

    public List<GeneratorConfig> GeneratorConfigs { get; set; } = [];

    public HashSet<Macro> Macros { get; set; } = new(0, MacroComparer.INSTANCE);

    public event Action? OnSave;

    public Config() { }

    public Config(List<GeneratorConfig> generatorConfigs)
    {
        GeneratorConfigs = generatorConfigs;
    }

    public void Save()
    {
        // Force removal of conflicting macro before save
        Macros = new(Macros, MacroComparer.INSTANCE);

        Plugin.PluginInterface.SavePluginConfig(this);
        OnSave?.Invoke();
    }
}
