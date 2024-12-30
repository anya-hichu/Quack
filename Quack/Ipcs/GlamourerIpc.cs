using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Quack.Ipcs;

public class GlamourerIpc : IDisposable
{
    private class SortOrderConfig
    {
        [JsonProperty(Required = Required.Always)]
        public Dictionary<string, string> Data { get; set; } = [];
    }

    private class DesignConfig
    {
        [JsonProperty(Required = Required.Always)]
        public string[] Tags { get; set; } = [];
        [JsonProperty(Required = Required.Always)]
        public string Color { get; set; } = string.Empty;
    }

    public static readonly string DESIGN_LIST = "Quack.Glamourer.GetDesignList";

    private IPluginLog PluginLog { get; init; }

    private string PluginConfigsDirectory { get; init; }
    private string SortOrderConfigPath {  get; init; }
    private string DesignConfigPathTemplate { get; init; }

    private ICallGateSubscriber<Dictionary<string, string>> BaseGetDesignListSubscriber { get; init; }
    private ICallGateProvider<Dictionary<string, object>[]> GetDesignListProvider { get; init; }

    public GlamourerIpc(IDalamudPluginInterface pluginInterface, IPluginLog pluginLog)
    {
        PluginLog = pluginLog;

        BaseGetDesignListSubscriber = pluginInterface.GetIpcSubscriber<Dictionary<string, string>>("Glamourer.GetDesignList.V2");
        GetDesignListProvider = pluginInterface.GetIpcProvider<Dictionary<string, object>[]>(DESIGN_LIST);

        PluginConfigsDirectory = Path.GetFullPath(Path.Combine(pluginInterface.GetPluginConfigDirectory(), ".."));

        // %appdata%\xivlauncher\pluginConfigs\Glamourer\sort_order.json
        SortOrderConfigPath = Path.Combine(PluginConfigsDirectory, "Glamourer\\sort_order.json");

        // %appdata%\xivlauncher\pluginConfigs\Glamourer\designs\{id}.json
        DesignConfigPathTemplate = Path.Combine(PluginConfigsDirectory, "Glamourer\\designs\\{0}.json");

        GetDesignListProvider.RegisterFunc(GetDesignList);
    }

    public void Dispose() {
        GetDesignListProvider.UnregisterFunc();
    }

    private Dictionary<string, object>[] GetDesignList()
    {
        var designList = BaseGetDesignListSubscriber.InvokeFunc();

        if (Path.Exists(SortOrderConfigPath))
        {
            using StreamReader sortOrderConfigFile = new(SortOrderConfigPath);
            var sortOrderConfigJson = sortOrderConfigFile.ReadToEnd();
            var sortOrderConfig = JsonConvert.DeserializeObject<SortOrderConfig>(sortOrderConfigJson)!;
            PluginLog.Debug($"Retrieved {sortOrderConfig.Data.Count} glamourer path infos from {Path.GetRelativePath(PluginConfigsDirectory, SortOrderConfigPath)}");

            return designList.Select(d =>
            {
                var designConfigPath = string.Format(DesignConfigPathTemplate, d.Key);
                if (Path.Exists(designConfigPath))
                {
                    using StreamReader designConfigFile = new(designConfigPath);
                    var designConfigJson = designConfigFile.ReadToEnd();
                    var designConfig = JsonConvert.DeserializeObject<DesignConfig>(designConfigJson)!;

                    PluginLog.Debug($"Retrieved {designConfig.Tags.Length} glamourer tags from {Path.GetRelativePath(PluginConfigsDirectory, designConfigPath)}");

                    return new Dictionary<string, object>() {
                        { "id", d.Key},
                        { "name", d.Value},
                        { "path", sortOrderConfig.Data.GetValueOrDefault(d.Key, d.Value)}, // Root items don't have a path
                        { "tags", designConfig.Tags },
                        { "color", designConfig.Color }
                    };
                }
                else
                {
                    throw new FileNotFoundException($"Failed to find glamourer tag infos file at #{designConfigPath}");
                }
            }).ToArray();

        }
        else
        {
            throw new FileNotFoundException($"Failed to find glamourer path infos file at #{SortOrderConfigPath}");
        }
    }
}
