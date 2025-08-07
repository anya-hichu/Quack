using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using Quack.Configs;
using Quack.UI;
using System;
using System.Collections.Generic;

namespace Quack.Macros;

public class MacroExecutionState : IDisposable
{
    private Config Config { get; init; }
    private MacroExecutor MacroExecutor { get; init; }
    private UIEvents UIEvents { get; init; }

    private Dictionary<Macro, MacroExecution> MacroExecutionByMacro { get; set; } = [];
    private HashSet<string> OpenPopupIds { get; init; } = [];
    private HashSet<Macro> ExecutionRequests { get; init; } = [];

    public MacroExecutionState(Config config, MacroExecutor macroExecutor, UIEvents uiEvents)
    {
        Config = config;
        MacroExecutor = macroExecutor;
        UIEvents = uiEvents;

        UIEvents.OnMacroExecutionRequest += OnExecutionRequest;
    }

    public void Dispose()
    {
        UIEvents.OnMacroExecutionRequest += OnExecutionRequest;
    }

    public void Button(string baseId, Macro macro)
    {
        if (!MacroExecutionByMacro.TryGetValue(macro, out var macroExecution))
        {
            MacroExecutionByMacro[macro] = macroExecution = BuildMacroExecution(macro);
        }

        var isExecutable = macroExecution.IsExecutable();
        var advancedExecutionPopupId = $"{baseId}AdvancedPopup";

        using (var popup = ImRaii.Popup(advancedExecutionPopupId))
        {
            if (popup)
            {
                var format = macroExecution.Format;
                if (ImGui.InputText($"Format###{baseId}Format", ref format, ushort.MaxValue))
                {
                    macroExecution.Format = format;
                }

                ImGui.SameLine();
                if (ImGui.Button($"Config###{baseId}UseConfigFormat"))
                {
                    macroExecution.UseConfigFormat();
                }

                var requiredArgsLength = macroExecution.RequiredArgsLength();

                if (requiredArgsLength > 0)
                {
                    var args = macroExecution.Args;
                    if (ImGui.InputText($"Args###{baseId}Args", ref args, ushort.MaxValue))
                    {
                        macroExecution.Args = args;
                        macroExecution.ParseArgs();
                    }
                }

                if (isExecutable)
                {
                    if (ImGui.Button($"Execute###{baseId}Execute"))
                    {
                        macroExecution.ExecuteTask();
                        ImGui.CloseCurrentPopup();
                        MacroExecutionByMacro.Remove(macro);
                        OpenPopupIds.Remove(advancedExecutionPopupId);
                    }
                }
                else
                {
                    ImGui.Text($"Execution requires {requiredArgsLength} argument(s)");
                }
            }
            else if (OpenPopupIds.Contains(advancedExecutionPopupId))
            {
                MacroExecutionByMacro.Remove(macro);
                OpenPopupIds.Remove(advancedExecutionPopupId);
            }
        }

        ImGui.Button($"Execute###{baseId}Execute");
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip($"Click <RIGHT> for advanced execution of macro [{macro.Name}]");
        }
        // TODO: Fix advanced execution popup on request
        if (ImGui.IsItemClicked(ImGuiMouseButton.Left) || ExecutionRequests.Remove(macro))
        {
            if (isExecutable)
            {
                macroExecution.ExecuteTask();
            }
            else
            {
                OpenPopup(advancedExecutionPopupId);
            }
        }
        else if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            OpenPopup(advancedExecutionPopupId);
        }
    }

    private MacroExecution BuildMacroExecution(Macro macro)
    {
        var macroExecution = new MacroExecution(macro, Config, MacroExecutor);
        macroExecution.ParseArgs();
        return macroExecution;
    }

    private void OpenPopup(string id)
    {
        ImGui.OpenPopup(id);
        OpenPopupIds.Add(id);
    }

    private void OnExecutionRequest(Macro macro)
    {
        ExecutionRequests.Add(macro);
    }
}
