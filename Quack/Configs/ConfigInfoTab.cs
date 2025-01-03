using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using Quack.Macros;
using Quack.UI;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Quack.Configs;

public class ConfigInfoTab
{
    private string DatabasePath { get; init; }
    private MacroTableQueue MacroTableQueue { get; init; }
    private UIEvents UIEvents { get; init; }

    private ConfigInfoTabState ConfigInfoTabState { get; init; }

    public ConfigInfoTab(HashSet<Macro> cachedMacros, string databasePath, MacroTable macroTable, MacroTableQueue macroTableQueue, UIEvents uiEvents)
    {
        DatabasePath = databasePath;
        MacroTableQueue = macroTableQueue;
        UIEvents = uiEvents;

        ConfigInfoTabState = new(cachedMacros, macroTable);
    }


    public void Draw()
    {
        var state = ConfigInfoTabState;
        if (ImGui.CollapsingHeader($"Registered Commands"))
        {
            using (ImRaii.PushIndent())
            {
                var registeredCommandsId = "registeredCommands";

                var filteredMacroWithCommands = state.FilteredMacroWithCommands;
                var filter = state.Filter;
                if (ImGui.InputTextWithHint("###macrosFilter", "Filter", ref filter, ushort.MaxValue))
                {
                    state.Filter = filter;
                    state.Update();
                }
                ImGui.SameLine();
                if (ImGui.Button("x###macrosFilterClear"))
                {
                    state.Filter = string.Empty;
                    state.Update();
                }

                using (ImRaii.Table($"{registeredCommandsId}Table", 4, ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY, new(ImGui.GetWindowWidth() - ImGui.GetCursorPosX(), 250)))
                {
                    ImGui.TableSetupColumn($"Command###{registeredCommandsId}Command", ImGuiTableColumnFlags.None, 1);
                    ImGui.TableSetupColumn($"Name###{registeredCommandsId}Name", ImGuiTableColumnFlags.None, 2);
                    ImGui.TableSetupColumn($"Path###{registeredCommandsId}Path", ImGuiTableColumnFlags.None, 4);
                    ImGui.TableSetupColumn($"Actions###{registeredCommandsId}Actions", ImGuiTableColumnFlags.None, 2);

                    ImGui.TableSetupScrollFreeze(0, 1);
                    ImGui.TableHeadersRow();

                    var clipper = UIListClipper.Build();
                    clipper.Begin(filteredMacroWithCommands.Count, 27);
                    while (clipper.Step())
                    {
                        for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                        {
                            var macro = filteredMacroWithCommands.ElementAt(i);
                            if (ImGui.TableNextColumn())
                            {
                                ImGui.Text(macro.Command);
                                if (ImGui.IsItemHovered())
                                {
                                    ImGui.SetTooltip(macro.Command);
                                }
                                EditMacroOnItemClick(macro);
                            }

                            if (ImGui.TableNextColumn())
                            {
                                ImGui.Text(macro.Name);
                                if (ImGui.IsItemHovered())
                                {
                                    ImGui.SetTooltip(macro.Name);
                                }
                                EditMacroOnItemClick(macro);
                            }

                            if (ImGui.TableNextColumn())
                            {
                                ImGui.Text(macro.Path);
                                if (ImGui.IsItemHovered())
                                {
                                    ImGui.SetTooltip(macro.Path);
                                }
                                EditMacroOnItemClick(macro);
                            }

                            if (ImGui.TableNextColumn())
                            {
                                if (ImGui.Button($"Unregister###{registeredCommandsId}{i}Unregister"))
                                {
                                    macro.Command = string.Empty;
                                    MacroTableQueue.Update("command", macro);
                                }

                                ImGui.SameLine();
                                ImGuiComponents.IconButton($"{registeredCommandsId}{i}Edit", FontAwesomeIcon.Edit);
                                EditMacroOnItemClick(macro);
                                if (ImGui.IsItemHovered())
                                {
                                    ImGui.SetTooltip($"Edit macro [{macro.Name}]");
                                }
                            }
                        }
                    }
                }
            } 
        }

        if (ImGui.CollapsingHeader("Database"))
        {
            using (ImRaii.PushIndent())
            {
                ImGui.Text($"Path: {DatabasePath}");
                ImGui.Text($"Size: {FormatFileLength(new FileInfo(DatabasePath).Length)}");
            }
        }
    }

    public void EditMacroOnItemClick(Macro macro)
    {
        if (ImGui.IsItemClicked())
        {
            Task.Run(() =>
            {
                // Delay otherwise SetSelected tab doesn't work
                Thread.Sleep(40);
                UIEvents.RequestEdit(macro);
            });
        }
    }

    public static string FormatFileLength(long length)
    {
        return length.ToString().Length switch
        {
            > 9 => $"{length / 1000000000} GB",
            > 6 => $"{length / 1000000} MB",
            > 3 => $"{length / 1000} KB",
            _ => $"{length} Bytes"
        };
    }
}
