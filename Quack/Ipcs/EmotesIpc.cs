using System;
using System.Collections.Generic;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets2;

namespace Quack.Ipcs; 

public class EmotesIpc: IDisposable
{
    public static readonly string LIST = "Quack.Emotes.GetList";
    private ICallGateProvider<List<Dictionary<string, string>>> GetListProvider { get; init; }

    public EmotesIpc(IDalamudPluginInterface pluginInterface, ExcelSheet<Emote>? excelSheetEmote) {
        GetListProvider = pluginInterface.GetIpcProvider<List<Dictionary<string, string>>>(LIST);
        GetListProvider.RegisterFunc(() =>
        {
            List<Dictionary<string, string>> list = [];

            foreach (var emote in excelSheetEmote!)
            {
                if (emote.EmoteCategory.Value != null && emote.TextCommand.Value != null)
                {
                    list.Add(new(){
                        { "name", emote.Name },
                        { "category", emote.EmoteCategory.Value!.Name },
                        { "command", emote.TextCommand.Value!.Command.RawString }
                    });
                }
            }
            return list;
        });
    }

    public void Dispose()
    {
        GetListProvider.UnregisterFunc();
    }
}
