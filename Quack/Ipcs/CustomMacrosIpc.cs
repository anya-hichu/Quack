using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Quack.Macros;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quack.Ipcs;

public class CustomMacrosIpc : IDisposable
{
    public static readonly string LIST = "Quack.CustomMacros.GetList";

    private HashSet<Macro> CachedMacros { get; init; }
    private ICallGateProvider<Dictionary<string, object>[]> GetListProvider { get; init; }

    public CustomMacrosIpc(HashSet<Macro> cachedMacros, IDalamudPluginInterface pluginInterface)
    {
        CachedMacros = cachedMacros;
        GetListProvider = pluginInterface.GetIpcProvider<Dictionary<string, object>[]>(LIST);

        GetListProvider.RegisterFunc(GetList);
    }

    public void Dispose()
    {
        GetListProvider.UnregisterFunc();
    }

    private Dictionary<string, object>[] GetList()
    {
        return CachedMacros.Select(m =>
        {
            return new Dictionary<string, object>() {
                {"name", m.Name },
                {"path", m.Path },
                {"command", m.Command },
                {"args", m.Args },
                {"tags", m.Tags },
                {"content", m.Content },
                {"loop", m.Loop },
            };
        }).ToArray();
    }
}
