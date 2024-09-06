using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Quack.Utils;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Quack.Macros;

public partial class Executor(ServerChat serverChat, IPluginLog pluginLog)
{
    [GeneratedRegexAttribute(@"<wait\.(\d+)>")]
    private static partial Regex WaitTimeGeneratedRegex();

    private ServerChat ServerChat { get; init; } = serverChat;
    private IPluginLog PluginLog { get; init; } = pluginLog;

    public void RunAsync(Macro macro, string format = "{0}")
    {
        PluginLog.Debug($"Executing macro {macro.Name} ({macro.Path}) in task with content: {macro.Content}");
        Task.Run(() =>
        {
            foreach (var command in macro.Content.Split("\n"))
            {
                if (!command.IsNullOrWhitespace())
                {
                    var commandWithoutWait = WaitTimeGeneratedRegex().Replace(command, string.Empty);

                    ServerChat.SendMessage(string.Format(new PMFormatter(), format, commandWithoutWait));

                    var waitTimeMatch = WaitTimeGeneratedRegex().Match(command);
                    if (waitTimeMatch != null && waitTimeMatch.Success)
                    {
                        var waitTimeValue = waitTimeMatch.Groups[1].Value;
                        PluginLog.Debug($"Pausing execution inside macro {macro.Name} ({macro.Path}) for {waitTimeValue} sec(s) to respect {waitTimeMatch.Value}");
                        Thread.Sleep(int.Parse(waitTimeValue) * 1000);
                    }
                }
            }
        });
    }
}
