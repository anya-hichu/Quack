using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Utility;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets2;

namespace Quack.Ipcs; 

public class EmotesIpc: IDisposable
{
    // TODO: Find a way to not hardcode the pose keys
    private static readonly Dictionary<string, string[]> POSE_KEYS_BY_TEXT_COMMAND = new() {
        { "/sit", ["emote/s_pose00_loop", "emote/s_pose01_loop", "emote/s_pose02_loop", "emote/s_pose03_loop", "emote/s_pose04_loop" ] },
        { "/groundsit", ["emote/j_pose00_loop", "emote/j_pose01_loop", "emote/j_pose02_loop", "emote/j_pose03_loop"] },
        { "/doze", ["emote/l_pose00_loop", "emote/l_pose01_loop", "emote/l_pose02_loop"] }
    };

    public class EmoteData(string name, string category, string command, string[] actionTimelineKeys, string[] poseKeys)
    {
        public string name = name;
        public string category = category;
        public string command = command;
        public string[] actionTimelineKeys = actionTimelineKeys;
        public string[] poseKeys = poseKeys;
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
                    var actionTimelineKeys = e.ActionTimeline.SelectMany<LazyRow<ActionTimeline>, string>(a =>
                    {
                        var key = a.Value?.Key?.RawString;
                        return key.IsNullOrEmpty() ? [] : [key];
                    }).ToArray();

                    var textCommandValue = e.TextCommand.Value;
                    var shortCommand = textCommandValue.ShortCommand.RawString;
                    var command = shortCommand.IsNullOrEmpty() ? textCommandValue.Command.RawString : shortCommand;

                    var poseKeys = POSE_KEYS_BY_TEXT_COMMAND.GetValueOrDefault(command, []);
                    return [new(e.Name, e.EmoteCategory.Value!.Name, command, actionTimelineKeys, poseKeys)];
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
