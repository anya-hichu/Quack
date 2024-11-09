

using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;
using ThrottleDebounce;

namespace Quack.Utils;

public class Debouncers(IPluginLog pluginLog) : IDisposable
{
    private IPluginLog PluginLog { get; init; } = pluginLog;

    private Dictionary<string, RateLimitedAction> DebouncedActions { get; init; } = [];

    public void Dispose()
    {
        foreach (var debouncer in DebouncedActions.Values)
        {
            debouncer.Dispose();
        }
    }
    public void Invoke(string key, Action action)
    {
        Invoke(key, action, TimeSpan.FromSeconds(1));
    }

    public void Invoke(string key, Action action, TimeSpan wait)
    {
        if (!DebouncedActions.TryGetValue(key, out var debouncedAction))
        {
            DebouncedActions[key] = debouncedAction = Debouncer.Debounce(() => {
                try
                {
                    PluginLog.Verbose($"Executing debounced action '{key}'");
                    action();
                }
                finally
                {
                    DebouncedActions.Remove(key);
                }
            }, wait, leading: false, trailing: true);
        }
        debouncedAction.Invoke();
    }
}
