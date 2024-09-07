using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Quack.Utils;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Quack.Macros;

public partial class Executor : IDisposable
{
    [GeneratedRegexAttribute(@"<wait\.(\d+)>")]
    private static partial Regex WaitTimeGeneratedRegex();
    
    private IFramework Framework { get; init; }
    private Chat Chat { get; init; }
    private IPluginLog PluginLog { get; init; }
    private Queue<string> PendingMessages { get; init; } = new();

    public Executor(IFramework framework, Chat chat, IPluginLog pluginLog)
    {
        Framework = framework;
        Chat = chat;
        PluginLog = pluginLog;


        Framework.Update += OnUpdate;
    }

    public void Dispose()
    {
        Framework.Update -= OnUpdate;
    }

    public void OnUpdate(IFramework framework)
    {
        while(PendingMessages.TryDequeue(out var message))
        {
            Chat.SendMessage(message);
        }
    }

    public void EnqueueMessagesAsync(Macro macro, string format = "{0}")
    {
        Task.Run(() => {
            PluginLog.Debug($"Executing macro {macro.Name} ({macro.Path}) with content: {macro.Content}");
            foreach (var command in macro.Content.Split("\n"))
            {
                if (!command.IsNullOrWhitespace())
                {
                    var commandWithoutWait = WaitTimeGeneratedRegex().Replace(command, string.Empty);
                    var message = string.Format(new PMFormatter(), format, commandWithoutWait);

                    PendingMessages.Enqueue(message);
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
