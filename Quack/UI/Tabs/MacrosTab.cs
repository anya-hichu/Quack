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
using Dalamud.Game.ClientState.Keys;

namespace Quack.UI.Configs.Tabs;

public partial class MacrosTab : ModelTab, IDisposable
{
    [GeneratedRegexAttribute(@"\d+")]
    private static partial Regex NumberGeneratedRegex();

    private HashSet<Macro> CachedMacros { get; init; }
    private IKeyState KeyState { get; init; }
    private MacroExecutionHelper MacroExecutionHelper { get; init; }
    private MacroExecutor MacroExecutor { get; init; }
    private MacroTable MacroTable { get; init; }
    private MacroTableQueue MacroTableQueue { get; init; }
    private MacroEditorState MacroEditorState { get; set; } = null!;
    private IPluginLog PluginLog { get; init; }
    private string? TmpConflictPath { get; set; }

    public MacrosTab(HashSet<Macro> cachedMacros, Debouncers debouncers, FileDialogManager fileDialogManager, IKeyState keyState, MacroExecutionHelper macroExecutionHelper, MacroExecutor macroExecutor, MacroTable macroTable, MacroTableQueue macroTableQueue, IPluginLog pluginLog) : base(debouncers, fileDialogManager)
    {
        CachedMacros = cachedMacros;
        KeyState = keyState;
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
            MaybeAddMacro(new());
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

        ImGui.SameLine(ImGui.GetWindowWidth() - 300);
        ImGui.Button("Export All##macrosExport");
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Right-click for clipboard base64 export");
        }
        if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
        {
            ExportToFile(CachedMacros, "Export Macros", "macros.json");
        }
        else if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            ExportToClipboard(CachedMacros);
        }

        ImGui.SameLine();
        ImGui.Button("Import All##macrosImport");
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Right-click for clipboard base64 import");
        }
        if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
        {
            WithFileContent(ImportMacrosFromJson, "Import Macros");
        }
        else if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            WithDecodedClipboardContent(ImportMacrosFromJson);
        }

        ImGui.SameLine();
        using (ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.DalamudRed))
        {
            var deleteAllPressed = ImGui.Button("Delete All##macrosDeleteAll");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Press <CTRL> while clicking to confirm deleting all macros");
            }
            if (deleteAllPressed && KeyState[VirtualKey.CONTROL])
            {
                DeleteAllMacros();
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

                ImGui.SameLine(ImGui.GetWindowWidth() - 200);
                ImGui.Button($"Export###macros{i}Export");
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Right-click for clipboard base64 export");
                }
                if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                {
                    ExportToFile([selectedMacro], "Export Macro", $"{(!selectedMacro.Name.IsNullOrWhitespace() ? "macro" : selectedMacro.Name)}.json");
                }
                else if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                {
                    ExportToClipboard([selectedMacro]);
                }

                ImGui.SameLine();
                if (ImGui.Button($"Duplicate###macros{i}Duplicate"))
                {
                    DuplicateMacro(selectedMacro);
                }
                ImGui.SameLine();

                using (ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.DalamudRed))
                {
                    var deletePressed = ImGui.Button($"Delete###macros{i}Delete");
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("Press <CTRL> while clicking to confirm macro deletion");
                    }
                    if (deletePressed && KeyState[VirtualKey.CONTROL])
                    {
                        DeleteMacro(selectedMacro);
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
            MacroTableQueue.Insert(macro);
        }
        MacroEditorState.SelectedPath = macro.Path;
    }

    private void ImportMacrosFromJson(string json)
    {
        var macros = JsonConvert.DeserializeObject<List<Macro>>(json)!;
        var conflictingMacros = CachedMacros.Intersect(macros);
        CachedMacros.ExceptWith(macros);
        CachedMacros.UnionWith(macros);

        MacroTableQueue.Delete(conflictingMacros);
        MacroTableQueue.Insert(macros);
    }

    private void DeleteAllMacros()
    {
        CachedMacros.Clear();
        MacroTableQueue.DeleteAll();
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
            var hash = node.GetHashCode();

            var name = Path.GetFileName(node.Item);
            if (node.Children.Count > 0)
            {
                using (var treeNode = ImRaii.TreeNode($"{name}###pathNode{hash}"))
                {
                    var treeNodeOpened = treeNode.Success;

                    var popupId = $"pathNode{hash}Popup";
                    using (var contextPopup = ImRaii.ContextPopupItem(popupId))
                    {
                        if (contextPopup.Success)
                        {
                            var nestedPath = $"{node.Item}/";
                            if (ImGui.MenuItem($"New###{popupId}New"))
                            {
                                MaybeAddMacro(new() { Path = nestedPath });
                            }

                            var macros = CachedMacros.Where(m => m.Path.StartsWith(nestedPath));
                            ImGui.MenuItem($"Export###{popupId}Export");
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip("Right-click for clipboard base64 export");
                            }
                            if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                            {
                                ExportToFile(macros, "Export Macros", "macros.json");
                            }
                            else if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                            {
                                ExportToClipboard(macros);
                            }

                            var deletePressed = ImGui.MenuItem($"Delete###{popupId}Delete");
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip("Press <CTRL> while clicking to confirm deleting folder macros");
                            }
                            if (deletePressed && KeyState[VirtualKey.CONTROL])
                            {
                                DeleteMacros(macros);
                            }
                        }
                    }

                    if (treeNodeOpened)
                    {
                        DrawPathNodes(node.Children);
                    }
                }
            }
            else
            {
                using (var treeNode = ImRaii.TreeNode($"{(name.IsNullOrWhitespace() ? BLANK_NAME : name)}###pathNode{hash}", ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet | (node.Item == MacroEditorState.SelectedPath ? ImGuiTreeNodeFlags.Selected : ImGuiTreeNodeFlags.None))) {
                    var treeNodeOpened = treeNode.Success;
                    var treeNodeClicked = ImGui.IsItemClicked();

                    var popupId = $"pathNode{hash}Popup";
                    using (var contextPopup = ImRaii.ContextPopupItem(popupId))
                    {
                        if (contextPopup.Success)
                        {
                            if (CachedMacros.FindFirst(m => m.Path == node.Item, out var macro))
                            {
                                if (ImGui.MenuItem($"Duplicate###{popupId}Duplicate"))
                                {
                                    DuplicateMacro(macro);
                                }

                                ImGui.MenuItem($"Export###{popupId}Export");
                                if (ImGui.IsItemHovered())
                                {
                                    ImGui.SetTooltip("Right-click for clipboard base64 export");
                                }
                                if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                                {
                                    ExportToFile([macro], "Export Macro", $"{(!macro.Name.IsNullOrWhitespace() ? "macro" : macro.Name)}.json");
                                }
                                else if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                                {
                                    ExportToClipboard([macro]);
                                }

                                var deletePressed = ImGui.MenuItem($"Delete###{popupId}Delete");
                                if (ImGui.IsItemHovered())
                                {
                                    ImGui.SetTooltip("Press <CTRL> while clicking to confirm macro deletion");
                                }
                                if (deletePressed && KeyState[VirtualKey.CONTROL])
                                {
                                    DeleteMacro(macro);
                                }
                            }
                        }
                    }

                    if (treeNodeOpened && treeNodeClicked)
                    {
                        MacroEditorState.SelectedPath = node.Item;
                    }
                }
            }
        }
    }
}
