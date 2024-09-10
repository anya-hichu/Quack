using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Quack.Ipcs;

public class PenumbraIpc : IDisposable
{
    private class SortOrderConfig
    {
        [JsonProperty(Required = Required.Always)]
        public Dictionary<string, string> Data { get; set; } = [];
    }

    private class ModDataConfig
    {
        [JsonProperty(Required = Required.Always)]
        public string[] LocalTags { get; set; } = [];
    }

    public class ModData(string dir, string name, string path, string[] localTags)
    {
        public string dir = dir;
        public string name = name;
        public string path = path;
        public string[] localTags = localTags;
    }

    public static readonly string MOD_LIST = "Quack.Penumbra.GetModList";

    private IPluginLog PluginLog { get; init; }

    private string PluginConfigsDirectory { get; init; }
    private string SortOrderConfigPath {  get; init; }
    private string ModDataConfigPathTemplate { get; init; }

    private ICallGateSubscriber<Dictionary<string, string>> BaseGetModListSubscriber { get; init; }
    private ICallGateProvider<ModData[]> GetModListProvider { get; init; }

    public PenumbraIpc(IDalamudPluginInterface pluginInterface, IPluginLog pluginLog)
    {
        PluginLog = pluginLog;

        BaseGetModListSubscriber = pluginInterface.GetIpcSubscriber<Dictionary<string, string>>("Penumbra.GetModList");
        GetModListProvider = pluginInterface.GetIpcProvider<ModData[]>(MOD_LIST);

        PluginConfigsDirectory = Path.GetFullPath(Path.Combine(pluginInterface.GetPluginConfigDirectory(), ".."));

        // %appdata%\xivlauncher\pluginConfigs\Penumbra\sort_order.json
        SortOrderConfigPath = Path.GetFullPath(Path.Combine(PluginConfigsDirectory, "Penumbra\\sort_order.json"));

        // %appdata%\xivlauncher\pluginConfigs\Penumbra\mod_data\{dir}.json
        ModDataConfigPathTemplate = Path.GetFullPath(Path.Combine(PluginConfigsDirectory, "Penumbra\\mod_data\\{0}.json"));

        GetModListProvider.RegisterFunc(GetModList);
    }

    public void Dispose() {
        GetModListProvider.UnregisterFunc();
    }

    private ModData[] GetModList()
    {
        var modList = BaseGetModListSubscriber.InvokeFunc();

        if (Path.Exists(SortOrderConfigPath))
        {
            using StreamReader sortOrderReader = new(SortOrderConfigPath);
            var sortOrderConfigJson = sortOrderReader.ReadToEnd();
            var sortOrderConfig = JsonConvert.DeserializeObject<SortOrderConfig>(sortOrderConfigJson)!;
            PluginLog.Debug($"Retrieved {sortOrderConfig.Data.Count} penumbra path infos from {Path.GetRelativePath(PluginConfigsDirectory, SortOrderConfigPath)}");

            return modList.Select(d => {
                var modDataConfigPath = string.Format(ModDataConfigPathTemplate, d.Key);

                if (Path.Exists(modDataConfigPath))
                {
                    using StreamReader modDataConfigReader = new(modDataConfigPath);
                    var modDataConfigJson = modDataConfigReader.ReadToEnd();
                    var modDataConfig = JsonConvert.DeserializeObject<ModDataConfig>(modDataConfigJson)!;
                    PluginLog.Debug($"Retrieved {modDataConfig.LocalTags.Length} penumbra local tags from {Path.GetRelativePath(PluginConfigsDirectory, modDataConfigPath)}");

                    return new ModData(d.Key, 
                                       d.Value, 
                                       sortOrderConfig.Data.GetValueOrDefault(d.Key, d.Value),  // Root items don't have a path
                                       modDataConfig.LocalTags);
                }
                else
                {
                    throw new FileNotFoundException($"Failed to find penumbra local tag infos file at #{Path.GetRelativePath(PluginConfigsDirectory, SortOrderConfigPath)}");
                }
            }).ToArray();
        }
        else
        {
            throw new FileNotFoundException($"Failed to find penumbra path infos file at #{SortOrderConfigPath}");
        }
    }
}

