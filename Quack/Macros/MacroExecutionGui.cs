using Dalamud.Utility;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quack.Macros;

public class MacroExecutionGui(Config config, MacroExecutor macroExecutor)
{
    private Config Config { get; init; } = config;
    private MacroExecutor MacroExecutor { get; init; } = macroExecutor;
    private Dictionary<Macro, MacroExecution> MacroExecutionByMacro { get; set; } = [];
    private HashSet<string> OpenPopupIds { get; init; } = [];

    public void UpdateExecutions(IEnumerable<Macro> macros)
    {
        MacroExecutionByMacro = macros.ToDictionary(m => m, BuildMacroExecution);
    }

    public void Button(Macro macro)
    {
        if (MacroExecutionByMacro.TryGetValue(macro, out var macroExecution))
        {
            var i = MacroExecutionByMacro.Keys.IndexOf(macro);

            var isExecutable = macroExecution.IsExecutable();

            var advancedExecutionPopupId = $"macros{i}AdvancedExecutionPopup";
            if (ImGui.BeginPopup(advancedExecutionPopupId))
            {
                var format = macroExecution.Format;
                if (ImGui.InputText($"Format###macros{i}ExecutionFormat", ref format, ushort.MaxValue))
                {
                    macroExecution.Format = format;
                }

                ImGui.SameLine();
                if (ImGui.Button($"Config###macros{i}ExecutionUseConfigFormat"))
                {
                    macroExecution.UseConfigFormat();
                }

                var args = macroExecution.Args;
                if (ImGui.InputText($"Args###macros{i}ExecutionArgs", ref args, ushort.MaxValue))
                {
                    macroExecution.Args = args;
                    macroExecution.ParseArgs();
                }

                if (isExecutable)
                {
                    if (ImGui.Button($"Execute###macros{i}ExecutionExecute"))
                    {
                        macroExecution.ExecuteTask();
                    }
                }
                else
                {
                    ImGui.Text($"Execution requires {macroExecution.RequiredArgumentsLength()} argument(s)");
                }
                ImGui.EndPopup();
            }
            else
            {
                // Clear on close
                if (OpenPopupIds.Contains(advancedExecutionPopupId))
                {
                    MacroExecutionByMacro[macro] = BuildMacroExecution(macro);
                    OpenPopupIds.Remove(advancedExecutionPopupId);
                }
            }

            var executeButton = ImGui.Button($"Execute###macros{i}Execute");
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
        return $"Expected {macroExecution.RequiredArgumentsLength()} argument(s) for macro '{macroExecution.Macro.Name}' (parsed {macroExecution.ParsedArgs.Length})";
    }
}
