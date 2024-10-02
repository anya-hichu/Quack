using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quack.Macros;

public unsafe class MacroSharedLock : IDisposable
{
    private SortedSet<int> Ids { get; init; } = [];
    private IFramework Framework { get; init; }
    private IPluginLog PluginLog { get; init; }
    private RaptureShellModule* RaptureShell { get; init; } = RaptureShellModule.Instance();

    public MacroSharedLock(IFramework framework, IPluginLog pluginLog)
    {
        Framework = framework;
        PluginLog = pluginLog;

        Framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;
        ReleaseAll();
    }

    public void Acquire(int id)
    {
        if (Ids.Count == 0)
        {
            SetGameLock(true);
        }
        Ids.Add(id);
        PluginLog.Verbose($"Acquired macro lock #{id} (current: [{string.Join(", ", Ids)}])");
    }

    public void Release(int id)
    {
        Ids.Remove(id);
        PluginLog.Verbose($"Released macro lock #{id} (current: [{string.Join(", ", Ids)}])");
        if (Ids.Count == 0)
        {
            SetGameLock(false);
        }
    }

    public void ReleaseAll()
    {
        Ids.Clear();
        PluginLog.Verbose("Released macro shared lock");
        SetGameLock(false);
    }

    public bool isAcquired(int id)
    {
        return Ids.Contains(id);
    }

    public bool isAcquired()
    {
        return Ids.Count > 0;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!RaptureShell->MacroLocked)
        {
            if (Ids.Count > 0)
            {
                var lastId = Ids.Last();
                Ids.Remove(lastId);
                PluginLog.Verbose($"Released macro shared lock #{lastId} through cancellation (current: [{string.Join(", ", Ids)}])");

                var macroLocked = Ids.Count > 0;
                SetGameLock(macroLocked);
            }
        }
    }

    private void SetGameLock(bool value)
    {
        if (RaptureShell->MacroLocked != value)
        {
            RaptureShell->MacroLocked = value;
            PluginLog.Verbose($"Changed game macro locked state to {value}");
        }
    }
}
