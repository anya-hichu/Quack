using Dalamud.Plugin.Services;
using Jint.Runtime;
using Quack.Macros;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Quack.Chat;

public class ChatSender: IDisposable
{
    private class Payload(string message, int lockId, TaskCompletionSource<bool> completion)
    {
        public int LockId { get; init; } = lockId;
        public string Message { get; init; } = message;
        public TaskCompletionSource<bool> Completion { get; init; } = completion;
    }

    private ChatServer ChatServer { get; init; }
    private IFramework Framework { get; init; }
    private MacroSharedLock MacroSharedLock { get; init; }
    public IPluginLog PluginLog { get; init; }
    private Queue<Payload> PendingPayloads { get; init; } = [];

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

    public Task SendOnFrameworkThread(string message, int lockId)
    {
        var completion = new TaskCompletionSource<bool>();
        PendingPayloads.Enqueue(new(message, lockId, completion));
        return completion.Task;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        while (PendingPayloads.TryDequeue(out var payload))
        {
            if (MacroSharedLock.IsAcquired(payload.LockId))
            {
                ChatServer.SendMessage(payload.Message);
                PluginLog.Verbose($"Sent chat message: '{payload.Message}' (lock #{payload.LockId})");
                payload.Completion.SetResult(true);
            } 
            else
            {
                PluginLog.Verbose($"Discarded chat message: '{payload.Message}' (lock #{payload.LockId} was released)");
                payload.Completion.SetResult(false);
            }
        }
    }
}
