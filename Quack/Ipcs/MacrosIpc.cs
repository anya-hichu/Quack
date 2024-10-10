using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quack.Ipcs;

public unsafe class MacrosIpc : IDisposable
{
    public static readonly string LIST = "Quack.Macros.GetList";
    private ICallGateProvider<Dictionary<string, object>[]> GetListProvider { get; init; }

    private readonly RaptureMacroModule* raptureMacroModule;

    public MacrosIpc(IDalamudPluginInterface pluginInterface)
    {
        raptureMacroModule = RaptureMacroModule.Instance();

        GetListProvider = pluginInterface.GetIpcProvider<Dictionary<string, object>[]>(LIST);
        GetListProvider.RegisterFunc(ListMacros);
    }

    public void Dispose()
    {
        GetListProvider.UnregisterFunc();
    }

    private Dictionary<string, object>[] ListMacros()
    {
        return ListSetMacros(0).Union(ListSetMacros(1)).ToArray();
    }

    private IEnumerable<Dictionary<string, object>> ListSetMacros(uint set)
    {
        return Enumerable.Range(0, 99).SelectMany<int, Dictionary<string, object>>(i =>
        {
            var rawMacro = raptureMacroModule->GetMacro(set, (uint)i);
            if (raptureMacroModule->GetLineCount(rawMacro) > 0)
            {
                return [new()
                {
                    { "index", i },
                    { "name", rawMacro->Name.ToString() },
                    { "set", set },
                    { "content", string.Join("\n", rawMacro->Lines.ToArray()
                                                         .Select(l => l.ToString())
                                                         .Where(l => !l.IsNullOrWhitespace())) }
                }];
            }
            else
            {
                return [];
            }
        });
    }


}
