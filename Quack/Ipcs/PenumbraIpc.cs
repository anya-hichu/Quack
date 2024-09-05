using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Quack.Ipcs;

public class PenumbraIpc : IDisposable
{
    private class SortOrder
    {
        public Dictionary<string, string> Data { get; set; } = [];
    }

    public class ModData(string dir, string name, string path)
    {
        public string dir = dir;
        public string name = name;
        public string path = path;
    }

    public static readonly string MOD_LIST = "Quack.Penumbra.GetModList";

    private string SortOrderJsonPath {  get; init; }
    private ICallGateSubscriber<Dictionary<string, string>> BaseGetModListSubscriber { get; init; }
    private ICallGateProvider<ModData[]> GetModListProvider { get; init; }

    public PenumbraIpc(IDalamudPluginInterface pluginInterface)
    {
        BaseGetModListSubscriber = pluginInterface.GetIpcSubscriber<Dictionary<string, string>>("Penumbra.GetModList");
        GetModListProvider = pluginInterface.GetIpcProvider<ModData[]>(MOD_LIST);

        // %appdata%\xivlauncher\pluginConfigs\Penumbra\sort_order.json
        SortOrderJsonPath = Path.GetFullPath(Path.Combine(pluginInterface.GetPluginConfigDirectory(), "..\\Penumbra\\sort_order.json"));

        GetModListProvider.RegisterFunc(GetModList);
    }

    public void Dispose() {
        GetModListProvider.UnregisterFunc();
    }


    private ModData[] GetModList()
    {
        var modList = BaseGetModListSubscriber.InvokeFunc();

        if (Path.Exists(SortOrderJsonPath))
        {
            using StreamReader reader = new(SortOrderJsonPath);
            var json = reader.ReadToEnd();
            var sortOrder = JsonConvert.DeserializeObject<SortOrder>(json)!;

            return modList.Select(d => new ModData(d.Key, d.Value, sortOrder.Data.GetValueOrDefault(d.Key, string.Empty))).ToArray();
        }
        else
        {
            throw new FileNotFoundException($"Failed to find penumbra path infos file at #{SortOrderJsonPath}");
        }
    }
}
