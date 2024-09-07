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
    }

    public class DesignData(string id, string name, string path, string[] tags)
    {
        public string id = id;
        public string name = name;
        public string path = path;
        public string[] tags = tags;
    }

    public static readonly string DESIGN_LIST = "Quack.Glamourer.GetDesignList";

    private IPluginLog PluginLog { get; init; }
    private string SortOrderConfigPath {  get; init; }
    private string DesignConfigPathTemplate { get; init; }

    private ICallGateSubscriber<Dictionary<string, string>> BaseGetDesignListSubscriber { get; init; }
    private ICallGateProvider<DesignData[]> GetDesignListProvider { get; init; }

    public GlamourerIpc(IDalamudPluginInterface pluginInterface, IPluginLog pluginLog)
    {
        PluginLog = pluginLog;

        BaseGetDesignListSubscriber = pluginInterface.GetIpcSubscriber<Dictionary<string, string>>("Glamourer.GetDesignList.V2");
        GetDesignListProvider = pluginInterface.GetIpcProvider<DesignData[]>(DESIGN_LIST);

        // %appdata%\xivlauncher\pluginConfigs\Glamourer\sort_order.json
        SortOrderConfigPath = Path.GetFullPath(Path.Combine(pluginInterface.GetPluginConfigDirectory(), "..\\Glamourer\\sort_order.json"));

        // %appdata%\xivlauncher\pluginConfigs\Glamourer\designs\{id}.json
        DesignConfigPathTemplate = Path.GetFullPath(Path.Combine(pluginInterface.GetPluginConfigDirectory(), "..\\Glamourer\\designs\\{0}.json"));

        GetDesignListProvider.RegisterFunc(GetDesignList);
    }

    public void Dispose() {
        GetDesignListProvider.UnregisterFunc();
    }


    private DesignData[] GetDesignList()
    {
        var designList = BaseGetDesignListSubscriber.InvokeFunc();

        if (Path.Exists(SortOrderConfigPath))
        {
            using StreamReader sortOrderConfigFile = new(SortOrderConfigPath);
            var sortOrderConfigJson = sortOrderConfigFile.ReadToEnd();
            var sortOrderConfig = JsonConvert.DeserializeObject<SortOrderConfig>(sortOrderConfigJson)!;

            return designList.Select(d =>
            {
                var designConfigPath = string.Format(DesignConfigPathTemplate, d.Key);
                if (Path.Exists(designConfigPath))
                {
                    using StreamReader designConfigFile = new(designConfigPath);
                    var designConfigJson = designConfigFile.ReadToEnd();
                    var designConfig = JsonConvert.DeserializeObject<DesignConfig>(designConfigJson)!;

                    return new DesignData(d.Key,
                                          d.Value,
                                          sortOrderConfig.Data.GetValueOrDefault(d.Key, d.Value), // Root items don't have a path
                                          designConfig.Tags);
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
