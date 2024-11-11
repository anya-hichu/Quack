using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Quack.Chat;
using System;
using System.Linq;

namespace Quack.Listeners;

public class TimeListener : IDisposable
{
    private Config Config { get; init; }
    private IFramework Framework { get; init; }
    private ChatServer ChatServer { get; init; }
    private IPluginLog PluginLog { get; init; }

    public TimeListener(Config config, IFramework framework, ChatServer chatServer, IPluginLog pluginLog)
    {
        Config = config;
        Framework = framework;
        ChatServer = chatServer;
        PluginLog = pluginLog;

        Framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        var nowUtc = DateTime.UtcNow;
        foreach (var schedulerConfig in Config.SchedulerConfigs.Where(c => c.Enabled))
        {
            foreach (var schedulerTriggerConfig in schedulerConfig.SchedulerTriggerConfigs.Where(c => !c.Command.IsNullOrWhitespace()))
            {
                var nextOccurrence = schedulerTriggerConfig.GetNextOccurrence(nowUtc);
                if (nextOccurrence.HasValue && Framework.UpdateDelta.CompareTo(nextOccurrence.Value - nowUtc) >= 0)
                {
                    ChatServer.SendMessage(schedulerTriggerConfig.Command);
                    PluginLog.Info($"Sent configured command '{schedulerTriggerConfig.Command}' from scheduler '{schedulerConfig.Name}' trigger with matching time expression '{schedulerTriggerConfig.TimeExpression}'");
                }
            }
        }
    }
}
