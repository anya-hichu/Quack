using Dalamud.Plugin.Services;
using Quack.Macros;
using System;
using System.Collections.Generic;

namespace Quack.Chat;

public class ChatSender: IDisposable
{
    private ChatServer ChatServer { get; init; }
    private IFramework Framework { get; init; }
    private MacroSharedLock MacroSharedLock { get; init; }
    public IPluginLog PluginLog { get; init; }
    private Queue<(int, string)> MessageEntries { get; init; } = [];

    public ChatSender(ChatServer chatServer, IFramework framework, MacroSharedLock macroSharedLock, IPluginLog pluginLog)
    {
        ChatServer = chatServer;
        Framework = framework;
        MacroSharedLock = macroSharedLock;
        PluginLog = pluginLog;

        Framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;
    }

    public void Enqueue(int id, string message)
    {
        MessageEntries.Enqueue((id, message));
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        while (MessageEntries.TryDequeue(out var entry))
        {
            var taskId = entry.Item1;
            var message = entry.Item2;
            if (MacroSharedLock.isAcquired(taskId))
            {
                ChatServer.SendMessage(message);
                PluginLog.Verbose($"Sent chat message for task #{taskId}: '{message}'");
            } 
            else
            {
                PluginLog.Verbose($"Dismissed chat message for task #{taskId} because macro shared lock was released: '{message}'");
            }
        }
    }


}
