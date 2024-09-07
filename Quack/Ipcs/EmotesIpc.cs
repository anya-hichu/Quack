using System;
using System.Linq;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets2;

namespace Quack.Ipcs; 

public class EmotesIpc: IDisposable
{
    public class EmoteData(string name, string category, string command)
    {
        public string name = name;
        public string category = category;
        public string command = command;
    }

    public static readonly string LIST = "Quack.Emotes.GetList";
    private ICallGateProvider<EmoteData[]> GetListProvider { get; init; }

    public EmotesIpc(IDalamudPluginInterface pluginInterface, ExcelSheet<Emote>? excelSheetEmote) {
        GetListProvider = pluginInterface.GetIpcProvider<EmoteData[]>(LIST);
        GetListProvider.RegisterFunc(() =>
        {
            return excelSheetEmote!.SelectMany<Emote, EmoteData>(e =>
            {
                if (e.EmoteCategory.Value != null && e.TextCommand.Value != null)
                {
                    return [new(e.Name, e.EmoteCategory.Value!.Name, e.TextCommand.Value!.Command.RawString)];
                } else
                {
                    return [];
                }
            }).ToArray();
        });
    }

    public void Dispose()
    {
        GetListProvider.UnregisterFunc();
    }
}
