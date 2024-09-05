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
    public class MacroData(int index, string name, uint set, string content)
    {
        public int index = index;
        public string name = name;
        public uint set = set;
        public string content = content;
    }

    public static readonly string LIST = "Quack.Macros.GetList";
    private ICallGateProvider<MacroData[]> GetListProvider { get; init; }

    private readonly RaptureMacroModule* raptureMacroModule;

    public MacrosIpc(IDalamudPluginInterface pluginInterface)
    {
        raptureMacroModule = RaptureMacroModule.Instance();

        GetListProvider = pluginInterface.GetIpcProvider<MacroData[]>(LIST);
        GetListProvider.RegisterFunc(() =>
        {
            var list = ListMacros(0);
            list.AddRange(ListMacros(1));
            return list.ToArray();
        });
    }

    private List<MacroData> ListMacros(uint set)
    {
        return Enumerable.Range(0, 99).SelectMany<int, MacroData>(i =>
        {
            var rawMacro = raptureMacroModule->GetMacro(set, (uint)i);
            if (raptureMacroModule->GetLineCount(rawMacro) > 0)
            {
                return [
                    new(i,
                        rawMacro->Name.ToString(),
                        set,
                        string.Join("\n", rawMacro->Lines.ToArray()
                                                         .Select(l => $"{l}")
                                                         .Where(l => !l.IsNullOrWhitespace()) )
                    )
                ];
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
