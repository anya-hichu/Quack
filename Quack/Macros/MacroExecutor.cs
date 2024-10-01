using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using Quack.Utils;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Quack.Macros;

public unsafe partial class MacroExecutor : IDisposable
{
    public const int DEFAULT_INTERVAL_MS = 20;
    public const string DEFAULT_FORMAT = "{0}";

    [GeneratedRegexAttribute(@"<wait\.(\d+)>")]
    private static partial Regex WaitTimeGeneratedRegex();

    private IFramework Framework { get; init; }
    private Chat Chat { get; init; }
    private IPluginLog PluginLog { get; init; }
    private Queue<string> PendingMessages { get; init; } = new();
    private List<CancellationTokenSource> CancellationTokenSources { get; set; } = [];

    public MacroExecutor(IFramework framework, Chat chat, IPluginLog pluginLog)
    {
        Framework = framework;
        Chat = chat;
        PluginLog = pluginLog;

        Framework.Update += OnUpdate;
    }

    public void Dispose()
    {
        Framework.Update -= OnUpdate;
        CancelTasks();
        CancellationTokenSources.ForEach(t => t.Cancel());
    }

    public void OnUpdate(IFramework framework)
    {
        while (PendingMessages.TryDequeue(out var message) && RaptureShellModule.Instance()->MacroLocked)
        {
            Chat.SendMessage(message);
        }
    }

    public void ExecuteTask(Macro macro, string format = DEFAULT_FORMAT, params string[] args)
    {
        var cancellationTokenSource = new CancellationTokenSource();
        Task.Run(() =>
        {
            try
            {
                if (CancellationTokenSources.Count == 0)
                {
                    RaptureShellModule.Instance()->MacroLocked = true;
                }
                CancellationTokenSources.Add(cancellationTokenSource);

                PluginLog.Debug($"Task #{Task.CurrentId} executing macro '{macro.Name}' ({macro.Path}) with format '{format}' and args [{string.Join(',', args)}]");
                Execute(macro, format, args);
            } 
            finally
            {
                // Wait until all the messages have been processed
                while(PendingMessages.Count > 0)
                {
                    Thread.Sleep(DEFAULT_INTERVAL_MS);
                }

                CancellationTokenSources.Remove(cancellationTokenSource);
                if (CancellationTokenSources.Count == 0)
                {
                    RaptureShellModule.Instance()->MacroLocked = false;
                }
            }
        }, cancellationTokenSource.Token);
    }

    private void Execute(Macro macro, string format, string[] args)
    {
        var formattedContent = macro.Content.Format(args);
        PluginLog.Verbose($"Executing macro content inside task #{Task.CurrentId}:\n{formattedContent}");

        var lines = formattedContent.Split("\n");
        for (var i = 0; i < lines.Length && RaptureShellModule.Instance()->MacroLocked; i++)
        {
            var line = lines[i];

            if (!line.IsNullOrWhitespace())
            {
                var waitTimeMatch = WaitTimeGeneratedRegex().Match(line);
                var lineWithoutWait = waitTimeMatch.Success ? WaitTimeGeneratedRegex().Replace(line, string.Empty) : line;

                var message = string.Format(new PMFormatter(), format, lineWithoutWait);
                PendingMessages.Enqueue(message);

                if (waitTimeMatch.Success)
                {
                    var waitTimeValue = waitTimeMatch.Groups[1].Value;
                    PluginLog.Verbose($"Pausing execution #{Task.CurrentId} inside macro '{macro.Name}' ({macro.Path}) at line #{i + 1} for {waitTimeValue} sec(s)");
                    Thread.Sleep(int.Parse(waitTimeValue) * 1000);
                } 
                else
                {
                    Thread.Sleep(DEFAULT_INTERVAL_MS);
                }
            }
        }

        if (macro.Loop && RaptureShellModule.Instance()->MacroLocked)
        {
            Execute(macro, format, args);
        }
    }

    public bool HasRunningTasks()
    {
        return RaptureShellModule.Instance()->MacroLocked;
    }

    public void CancelTasks()
    {
        Chat.SendMessage("/macrocancel");
    }
}
