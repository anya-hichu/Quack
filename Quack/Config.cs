using Dalamud.Configuration;
using Dalamud.Game.ClientState.Keys;
using JavaScriptEngineSwitcher.Core;
using JavaScriptEngineSwitcher.Jint;
using JavaScriptEngineSwitcher.V8;
using Quack.Generators;
using Quack.Macros;
using System;
using System.Collections.Generic;

namespace Quack;

[Serializable]
public class Config : IPluginConfiguration
{
    public static readonly int CURRENT_VERSION = 2;

    public static readonly VirtualKey[] MODIFIER_KEYS = [ VirtualKey.NO_KEY, VirtualKey.CONTROL, VirtualKey.SHIFT, VirtualKey.MENU ];

    public int Version { get; set; } = CURRENT_VERSION;

    public VirtualKey KeyBind { get; set; } = VirtualKey.INSERT;
    public VirtualKey KeyBindExtraModifier { get; set; } = VirtualKey.NO_KEY;

    public string GeneratorEngineName { get; set; } = V8JsEngine.EngineName;

    [ObsoleteAttribute("CommandFormat renamed to ExtraCommandFormat")]
    public string CommandFormat { get; set; } = string.Empty;
    public string ExtraCommandFormat { get; set; } = "/echo {0}";

    public List<GeneratorConfig> GeneratorConfigs { get; set; } = [];

    public HashSet<Macro> Macros { get; set; } = new(0, MacroComparer.INSTANCE);

    public event Action? OnSave;

    public Config() { 
    }

    public Config(List<GeneratorConfig> generatorConfigs)
    {
        GeneratorConfigs = generatorConfigs;
    }

    public void MigrateIfNeeded()
    {
        if (Version < CURRENT_VERSION)
        {
            if (Version < 1)
            {
                GeneratorConfigs.ForEach(c =>
                {
                    c.IpcConfigs.Add(new(c.IpcName, c.IpcArgs));
                    c.IpcName = c.IpcArgs = string.Empty;
                });
            }

            if (Version < 2)
            {
                ExtraCommandFormat = CommandFormat;
                CommandFormat = string.Empty;
            }

            Version = CURRENT_VERSION;
            Save();
        }
    }

    public void Save()
    {
        // Force removal of conflicting macro before save
        Macros = new(Macros, MacroComparer.INSTANCE);

        Plugin.PluginInterface.SavePluginConfig(this);
        OnSave?.Invoke();
    }
}
