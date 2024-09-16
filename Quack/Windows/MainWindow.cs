using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ImGuiNET;
using Quack.Macros;

namespace Quack.Windows;

public class MainWindow : Window, IDisposable
{
    private MacroExecutor MacroExecutor { get; init; }
    private Config Config { get; init; }
    private IPluginLog PluginLog { get; init; }
    private string Filter { get; set; } = string.Empty;
    private List<Macro> FilteredMacros { get; set; } = [];

    public MainWindow(MacroExecutor macroExecutor, Config config, IPluginLog pluginLog) : base("Quack##mainWindow")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(300, 200),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        MacroExecutor = macroExecutor;
        Config = config;
        PluginLog = pluginLog;

        UpdateFilteredMacros();

        Config.OnSave += UpdateFilteredMacros;
    }

    public void UpdateFilteredMacros()
    {
        FilteredMacros = MacroSearch.Lookup(Config.Macros, Filter).ToList();
    }

    public void Dispose() 
    {
        Config.OnSave -= UpdateFilteredMacros;
    }

    public override void Draw()
    {
        var filter = Filter;
        ImGui.PushItemWidth(ImGui.GetWindowWidth() - 220);
        if (ImGui.InputText($"Filter ({FilteredMacros.Count}/{Config.Macros.Count})###filter", ref filter, ushort.MaxValue))
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

        if (MacroExecutor.HasRunningTasks())
        {
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.DalamudOrange);
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0, 0, 0, 1));
            if (ImGui.Button($"Cancel All###macrosCancelAll"))
            {
                MacroExecutor.CancelTasks();
            }
            ImGui.PopStyleColor();
            ImGui.PopStyleColor();
        }

        if (ImGui.BeginTable("macros", 4, ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
        {
            ImGui.TableSetupColumn($"Name##macroName", ImGuiTableColumnFlags.None, 3);
            ImGui.TableSetupColumn($"Path##macroPath", ImGuiTableColumnFlags.None, 2);
            ImGui.TableSetupColumn($"Tags##macroTags", ImGuiTableColumnFlags.None, 1);
            ImGui.TableSetupColumn($"Actions##macroActions", ImGuiTableColumnFlags.None, 2);
            ImGui.TableHeadersRow();

            var clipper = newListClipper();
            clipper.Begin(FilteredMacros.Count(), 27);
            while(clipper.Step())
            {
                for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
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
                            MacroExecutor.ExecuteTask(macro);
                        }

                        ImGui.SameLine();
                        if (ImGui.Button($"+ Format###macros{i}ExecuteWithFormat"))
                        {
                            MacroExecutor.ExecuteTask(macro, Config.ExtraCommandFormat);
                        }
                    }

                    ImGui.TableNextRow();
                }
            }
            clipper.Destroy();

            ImGui.EndTable();
        }
    }

    private unsafe static ImGuiListClipperPtr newListClipper()
    {
        return new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
    }
}
