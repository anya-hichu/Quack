using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using ImGuiNET;
using Quack.Configs;
using Quack.Macros;
using Quack.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Quack.Mains;

public class MainWindow : Window, IDisposable
{
    private static readonly string ANY_COLLECTION = "All";

    private HashSet<Macro> CachedMacros { get; init; }
    private Config Config { get; init; }
    private MacroExecutionState MacroExecutionState { get; init; }
    private MacroExecutor MacroExecutor { get; init; }
    private MacroTable MacroTable { get; init; }
    private UIEvents UIEvents { get; init; }

    private MainWindowState State { get; init; }

    public MainWindow(HashSet<Macro> cachedMacros, Config config, MacroExecutionState macroExecutionState, MacroExecutor macroExecutor, MacroTable macroTable, UIEvents uiEvents) : base("Quack###mainWindow")
    {
        SizeConstraints = new()
        {
            MinimumSize = new(300, 200),
            MaximumSize = new(float.MaxValue, float.MaxValue)
        };

        CachedMacros = cachedMacros;
        Config = config;
        UIEvents = uiEvents;
        MacroExecutor = macroExecutor;
        MacroExecutionState = macroExecutionState;
        MacroTable = macroTable;

        State = new(MacroTable, UIEvents);
    }

    public void Dispose()
    {
        State.Dispose();
    }

    public override void Draw()
    {
        using (ImRaii.ItemWidth(ImGui.GetWindowWidth() - 320))
        {
            var query = State.Query;
            if (ImGui.InputTextWithHint($"Query###filter", "Search Query (min 3 chars)", ref query, ushort.MaxValue))
            {
                State.Query = query;
                State.Update();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip($"Result count: {State.FilteredMacros.Count}/{CachedMacros.Count}\n\nIndexed columns with trigram: name, path, command, tags\n\nExample queries:\n - PEDRO\n - cute tags:design\n - ^Custom tags:throw NOT cheese\n\nSee FTS5 query documentation for syntax and more examples: https://www.sqlite.org/fts5.html");
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("x###queryClear"))
        {
            State.Query = string.Empty;
            State.Update();
        }

        ImGui.SameLine();

        var collectionNames = Config.CollectionConfigs.Where(c => c.Selectable).Select(c => c.Name).Prepend(ANY_COLLECTION);
        var collectionNameIndex = State.MaybeCollectionConfig == null ? 0 : collectionNames.IndexOf(State.MaybeCollectionConfig.Name);

        using (ImRaii.ItemWidth(140))
        {
            if (ImGui.Combo($"###collectionName", ref collectionNameIndex, collectionNames.ToArray(), collectionNames.Count()))
            {
                var collectionName = collectionNames.ElementAt(collectionNameIndex);
                State.MaybeCollectionConfig = collectionName == ANY_COLLECTION ? null : Config.CollectionConfigs.Find(c => c.Name == collectionName)!;
                State.Update();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Collection Selection");
            }
        }

        if (MacroExecutor.HasRunningTasks())
        {
            ImGui.SameLine();
            using (ImRaii.Color? _ = ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.DalamudOrange), __ = ImRaii.PushColor(ImGuiCol.Text, new Vector4(0, 0, 0, 1)))
            {
                if (ImGui.Button($"Cancel All###macrosCancelAll"))
                {
                    MacroExecutor.CancelTasks();
                }
            }
        }

        var queriedMacrosId = "queriedMacros";
        using (ImRaii.Table($"{queriedMacrosId}Table", 4, ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY))
        {
            ImGui.TableSetupColumn($"Name###{queriedMacrosId}Name", ImGuiTableColumnFlags.None, 3);
            ImGui.TableSetupColumn($"Path###{queriedMacrosId}Path", ImGuiTableColumnFlags.None, 2);
            ImGui.TableSetupColumn($"Tags###{queriedMacrosId}Tags", ImGuiTableColumnFlags.None, 1);
            ImGui.TableSetupColumn($"Actions###{queriedMacrosId}Actions", ImGuiTableColumnFlags.None, 2);

            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableHeadersRow();

            var clipper = UIListClipper.Build();
            clipper.Begin(State.FilteredMacros.Count, 27);
            while (clipper.Step())
            {
                for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                {
                    var macro = State.FilteredMacros.ElementAt(i);
                    if (ImGui.TableNextColumn())
                    {
                        using (ImRaii.PushColor(ImGuiCol.Text, Config.CollectionConfigs.FindFirst(collection => collection.Matches(macro), out var collectionConfig) ? collectionConfig.Color : ImGuiColors.DalamudWhite))
                        {
                            ImGui.Text(macro.Name);
                        }
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(macro.Name);
                        }
                        if (ImGui.IsItemClicked())
                        {
                            UIEvents.RequestExecution(macro);
                        }
                    }

                    if (ImGui.TableNextColumn())
                    {
                        ImGui.Text(macro.Path);
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(macro.Path);
                        }
                        if (ImGui.IsItemClicked())
                        {
                            UIEvents.RequestExecution(macro);
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
                        if (ImGui.IsItemClicked())
                        {
                            UIEvents.RequestExecution(macro);
                        }
                    }

                    if (ImGui.TableNextColumn())
                    {
                        MacroExecutionState.Button($"{queriedMacrosId}{i}Execute", macro);
                        ImGui.SameLine();
                        if (ImGuiComponents.IconButton($"{queriedMacrosId}{i}Edit", FontAwesomeIcon.Edit))
                        {
                            UIEvents.RequestEdit(macro);
                        }
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip($"Edit macro [{macro.Name}]");
                        }
                    }

                    ImGui.TableNextRow();
                }
            }
            clipper.Destroy();
        }
    }
}
