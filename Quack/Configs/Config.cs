using Dalamud.Configuration;
using Dalamud.Game.ClientState.Keys;
using Quack.Generators;
using Quack.Macros;
using Quack.Schedulers;
using System;
using System.Collections.Generic;

namespace Quack.Configs;

[Serializable]
public class Config : IPluginConfiguration
{
    public static readonly int CURRENT_VERSION = 6;

    public int Version { get; set; } = CURRENT_VERSION;

    public VirtualKey KeyBind { get; set; } = VirtualKey.INSERT;
    public VirtualKey KeyBindExtraModifier { get; set; } = VirtualKey.NO_KEY;

    public string ExtraCommandFormat { get; set; } = "/echo {0}";

    public List<GeneratorConfig> GeneratorConfigs { get; set; } = GeneratorConfig.GetDefaults();

    public List<SchedulerConfig> SchedulerConfigs { get; set; } = [];

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }

    #region deprecated
    [Obsolete("CommandFormat renamed to ExtraCommandFormat in config version 2")]
    public string CommandFormat { get; set; } = string.Empty;

    [Obsolete("Macros migrated to sqlite db with full text search in config version 3")]
    public HashSet<Macro> Macros { get; set; } = [];
    #endregion
}
