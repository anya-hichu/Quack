using Dalamud.Game.ClientState.Keys;
using Dalamud;
using Dalamud.Plugin.Services;
using System;

namespace Quack.Listeners;

public class KeyBindListener : IDisposable
{
    private IFramework Framework { get; init; }
    private Config Config { get; init; }
    private Action Action { get; init; }
    private bool WasPressed { get; set; } = false;

    public KeyBindListener(IFramework framework, Config config, Action action)
    {
        Framework = framework;
        Config = config;
        Action = action;

        Framework.Update += OnUpdate;
    }

    public void Dispose()
    {
        Framework.Update -= OnUpdate;
    }

    private void OnUpdate(IFramework _)
    {
        if(Config.KeyBind != VirtualKey.NO_KEY)
        {
            var keyState = Service<KeyState>.Get();
            if (keyState[Config.KeyBind] && (Config.KeyBindExtraModifier == VirtualKey.NO_KEY || keyState[Config.KeyBindExtraModifier]))
            {
                if (!WasPressed)
                {
                    Action();
                }
                WasPressed = true;
            }
            else
            {
                WasPressed = false;
            }
        }
    }
}
