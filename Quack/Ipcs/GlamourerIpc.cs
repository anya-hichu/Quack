using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Quack.Ipcs;

public class GlamourerIpc : IDisposable
{
    private class SortOrder
    {
        public Dictionary<string, string> Data { get; set; } = [];
    }

    public class DesignData(string id, string name, string path)
    {
        public string id = id;
        public string name = name;
        public string path = path;
    }

    public static readonly string DESIGN_LIST = "Quack.Glamourer.GetDesignList";

    private string SortOrderJsonPath {  get; init; }
    private ICallGateSubscriber<Dictionary<string, string>> BaseGetDesignListSubscriber { get; init; }
    private ICallGateProvider<DesignData[]> GetDesignListProvider { get; init; }

    public GlamourerIpc(IDalamudPluginInterface pluginInterface)
    {
        BaseGetDesignListSubscriber = pluginInterface.GetIpcSubscriber<Dictionary<string, string>>("Glamourer.GetDesignList.V2");
        GetDesignListProvider = pluginInterface.GetIpcProvider<DesignData[]>(DESIGN_LIST);

        // %appdata%\xivlauncher\pluginConfigs\Glamourer\sort_order.json
        SortOrderJsonPath = Path.GetFullPath(Path.Combine(pluginInterface.GetPluginConfigDirectory(), "..\\Glamourer\\sort_order.json"));

        GetDesignListProvider.RegisterFunc(GetDesignList);
    }

    public void Dispose() {
        GetDesignListProvider.UnregisterFunc();
    }


    private DesignData[] GetDesignList()
    {
        var designList = BaseGetDesignListSubscriber.InvokeFunc();

        if (Path.Exists(SortOrderJsonPath))
        {
            using StreamReader reader = new(SortOrderJsonPath);
            var json = reader.ReadToEnd();
            var sortOrder = JsonConvert.DeserializeObject<SortOrder>(json)!;

            return designList.Select(d => new DesignData(d.Key, d.Value, sortOrder.Data.GetValueOrDefault(d.Key, string.Empty))).ToArray();
        }
        else
        {
            throw new FileNotFoundException($"Failed to find glamourer path infos file at #{SortOrderJsonPath}");
        }
    }
}
