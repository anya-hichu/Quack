using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quack.Ipcs;

// https://github.com/grittyfrog/MacroMate/blob/master/MacroMate/Extensions/Dalamud/Macros/VanillaMacroManager.cs
public unsafe class MacrosIpc : IDisposable
{
    private enum RawMacroSet : uint
    {
        INDIVIDUAL = 0,
        SHARED = 1
    }

    public static readonly string LIST = "Quack.Macros.GetList";
    private ICallGateProvider<List<Dictionary<string, string>>> GetListProvider { get; init; }

    private readonly RaptureMacroModule* raptureMacroModule;

    public MacrosIpc(IDalamudPluginInterface pluginInterface)
    {
        raptureMacroModule = RaptureMacroModule.Instance();

        GetListProvider = pluginInterface.GetIpcProvider<List<Dictionary<string, string>>>(LIST);
        GetListProvider.RegisterFunc(() =>
        {
            var list = ListMacros(RawMacroSet.INDIVIDUAL);
            list.AddRange(ListMacros(RawMacroSet.SHARED));
            return list;
        });
    }

    private List<Dictionary<string, string>> ListMacros(RawMacroSet set)
    {
        var setName = Enum.GetName(set)!.ToLower();
        return Enumerable.Range(0, 99).SelectMany<int, Dictionary<string, string>>(i =>
        {
            var rawMacro = raptureMacroModule->GetMacro((uint)set, (uint)i);
            if (raptureMacroModule->GetLineCount(rawMacro) > 0)
            {
                return [new(){
                    { "index", i.ToString()},
                    { "name", rawMacro->Name.ToString() },
                    { "set", setName },
                    { "content", string.Join("\n", rawMacro->Lines.ToArray()
                                                                  .Select(l => $"{l}")
                                                                  .Where(l => !l.IsNullOrWhitespace()) ) }
                }];
            } 
            else
            {
                return [];
            }

        }).ToList();
    }

    public void Dispose()
    {
        GetListProvider.UnregisterFunc();
    }
}
