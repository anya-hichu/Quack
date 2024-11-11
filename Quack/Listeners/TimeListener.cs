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
            foreach (var schedulerCommandConfig in schedulerConfig.SchedulerCommandConfigs.Where(c => !c.Command.IsNullOrWhitespace()))
            {
                var nextOccurrence = schedulerCommandConfig.GetNextOccurrence(nowUtc);
                if (nextOccurrence.HasValue && Framework.UpdateDelta.CompareTo(nextOccurrence.Value - nowUtc) >= 0)
                {
                    ChatServer.SendMessage(schedulerCommandConfig.Command);
                    PluginLog.Info($"Sent configured command '{schedulerCommandConfig.Command}' from scheduler '{schedulerConfig.Name}' with matching time expression '{schedulerCommandConfig.TimeExpression}'");
                }
            }
        }
    }
}
