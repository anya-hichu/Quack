using ImGuiNET;
using Quack.Macros;
using System.Collections.Generic;

namespace Quack.UI.Helpers;

public class MacroExecutionHelper(Config config, MacroExecutor macroExecutor)
{
    private Config Config { get; init; } = config;
    private MacroExecutor MacroExecutor { get; init; } = macroExecutor;
    private Dictionary<Macro, MacroExecution> MacroExecutionByMacro { get; set; } = [];
    private HashSet<string> OpenPopupIds { get; init; } = [];

    public void Button(Macro macro)
    {
        if (!MacroExecutionByMacro.TryGetValue(macro, out var macroExecution))
        {
            MacroExecutionByMacro[macro] = macroExecution = BuildMacroExecution(macro);
        }

        var hash = macro.GetHashCode();
        var isExecutable = macroExecution.IsExecutable();
        var advancedExecutionPopupId = $"macros{hash}AdvancedExecutionPopup";
        if (ImGui.BeginPopup(advancedExecutionPopupId))
        {
            var format = macroExecution.Format;
            if (ImGui.InputText($"Format###macros{hash}ExecutionFormat", ref format, ushort.MaxValue))
            {
                macroExecution.Format = format;
            }

            ImGui.SameLine();
            if (ImGui.Button($"Config###macros{hash}ExecutionUseConfigFormat"))
            {
                macroExecution.UseConfigFormat();
            }

            if (macroExecution.RequiredArgsLength() > 0)
            {
                var args = macroExecution.Args;
                if (ImGui.InputText($"Args###macros{hash}ExecutionArgs", ref args, ushort.MaxValue))
                {
                    macroExecution.Args = args;
                    macroExecution.ParseArgs();
                }
            }

            if (isExecutable)
            {
                if (ImGui.Button($"Execute###macros{hash}ExecutionExecute"))
                {
                    macroExecution.ExecuteTask();
                    ImGui.CloseCurrentPopup();
                    OpenPopupIds.Remove(advancedExecutionPopupId);
                    MacroExecutionByMacro.Remove(macro);
                }
            }
            else
            {
                ImGui.Text($"Execution requires {macroExecution.RequiredArgsLength()} argument(s)");
            }
            ImGui.EndPopup();
        }
        else
        {
            // Clear on close
            if (OpenPopupIds.Contains(advancedExecutionPopupId))
            {
                MacroExecutionByMacro.Remove(macro);
                OpenPopupIds.Remove(advancedExecutionPopupId);
            }
        }

        var executeButton = ImGui.Button($"Execute###macros{hash}Execute");
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Right click for advanced execution options");
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
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
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

    public static string GetNonExecutableMessage(MacroExecution macroExecution)
    {
        return $"Expected {macroExecution.RequiredArgsLength()} argument(s) for macro '{macroExecution.Macro.Name}' (parsed {macroExecution.ParsedArgs.Length})";
    }
}
