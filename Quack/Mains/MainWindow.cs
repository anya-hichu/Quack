using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ImGuiNET;
using Quack.Configs;
using Quack.Macros;
using Quack.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Quack.Mains;

public class MainWindow : Window, IDisposable
{
    private HashSet<Macro> CachedMacros { get; init; }
    private MacroExecutionState MacroExecutionState { get; init; }
    private MacroExecutor MacroExecutor { get; init; }
    private MacroTable MacroTable { get; init; }
    private Config Config { get; init; }
    private IPluginLog PluginLog { get; init; }
    private MainWindowState MainWindowState { get; init; }

    public MainWindow(HashSet<Macro> cachedMacros, Config config, MacroExecutionState macroExecutionState, MacroExecutor macroExecutor, MacroTable macroTable, IPluginLog pluginLog) : base("Quack##mainWindow")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(300, 200),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        CachedMacros = cachedMacros;
        Config = config;
        MacroExecutor = macroExecutor;
        MacroExecutionState = macroExecutionState;
        MacroTable = macroTable;
        PluginLog = pluginLog;

        MainWindowState = new(MacroTable);
    }

    public void Dispose()
    {
        MainWindowState.Dispose();
    }

    public override void Draw()
    {
        var state = MainWindowState;
        using (ImRaii.ItemWidth(ImGui.GetWindowWidth() - 225))
        {
            var query = state.Query;
            if (ImGui.InputTextWithHint($"Query ({state.FilteredMacros.Count}/{CachedMacros.Count})##filter", "Search Query (min 3 chars)", ref query, ushort.MaxValue))
            {
                state.Query = query;
                state.Update();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Indexed columns with trigram: name, path, command, tags\n\nExample queries:\n - PEDRO\n - cute tags:design\n - ^Custom tags:throw NOT cheese\n\nSee FTS5 query documentation for syntax and more examples: https://www.sqlite.org/fts5.html");
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("x##queryClear"))
        {
            state.Query = string.Empty;
            state.Update();
        }

        if (MacroExecutor.HasRunningTasks())
        {
            ImGui.SameLine();
            using (ImRaii.Color? _ = ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.DalamudOrange), __ = ImRaii.PushColor(ImGuiCol.Text, new Vector4(0, 0, 0, 1)))
            {
                if (ImGui.Button($"Cancel All##macrosCancelAll"))
                {
                    MacroExecutor.CancelTasks();
                }
            }
        }

        var queriedMacrosId = "queriedMacros";
        using (ImRaii.Table($"{queriedMacrosId}Table", 4, ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
        {
            ImGui.TableSetupColumn($"Name##{queriedMacrosId}Name", ImGuiTableColumnFlags.None, 3);
            ImGui.TableSetupColumn($"Path##{queriedMacrosId}Path", ImGuiTableColumnFlags.None, 2);
            ImGui.TableSetupColumn($"Tags##{queriedMacrosId}Tags", ImGuiTableColumnFlags.None, 1);
            ImGui.TableSetupColumn($"Actions##{queriedMacrosId}Actions", ImGuiTableColumnFlags.None, 2);
            ImGui.TableHeadersRow();

            var clipper = ListClipper.Build();
            clipper.Begin(state.FilteredMacros.Count, 27);
            while (clipper.Step())
            {
                for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                {
                    var macro = state.FilteredMacros.ElementAt(i);
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
                        MacroExecutionState.Button($"{queriedMacrosId}{i}Execution", macro);
                    }

                    ImGui.TableNextRow();
                }
            }
            clipper.Destroy();
        }
    }
}
