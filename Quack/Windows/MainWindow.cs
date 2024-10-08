using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ImGuiNET;
using Quack.Macros;
using Quack.Utils;

namespace Quack.Windows;

public class MainWindow : Window, IDisposable
{
    private HashSet<Macro> CachedMacros { get; init; }
    private MacroExecutor MacroExecutor { get; init; }
    private MacroTable MacroTable { get; init; }
    private Config Config { get; init; }
    private IPluginLog PluginLog { get; init; }
    private string Filter { get; set; } = string.Empty;
    private HashSet<Macro> FilteredMacros { get; set; } = [];
    private MacroExecutionGui MacroExecutionGui { get; init; }

    public MainWindow(HashSet<Macro> cachedMacros, MacroExecutor macroExecutor, MacroTable macroTable, Config config, IPluginLog pluginLog) : base("Quack##mainWindow")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(300, 200),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        CachedMacros = cachedMacros;
        MacroExecutor = macroExecutor;
        MacroTable = macroTable;
        Config = config;
        PluginLog = pluginLog;

        MacroExecutionGui = new(config, macroExecutor);

        UpdateFilteredMacros();
        MacroTable.OnChange += UpdateFilteredMacros;
    }

    public void UpdateFilteredMacros()
    {
        FilteredMacros = MacroTable.Search(Filter);
        MacroExecutionGui.UpdateExecutions(FilteredMacros);
    }

    public void Dispose()
    {
        MacroTable.OnChange -= UpdateFilteredMacros;
    }

    public override void Draw()
    {
        var filter = Filter;
        ImGui.PushItemWidth(ImGui.GetWindowWidth() - 220);
        var filterInput = ImGui.InputTextWithHint($"Filter ({FilteredMacros.Count}/{CachedMacros.Count})###filter", "Search Query (min 3 chars)", ref filter, ushort.MaxValue);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Indexed columns with trigram: name, path, command, tags\n\nExample queries:\n - PEDRO\n - cute tags:design\n - ^Custom tags:throw NOT cheese\n\nSee FTS5 query documentation for syntax and more examples: https://www.sqlite.org/fts5.html");
        }
        if (filterInput)
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

            var clipper = ImGuiHelper.NewListClipper();
            clipper.Begin(FilteredMacros.Count, 27);
            while(clipper.Step())
            {
                for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                {
                    var macro = FilteredMacros.ElementAt(i);
                    if (ImGui.TableNextColumn())
                    {
                        ImGui.Text(macro.Name);
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(macro.Name);
                        }
                    }

                    if (ImGui.TableNextColumn())
                    {
                        ImGui.Text(macro.Path);
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(macro.Path);
                        }
                    }

                    if (ImGui.TableNextColumn())
                    {
                        var joinedTags = string.Join(", ", macro.Tags);
                        ImGui.Text(joinedTags);
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(joinedTags);
                        }
                    }

                    if (ImGui.TableNextColumn())
                    {
                        MacroExecutionGui.Button(macro);
                    }

                    ImGui.TableNextRow();
                }
            }
            clipper.Destroy();

            ImGui.EndTable();
        }
    }


}
