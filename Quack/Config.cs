using Dalamud.Configuration;
using Dalamud.Game.ClientState.Keys;
using JavaScriptEngineSwitcher.V8;
using Quack.Generators;
using Quack.Macros;
using Quack.Schedulers;
using System;
using System.Collections.Generic;

namespace Quack;

[Serializable]
public class Config : IPluginConfiguration
{
    public static readonly int CURRENT_VERSION = 6;

    public static readonly VirtualKey[] MODIFIER_KEYS = [ VirtualKey.NO_KEY, VirtualKey.CONTROL, VirtualKey.SHIFT, VirtualKey.MENU ];

    public int Version { get; set; } = CURRENT_VERSION;

    public VirtualKey KeyBind { get; set; } = VirtualKey.INSERT;
    public VirtualKey KeyBindExtraModifier { get; set; } = VirtualKey.NO_KEY;

    public string GeneratorEngineName { get; set; } = V8JsEngine.EngineName;

    [ObsoleteAttribute("CommandFormat renamed to ExtraCommandFormat in config version 2")]
    public string CommandFormat { get; set; } = string.Empty;
    public string ExtraCommandFormat { get; set; } = "/echo {0}";

    public List<GeneratorConfig> GeneratorConfigs { get; set; } = [];

    [ObsoleteAttribute("Macros migrated to sqlite db with full text search in config version 3")]
    public HashSet<Macro> Macros { get; set; } = [];
    public List<SchedulerConfig> SchedulerConfigs { get; set; } = [];

    public Config() { 
    }

    public Config(List<GeneratorConfig> generatorConfigs)
    {
        GeneratorConfigs = generatorConfigs;
    }

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
