using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Quack.Utils;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Quack.Macros;

public partial class MacroExecutor : IDisposable
{
    public const string DEFAULT_FORMAT = "{0}";

    [GeneratedRegexAttribute(@"<wait\.(\d+)>")]
    private static partial Regex WaitTimeGeneratedRegex();
    
    private IFramework Framework { get; init; }
    private Chat Chat { get; init; }
    private IPluginLog PluginLog { get; init; }
    private List<CancellationTokenSource> CancellationTokenSources { get; init; } = [];
    private Queue<string> PendingMessages { get; init; } = new();

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
    }

    public void OnUpdate(IFramework framework)
    {
        while(PendingMessages.TryDequeue(out var message))
        {
            Chat.SendMessage(message);
        }
    }

    public void ExecuteTask(Macro macro, string format = DEFAULT_FORMAT, params string[] args)
    {
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        Task.Run(() =>
        {
            try
            {
                PluginLog.Debug($"Task #{Task.CurrentId} executing macro '{macro.Name}' ({macro.Path}) with format '{format}' and args [{string.Join(',', args)}]");
                CancellationTokenSources.Add(cancellationTokenSource);

                foreach (var command in macro.Content.Format(args).Split("\n"))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!command.IsNullOrWhitespace())
                    {
                        var commandWithoutWait = WaitTimeGeneratedRegex().Replace(command, string.Empty);
                        if (!commandWithoutWait.IsNullOrWhitespace())
                        {
                            var message = string.Format(new PMFormatter(), format, commandWithoutWait);
                            PendingMessages.Enqueue(message);
                        }
                            
                        var waitTimeMatch = WaitTimeGeneratedRegex().Match(command);
                        if (waitTimeMatch != null && waitTimeMatch.Success)
                        {
                            var waitTimeValue = waitTimeMatch.Groups[1].Value;
                            PluginLog.Debug($"Pausing execution #{Task.CurrentId} inside macro '{macro.Name}' ({macro.Path}) for {waitTimeValue} sec(s) to respect {waitTimeMatch.Value}");
                            Thread.Sleep(int.Parse(waitTimeValue) * 1000);
                        }
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (macro.Loop)
                {
                    ExecuteTask(macro, format, args);
                }
            }
            catch(OperationCanceledException)
            {
                PluginLog.Debug($"Canceled execution #{Task.CurrentId} for macro '{macro.Name}' ({macro.Path})");
            }
            finally
            {
                CancellationTokenSources.Remove(cancellationTokenSource);
            }
        }, cancellationToken);
    }

    public bool HasRunningTasks()
    {
        return CancellationTokenSources.Count > 0;
    }

    public void CancelTasks()
    {
        CancellationTokenSources.ForEach(s => s.Cancel());
        CancellationTokenSources.Clear();
    }
}
