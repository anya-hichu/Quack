using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Quack.Chat;
using Quack.Macros;
using Quack.Utils;
using Quack.UI.Configs.Tabs;
using Quack.UI.Helpers;
using Quack.UI.Tabs;
using Dalamud.Interface.Utility.Raii;

namespace Quack.UI.Windows;

public class ConfigWindow : Window, IDisposable
{
    private FileDialogManager FileDialogManager { get; init; } = new();

    private GeneralTab GeneralTab { get; init; }
    private MacrosTab MacrosTab { get; init; }
    private SchedulersTab SchedulersTab { get; init; }
    private GeneratorsTab GeneratorsTab { get; init; }

    public ConfigWindow(HashSet<Macro> cachedMacros, ChatSender chatSender, Config config, Debouncers debouncers, IKeyState keyState, MacroExecutionHelper macroExecutionHelper, MacroExecutor macroExecutor, MacroTable macroTable, MacroTableQueue macroTableQueue, IPluginLog pluginLog) : base("Quack Config##configWindow")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(1070, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        GeneralTab = new(config, keyState);
        MacrosTab = new(cachedMacros, debouncers, FileDialogManager, macroExecutionHelper, macroExecutor, macroTable, macroTableQueue, pluginLog);

        SchedulersTab = new(chatSender, config, debouncers, FileDialogManager);
        GeneratorsTab = new(cachedMacros, config, debouncers, FileDialogManager, macroTableQueue, pluginLog);
    }

    public void Dispose()
    {
        MacrosTab.Dispose();
    }

    public override void Draw()
    {
        using (ImRaii.TabBar("configTabs"))
        {
            using (var tab = ImRaii.TabItem("General##generalTab"))
            {
                if (tab.Success)
                {
                    GeneralTab.Draw();
                }
            }
            using (var tab = ImRaii.TabItem("Macros##macrosTab"))
            {
                if (tab.Success)
                {
                    MacrosTab.Draw();
                }
            }
            using (var tab = ImRaii.TabItem("Schedulers##schedulersTab"))
            {
                if (tab.Success)
                {
                    SchedulersTab.Draw();
                }
            }
            using (var tab = ImRaii.TabItem("Generators##generatorsTab"))
            {
                if (tab.Success)
                {
                    GeneratorsTab.Draw();
                }
            }
        }
        FileDialogManager.Draw();
    }
}
