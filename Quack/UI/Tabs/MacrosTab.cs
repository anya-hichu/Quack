using Dalamud.Interface.Colors;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using ImGuiNET;
using Newtonsoft.Json;
using Quack.Macros;
using Quack.Utils;
using Quack.UI.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Quack.UI.States;
using Quack.UI.Tabs;
using Dalamud.Interface.Utility.Raii;

namespace Quack.UI.Configs.Tabs;

public partial class MacrosTab : ModelTab, IDisposable
{
    [GeneratedRegexAttribute(@"\d+")]
    private static partial Regex NumberGeneratedRegex();

    private HashSet<Macro> CachedMacros { get; init; }
    private FileDialogManager FileDialogManager { get; init; }
    private MacroExecutionHelper MacroExecutionHelper { get; init; }
    private MacroExecutor MacroExecutor { get; init; }
    private MacroTable MacroTable { get; init; }
    private MacroTableQueue MacroTableQueue { get; init; }
    private MacroEditorState MacroEditorState { get; set; } = null!;
    private IPluginLog PluginLog { get; init; }
    private string? TmpConflictPath { get; set; }

    public MacrosTab(HashSet<Macro> cachedMacros, Debouncers debouncers, FileDialogManager fileDialogManager, MacroExecutionHelper macroExecutionHelper, MacroExecutor macroExecutor, MacroTable macroTable, MacroTableQueue macroTableQueue, IPluginLog pluginLog) : base(debouncers)
    {
        CachedMacros = cachedMacros;
        FileDialogManager = fileDialogManager;
        MacroExecutionHelper = macroExecutionHelper;
        MacroExecutor = macroExecutor;
        MacroTable = macroTable;
        MacroTableQueue = macroTableQueue;
        PluginLog = pluginLog;

        UpdateMacroEditorState();
        MacroTable.OnChange += UpdateMacroEditorState;
    }

    public void Dispose()
    {
        MacroTable.OnChange -= UpdateMacroEditorState;
    }

    public void Draw()
    {
        if (ImGui.Button("New##newMacro"))
        {
            NewMacro();
        }

        ImGui.SameLine(ImGui.GetWindowWidth() - 387);
        if (MacroExecutor.HasRunningTasks())
        {
            using (ImRaii.Color? _ = ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.DalamudOrange), __ = ImRaii.PushColor(ImGuiCol.Text, new Vector4(0, 0, 0, 1)))
            {
                if (ImGui.Button($"Cancel All###macrosCancelAll"))
                {
                    MacroExecutor.CancelTasks();
                }
            }
            ImGui.SameLine();
        }

        ImGui.SameLine(ImGui.GetWindowWidth() - 317);
        if (ImGui.Button("Export Filtered##filteredMacrosExport"))
        {
            ExportMacros(MacroTable.Search(MacroEditorState.Filter));
        }

        ImGui.SameLine();
        if (ImGui.Button("Export All##macrosExport"))
        {
            ExportMacros(CachedMacros);
        }

        ImGui.SameLine();
        if (ImGui.Button("Import All##macrosImport"))
        {
            ImportMacros();
        }

        var deleteAllMacrosPopupId = "deleteAllMacrosPopup";
        using (var popup = ImRaii.Popup(deleteAllMacrosPopupId))
        {
            if (popup.Success)
            {
                ImGui.Text($"Confirm deleting {CachedMacros.Count} macros?");

                ImGui.SetCursorPosX(15);
                using (ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.DalamudRed))
                {
                    if (ImGui.Button("Yes", new(100, 30)))
                    {
                        CachedMacros.Clear();
                        MacroTableQueue.DeleteAll();
                        ImGui.CloseCurrentPopup();
                    }
                }
                ImGui.SameLine();
                if (ImGui.Button("No", new(100, 30)))
                {
                    ImGui.CloseCurrentPopup();
                }
            }
        }

        ImGui.SameLine();
        using (ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.DalamudRed))
        {
            if (ImGui.Button("Delete All##macrosDeleteAll"))
            {
                if (CachedMacros.Count > 0)
                {
                    ImGui.OpenPopup(deleteAllMacrosPopupId);
                }
            }
        }

        var leftChildWidth = ImGui.GetWindowWidth() * 0.3f;
        var filter = MacroEditorState.Filter;
        using (ImRaii.ItemWidth(leftChildWidth))
        {
            if (ImGui.InputText("##macrosFilter", ref filter, ushort.MaxValue))
            {
                MacroEditorState.Filter = filter;
                UpdateMacroEditorState();
            }
        }

        using (ImRaii.PushColor(ImGuiCol.ChildBg, new Vector4(0.2f, 0.2f, 0.2f, 0.5f)))
        {
            using (ImRaii.Child("paths", new(leftChildWidth, ImGui.GetWindowHeight() - ImGui.GetCursorPosY() - 10)))
            {
                DrawPathNodes(MacroEditorState.PathNodes);
            }
        }

        ImGui.SameLine();
        using (ImRaii.Child("macro", new(ImGui.GetWindowWidth() * 0.7f, ImGui.GetWindowHeight() - ImGui.GetCursorPosY() - 10)))
        {
            CachedMacros.FindFirst(m => m.Path == MacroEditorState.SelectedPath, out var selectedMacro);
            if (selectedMacro != null)
            {
                var i = CachedMacros.IndexOf(selectedMacro);

                var name = selectedMacro.Name;
                var nameInputId = $"macros{i}Name";
                if (ImGui.InputText($"Name###{nameInputId}", ref name, ushort.MaxValue))
                {
                    selectedMacro.Name = name;
                    Debounce(nameInputId, () => MacroTableQueue.Update("name", selectedMacro));
                }

                var deleteMacroPopupId = $"macros{i}DeletePopup";
                using (var popup = ImRaii.Popup(deleteMacroPopupId))
                {
                    if (popup.Success)
                    {
                        ImGui.Text($"Confirm deleting {(selectedMacro.Name.IsNullOrWhitespace() ? BLANK_NAME : selectedMacro.Name)} macro?");

                        ImGui.SetCursorPosX(15);
                        using (ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.DalamudRed))
                        {
                            if (ImGui.Button($"Yes###{deleteMacroPopupId}Yes", new(100, 30)))
                            {
                                DeleteMacro(selectedMacro);
                                ImGui.CloseCurrentPopup();
                            }
                        }

                        ImGui.SameLine();
                        if (ImGui.Button($"No###{deleteMacroPopupId}No", new(100, 30)))
                        {
                            ImGui.CloseCurrentPopup();
                        }
                    }
                }

                ImGui.SameLine(ImGui.GetWindowWidth() - 200);
                if (ImGui.Button($"Export###macros{i}Export"))
                {
                    ExportMacros([selectedMacro]);
                }
                ImGui.SameLine();
                if (ImGui.Button($"Duplicate###macros{i}Duplicate"))
                {
                    DuplicateMacro(selectedMacro);
                }
                ImGui.SameLine();

                using (ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.DalamudRed))
                {
                    if (ImGui.Button($"Delete###macros{i}Delete"))
                    {
                        ImGui.OpenPopup(deleteMacroPopupId);
                    }
                }
                
                var pathConflictPopupId = $"macros{i}PathConflictPopup";
                using (var popup = ImRaii.Popup(pathConflictPopupId))
                {
                    if (popup.Success)
                    {
                        ImGui.Text($"Confirm macro override?");
                        ImGui.SetCursorPosX(15);
                        using (ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.DalamudRed))
                        {
                            if (ImGui.Button("Yes", new(100, 30)))
                            {
                                var oldPath = selectedMacro.Path;
                                CachedMacros.Remove(selectedMacro);
                                selectedMacro.Path = TmpConflictPath!;
                                CachedMacros.Add(selectedMacro);

                                MacroEditorState.SelectedPath = TmpConflictPath;
                                MacroTableQueue.Update("path", selectedMacro, oldPath);

                                TmpConflictPath = null;
                                ImGui.CloseCurrentPopup();
                            }
                        }
                        ImGui.SameLine();
                        if (ImGui.Button("No", new(100, 30)))
                        {
                            ImGui.CloseCurrentPopup();
                        }
                    }                 
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
                        MacroEditorState.SelectedPath = path;
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
                        using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudRed))
                        {
                            ImGui.Text($"Command conflicts with {string.Join(", ", conflictingMacros.Select(m => m.Name))}");
                        }
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
                MacroExecutionHelper.Button(selectedMacro);
            }
            else
            {
                ImGui.Text("No macro selected");
            }
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
            MacroEditorState.SelectedPath = macro.Path;
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

    private void UpdateMacroEditorState()
    {
        var selectedPath = MacroEditorState?.SelectedPath;
        var filter = MacroEditorState != null ? MacroEditorState.Filter : string.Empty;
        PluginLog.Verbose($"Filtering {CachedMacros.Count} macros with filter '{filter}'");
        var filteredMacros = MacroSearch.Lookup(CachedMacros, filter);

        MacroEditorState = new(
            MacroEditorHelper.BuildPathNodes(filteredMacros),
            selectedPath,
            filter
        );
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
                using (var contextPopup = ImRaii.ContextPopupItem(popupId))
                {
                    if (contextPopup.Success)
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
                    }
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
                if (node.Item == MacroEditorState.SelectedPath)
                {
                    flags |= ImGuiTreeNodeFlags.Selected;
                }

                var opened = ImGui.TreeNodeEx($"{(name.IsNullOrWhitespace() ? BLANK_NAME : name)}###macro{node.Item}TreeLeaf", flags);
                var popupId = $"macro{node.Item}TreeLeafPopup";
                using (var contextPopup = ImRaii.ContextPopupItem(popupId))
                {
                    if (contextPopup.Success)
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
                    }
                }
                if (opened)
                {
                    if (ImGui.IsItemClicked())
                    {
                        MacroEditorState.SelectedPath = node.Item;
                    }
                    ImGui.TreePop();
                }
            }
        }
    }
}
