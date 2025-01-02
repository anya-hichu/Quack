using Quack.Ipcs;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quack.Generators;

[Serializable]
public class GeneratorConfig
{
    public static readonly int DEFAULTS_VERSION = 11;
    private static readonly List<GeneratorConfig> DEFAULTS = [
        new() {
            Name = $"Addresses (V{DEFAULTS_VERSION})",
            IpcConfigs = [new() { Name = LifestreamIpc.ADDRESS_LIST }],
            Script = Properties.Resources.AddressesJsContent
        },
        new() {
            Name = $"Customize Profiles (V{DEFAULTS_VERSION})", 
            IpcConfigs = [new() { Name = "CustomizePlus.Profile.GetList" }],
            Script = Properties.Resources.CustomizeProfilesJsContent
        },
        new() {
            Name = $"Custom Emotes (V{DEFAULTS_VERSION})", 
            IpcConfigs = [new() { Name = PenumbraIpc.MOD_LIST_WITH_SETTINGS }, new() { Name = EmotesIpc.LIST }], 
            Script = Properties.Resources.CustomEmotesJsContent
        },
        new() {
            Name = $"Emotes (V{DEFAULTS_VERSION})",
            IpcConfigs = [new() { Name = EmotesIpc.LIST }],
            Script = Properties.Resources.EmotesJsContent
        },
        new() {
            Name = $"Glamours (V{DEFAULTS_VERSION})",
            IpcConfigs = [new() { Name = GlamourerIpc.DESIGN_LIST }],
            Script = Properties.Resources.GlamoursJsContent
        },
        new() {
            Name = $"Honorifics (V{DEFAULTS_VERSION})",
            IpcConfigs = [new() { Name = "Honorific.GetCharacterTitleList", Args = """["Character Name", WorldId]""" }],
            Script = Properties.Resources.HonorificsJsContent
        },
        new() {
            Name = $"Jobs (V{DEFAULTS_VERSION})",
            Script = Properties.Resources.JobsJsContent
        },
        new() {
            Name = $"Macros (V{DEFAULTS_VERSION})",
            IpcConfigs = [new() { Name = MacrosIpc.LIST }, new() { Name = LocalPlayerIpc.INFO }],
            Script = Properties.Resources.MacrosJsContent
        },
        new() {
            Name = $"Mods (V{DEFAULTS_VERSION})",
            IpcConfigs = [new() { Name = PenumbraIpc.MOD_LIST }],
            Script = Properties.Resources.ModsJsContent
        },
        new() {
            Name = $"Mod Options (V{DEFAULTS_VERSION})",
            IpcConfigs = [new() { Name = PenumbraIpc.MOD_LIST_WITH_SETTINGS }],
            Script = Properties.Resources.ModOptionsJsContent
        },
        new() {
            Name = $"Moodles (V{DEFAULTS_VERSION})",
            IpcConfigs = [new() { Name = "Moodles.GetRegisteredMoodles" }],
            Script = Properties.Resources.MoodlesJsContent
        },
        new() {
            Name = $"Overrides (V{DEFAULTS_VERSION})",
            IpcConfigs = [new() { Name = CustomMacrosIpc.LIST }, new() { Name = PenumbraIpc.MOD_LIST_WITH_SETTINGS }],
            Script = Properties.Resources.OverridesJsContent
        },
        new() {
            Name = $"Plugin Collections (V{DEFAULTS_VERSION})",
            IpcConfigs = [new() { Name = DalamudIpc.PLUGIN_COLLECTION_NAME_LIST }],
            Script = Properties.Resources.PluginCollectionsJsContent
        }
    ];

    public static List<GeneratorConfig> GetDefaults()
    {
        return new(DEFAULTS.Select(c => c.Clone()));
    }

    public string Name { get; set; } = string.Empty;

    #region deprecated
    [ObsoleteAttribute($"IpcName deprecated to support multiple ipcs in config version 1")]
    public string IpcName { get; set; } = string.Empty;

    [ObsoleteAttribute($"IpcArgs deprecated to support multiple ipcs in config version 1")]
    public string IpcArgs { get; set; } = string.Empty;
    #endregion

    public List<GeneratorIpcConfig> IpcConfigs { get; set; } = [];

    public string Script { get; set; } = string.Empty;
    public bool AwaitDebugger { get; set; } = false;

    public GeneratorConfig Clone()
    {
        return new()
        {
            Name = Name,
            IpcConfigs = new(IpcConfigs.Select(c => c.Clone())),
            Script = Script,
            AwaitDebugger = AwaitDebugger
        };
    }
}
