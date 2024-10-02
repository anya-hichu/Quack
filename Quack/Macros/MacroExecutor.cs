using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Quack.Utils;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Quack.Macros;

public partial class MacroExecutor(ChatServer chatServer, MacroMultiLock macroMultiLock, IPluginLog pluginLog) : IDisposable
{
    public static readonly int DEFAULT_MESSAGE_INTERVAL_MS = 60;
    public const string DEFAULT_FORMAT = "{0}";

    [GeneratedRegexAttribute(@"<wait\.(\d+)>")]
    private static partial Regex WaitTimeGeneratedRegex();

    private ChatServer ChatServer { get; init; } = chatServer;
    private MacroMultiLock MacroMultiLock { get; init; } = macroMultiLock;
    private IPluginLog PluginLog { get; init; } = pluginLog;
    private List<CancellationTokenSource> CancellationTokenSources { get; set; } = [];

    public void Dispose()
    {
        CancelTasks();
        CancellationTokenSources.ForEach(t => t.Cancel());
    }

    public void ExecuteTask(Macro macro, string format = DEFAULT_FORMAT, params string[] args)
    {
        var cancellationTokenSource = new CancellationTokenSource();
        Task.Run(() =>
        {
            var taskId = Task.CurrentId!.Value;
            try
            {
                MacroMultiLock.Acquire(taskId);
                PluginLog.Debug($"Task #{taskId} executing macro '{macro.Name}' ({macro.Path}) with format '{format}' and args [{string.Join(',', args)}]");
                Execute(macro, format, args);
            } 
            finally
            {
                MacroMultiLock.Release(taskId);
            }
            
        }, cancellationTokenSource.Token);
    }

    private void Execute(Macro macro, string format, string[] args)
    {
        var taskId = Task.CurrentId!.Value;

        var formattedContent = macro.Content.Format(args);
        PluginLog.Verbose($"Executing macro content inside task #{taskId}:\n{formattedContent}");

        var lines = formattedContent.Split("\n");
        for (var i = 0; i < lines.Length && MacroMultiLock.isAcquired(taskId); i++)
        {
            var line = lines[i];

            if (!line.IsNullOrWhitespace())
            {
                var waitTimeMatch = WaitTimeGeneratedRegex().Match(line);
                var lineWithoutWait = waitTimeMatch.Success ? WaitTimeGeneratedRegex().Replace(line, string.Empty) : line;

                var message = string.Format(new PMFormatter(), format, lineWithoutWait).TrimEnd();

                ChatServer.SendMessage(message);
                PluginLog.Verbose($"Send message: '{message}'");

                if (waitTimeMatch.Success)
                {
                    var waitTimeValue = waitTimeMatch.Groups[1].Value;
                    PluginLog.Verbose($"Pausing execution #{Task.CurrentId} inside macro '{macro.Name}' ({macro.Path}) at line #{i + 1} for {waitTimeValue} sec(s)");
                    Thread.Sleep(int.Parse(waitTimeValue) * 1000);
                    PluginLog.Verbose($"Resuming execution #{Task.CurrentId}");
                } 
                else
                {
                    Thread.Sleep(DEFAULT_MESSAGE_INTERVAL_MS);
                }
            }
        }

        if (macro.Loop && MacroMultiLock.isAcquired(taskId))
        {
            Execute(macro, format, args);
        }
    }

    public bool HasRunningTasks()
    {
        return MacroMultiLock.isAcquired();
    }

    public void CancelTasks()
    {
        MacroMultiLock.ReleaseAll();
    }
}
