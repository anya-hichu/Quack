using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;

namespace Quack.Ipcs;

public class LocalPlayerIpc : IDisposable
{
    public static readonly string INFO = "Quack.LocalPlayer.GetInfo";

    private IClientState ClientState { get; init; }

    private ICallGateProvider<Dictionary<string, object>> GetInfoProvider { get; init; }

    public LocalPlayerIpc(IDalamudPluginInterface pluginInterface, IClientState clientState)
    {
        ClientState = clientState;

        GetInfoProvider = pluginInterface.GetIpcProvider<Dictionary<string, object>>(INFO);
        GetInfoProvider.RegisterFunc(GetInfo);
    }

    public void Dispose()
    {
        GetInfoProvider.UnregisterFunc();
    }

    private Dictionary<string, object> GetInfo()
    {
        var localPlayer = ClientState.LocalPlayer;
        if (localPlayer != null)
        {
            return new() {
                {"name", localPlayer.Name.TextValue},
                {"homeWorld", localPlayer.HomeWorld.GameData!.Name}
            };
        } 
        else
        {
            return [];
        }
    }
}
