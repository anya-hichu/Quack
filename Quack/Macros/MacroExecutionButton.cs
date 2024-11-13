using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using Quack.Configs;
using System.Collections.Generic;

namespace Quack.Macros;

public class MacroExecutionButton(Config config, MacroExecutor macroExecutor)
{
    private Config Config { get; init; } = config;
    private MacroExecutor MacroExecutor { get; init; } = macroExecutor;
    private Dictionary<Macro, MacroExecution> MacroExecutionByMacro { get; set; } = [];
    private HashSet<string> OpenPopupIds { get; init; } = [];

    public void Draw(Macro macro)
    {
        if (!MacroExecutionByMacro.TryGetValue(macro, out var macroExecution))
        {
            MacroExecutionByMacro[macro] = macroExecution = BuildMacroExecution(macro);
        }

        var hash = macro.GetHashCode();
        var isExecutable = macroExecution.IsExecutable();
        var advancedExecutionPopupId = $"macros{hash}AdvancedExecutionPopup";

        using (var popup = ImRaii.Popup(advancedExecutionPopupId))
        {
            if (popup.Success)
            {
                var format = macroExecution.Format;
                if (ImGui.InputText($"Format##macros{hash}ExecutionFormat", ref format, ushort.MaxValue))
                {
                    macroExecution.Format = format;
                }

                ImGui.SameLine();
                if (ImGui.Button($"Config##macros{hash}ExecutionUseConfigFormat"))
                {
                    macroExecution.UseConfigFormat();
                }

                var requiredArgsLength = macroExecution.RequiredArgsLength();

                if (requiredArgsLength > 0)
                {
                    var args = macroExecution.Args;
                    if (ImGui.InputText($"Args##macros{hash}ExecutionArgs", ref args, ushort.MaxValue))
                    {
                        macroExecution.Args = args;
                        macroExecution.ParseArgs();
                    }
                }

                if (isExecutable && ImGui.Button($"Execute##macros{hash}ExecutionExecute"))
                {
                    macroExecution.ExecuteTask();
                    ImGui.CloseCurrentPopup();
                    MacroExecutionByMacro.Remove(macro);
                    OpenPopupIds.Remove(advancedExecutionPopupId);
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

        ImGui.Button($"Execute##macros{hash}Execute");
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Right-click for advanced execution options");
        }
        if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
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
}
