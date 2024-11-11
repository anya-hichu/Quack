using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ImGuiNET;
using Quack.Chat;
using Quack.Macros;
using Quack.Utils;
using Quack.Windows.Configs.Tabs;

namespace Quack.Windows;

public class ConfigWindow : Window, IDisposable
{
    private FileDialogManager FileDialogManager { get; init; } = new();

    private GeneralTab GeneralTab { get; init; }
    private MacrosTab MacrosTab { get; init; }
    private SchedulersTab SchedulersTab { get; init; }
    private GeneratorsTab GeneratorsTab { get; init; }

    public ConfigWindow(HashSet<Macro> cachedMacros, ChatSender chatSender, Config config, Debouncers debouncers, IKeyState keyState, MacroExecutionGui macroExecutionGui, MacroExecutor macroExecutor, MacroTable macroTable, MacroTableQueue macroTableQueue, IPluginLog pluginLog) : base("Quack Config##configWindow")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(1070, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        GeneralTab = new(config, keyState);
        MacrosTab = new(cachedMacros, debouncers, FileDialogManager, macroExecutionGui, macroExecutor, macroTable, macroTableQueue, pluginLog);

        SchedulersTab = new(chatSender, config, debouncers, FileDialogManager);
        GeneratorsTab = new(cachedMacros, config, debouncers, FileDialogManager, macroTableQueue, pluginLog);
    }

    public void Dispose() 
    {
        MacrosTab.Dispose();
    }

    public override void Draw()
    {
        if(ImGui.BeginTabBar("tabs"))
        {
            if (ImGui.BeginTabItem("General##generalTab"))
            {
                GeneralTab.Draw();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Macros##macrosTab"))
            {
                MacrosTab.Draw();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Schedulers##schedulersTab"))
            {
                SchedulersTab.Draw();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Generators##generatorsTab"))
            {
                GeneratorsTab.Draw();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
        FileDialogManager.Draw();
    }
}
