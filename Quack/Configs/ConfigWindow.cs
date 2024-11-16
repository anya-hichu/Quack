using Dalamud.Game.Gui.Toast;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Ipc.Internal;
using Dalamud.Plugin.Services;
using Quack.Chat;
using Quack.Generators;
using Quack.Macros;
using Quack.Schedulers;
using Quack.Utils;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Quack.Configs;

public class ConfigWindow : Window, IDisposable
{
    private FileDialogManager FileDialogManager { get; init; } = new();

    private ConfigGeneralTab GeneralTab { get; init; }
    private MacroConfigTab MacroConfigTab { get; init; }
    private SchedulerConfigTab SchedulerTab { get; init; }
    private GeneratorConfigTab GeneratorTab { get; init; }

    public ConfigWindow(HashSet<Macro> cachedMacros, CallGate callGate, ChatSender chatSender, Config config, ICommandManager commandManager, Debouncers debouncers, 
                        IKeyState keyState, MacroExecutionState macroExecutionState, MacroExecutor macroExecutor, MacroTable macroTable, MacroTableQueue macroTableQueue, 
                        IPluginLog pluginLog, IToastGui toastGui) : base("Quack Config###configWindow")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(1070, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        GeneralTab = new(config, keyState);
        MacroConfigTab = new(cachedMacros, config, commandManager, debouncers, FileDialogManager, keyState, macroExecutionState, macroExecutor, macroTable, macroTableQueue, pluginLog, toastGui);
        SchedulerTab = new(chatSender, config, debouncers, FileDialogManager, keyState, pluginLog, toastGui);
        GeneratorTab = new(cachedMacros, callGate, config, debouncers, FileDialogManager, keyState, macroTableQueue, pluginLog, toastGui);
    }

    public void Dispose()
    {
        MacroConfigTab.Dispose();
    }

    public override void Draw()
    {
        using (ImRaii.TabBar("configTabs"))
        {
            WithTab(GeneralTab.Draw, "General###generalConfigTab");
            WithTab(MacroConfigTab.Draw, "Macros###macroConfigTab");
            WithTab(SchedulerTab.Draw, "Schedulers###schedulerConfigTab");
            WithTab(GeneratorTab.Draw, "Generators###generatorConfigTab");
        }
        FileDialogManager.Draw();
    }

    private static void WithTab(Action callback, string label)
    {
        using var tab = ImRaii.TabItem(label);
        if (tab)
        {
            callback();
        }
    }
}
