using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Quack.Chat;
using Quack.Utils;
using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Quack.Macros;

public partial class MacroExecutor(ChatSender chatSender, MacroSharedLock macroSharedLock, IPluginLog pluginLog) : IDisposable
{
    public static readonly int DEFAULT_MESSAGE_INTERVAL_MS = 60;
    public const string DEFAULT_FORMAT = "{0}";

    [GeneratedRegexAttribute(@"<wait\.(?<wait>\d+|macro|cancel)>")]
    private static partial Regex WaitPlaceholderGeneratedRegex();

    [GeneratedRegexAttribute(@"^/wait(?: (?<wait>\d+))?\s*$")]
    private static partial Regex WaitCommandGeneratedRegex();

    private ChatSender ChatSender { get; init; } = chatSender;
    private MacroSharedLock MacroSharedLock { get; init; } = macroSharedLock;
    private IPluginLog PluginLog { get; init; } = pluginLog;

    public void Dispose()
    {
        CancelTasks();
    }

    public void ExecuteTask(Macro macro, string format = DEFAULT_FORMAT, params string[] args)
    {
        Task.Run(() =>
        {
            var taskId = Task.CurrentId!.Value;
            try
            {
                MacroSharedLock.Acquire(taskId);
                PluginLog.Debug($"Task #{taskId} executing macro '{macro.Name}' ({macro.Path}) with format '{format}' and args [{string.Join(',', args)}]");
                Execute(taskId, macro, format, args);
            }
            finally
            {
                MacroSharedLock.Release(taskId);
            }
        });
    }

    private void Execute(int taskId, Macro macro, string format, string[] args)
    {
        var formattedContent = macro.Content.Format(args);
        PluginLog.Verbose($"Executing macro content inside task #{taskId}:\n{formattedContent}");

        var lines = formattedContent.Split("\n");
        for (var i = 0; i < lines.Length && MacroSharedLock.IsAcquired(taskId); i++)
        {
            var line = lines[i];

            if (!line.IsNullOrWhitespace())
            {
                var waitPlaceholderMatches = WaitPlaceholderGeneratedRegex().Matches(line);
                var lineWithoutWait = waitPlaceholderMatches.Count > 0 ? WaitPlaceholderGeneratedRegex().Replace(line, string.Empty) : line;

                var message = string.Format(new PMFormatter(), format, lineWithoutWait);

                var waitCommandMatch = WaitCommandGeneratedRegex().Match(message);
                if (waitCommandMatch.Success)
                {
                    var value = waitCommandMatch.Groups["wait"].Value;
                    var secs = value.IsNullOrEmpty() ? 1 : int.Parse(value); 
                    Sleep(secs, macro, i);
                }
                else
                {
                    Task.WaitAny(ChatSender.SendOnFrameworkThread(message, taskId));
                }

                if (waitPlaceholderMatches.Count > 0)
                {
                    foreach (Match waitPlaceholderMatch in waitPlaceholderMatches)
                    {
                        var value = waitPlaceholderMatch.Groups["wait"].Value;

                        var isWaitCancel = value == "cancel";
                        if (value == "macro" || isWaitCancel)
                        {
                            if (isWaitCancel)
                            {
                                // Negate for an easy recognizable unique id
                                MacroSharedLock.Acquire(-taskId);
                            }

                            PluginLog.Verbose($"Pausing execution #{taskId} inside macro '{macro.Name}' ({macro.Path}) for <wait.{value}> at line #{i + 1}");
                            while (!MacroSharedLock.HasAcquiredLast(taskId))
                            {
                                Thread.Sleep(DEFAULT_MESSAGE_INTERVAL_MS);
                            }
                            PluginLog.Verbose($"Resuming execution #{taskId}");
                        }
                        else if (int.TryParse(value, out var secs))
                        {
                            Sleep(secs, macro, i);
                        }
                        else
                        {
                            throw new ArgumentException($"Unsupported <wait.{value}> placeholder");
                        }
                    }
                }
                else if (!waitCommandMatch.Success)
                {
                    Thread.Sleep(DEFAULT_MESSAGE_INTERVAL_MS);
                }
            }
        }

        if (macro.Loop && MacroSharedLock.IsAcquired(taskId))
        {
            Execute(taskId, macro, format, args);
        }
    }

    public bool HasRunningTasks()
    {
        return MacroSharedLock.IsAcquired();
    }

    public void CancelTasks()
    {
        MacroSharedLock.ReleaseAll();
    }

    private void Sleep(int secs, Macro macro, int i)
    {
        PluginLog.Verbose($"Pausing execution #{Task.CurrentId} inside macro '{macro.Name}' ({macro.Path}) for {secs} sec(s) at line #{i + 1}");
        Thread.Sleep(secs * 1000);
        PluginLog.Verbose($"Resuming execution #{Task.CurrentId}");
    }
}
