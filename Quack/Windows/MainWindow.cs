using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ImGuiNET;
using Quack.Macros;

namespace Quack.Windows;

public class MainWindow : Window, IDisposable
{
    private Executor Executor { get; init; }
    private Config Config { get; init; }
    private IPluginLog PluginLog { get; init; }

    private string Filter { get; set; } = string.Empty;
    private List<Macro> FilteredMacros { get; set; } = [];

    public MainWindow(Executor executor, Config config, IPluginLog pluginLog) : base("Quack##mainWindow")
    {
        Executor = executor;
        Config = config;
        PluginLog = pluginLog;

        UpdateFilteredMacros();

        Config.OnSave += UpdateFilteredMacros;
    }

    public void UpdateFilteredMacros()
    {
        FilteredMacros = Search.Lookup(Config.Macros, Filter).Take(Config.MaxMatches).ToList();
    }

    public void Dispose() 
    {
        Config.OnSave -= UpdateFilteredMacros;
    }

    public override void Draw()
    {
        var filter = Filter;
        ImGui.PushItemWidth(ImGui.GetWindowWidth() - 200);
        if (ImGui.InputText($"Filter ({FilteredMacros.Count()}/{Config.Macros.Count})###filter", ref filter, ushort.MaxValue))
        {
            Filter = filter;
            UpdateFilteredMacros();
        }
        ImGui.PopItemWidth();

        ImGui.SameLine();
        if (ImGui.Button("X##filterClear"))
        {
            Filter = string.Empty;
            UpdateFilteredMacros();
        }
        
        if (ImGui.BeginTable("macros", 4, ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
        {
            ImGui.TableSetupColumn($"Name##macroName", ImGuiTableColumnFlags.None, 3);
            ImGui.TableSetupColumn($"Path##macroPath", ImGuiTableColumnFlags.None, 2);
            ImGui.TableSetupColumn($"Tags##macroTags", ImGuiTableColumnFlags.None, 1);
            ImGui.TableSetupColumn($"Actions##macroActions", ImGuiTableColumnFlags.None, 2);
            ImGui.TableHeadersRow();


            for (var i = 0; i < FilteredMacros.Count(); i++)
            {
                var macro = FilteredMacros.ElementAt(i);
                if (ImGui.TableNextColumn())
                {
                    ImGui.Text(macro.Name);
                }

                if (ImGui.TableNextColumn())
                {
                    ImGui.Text(macro.Path);
                }

                if (ImGui.TableNextColumn())
                {
                    ImGui.Text(string.Join(", ", macro.Tags));
                }

                if (ImGui.TableNextColumn())
                {
                    if (ImGui.Button($"Execute###macros{i}Execute"))
                    {
                        Executor.RunAsync(macro);
                    }

                    ImGui.SameLine();
                    if (ImGui.Button($"+ Format###macros{i}ExecuteWithFormatting"))
                    {
                        Executor.RunAsync(macro, Config.CommandFormat);
                    }

                    // TODO REMOVE AND HAVE PROPER CONFIG EDITOR FOR MACROS in dedicated tab with a folder like structure on right like penumbra
                    ImGui.SameLine();
                    if (ImGui.Button($"Delete###macros{i}Delete"))
                    {
                        Config.Macros.Remove(macro);
                        Config.Save();
                    }
                }

                ImGui.TableNextRow();
            }
            ImGui.EndTable();
        }
    }
}
