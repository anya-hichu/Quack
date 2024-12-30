using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using ImGuiNET;
using Newtonsoft.Json;
using Quack.Configs;
using Quack.Exports;
using Quack.UI;
using Quack.Utils;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;

namespace Quack.Macros;

public partial class MacroConfigTab : ConfigEntityTab, IDisposable
{
    private static readonly string ANY_COLLECTION = "All";

    [GeneratedRegexAttribute(@"\d+")]
    private static partial Regex NumberGeneratedRegex();

    private HashSet<Macro> CachedMacros { get; init; }
    private ICommandManager CommandManager { get; init; }
    private Config Config { get; init; }
    private IKeyState KeyState { get; init; }
    private MacroExecutionState MacroExecutionState { get; init; }
    private MacroExecutor MacroExecutor { get; init; }
    private MacroTableQueue MacroTableQueue { get; init; }
    private MacroConfigTabState MacroConfigTabState { get; init; }
    private IPluginLog PluginLog { get; init; }
    private UIEvents UiEvents { get; init; }

    public MacroConfigTab(HashSet<Macro> cachedMacros, Config config, ICommandManager commandManager, Debouncers debouncers, 
                          FileDialogManager fileDialogManager, IKeyState keyState, MacroExecutionState macroExecutionState, MacroExecutor macroExecutor, MacroTable macroTable, 
                          MacroTableQueue macroTableQueue, IPluginLog pluginLog, INotificationManager notificationManager, UIEvents uiEvents) : base(debouncers, fileDialogManager, notificationManager)
    {
        CachedMacros = cachedMacros;
        Config = config;
        CommandManager = commandManager;
        KeyState = keyState;
        MacroExecutionState = macroExecutionState;
        MacroExecutor = macroExecutor;
        MacroTableQueue = macroTableQueue;
        PluginLog = pluginLog;

        MacroConfigTabState = new(CachedMacros, macroTable, uiEvents);
    }

    public void Dispose()
    {
        MacroConfigTabState.Dispose();
    }

    public void Draw()
    {
        if (ImGui.Button("New###newMacro"))
        {
            MaybeAddMacro(new());
        }

        ImGui.SameLine();
        var leftChildWidth = ImGui.GetWindowWidth() * 0.3f;
        var state = MacroConfigTabState;
        var filter = state.Filter;
        using (ImRaii.ItemWidth(leftChildWidth - 211))
        {
            if (ImGui.InputTextWithHint("###macrosFilter", "Filter", ref filter, ushort.MaxValue))
            {
                state.Filter = filter;
                state.Update();
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("x###macrosFilterClear"))
        {
            state.Filter = string.Empty;
            state.Update();
        }

        ImGui.SameLine();
        var collectionNames = Config.CollectionConfigs.Where(c => c.Selectable).Select(c => c.Name).Prepend(ANY_COLLECTION);
        var collectionNameIndex = state.MaybeCollectionConfig == null ? 0 : collectionNames.IndexOf(state.MaybeCollectionConfig.Name);

        using (ImRaii.ItemWidth(140))
        {
            if (ImGui.Combo($"###collectionName", ref collectionNameIndex, collectionNames.ToArray(), collectionNames.Count()))
            {
                var collectionName = collectionNames.ElementAt(collectionNameIndex);
                state.MaybeCollectionConfig = collectionName == ANY_COLLECTION ? null : Config.CollectionConfigs.Find(c => c.Name == collectionName)!;
                state.Update();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Collection Selection");
            }
        }


        ImGui.SameLine(ImGui.GetWindowWidth() - 288);
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

        ImGui.SameLine(ImGui.GetWindowWidth() - 217);
        ImGui.Button("Export All###macrosExport");
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(EXPORT_HINT);
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
        ImGui.Button("Import All###macrosImport");
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(IMPORT_HINT);
        }
        if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
        {
            ImportFromFile(ProcessExportJson, "Import Macros");
        }
        else if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            ImportFromClipboard(ProcessExportJson);
        }

        ImGui.SameLine();
        using (ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.DalamudRed))
        {
            if (ImGui.Button("Delete All###macrosDeleteAll") && KeyState[VirtualKey.CONTROL])
            {
                DeleteAllMacros();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(CONFIRM_DELETE_HINT);
            }
        }

        using (ImRaii.PushColor(ImGuiCol.ChildBg, new Vector4(0.2f, 0.2f, 0.2f, 0.5f)))
        {
            using (ImRaii.Child("paths", new(leftChildWidth, ImGui.GetWindowHeight() - ImGui.GetCursorPosY() - 10)))
            {
                DrawPathNodes(state.PathNodes);
            }
        }

        ImGui.SameLine();
        var macroConfigsId = $"macroConfigs";
        using (ImRaii.Child($"{macroConfigsId}Child", new(ImGui.GetWindowWidth() * 0.7f, ImGui.GetWindowHeight() - ImGui.GetCursorPosY() - 10)))
        {
            var selectedMacros = state.SelectedMacros;
            if (selectedMacros.Count == 1)
            {
                var selectedMacro = selectedMacros.ElementAt(0);

                var i = CachedMacros.IndexOf(selectedMacro);
                var macroConfigId = $"{macroConfigsId}{i}";

                var name = selectedMacro.Name;
                var nameInputId = $"{macroConfigId}Name";
                if (ImGui.InputText($"Name###{nameInputId}", ref name, ushort.MaxValue))
                {
                    selectedMacro.Name = name;
                    Debounce(nameInputId, () => MacroTableQueue.Update("name", selectedMacro));
                }

                ImGui.SameLine(ImGui.GetWindowWidth() - 197);
                ImGui.Button($"Export###{macroConfigId}Export");
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(EXPORT_HINT);
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
                if (ImGui.Button($"Duplicate###{macroConfigId}Duplicate"))
                {
                    DuplicateMacro(selectedMacro);
                }
                ImGui.SameLine();

                using (ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.DalamudRed))
                {
                    if (ImGui.Button($"Delete###{macroConfigId}Delete") && KeyState[VirtualKey.CONTROL])
                    {
                        DeleteMacro(selectedMacro);
                    }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(CONFIRM_DELETE_HINT);
                    }
                }

                var maybeConflictPath = state.MaybeConflictPath;
                var pathConflictPopupId = $"{macroConfigId}PathConflictPopup";
                using (var popup = ImRaii.Popup(pathConflictPopupId))
                {
                    if (popup)
                    {
                        ImGui.Text($"Confirm macro override?");
                        ImGui.SetCursorPosX(15);
                        using (ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.DalamudRed))
                        {
                            if (ImGui.Button("Yes", new(100, 30)))
                            {
                                var oldPath = selectedMacro.Path;
                                CachedMacros.Remove(selectedMacro);
                                selectedMacro.Path = maybeConflictPath!;
                                CachedMacros.Add(selectedMacro);
                                MacroTableQueue.Update("path", selectedMacro, oldPath);

                                state.MaybeConflictPath = null;
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

                var path = maybeConflictPath ?? selectedMacro.Path;
                var pathInputId = $"{macroConfigId}Path";
                if (ImGui.InputText($"Path###{pathInputId}", ref path, ushort.MaxValue))
                {
                    state.MaybeConflictPath = null;
                    if (CachedMacros.FindFirst(m => m.Path == path, out var conflictingMacro) && selectedMacro != conflictingMacro)
                    {
                        state.MaybeConflictPath = path;
                        ImGui.OpenPopup(pathConflictPopupId);
                    }
                    else
                    {
                        var oldPath = selectedMacro.Path;
                        CachedMacros.Remove(selectedMacro);
                        selectedMacro.Path = path;
                        CachedMacros.Add(selectedMacro);
                        state.SelectedMacros = new([selectedMacro], MacroComparer.INSTANCE);
                        Debounce(pathInputId, () => MacroTableQueue.Update("path", selectedMacro, oldPath));
                    }
                }

                var tags = string.Join(',', selectedMacro.Tags);
                var tagInputId = $"{macroConfigId}Tags";
                if (ImGui.InputText($"Tags (comma separated)###{tagInputId}", ref tags, ushort.MaxValue))
                {
                    CachedMacros.Remove(selectedMacro);
                    selectedMacro.Tags = new(tags.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
                    CachedMacros.Add(selectedMacro);
                    Debounce(tagInputId, () => MacroTableQueue.Update("tags", selectedMacro));
                }

                var command = selectedMacro.Command;
                var commandInputId = $"{macroConfigId}Command";
                if (ImGui.InputText($"Command###{commandInputId}", ref command, ushort.MaxValue))
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
                    var nonMacroCommands = CommandManager.Commands.Where(c => !c.Value.HelpMessage.StartsWith(MacroCommands.HELP_MESSAGE_PREFIX)).Select(c => c.Key);
                    if (nonMacroCommands.Contains(command))
                    {
                        using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudRed))
                        {
                            ImGui.Text($"Command is already registered outside of quack");
                        }
                    }

                    var conflictingMacroCommands = CachedMacros.Where(m => m != selectedMacro && m.Command == selectedMacro.Command);
                    if (conflictingMacroCommands.Any())
                    {
                        using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudRed))
                        {
                            ImGui.Text($"Command conflicts with {string.Join(", ", conflictingMacroCommands.Select(m => m.Name))}");
                        }
                    }
                }

                var args = selectedMacro.Args;
                var argsInputId = $"{macroConfigId}Args";
                if (ImGui.InputText($"Args###{argsInputId}", ref args, ushort.MaxValue))
                {
                    selectedMacro.Args = args;
                    Debounce(argsInputId, () => MacroTableQueue.Update("args", selectedMacro));
                }
                ImGuiComponents.HelpMarker("Space separated list of default arguments (supports double quoting) used to replace content placeholders ({0}, {1}, etc.)");

                var content = selectedMacro.Content;
                var contentInputId = $"{macroConfigId}Content";
                if (ImGui.InputTextMultiline($"Content###{contentInputId}", ref content, ushort.MaxValue, new(ImGui.GetWindowWidth() - 200, ImGui.GetWindowHeight() - ImGui.GetCursorPosY() - 30)))
                {
                    selectedMacro.Content = content;
                    Debounce(contentInputId, () => MacroTableQueue.Update("content", selectedMacro));
                }
                ImGuiComponents.HelpMarker("Additional behaviors:\n - Possible to wait until a nested macro is completed using <wait.macro> placeholder\n - Macro cancellation (/macrocancel) is scoped to the currently executing macro and can also be waited on using <wait.cancel> (trap)\n - Supports commenting out lines by adding '//' at the beginning without leading space");

                var loop = selectedMacro.Loop;
                var loopInputId = $"{macroConfigId}Loop";
                if (ImGui.Checkbox($"Loop###{loopInputId}", ref loop))
                {
                    selectedMacro.Loop = loop;
                    Debounce(loopInputId, () => MacroTableQueue.Update("loop", selectedMacro));
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Execution can be stopped using 'Cancel All' button or '/quack cancel' command");
                }

                ImGui.SameLine(ImGui.GetWindowWidth() - 255);
                MacroExecutionState.Button($"{macroConfigId}Execute", selectedMacro);
            }
            else if (selectedMacros.Count > 1)
            {
                ImGui.Text($"{selectedMacros.Count} macros selected");

                ImGui.SameLine(ImGui.GetWindowWidth() - 127);
                ImGui.Button($"Export###selectedMacrosExport");
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(EXPORT_HINT);
                }
                if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                {
                    ExportToFile(selectedMacros, "Export Selected Macros", "macros.json");
                }
                else if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                {
                    ExportToClipboard(selectedMacros);
                }

                ImGui.SameLine();
                using (ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.DalamudRed))
                {
                    if (ImGui.Button($"Delete###selectedMacrosDelete") && KeyState[VirtualKey.CONTROL])
                    {
                        DeleteMacros(selectedMacros);
                    }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(CONFIRM_DELETE_HINT);
                    }
                }

                var macroTableId = $"{macroConfigsId}Table";
                using (ImRaii.Table(macroTableId, 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
                {
                    ImGui.TableSetupColumn($"Name###{macroTableId}Name", ImGuiTableColumnFlags.None, 2);
                    ImGui.TableSetupColumn($"Path###{macroTableId}Path", ImGuiTableColumnFlags.None, 8);
                    ImGui.TableHeadersRow();

                    foreach(var selectedMacro in selectedMacros)
                    {
                        if (ImGui.TableNextColumn())
                        {
                            var name = selectedMacro.Name.IsNullOrWhitespace() ? BLANK_NAME : selectedMacro.Name;
                            ImGui.Text(name);
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip(name);
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
                ImGui.Text("No macro selected");
                ImGuiComponents.HelpMarker("Click on the left panel to select one and hold <CTRL> for multi-selection\nClick <RIGHT> to open the context menu on panel items");
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
        MacroConfigTabState.SelectedMacros = new([macro], MacroComparer.INSTANCE);
    }

    private List<Macro>? ProcessExportJson(string exportJson)
    {
        var export = JsonConvert.DeserializeObject<Export<Macro>>(exportJson);
        if (export == null || export.Type != typeof(Macro).Name)
        {
            PluginLog.Verbose($"Failed to import macro from json: {exportJson}");
            return null;
        }
        var macros = export.Entities;
        var conflictingMacros = CachedMacros.Intersect(macros);
        CachedMacros.ExceptWith(macros);
        CachedMacros.UnionWith(macros);

        MacroTableQueue.Delete(conflictingMacros);
        MacroTableQueue.Insert(macros);
        return macros;
    }

    private void DeleteAllMacros()
    {
        CachedMacros.Clear();
        MacroTableQueue.DeleteAll();
        MacroConfigTabState.SelectedMacros.Clear();
    }

    private void DeleteMacro(Macro macro)
    {
        CachedMacros.Remove(macro);
        MacroTableQueue.Delete(macro);
        MacroConfigTabState.SelectedMacros.Remove(macro);
    }

    private void DeleteMacros(IEnumerable<Macro> macros)
    {
        var list = macros.ToList();
        CachedMacros.ExceptWith(list);
        MacroTableQueue.Delete(list);
        MacroConfigTabState.SelectedMacros.ExceptWith(macros);
    }

    private void DrawPathNodes(HashSet<TreeNode<string>> nodes)
    {
        // Pad with zeros to improve sorting with numbers
        foreach (var treeNode in nodes.OrderBy(n => NumberGeneratedRegex().Replace(n.Node, m => m.Value.PadLeft(10, '0'))))
        {
            var hash = treeNode.Node.GetHashCode();

            var name = Path.GetFileName(treeNode.Node);
            if (treeNode.ChildNodes.Count > 0)
            {
                using (var treeNodeItem = ImRaii.TreeNode($"{name}###pathNode{hash}"))
                {
                    var treeNodeOpened = treeNodeItem.Success;

                    var popupId = $"pathNode{hash}Popup";
                    using (var contextPopup = ImRaii.ContextPopupItem(popupId))
                    {
                        if (contextPopup)
                        {
                            var nestedPath = $"{treeNode.Node}/";
                            if (ImGui.MenuItem($"New###{popupId}New"))
                            {
                                MaybeAddMacro(new() { Path = nestedPath });
                            }

                            var nestedMacros = CachedMacros.Where(m => m.Path.StartsWith(nestedPath));
                            ImGui.MenuItem($"Export###{popupId}Export");
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip(EXPORT_HINT);
                            }
                            if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                            {
                                ExportToFile(nestedMacros, "Export Folder Macros", "macros.json");
                            }
                            else if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                            {
                                ExportToClipboard(nestedMacros);
                            }

                            if (ImGui.MenuItem($"Delete###{popupId}Delete") && KeyState[VirtualKey.CONTROL])
                            {
                                DeleteMacros(nestedMacros);
                            }
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip(CONFIRM_DELETE_HINT);
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
                var state = MacroConfigTabState;

                if (CachedMacros.FindFirst(m => m.Path == treeNode.Node, out var macro))
                {
                    using (ImRaii.PushColor(ImGuiCol.Text, Config.CollectionConfigs.FindFirst(collection => collection.Matches(macro), out var collectionConfig) ? collectionConfig.Color : ImGuiColors.DalamudWhite))
                    {
                        using (ImRaii.TreeNode($"{(name.IsNullOrWhitespace() ? BLANK_NAME : name)}###pathNode{hash}", ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet | (state.SelectedMacros.Contains(macro) ? ImGuiTreeNodeFlags.Selected : ImGuiTreeNodeFlags.None)))
                        {
                            using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudWhite))
                            {
                                var nodeClicked = ImGui.IsItemClicked();

                                var selectedMacros = state.SelectedMacros;
                                var popupId = $"pathNode{hash}Popup";
                                using (var contextPopup = ImRaii.ContextPopupItem(popupId))
                                {
                                    if (contextPopup)
                                    {
                                        var selectedMacro = selectedMacros.FirstOrDefault();
                                        var selectedMacroList = selectedMacros.ToList();
                                        if (ImGui.MenuItem($"Duplicate###{popupId}Duplicate"))
                                        {
                                            selectedMacroList.ForEach(DuplicateMacro);
                                        }
                                        ImGui.MenuItem($"Export###{popupId}Export");
                                        if (ImGui.IsItemHovered())
                                        {
                                            ImGui.SetTooltip(EXPORT_HINT);
                                        }
                                        if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                                        {
                                            ExportToFile(selectedMacros, "Export Selected Macro(s)", selectedMacros.Count == 1 ? $"{(!selectedMacro!.Name.IsNullOrWhitespace() ? "macro" : selectedMacro.Name)}.json" : "macros.json");
                                        }
                                        else if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                                        {
                                            ExportToClipboard(selectedMacros);
                                        }

                                        if (ImGui.MenuItem($"Delete###{popupId}Delete") && KeyState[VirtualKey.CONTROL])
                                        {
                                            selectedMacroList.ForEach(DeleteMacro);
                                        }
                                        if (ImGui.IsItemHovered())
                                        {
                                            ImGui.SetTooltip(CONFIRM_DELETE_HINT);
                                        }
                                    }
                                }

                                if (nodeClicked)
                                {
                                    if (!KeyState[VirtualKey.CONTROL])
                                    {
                                        selectedMacros.Clear();
                                    }

                                    // Toggle
                                    if (!selectedMacros.Remove(macro))
                                    {
                                        selectedMacros.Add(macro);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
