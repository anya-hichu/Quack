using Dalamud;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Plugin.Services;
using Quack.Configs;
using System;

namespace Quack.Mains;

public class MainKeyBind : IDisposable
{
    private IFramework Framework { get; init; }
    private Config Config { get; init; }
    private Action Action { get; init; }
    private bool IsHolding { get; set; } = false;

    public MainKeyBind(IFramework framework, Config config, Action action)
    {
        Framework = framework;
        Config = config;
        Action = action;

        Framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        if (Config.KeyBind != VirtualKey.NO_KEY)
        {
            var keyState = Service<KeyState>.Get();
            var pressing = keyState[Config.KeyBind] && (Config.KeyBindExtraModifier == VirtualKey.NO_KEY || keyState[Config.KeyBindExtraModifier]);
            if (pressing && !IsHolding)
            {
                Action();
            }
            IsHolding = pressing;
        }
    }
}
