using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Utility;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quack.Ipcs; 

public class EmotesIpc: IDisposable
{
    // TODO: Find a way to not hardcode the pose keys
    private static readonly Dictionary<string, string[]> POSE_KEYS_BY_TEXT_COMMAND = new() {
        { "/sit", ["emote/s_pose00_loop", "emote/s_pose01_loop", "emote/s_pose02_loop", "emote/s_pose03_loop", "emote/s_pose04_loop" ] },
        { "/groundsit", ["emote/j_pose00_loop", "emote/j_pose01_loop", "emote/j_pose02_loop", "emote/j_pose03_loop"] },
        { "/doze", ["emote/l_pose00_loop", "emote/l_pose01_loop", "emote/l_pose02_loop"] }
    };

    public static readonly string LIST = "Quack.Emotes.GetList";

    private ExcelSheet<Emote> EmoteSheet { get; init; }
    private ICallGateProvider<Dictionary<string, object>[]> GetListProvider { get; init; }

    public EmotesIpc(IDalamudPluginInterface pluginInterface, ExcelSheet<Emote> emoteSheet) {
        EmoteSheet = emoteSheet;

        GetListProvider = pluginInterface.GetIpcProvider<Dictionary<string, object>[]>(LIST);
        GetListProvider.RegisterFunc(GetList);
    }

    public void Dispose()
    {
        GetListProvider.UnregisterFunc();
    }

    public Dictionary<string, object>[] GetList()
    {
        return EmoteSheet.SelectMany<Emote, Dictionary<string, object>>(e => {
            if (e.EmoteCategory.IsValid && e.TextCommand.IsValid)
            {
                var actionTimelineKeys = e.ActionTimeline.SelectMany<RowRef<ActionTimeline>, string>(t => !t.IsValid || t.Value.Key.IsEmpty ? [] : [t.Value.Key.ToString()]).ToArray();

                var textCommandValue = e.TextCommand.Value;
                var shortCommand = textCommandValue.ShortCommand.ToString();
                var command = shortCommand.IsNullOrEmpty() ? textCommandValue.Command.ToString() : shortCommand;

                var poseKeys = POSE_KEYS_BY_TEXT_COMMAND.GetValueOrDefault(command, []);

                return [new() {
                    { "name", e.Name.ToString() },
                    { "category",  e.EmoteCategory.Value.Name.ToString() },
                    { "command", command },
                    { "actionTimelineKeys", actionTimelineKeys },
                    { "poseKeys", poseKeys }
                }];
            }
            else
            {
                return [];
            }
        }).ToArray();
    }
}
