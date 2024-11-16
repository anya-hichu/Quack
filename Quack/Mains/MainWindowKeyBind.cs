using Dalamud.Game.ClientState.Keys;
using Dalamud.Plugin.Services;
using Quack.Configs;
using System;

namespace Quack.Mains;

public class MainWindowKeyBind : IDisposable
{
    private Action Action { get; init; }
    private Config Config { get; init; }
    private IFramework Framework { get; init; }
    private IKeyState KeyState { get; init; }

    private bool IsHolding { get; set; } = false;

    public MainWindowKeyBind(Action action, Config config, IFramework framework, IKeyState keyState)
    {
        Action = action;
        Config = config;
        Framework = framework;
        KeyState = keyState;

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
            var pressing = KeyState[Config.KeyBind] && (Config.KeyBindExtraModifier == VirtualKey.NO_KEY || KeyState[Config.KeyBindExtraModifier]);
            if (pressing && !IsHolding)
            {
                Action();
            }
            IsHolding = pressing;
        }
    }
}
