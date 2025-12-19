using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;

namespace Quack.Ipcs;

public class LocalPlayerIpc : IDisposable
{
    public static readonly string INFO = "Quack.LocalPlayer.GetInfo";

    private IPlayerState PlayerState { get; init; }
    private IFramework Framework { get; init; }

    private ICallGateProvider<Dictionary<string, object>> GetInfoProvider { get; init; }

    public LocalPlayerIpc(IDalamudPluginInterface pluginInterface, IPlayerState playerState, IFramework framework)
    {
        PlayerState = playerState;
        Framework = framework;

        GetInfoProvider = pluginInterface.GetIpcProvider<Dictionary<string, object>>(INFO);
        GetInfoProvider.RegisterFunc(GetInfo);
    }

    public void Dispose()
    {
        GetInfoProvider.UnregisterFunc();
    }

    private Dictionary<string, object> GetInfo()
    {
        return Framework.RunOnFrameworkThread<Dictionary<string, object>>(() =>
        {
            if (PlayerState.IsLoaded)
            {
                var homeWorld = PlayerState.HomeWorld.Value;
                return new() {
                    { "name", PlayerState.CharacterName },
                    { "homeWorld", homeWorld.Name.ToString() },
                    { "homeWorldId", homeWorld.RowId }
                };
            }
            else
            {
                return [];
            }
        }).GetAwaiter().GetResult();
    }
}
