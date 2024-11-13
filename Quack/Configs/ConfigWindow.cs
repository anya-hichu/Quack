using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
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
    private MacroUITab MacroTab { get; init; }
    private SchedulerConfigTab SchedulerTab { get; init; }
    private GeneratorConfigTab GeneratorTab { get; init; }

    public ConfigWindow(HashSet<Macro> cachedMacros, ChatSender chatSender, Config config, Debouncers debouncers, IKeyState keyState, MacroExecutionButton macroExecutionHelper, MacroExecutor macroExecutor, MacroTable macroTable, MacroTableQueue macroTableQueue, IPluginLog pluginLog) : base("Quack Config##configWindow")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(1070, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        GeneralTab = new(config, keyState);
        MacroTab = new(cachedMacros, debouncers, FileDialogManager, keyState, macroExecutionHelper, macroExecutor, macroTable, macroTableQueue, pluginLog);
        SchedulerTab = new(chatSender, config, debouncers, FileDialogManager, keyState, pluginLog);
        GeneratorTab = new(cachedMacros, config, debouncers, FileDialogManager, keyState, macroTableQueue, pluginLog);
    }

    public void Dispose()
    {
        MacroTab.Dispose();
    }

    public override void Draw()
    {
        using (ImRaii.TabBar("configTabs"))
        {
            WithTab(GeneralTab.Draw, "General##generalConfigTab");
            WithTab(MacroTab.Draw, "Macros##macroConfigTab");
            WithTab(SchedulerTab.Draw, "Schedulers##schedulerConfigTab");
            WithTab(GeneratorTab.Draw, "Generators##generatorConfigTab");
        }
        FileDialogManager.Draw();
    }

    private void WithTab(Action callback, string label)
    {
        using (var tab = ImRaii.TabItem(label))
        {
            if (tab.Success)
            {
                callback();
            }
        }
    }
}
