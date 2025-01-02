using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Ipc.Internal;
using Dalamud.Plugin.Services;
using ImGuiNET;
using Quack.Chat;
using Quack.Generators;
using Quack.Macros;
using Quack.Schedulers;
using Quack.UI;
using Quack.Utils;
using System;
using System.Collections.Generic;

namespace Quack.Configs;

public class ConfigWindow : Window, IDisposable
{
    private FileDialogManager FileDialogManager { get; init; } = new();

    private ConfigGeneralTab GeneralTab { get; init; }
    private MacroConfigTab MacroConfigTab { get; init; }
    private ImGuiTabItemFlags MacroConfigTabFlags { get; set; } = ImGuiTabItemFlags.None;
    private UIEvents UIEvents { get; init; }
    private SchedulerConfigTab SchedulerTab { get; init; }
    private GeneratorConfigTab GeneratorTab { get; init; }
    private ConfigInfoTab InfoTab { get; init; }

    public ConfigWindow(HashSet<Macro> cachedMacros, CallGate callGate, ChatSender chatSender, Config config, ICommandManager commandManager, string databasePath, Debouncers debouncers, 
                        IKeyState keyState, MacroExecutionState macroExecutionState, MacroExecutor macroExecutor, MacroTable macroTable, MacroTableQueue macroTableQueue, 
                        IPluginLog pluginLog, INotificationManager notificationManager, UIEvents uiEvents) : base("Quack Config###configWindow")
    {
        SizeConstraints = new()
        {
            MinimumSize = new(1070, 400),
            MaximumSize = new(float.MaxValue, float.MaxValue)
        };

        GeneralTab = new(config, debouncers, FileDialogManager, keyState, notificationManager, uiEvents);
        MacroConfigTab = new(cachedMacros, config, commandManager, debouncers, FileDialogManager, keyState, macroExecutionState, macroExecutor, macroTable, macroTableQueue, pluginLog, notificationManager, uiEvents);
        SchedulerTab = new(chatSender, config, debouncers, FileDialogManager, keyState, pluginLog, notificationManager);
        GeneratorTab = new(cachedMacros, callGate, config, debouncers, FileDialogManager, keyState, macroTableQueue, pluginLog, notificationManager);
        InfoTab = new(cachedMacros, databasePath, macroTable, macroTableQueue, uiEvents);

        UIEvents = uiEvents;
        UIEvents.OnMacroEditRequest += OnMacroEditRequest;
    }

    public void Dispose()
    {
        UIEvents.OnMacroEditRequest -= OnMacroEditRequest;
        MacroConfigTab.Dispose();
    }

    public override void Draw()
    {
        using (ImRaii.TabBar("configTabs"))
        {
            WithTab(GeneralTab.Draw, "General###generalConfigTab", ImGuiTabItemFlags.None);
            WithTab(MacroConfigTab.Draw, "Macros###macroConfigTab", MacroConfigTabFlags);
            WithTab(SchedulerTab.Draw, "Schedulers###schedulerConfigTab", ImGuiTabItemFlags.None);
            WithTab(GeneratorTab.Draw, "Generators###generatorConfigTab", ImGuiTabItemFlags.None);
            WithTab(InfoTab.Draw, "Infos###infoConfigTab", ImGuiTabItemFlags.None);
        }
        FileDialogManager.Draw();
        MacroConfigTabFlags = ImGuiTabItemFlags.None;
    }

    private static void WithTab(Action callback, string label, ImGuiTabItemFlags flags)
    {
        using var tab = ImRaii.TabItem(label, flags);
        if (tab)
        {
            callback();
        }
    }

    private void OnMacroEditRequest(Macro macro)
    {
        if(!IsOpen)
        {
            Toggle();
        }
        MacroConfigTabFlags = ImGuiTabItemFlags.SetSelected;
    }
}
