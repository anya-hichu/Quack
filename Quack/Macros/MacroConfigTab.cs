using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface.Colors;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using ImGuiNET;
using Newtonsoft.Json;
using Quack.Configs;
using Quack.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using static FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureMacroModule;

namespace Quack.Macros;

public partial class MacroUITab : ConfigEntityTab, IDisposable
{
    private static char PATH_SEPARATOR = '/';

    [GeneratedRegexAttribute(@"\d+")]
    private static partial Regex NumberGeneratedRegex();

    private HashSet<Macro> CachedMacros { get; init; }
    private IKeyState KeyState { get; init; }
    private MacroExecutionButton MacroExecutionHelper { get; init; }
    private MacroExecutor MacroExecutor { get; init; }
    private MacroTable MacroTable { get; init; }
    private MacroTableQueue MacroTableQueue { get; init; }
    private MacroConfigTabState MacroConfigTabState { get; set; } = new();
    private IPluginLog PluginLog { get; init; }
    private string? TmpConflictPath { get; set; }

    public MacroUITab(HashSet<Macro> cachedMacros, Debouncers debouncers, FileDialogManager fileDialogManager, IKeyState keyState, MacroExecutionButton macroExecutionHelper, MacroExecutor macroExecutor, MacroTable macroTable, MacroTableQueue macroTableQueue, IPluginLog pluginLog) : base(debouncers, fileDialogManager)
    {
        CachedMacros = cachedMacros;
        KeyState = keyState;
        MacroExecutionHelper = macroExecutionHelper;
        MacroExecutor = macroExecutor;
        MacroTable = macroTable;
        MacroTableQueue = macroTableQueue;
        PluginLog = pluginLog;

        UpdateStatePathNodes();
        MacroTable.OnChange += UpdateStatePathNodes;
    }

    public void Dispose()
    {
        MacroTable.OnChange -= UpdateStatePathNodes;
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
                if (ImGui.Button($"Cancel All##macrosCancelAll"))
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
            ImportFromFile(ImportMacroExportsJson, "Import Macros");
        }
        else if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            ImportFromClipboard(ImportMacroExportsJson);
        }

        ImGui.SameLine();
        using (ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.DalamudRed))
        {
            if (ImGui.Button("Delete All##macrosDeleteAll") && KeyState[VirtualKey.CONTROL])
            {
                DeleteAllMacros();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Press <CTRL> while clicking to confirm deleting all macros");
            }
        }

        var leftChildWidth = ImGui.GetWindowWidth() * 0.3f;

        var filter = MacroConfigTabState.Filter;
        using (ImRaii.ItemWidth(leftChildWidth))
        {
            if (ImGui.InputText("##macrosFilter", ref filter, ushort.MaxValue))
            {
                MacroConfigTabState.Filter = filter;
                UpdateStatePathNodes();
            }
        }

        using (ImRaii.PushColor(ImGuiCol.ChildBg, new Vector4(0.2f, 0.2f, 0.2f, 0.5f)))
        {
            using (ImRaii.Child("paths", new(leftChildWidth, ImGui.GetWindowHeight() - ImGui.GetCursorPosY() - 10)))
            {
                DrawPathNodes(MacroConfigTabState.PathNodes);
            }
        }

        ImGui.SameLine();
        var macroEditorId = "macroEditor";
        using (ImRaii.Child(macroEditorId, new(ImGui.GetWindowWidth() * 0.7f, ImGui.GetWindowHeight() - ImGui.GetCursorPosY() - 10)))
        {
            var selectedMacros = MacroConfigTabState.SelectedMacros;
            if (selectedMacros.Count == 1)
            {
                var selectedMacro = selectedMacros.ElementAt(0);
                var i = CachedMacros.IndexOf(selectedMacro);

                var name = selectedMacro.Name;
                var nameInputId = $"{macroEditorId}Macros{i}Name";
                if (ImGui.InputText($"Name##{nameInputId}", ref name, ushort.MaxValue))
                {
                    selectedMacro.Name = name;
                    Debounce(nameInputId, () => MacroTableQueue.Update("name", selectedMacro));
                }

                ImGui.SameLine(ImGui.GetWindowWidth() - 200);
                ImGui.Button($"Export##macros{i}Export");
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
                if (ImGui.Button($"Duplicate##macros{i}Duplicate"))
                {
                    DuplicateMacro(selectedMacro);
                }
                ImGui.SameLine();

                using (ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.DalamudRed))
                {
                    if (ImGui.Button($"Delete##macros{i}Delete") && KeyState[VirtualKey.CONTROL])
                    {
                        DeleteMacro(selectedMacro);
                    }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("Press <CTRL> while clicking to confirm macro deletion");
                    }
                }
                
                var pathConflictPopupId = $"{macroEditorId}Macros{i}PathConflictPopup";
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
                var pathInputId = $"{macroEditorId}Macros{i}Path";
                if (ImGui.InputText($"Path##{pathInputId}", ref path, ushort.MaxValue))
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
                        MacroConfigTabState.SelectedMacros = [selectedMacro];
                        Debounce(pathInputId, () => MacroTableQueue.Update("path", selectedMacro, oldPath));
                    }
                }

                var tags = string.Join(',', selectedMacro.Tags);
                var tagInputId = $"{macroEditorId}Macros{i}Tags";
                if (ImGui.InputText($"Tags (comma separated)##{tagInputId}", ref tags, ushort.MaxValue))
                {
                    selectedMacro.Tags = tags.Split(',').Select(t => t.Trim()).ToArray();
                    Debounce(tagInputId, () => MacroTableQueue.Update("tags", selectedMacro));
                }

                var command = selectedMacro.Command;
                var commandInputId = $"{macroEditorId}Macros{i}Command";
                if (ImGui.InputText($"Command##{commandInputId}", ref command, ushort.MaxValue))
                {
                    selectedMacro.Command = command;
                    Debounce(commandInputId, () => MacroTableQueue.Update("command", selectedMacro));
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Example: /shock\n\nExtra calling arguments will replace the content placeholders ({0}, {1}, etc.) dynamically.\nAdditionally placeholders {i} can be escaped by doubling the brackets {{i}} if needed.");
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
                var argsInputId = $"{macroEditorId}Macros{i}Args";
                if (ImGui.InputText($"Args##{argsInputId}", ref args, ushort.MaxValue))
                {
                    selectedMacro.Args = args;
                    Debounce(argsInputId, () => MacroTableQueue.Update("args", selectedMacro));
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Space separated list of default arguments (supports double quoting) used to replace content placeholders ({0}, {1}, etc.)");
                }

                var content = selectedMacro.Content;
                var contentInputId = $"{macroEditorId}Macros{i}Content";
                if (ImGui.InputTextMultiline($"Content##{contentInputId}", ref content, ushort.MaxValue, new(ImGui.GetWindowWidth() - 200, ImGui.GetWindowHeight() - ImGui.GetCursorPosY() - 30)))
                {
                    selectedMacro.Content = content;
                    Debounce(contentInputId, () => MacroTableQueue.Update("content", selectedMacro));
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Additional behaviors:\n - Possible to wait until a nested macro is completed using <wait.macro> placeholder\n - Macro cancellation (/macrocancel) is scoped to the currently executing macro and can also be waited on using <wait.cancel> (trap)");
                }

                var loop = selectedMacro.Loop;
                var loopInputId = $"{macroEditorId}Macros{i}Loop";
                if (ImGui.Checkbox($"Loop##{loopInputId}", ref loop))
                {
                    selectedMacro.Loop = loop;
                    Debounce(loopInputId, () => MacroTableQueue.Update("loop", selectedMacro));
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Execution can be stopped using 'Cancel All' button or '/quack cancel' command");
                }

                ImGui.SameLine(ImGui.GetWindowWidth() - 255);
                MacroExecutionHelper.Draw(selectedMacro);
            }
            else if (selectedMacros.Count > 1)
            {
                ImGui.Text($"{selectedMacros.Count} macros selected");

                ImGui.SameLine(ImGui.GetWindowWidth() - 200);
                ImGui.Button($"Export Selected##selectedMacrosExport");
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Right-click for clipboard base64 export");
                }
                if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                {
                    ExportToFile(selectedMacros, "Export Selected Macros", "macros.json");
                }
                else if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                {
                    ExportToClipboard(selectedMacros);
                }

                var macroTableId = $"{macroEditorId}Table";
                using (ImRaii.Table(macroTableId, 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
                {
                    ImGui.TableSetupColumn($"Name##{macroTableId}Name", ImGuiTableColumnFlags.None, 2);
                    ImGui.TableSetupColumn($"Path##{macroTableId}Path", ImGuiTableColumnFlags.None, 8);
                    ImGui.TableHeadersRow();

                    foreach(var selectedMacro in selectedMacros)
                    {
                        if (ImGui.TableNextColumn())
                        {
                            ImGui.Text(selectedMacro.Name);
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip(selectedMacro.Name);
                            }
                        }

                        if (ImGui.TableNextColumn())
                        {
                            ImGui.Text(selectedMacro.Path);
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip(selectedMacro.Path);
                            }
                        }
                    }
                }
            }
            else
            {
                ImGui.Text("No macro selected\n\nHelp:\n - Click on the left panel to select one and hold <CTRL> for multi selection\n - Open context menu using right-click");
            }
        }
    }

    private void DuplicateMacro(Macro macro)
    {
        var clone = macro.Clone();
        for (var i = 2; CachedMacros.Contains(clone); i++)
        {
            var suffix = $" ({i})";
            clone.Name = $"{macro.Name}{suffix}";
            clone.Path = $"{macro.Path}{suffix}";
        }

        MaybeAddMacro(clone);
    }

    private void MaybeAddMacro(Macro macro)
    {
        if (CachedMacros.Add(macro))
        {
            MacroTableQueue.Insert(macro);
        }
        MacroConfigTabState.SelectedMacros = [macro];
    }

    private void ImportMacroExportsJson(string exportsJson)
    {
        var exports = JsonConvert.DeserializeObject<ConfigEntityExports<Macro>>(exportsJson);
        if (exports == null)
        {
            PluginLog.Error($"Failed to import macros from json");
            return;
        }
        var macros = exports.Entities;
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

    private void UpdateStatePathNodes()
    {
        MacroConfigTabState.PathNodes = BuildPathNodes(MacroSearch.Lookup(CachedMacros, MacroConfigTabState.Filter));
    }

    public static HashSet<TreeNode<string>> BuildPathNodes(IEnumerable<Macro> macros)
    {
        var pathNodes = new HashSet<TreeNode<string>>(0, new TreeNodeComparer<string>());
        foreach (var macro in macros)
        {
            var current = pathNodes;
            var parts = macro.Path.Split(PATH_SEPARATOR, System.StringSplitOptions.RemoveEmptyEntries);
            for (var take = 1; take <= parts.Length; take++)
            {
                var newNode = new TreeNode<string>(string.Join(PATH_SEPARATOR, parts.Take(take)));
                if (current.TryGetValue(newNode, out var existingNode))
                {
                    current = existingNode.ChildNodes;
                }
                else
                {
                    current.Add(newNode);
                    current = newNode.ChildNodes;
                }
            }
        }
        return pathNodes;
    }

    private void DrawPathNodes(HashSet<TreeNode<string>> nodes)
    {
        // Pad with zeros to improve sorting with numbers
        foreach (var treeNode in nodes.OrderBy(n => NumberGeneratedRegex().Replace(n.Node, m => m.Value.PadLeft(10, '0'))))
        {
            var hash = treeNode.GetHashCode();

            var name = Path.GetFileName(treeNode.Node);
            if (treeNode.ChildNodes.Count > 0)
            {
                using (var treeNodeItem = ImRaii.TreeNode($"{name}##pathNode{hash}"))
                {
                    var treeNodeOpened = treeNodeItem.Success;

                    var popupId = $"pathNode{hash}Popup";
                    using (var contextPopup = ImRaii.ContextPopupItem(popupId))
                    {
                        if (contextPopup.Success)
                        {
                            var nestedPath = $"{treeNode.Node}/";
                            if (ImGui.MenuItem($"New##{popupId}New"))
                            {
                                MaybeAddMacro(new() { Path = nestedPath });
                            }

                            var nestedMacros = CachedMacros.Where(m => m.Path.StartsWith(nestedPath));
                            ImGui.MenuItem($"Export##{popupId}Export");
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip("Right-click for clipboard base64 export");
                            }
                            if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                            {
                                ExportToFile(nestedMacros, "Export Folder Macros", "macros.json");
                            }
                            else if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                            {
                                ExportToClipboard(nestedMacros);
                            }

                            if (ImGui.MenuItem($"Delete##{popupId}Delete") && KeyState[VirtualKey.CONTROL])
                            {
                                DeleteMacros(nestedMacros);
                            }
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip("Press <CTRL> while clicking to confirm deleting folder macros");
                            }
                        }
                    }

                    if (treeNodeOpened)
                    {
                        DrawPathNodes(treeNode.ChildNodes);
                    }
                }
            }
            else
            {
                using (ImRaii.TreeNode($"{(name.IsNullOrWhitespace() ? BLANK_NAME : name)}##pathNode{hash}", ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet | (MacroConfigTabState.SelectedMacros.Any(m => m.Path == treeNode.Node) ? ImGuiTreeNodeFlags.Selected : ImGuiTreeNodeFlags.None))) {
                    var treeNodeClicked = ImGui.IsItemClicked();

                    var selectedMacros = MacroConfigTabState.SelectedMacros.ToList();

                    var popupId = $"pathNode{hash}Popup";
                    using (var contextPopup = ImRaii.ContextPopupItem(popupId))
                    {
                        if (contextPopup.Success)
                        {
                            var selectedMacro = selectedMacros.FirstOrDefault();
                            if (ImGui.MenuItem($"Duplicate##{popupId}Duplicate"))
                            {
                                selectedMacros.ForEach(DuplicateMacro);
                            }

                            ImGui.MenuItem($"Export##{popupId}Export");
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip("Right-click for clipboard base64 export");
                            }
                            if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                            {
                                ExportToFile(selectedMacros, "Export Selected Macro(s)", selectedMacros.Count == 1 ? $"{(!selectedMacro!.Name.IsNullOrWhitespace() ? "macro" : selectedMacro.Name)}.json" : "macros.json");
                            }
                            else if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                            {
                                ExportToClipboard(selectedMacros);
                            }

                            if (ImGui.MenuItem($"Delete##{popupId}Delete") && KeyState[VirtualKey.CONTROL])
                            {
                                selectedMacros.ForEach(DeleteMacro);
                            }
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip("Press <CTRL> while clicking to confirm macro(s) deletion");
                            }
                        }
                    }

                    if (!treeNodeClicked || !CachedMacros.FindFirst(m => m.Path == treeNode.Node, out var clickedMacro))
                    {
                        continue;
                    }

                    if (!KeyState[VirtualKey.CONTROL])
                    {
                        selectedMacros.Clear();
                    }

                    // Toggle
                    if (!selectedMacros.Remove(clickedMacro))
                    {
                        selectedMacros.Add(clickedMacro);
                    }
                }
            }
        }
    }
}
