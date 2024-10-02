using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quack.Macros;

public unsafe class MacroMultiLock : IDisposable
{
    private SortedSet<int> Ids { get; init; } = [];
    private IFramework Framework { get; init; }
    private IPluginLog PluginLog { get; init; }
    private RaptureShellModule* RaptureShell { get; init; } = RaptureShellModule.Instance();

    public MacroMultiLock(IFramework framework, IPluginLog pluginLog)
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
            SetMacroLocked(true);
        }
        Ids.Add(id);
        PluginLog.Verbose($"Acquired macro multi lock #{id} (current: [{string.Join(", ", Ids)}])");
    }

    public void Release(int id)
    {
        Ids.Remove(id);
        if (Ids.Count == 0)
        {
            PluginLog.Verbose($"Released macro lock #{id} (current: [{string.Join(", ", Ids)}])");
            SetMacroLocked(false);
        }
    }

    public void ReleaseAll()
    {
        Ids.Clear();
        PluginLog.Verbose("Released macro multi lock");
        SetMacroLocked(false);
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
                PluginLog.Verbose($"Released macro multi lock #{lastId} through cancellation (current: [{string.Join(", ", Ids)}])");

                var macroLocked = Ids.Count > 0;
                SetMacroLocked(macroLocked);
            }
        }
    }

    private void SetMacroLocked(bool value)
    {
        if (RaptureShell->MacroLocked != value)
        {
            RaptureShell->MacroLocked = value;
            PluginLog.Verbose($"Changed game macro locked state to {value}");
        }
    }
}
