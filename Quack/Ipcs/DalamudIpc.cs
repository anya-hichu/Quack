using Dalamud;
using Dalamud.Plugin;
using Dalamud.Plugin.Internal.Profiles;
using Dalamud.Plugin.Ipc;
using System;
using System.Linq;

namespace Quack.Ipcs;

public class DalamudIpc : IDisposable
{
    public static readonly string PLUGIN_COLLECTION_NAME_LIST = "Quack.Dalamud.PluginCollections.GetNameList";

    private ICallGateProvider<string[]> GetPluginCollectionNameListProvider { get; init; }

    public DalamudIpc(IDalamudPluginInterface pluginInterface)
    {
        GetPluginCollectionNameListProvider = pluginInterface.GetIpcProvider<string[]>(PLUGIN_COLLECTION_NAME_LIST);
        GetPluginCollectionNameListProvider.RegisterFunc(GetPluginCollectionNameList);
    }

    public void Dispose()
    {
        GetPluginCollectionNameListProvider.UnregisterFunc();
    }

    private string[] GetPluginCollectionNameList()
    {
        return Service<ProfileManager>.Get().Profiles.Where(p => !p.IsDefaultProfile).Select(p => p.Name).ToArray();
    }
}
