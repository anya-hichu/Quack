using Dalamud.Interface.Colors;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using ImGuiNET;
using Newtonsoft.Json;
using Quack.Macros;
using Quack.Utils;
using Quack.Windows.Configs.States;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;

namespace Quack.Windows.Configs.Tabs;

public partial class MacrosTab : ModelTab, IDisposable
{
    [GeneratedRegexAttribute(@"\d+")]
    private static partial Regex NumberGeneratedRegex();

    private HashSet<Macro> CachedMacros { get; init; }
    private FileDialogManager FileDialogManager { get; init; }
    private MacroExecutionGui MacroExecutionGui { get; init; }
    private MacroExecutor MacroExecutor { get; init; }
    private MacroTable MacroTable { get; init; }
    private MacroTableQueue MacroTableQueue { get; init; }
    private MacroState MacroState { get; set; } = null!;
    private IPluginLog PluginLog { get; init; }
    private string? TmpConflictPath { get; set; }

    public MacrosTab(HashSet<Macro> cachedMacros, Debouncers debouncers, FileDialogManager fileDialogManager, MacroExecutionGui macroExecutionGui, MacroExecutor macroExecutor, MacroTable macroTable, MacroTableQueue macroTableQueue, IPluginLog pluginLog) : base(debouncers)
    {
        CachedMacros = cachedMacros;
        FileDialogManager = fileDialogManager;
        MacroExecutionGui = macroExecutionGui;
        MacroExecutor = macroExecutor;
        MacroTable = macroTable;
        MacroTableQueue = macroTableQueue;
        PluginLog = pluginLog;

        UpdateMacrosState();
        MacroTable.OnChange += UpdateMacrosState;
    }

    public void Dispose()
    {
        MacroTable.OnChange -= UpdateMacrosState;
    }

    public void Draw()
    {
        if (ImGui.Button("New##newMacro"))
        {
            NewMacro();
        }

        ImGui.SameLine(ImGui.GetWindowWidth() - 375);
        if (MacroExecutor.HasRunningTasks())
        {
            ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.DalamudOrange);
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0, 0, 0, 1));
            if (ImGui.Button($"Cancel All###macrosCancelAll"))
            {
                MacroExecutor.CancelTasks();
            }
            ImGui.PopStyleColor();
            ImGui.PopStyleColor();

            ImGui.SameLine();
        }

        ImGui.SameLine(ImGui.GetWindowWidth() - 305);
        if (ImGui.Button("Export Filtered##filteredMacrosExport"))
        {
            ExportMacros(MacroTable.Search(MacroState.Filter));
        }

        ImGui.SameLine();
        if (ImGui.Button("Export All##macrosExport"))
        {
            ExportMacros(CachedMacros);
        }

        ImGui.SameLine();
        if (ImGui.Button("Import##macrosImport"))
        {
            ImportMacros();
        }

        var deleteAllMacrosPopupId = "deleteAllMacrosPopup";
        if (ImGui.BeginPopup(deleteAllMacrosPopupId))
        {
            ImGui.Text($"Confirm deleting {CachedMacros.Count} macros?");

            ImGui.SetCursorPosX(15);
            ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.DalamudRed);
            if (ImGui.Button("Yes", new(100, 30)))
            {
                CachedMacros.Clear();
                MacroTableQueue.DeleteAll();
                ImGui.CloseCurrentPopup();
            }
            ImGui.PopStyleColor();

            ImGui.SameLine();
            if (ImGui.Button("No", new(100, 30)))
            {
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }

        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.DalamudRed);
        if (ImGui.Button("Delete All##macrosDeleteAll"))
        {
            if (CachedMacros.Count > 0)
            {
                ImGui.OpenPopup(deleteAllMacrosPopupId);
            }
        }
        ImGui.PopStyleColor();

        var leftChildWidth = ImGui.GetWindowWidth() * 0.3f;
        var filter = MacroState.Filter;
        ImGui.PushItemWidth(leftChildWidth);
        if (ImGui.InputText("##macrosFilter", ref filter, ushort.MaxValue))
        {
            MacroState.Filter = filter;
            UpdateMacrosState();
        }
        ImGui.PopItemWidth();

        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.2f, 0.2f, 0.2f, 0.5f));
        if (ImGui.BeginChild("paths", new(leftChildWidth, ImGui.GetWindowHeight() - ImGui.GetCursorPosY() - 10)))
        {
            DrawPathNodes(MacroState.PathNodes);
            ImGui.EndChildFrame();
        }
        ImGui.PopStyleColor();

        ImGui.SameLine();
        if (ImGui.BeginChild("macro", new(ImGui.GetWindowWidth() * 0.7f, ImGui.GetWindowHeight() - ImGui.GetCursorPosY() - 10)))
        {
            CachedMacros.FindFirst(m => m.Path == MacroState.SelectedPath, out var selectedMacro);
            if (selectedMacro != null)
            {
                var i = CachedMacros.IndexOf(selectedMacro);

                var name = selectedMacro.Name;
                var nameInputId = $"macros{i}Name";
                if (ImGui.InputText($"Name###{nameInputId}", ref name, ushort.MaxValue))
                {
                    selectedMacro.Name = name;
                    ImGui.GetID(nameInputId);
                    Debounce(nameInputId, () => MacroTableQueue.Update("name", selectedMacro));
                }

                var deleteMacroPopupId = $"macros{i}DeletePopup";
                if (ImGui.BeginPopup(deleteMacroPopupId))
                {
                    ImGui.Text($"Confirm deleting {(selectedMacro.Name.IsNullOrWhitespace() ? BLANK_NAME : selectedMacro.Name)} macro?");

                    ImGui.SetCursorPosX(15);
                    ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.DalamudRed);
                    if (ImGui.Button($"Yes###{deleteMacroPopupId}Yes", new(100, 30)))
                    {
                        DeleteMacro(selectedMacro);
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.PopStyleColor();

                    ImGui.SameLine();
                    if (ImGui.Button($"No###{deleteMacroPopupId}No", new(100, 30)))
                    {
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.EndPopup();
                }

                ImGui.SameLine(ImGui.GetWindowWidth() - 150);
                if (ImGui.Button($"Duplicate###macros{i}Duplicate"))
                {
                    DuplicateMacro(selectedMacro);
                }
                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.DalamudRed);
                if (ImGui.Button($"Delete###macros{i}Delete"))
                {
                    ImGui.OpenPopup(deleteMacroPopupId);
                }
                ImGui.PopStyleColor();

                var pathConflictPopupId = $"macros{i}PathConflictPopup";
                if (ImGui.BeginPopup(pathConflictPopupId))
                {
                    ImGui.Text($"Confirm macro override?");

                    ImGui.SetCursorPosX(15);
                    ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.DalamudRed);
                    if (ImGui.Button("Yes", new(100, 30)))
                    {
                        var oldPath = selectedMacro.Path;
                        CachedMacros.Remove(selectedMacro);
                        selectedMacro.Path = TmpConflictPath!;
                        CachedMacros.Add(selectedMacro);

                        MacroState.SelectedPath = TmpConflictPath;
                        MacroTableQueue.Update("path", selectedMacro, oldPath);

                        TmpConflictPath = null;
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.PopStyleColor();

                    ImGui.SameLine();
                    if (ImGui.Button("No", new(100, 30)))
                    {
                        ImGui.CloseCurrentPopup();
                    }

                    ImGui.EndPopup();
                }

                var path = TmpConflictPath ?? selectedMacro.Path;
                var pathInputId = $"macros{i}Path";
                if (ImGui.InputText($"Path###{pathInputId}", ref path, ushort.MaxValue))
                {
                    TmpConflictPath = null;
                    if (CachedMacros.FindFirst(m => m.Path == path, out var conflictingMacro) && selectedMacro != conflictingMacro)
                    {
                        TmpConflictPath = path;
                        ImGui.OpenPopup(pathConflictPopupId);
                    }
                    else
                    {
                        var oldPath = selectedMacro.Path;
                        CachedMacros.Remove(selectedMacro);
                        selectedMacro.Path = path;
                        CachedMacros.Add(selectedMacro);
                        MacroState.SelectedPath = path;
                        Debounce(pathInputId, () => MacroTableQueue.Update("path", selectedMacro, oldPath));
                    }
                }

                var tags = string.Join(',', selectedMacro.Tags);
                var tagInputId = $"macros{i}Tags";
                if (ImGui.InputText($"Tags (comma separated)###{tagInputId}", ref tags, ushort.MaxValue))
                {
                    selectedMacro.Tags = tags.Split(',').Select(t => t.Trim()).ToArray();
                    Debounce(tagInputId, () => MacroTableQueue.Update("tags", selectedMacro));
                }

                var command = selectedMacro.Command;
                var commandInputId = $"macros{i}Command";
                var commandInput = ImGui.InputText($"Command###{commandInputId}", ref command, ushort.MaxValue);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Example: /shock\n\nExtra calling arguments will replace the content placeholders ({0}, {1}, etc.) dynamically.\nAdditionally placeholders {i} can be escaped by doubling the brackets {{i}} if needed.");
                }
                if (commandInput)
                {
                    selectedMacro.Command = command;
                    Debounce(commandInputId, () => MacroTableQueue.Update("command", selectedMacro));
                }

                if (!command.IsNullOrWhitespace())
                {
                    var conflictingMacros = CachedMacros.Where(m => m != selectedMacro && m.Command == selectedMacro.Command);
                    if (conflictingMacros.Any())
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudRed);
                        ImGui.Text($"Command conflicts with {string.Join(", ", conflictingMacros.Select(m => m.Name))}");
                        ImGui.PopStyleColor();
                    }
                }

                var args = selectedMacro.Args;
                var argsInputId = $"macros{i}Args";
                var argsInput = ImGui.InputText($"Args###{argsInputId}", ref args, ushort.MaxValue);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Space separated list of default arguments (quoting optional) used to replace content placeholders ({0}, {1}, etc.)");
                }
                if (argsInput)
                {
                    selectedMacro.Args = args;
                    Debounce(argsInputId, () => MacroTableQueue.Update("args", selectedMacro));
                }

                var content = selectedMacro.Content;
                var contentInputId = $"macros{i}Content";
                var contentInput = ImGui.InputTextMultiline($"Content###{contentInputId}", ref content, ushort.MaxValue, new(ImGui.GetWindowWidth() - 200, ImGui.GetWindowHeight() - ImGui.GetCursorPosY() - 30));
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Additional behaviors:\n - Possible to wait until a nested macro is completed using <wait.macro> placeholder\n - Macro cancellation (/macrocancel) is scoped to the currently executing macro and can also be waited on using <wait.cancel> (trap)");
                }
                if (contentInput)
                {
                    selectedMacro.Content = content;
                    Debounce(contentInputId, () => MacroTableQueue.Update("content", selectedMacro));
                }

                var loop = selectedMacro.Loop;
                var loopInputId = $"macros{i}Loop";
                var loopInput = ImGui.Checkbox($"Loop###{loopInputId}", ref loop);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Execution can be stopped using 'Cancel All' button or '/quack cancel' command");
                }
                if (loopInput)
                {
                    selectedMacro.Loop = loop;
                    Debounce(loopInputId, () => MacroTableQueue.Update("loop", selectedMacro));
                }

                ImGui.SameLine(ImGui.GetWindowWidth() - 255);
                MacroExecutionGui.Button(selectedMacro);
            }
            else
            {
                ImGui.Text("No macro selected");
            }

            ImGui.EndChildFrame();
        }
    }


    private void NewMacro()
    {
        MaybeAddMacro(new());
    }

    private void NewMacro(string path)
    {
        var newMacro = new Macro();
        newMacro.Path = path;
        MaybeAddMacro(newMacro);
    }

    private void DuplicateMacro(Macro macro)
    {
        var duplicateMacro = macro.Clone();
        for (var i = 2; CachedMacros.Contains(duplicateMacro); i++)
        {
            var suffix = $" ({i})";
            duplicateMacro.Name = $"{macro.Name}{suffix}";
            duplicateMacro.Path = $"{macro.Path}{suffix}";
        }

        MaybeAddMacro(duplicateMacro);
    }

    private void MaybeAddMacro(Macro macro)
    {
        if (CachedMacros.Add(macro))
        {
            MacroState.SelectedPath = macro.Path;
            MacroTableQueue.Insert(macro);
        }
    }

    private void ExportMacros(IEnumerable<Macro> macros)
    {
        FileDialogManager.SaveFileDialog("Export Macros", ".*", "macros.json", ".json", (valid, path) =>
        {
            if (valid)
            {
                using var file = File.CreateText(path);
                new JsonSerializer().Serialize(file, macros);
            }
        });
    }

    private void ImportMacros()
    {
        FileDialogManager.OpenFileDialog("Import Macros", "{.json}", (valid, path) =>
        {
            if (valid)
            {
                using StreamReader reader = new(path);
                var json = reader.ReadToEnd();
                var importedMacros = JsonConvert.DeserializeObject<List<Macro>>(json)!;

                var conflictingMacros = CachedMacros.Intersect(importedMacros);
                CachedMacros.ExceptWith(importedMacros);
                CachedMacros.UnionWith(importedMacros);

                MacroTableQueue.Delete(conflictingMacros);
                MacroTableQueue.Insert(importedMacros);
            }
        });
    }

    private void DeleteMacro(Macro macro)
    {
        CachedMacros.Remove(macro);
        MacroTableQueue.Delete(macro);
    }

    private void DeleteMacros(IEnumerable<Macro> macros)
    {
        var list = macros.ToList();
        CachedMacros.ExceptWith(list);
        MacroTableQueue.Delete(list);
    }

    private void UpdateMacrosState()
    {
        var selectedPath = MacroState?.SelectedPath;
        var filter = MacroState != null ? MacroState.Filter : string.Empty;
        PluginLog.Verbose($"Filtering {CachedMacros.Count} macros with filter '{filter}'");
        var filteredMacros = MacroSearch.Lookup(CachedMacros, filter);

        MacroState = new(
            MacroState.GeneratePathNodes(filteredMacros),
            selectedPath,
            filter
        );

        MacroExecutionGui.UpdateExecutions(CachedMacros.FindFirst(m => m.Path == selectedPath, out var selectedMacro) ? filteredMacros.Union([selectedMacro]) : filteredMacros);
    }

    private void DrawPathNodes(HashSet<TreeNode<string>> nodes)
    {
        // Pad with zeros to improve sorting with numbers
        var sortedNodes = nodes.OrderBy(n => NumberGeneratedRegex().Replace(n.Item, m => m.Value.PadLeft(10, '0')));
        foreach (var node in sortedNodes)
        {
            var name = Path.GetFileName(node.Item);
            if (node.Children.Count > 0)
            {
                var opened = ImGui.TreeNodeEx($"{name}###macro{node.Item}TreeNode");

                var popupId = $"macro{node.Item}TreeNodePopup";
                if (ImGui.BeginPopupContextItem(popupId))
                {
                    if (ImGui.MenuItem($"New###{popupId}New"))
                    {
                        NewMacro($"{node.Item}/");
                    }
                    var macros = CachedMacros.Where(m => m.Path.StartsWith($"{node.Item}/"));
                    if (ImGui.MenuItem($"Export###{popupId}Export"))
                    {
                        ExportMacros(macros);
                    }

                    if (ImGui.MenuItem($"Delete###{popupId}Delete"))
                    {
                        DeleteMacros(macros);
                    }
                    ImGui.EndPopup();
                }

                if (opened)
                {
                    DrawPathNodes(node.Children);
                    ImGui.TreePop();
                }
            }
            else
            {
                var flags = ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet;
                if (node.Item == MacroState.SelectedPath)
                {
                    flags |= ImGuiTreeNodeFlags.Selected;
                }

                var opened = ImGui.TreeNodeEx($"{(name.IsNullOrWhitespace() ? BLANK_NAME : name)}###macro{node.Item}TreeLeaf", flags);

                var popupId = $"macro{node.Item}TreeLeafPopup";
                if (ImGui.BeginPopupContextItem(popupId))
                {
                    CachedMacros.FindFirst(m => m.Path == node.Item, out var macro);
                    if (macro != null)
                    {
                        if (ImGui.MenuItem($"Duplicate###{popupId}Duplicate"))
                        {
                            DuplicateMacro(macro);
                        }

                        if (ImGui.MenuItem($"Export###{popupId}Export"))
                        {
                            ExportMacros([macro]);
                        }

                        if (ImGui.MenuItem($"Delete###{popupId}Delete"))
                        {
                            DeleteMacro(macro);
                        }
                    }
                    ImGui.EndPopup();
                }

                if (opened)
                {
                    if (ImGui.IsItemClicked())
                    {
                        MacroState.SelectedPath = node.Item;
                    }
                    ImGui.TreePop();
                }
            }
        }
    }
}
