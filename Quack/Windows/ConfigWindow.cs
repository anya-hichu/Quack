using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dalamud;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface.Colors;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Ipc.Internal;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using ImGuiNET;
using JavaScriptEngineSwitcher.Core;
using Newtonsoft.Json;
using Quack.Generators;
using Quack.Macros;
using Quack.Utils;
using Quack.Windows.States;

namespace Quack.Windows;

public partial class ConfigWindow : Window, IDisposable
{
    private static string BLANK_NAME = "(Blank)";

    [GeneratedRegexAttribute(@"\d+")]
    private static partial Regex NumberGeneratedRegex();

    private HashSet<Macro> CachedMacros { get; init; }
    private MacroExecutor MacroExecutor { get; init; }
    private MacroTable MacroTable { get; init; }
    private IKeyState KeyState { get; init; }
    private Config Config { get; init; }
    private IPluginLog PluginLog { get; init; }
    private TaskQueue TaskQueue { get; init; }

    private MacrosState MacrosState { get; set; } = null!;

    private Dictionary<GeneratorConfig, GeneratorConfigState> GeneratorConfigToState { get; set; }
    private GeneratorException? GeneratorException { get; set; } = null;

    private FileDialogManager FileDialogManager { get; init; } = new();

    private string? TmpConflictPath { get; set; }
    private IJsEngine? CurrentJsEngine { get; set; }
    private MacroExecutionGui MacroExecutionGui { get; init; }

    public ConfigWindow(HashSet<Macro> cachedMacros, MacroExecutor macroExecutor, MacroTable macroTable, IKeyState keyState, Config config, IPluginLog pluginLog, TaskQueue taskQueue) : base("Quack Config##configWindow")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(1070, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        CachedMacros = cachedMacros;
        MacroExecutor = macroExecutor;
        MacroTable = macroTable;
        KeyState = keyState;
        Config = config;
        PluginLog = pluginLog;
        TaskQueue = taskQueue;

        MacroExecutionGui = new(config, macroExecutor);

        UpdateMacrosState();
        GeneratorConfigToState = Config.GeneratorConfigs.ToDictionary(c => c, c => new GeneratorConfigState());

        MacroTable.OnChange += UpdateMacrosState;
    }

    public void Dispose() 
    {
        MacroTable.OnChange -= UpdateMacrosState;
    }

    public override void Draw()
    {
        if(ImGui.BeginTabBar("tabs"))
        {
            if (ImGui.BeginTabItem("General##generalTab"))
            {
                DrawGeneralTab();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Macros##macrosTab"))
            {
                DrawMacrosTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Generators##generatorsTab"))
            {
                DrawGeneratorsTab();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }

        FileDialogManager.Draw();
    }

    private void DrawGeneralTab()
    {
        if(ImGui.CollapsingHeader("Search##searchHeader", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var validVirtualKeys = KeyState.GetValidVirtualKeys().Prepend(VirtualKey.NO_KEY);

            var keyBind = Config.KeyBind;
            var keyBindIndex = validVirtualKeys.IndexOf(keyBind);
            if (ImGui.Combo($"Key Bind###keyBind", ref keyBindIndex, validVirtualKeys.Select(k => k.GetFancyName()).ToArray(), validVirtualKeys.Count()))
            {
                Config.KeyBind = validVirtualKeys.ElementAt(keyBindIndex);
                Config.Save();
            }

            var modifierVirtualKeys = validVirtualKeys.Where(k => Config.MODIFIER_KEYS.Contains(k));
            var keybindExtraModifier = Config.KeyBindExtraModifier;
            var keybindExtraModifierIndex = modifierVirtualKeys.IndexOf(keybindExtraModifier);
            if (ImGui.Combo($"Key Bind Extra Modifier###keyBindExtraModifier", ref keybindExtraModifierIndex, modifierVirtualKeys.Select(k => k.GetFancyName()).ToArray(), modifierVirtualKeys.Count()))
            {
                Config.KeyBindExtraModifier = modifierVirtualKeys.ElementAt(keybindExtraModifierIndex);
                Config.Save();
            }
        }

        ImGui.NewLine();
        if (ImGui.CollapsingHeader("Generator##generatorHeader", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var generatorEngineName = Config.GeneratorEngineName;
            var generatorEngineNames = JsEngineSwitcher.Current.EngineFactories.Select(f => f.EngineName).ToArray();

            var currentIndex = generatorEngineNames.IndexOf(generatorEngineName);
            if (ImGui.Combo("Engine##generatorEngineName", ref currentIndex, generatorEngineNames, generatorEngineNames.Length))
            {
                Config.GeneratorEngineName = generatorEngineNames.ElementAt(currentIndex);
                Config.Save();
            };
        }

        ImGui.NewLine();
        if (ImGui.CollapsingHeader("Macro Executor##macroExecutorHeader", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var extraCommandFormat = Config.ExtraCommandFormat;
            if (ImGui.InputText("Extra Command Format##commandFormat", ref extraCommandFormat, ushort.MaxValue))
            {
                Config.ExtraCommandFormat = extraCommandFormat;
                Config.Save();
            }
            ImGui.Text("PM format supported via {0:P} placeholder for example: \"/cwl2 puppet now ({0:P})\"");
        }
    }

    public void UpdateMacrosState()
    {
        var selectedPath = MacrosState?.SelectedPath;
        var filter = MacrosState != null? MacrosState.Filter : string.Empty;
        PluginLog.Verbose($"Filtering {CachedMacros.Count} macros with filter '{filter}'");
        var filteredMacros = MacroSearch.Lookup(CachedMacros, filter);
        
        MacrosState = new(
            MacrosState.GeneratePathNodes(filteredMacros), 
            selectedPath, 
            filter
        );

        MacroExecutionGui.UpdateExecutions(CachedMacros.FindFirst(m => m.Path == selectedPath, out var selectedMacro) ? filteredMacros.Union([selectedMacro]) : filteredMacros);
    }


    private void DrawMacrosTab()
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
            ExportMacros(MacroTable.Search(MacrosState.Filter));
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
                TaskQueue.Enqueue(() => MacroTable.DeleteAll());
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
        var filter = MacrosState.Filter;
        ImGui.PushItemWidth(leftChildWidth);
        if (ImGui.InputText("##macrosFilter", ref filter, ushort.MaxValue))
        {
            MacrosState.Filter = filter;
            UpdateMacrosState();
        }
        ImGui.PopItemWidth();

        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.2f, 0.2f, 0.2f, 0.5f));
        if (ImGui.BeginChild("paths", new(leftChildWidth, ImGui.GetWindowHeight() - ImGui.GetCursorPosY() - 10)))
        {
            DrawPathNodes(MacrosState.PathNodes);
            ImGui.EndChildFrame();
        }
        ImGui.PopStyleColor();

        ImGui.SameLine();
        if (ImGui.BeginChild("macro", new(ImGui.GetWindowWidth() * 0.7f, ImGui.GetWindowHeight() - ImGui.GetCursorPosY() - 10)))
        {
            CachedMacros.FindFirst(m => m.Path == MacrosState.SelectedPath, out var selectedMacro);
            if (selectedMacro != null)
            {
                var i = CachedMacros.IndexOf(selectedMacro);

                var name = selectedMacro.Name;
                if (ImGui.InputText($"Name###macros{i}Name", ref name, ushort.MaxValue))
                {
                    selectedMacro.Name = name;
                    TaskQueue.Enqueue(() => MacroTable.Update(selectedMacro));
                }

                var deleteMacroPopupId = $"macros{i}DeletePopup";
                if (ImGui.BeginPopup(deleteMacroPopupId))
                {
                    ImGui.Text($"Confirm deleting {(selectedMacro.Name.IsNullOrWhitespace()? BLANK_NAME : selectedMacro.Name)} macro?");

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

                var pathConflictPopupId = $"###macros{i}PathConflictPopup";
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

                        MacrosState.SelectedPath = TmpConflictPath;
                        TaskQueue.Enqueue(() => MacroTable.Update(oldPath, selectedMacro));

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

                var path = TmpConflictPath != null ? TmpConflictPath : selectedMacro.Path;
                if (ImGui.InputText($"Path###macros{i}Path", ref path, ushort.MaxValue))
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
                        MacrosState.SelectedPath = path;
                        TaskQueue.Enqueue(() => MacroTable.Update(oldPath, selectedMacro));
                    }
                }

                var tags = string.Join(',', selectedMacro.Tags);
                if (ImGui.InputText($"Tags (comma separated)###macros{i}Tags", ref tags, ushort.MaxValue))
                {
                    selectedMacro.Tags = tags.Split(',').Select(t => t.Trim()).ToArray();
                    TaskQueue.Enqueue(() => MacroTable.Update(selectedMacro));
                }

                var command = selectedMacro.Command;
                var commandInput = ImGui.InputText($"Command###macros{i}Command", ref command, ushort.MaxValue);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Example: /shock\n\nExtra calling arguments will replace the content placeholders ({0}, {1}, etc.) dynamically.\nAdditionally placeholders {i} can be escaped by doubling the brackets {{i}} if needed.");
                }
                if (commandInput)
                {
                    selectedMacro.Command = command;
                    TaskQueue.Enqueue(() => MacroTable.Update(selectedMacro));
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
                var argsInput = ImGui.InputText($"Args###macros{i}Args", ref args, ushort.MaxValue);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Space separated list of default arguments (quoting optional) used to replace content placeholders ({0}, {1}, etc.)");
                }
                if (argsInput)
                {
                    selectedMacro.Args = args;
                    TaskQueue.Enqueue(() => MacroTable.Update(selectedMacro));
                }

                var content = selectedMacro.Content;
                var contentInput = ImGui.InputTextMultiline($"Content###macros{i}Content", ref content, ushort.MaxValue, new(ImGui.GetWindowWidth() - 200, ImGui.GetWindowHeight() - ImGui.GetCursorPosY() - 30));
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Additional behaviors:\n - Possible to wait until a nested macro is completed using <wait.macro> placeholder\n - Macro cancellation (/macrocancel) is scoped to the currently executing macro and can also be waited on using <wait.cancel> (trap)");
                }
                if (contentInput)
                {
                    selectedMacro.Content = content;
                    TaskQueue.Enqueue(() => MacroTable.Update(selectedMacro));
                }

                var loop = selectedMacro.Loop;
                var loopInput = ImGui.Checkbox($"Loop###macros{i}Loop", ref loop);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Execution can be stopped using 'Cancel All' button or '/quack cancel' command");
                }
                if (loopInput)
                {
                    selectedMacro.Loop = loop;
                    TaskQueue.Enqueue(() => MacroTable.Update(selectedMacro));
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
                if (node.Item == MacrosState.SelectedPath)
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
                        MacrosState.SelectedPath = node.Item;
                    }
                    ImGui.TreePop();
                }
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
            MacrosState.SelectedPath = macro.Path;
            TaskQueue.Enqueue(() => MacroTable.Insert(macro));
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

                TaskQueue.Enqueue(() =>
                {
                    MacroTable.Delete(conflictingMacros);
                    MacroTable.Insert(importedMacros);
                });
            }
        });
    }

    private void DeleteMacro(Macro macro)
    {
        CachedMacros.Remove(macro);
        TaskQueue.Enqueue(() => MacroTable.Delete(macro));
    }

    private void DeleteMacros(IEnumerable<Macro> macros)
    {
        var list = macros.ToList();
        CachedMacros.ExceptWith(list);
        TaskQueue.Enqueue(() => MacroTable.Delete(list));
    }

    private void DrawGeneratorsTab()
    {
        if (GeneratorException != null)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudRed);
            ImGui.Text(GeneratorException.ToString());
            ImGui.PopStyleColor();
            ImGui.SameLine();
            if (ImGui.Button("x##clearException"))
            {
                GeneratorException = null;
            }
        }

        if (ImGui.Button("New##generatorConfigsNew"))
        {
            NewGeneratorConfig();
        }

        ImGui.SameLine(ImGui.GetWindowWidth() - 360);
        if (ImGui.Button("Export All##generatorConfigsExportAll"))
        {
            ExportGeneratorConfigs(Config.GeneratorConfigs);
        }

        ImGui.SameLine();
        if (ImGui.Button("Import All##generatorConfigsImportAll"))
        {
            ImportGeneratorConfigs();
        }

        ImGui.SameLine();
        if (ImGui.Button($"Recreate Defaults (V{GeneratorConfig.DEFAULTS_VERSION})##generatorConfigsRecreateDefaults"))
        {
            RecreateDefaultGeneratorConfigs();
        }

        var deleteAllGeneratorConfigsPopup = "deleteAllGeneratorConfigsPopup";
        if (ImGui.BeginPopup(deleteAllGeneratorConfigsPopup))
        {
            ImGui.Text($"Confirm deleting {Config.GeneratorConfigs.Count} generators?");

            ImGui.SetCursorPosX(15);
            ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.DalamudRed);
            if (ImGui.Button($"Yes##{deleteAllGeneratorConfigsPopup}Yes", new(100, 30)))
            {
                DeleteGeneratorConfigs();
                ImGui.CloseCurrentPopup();
            }
            ImGui.PopStyleColor();

            ImGui.SameLine();
            if (ImGui.Button($"No##{deleteAllGeneratorConfigsPopup}No", new(100, 30)))
            {
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }

        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.DalamudRed);
        if (ImGui.Button("Delete All##generatorConfigsDeleteAll"))
        {
            if (Config.GeneratorConfigs.Count > 0)
            {
                ImGui.OpenPopup(deleteAllGeneratorConfigsPopup);
            }
        }
        ImGui.PopStyleColor();

        var generatorConfigs = Config.GeneratorConfigs;
        var funcChannels = Service<CallGate>.Get().Gates.Values.Where(g => g.Func != null);
        foreach (var generatorConfig in generatorConfigs)
        {
            var hash = generatorConfig.GetHashCode();
            if (ImGui.BeginTabBar("generatorConfigs", ImGuiTabBarFlags.AutoSelectNewTabs | ImGuiTabBarFlags.TabListPopupButton | ImGuiTabBarFlags.FittingPolicyScroll))
            {
                if (ImGui.BeginTabItem($"{(generatorConfig.Name.IsNullOrWhitespace() ? BLANK_NAME : generatorConfig.Name)}###generatorConfigs{hash}"))
                {
                    ImGui.NewLine();
                    DrawDefinitionHeader(generatorConfig, funcChannels);
                    DrawOutputHeader(generatorConfig);
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }

        }
    }

    private void ExportGeneratorConfigs(IEnumerable<GeneratorConfig> generatorConfigs)
    {
        FileDialogManager.SaveFileDialog("Export Generators", ".*", "generators.json", ".json", (valid, path) =>
        {
            if (valid)
            {
                using var file = File.CreateText(path);
                new JsonSerializer().Serialize(file, generatorConfigs);
            }
        });
    }

    private void ImportGeneratorConfigs()
    {
        FileDialogManager.OpenFileDialog("Import Generators", "{.json}", (valid, path) =>
        {
            if (valid)
            {
                using StreamReader reader = new(path);
                var json = reader.ReadToEnd();
                var importedGeneratorConfigs = JsonConvert.DeserializeObject<List<GeneratorConfig>>(json)!;
                Config.GeneratorConfigs.AddRange(importedGeneratorConfigs);
                Config.Save();

                importedGeneratorConfigs.ForEach(g => GeneratorConfigToState.Add(g, new()));
            }
        });
    }

    private void DrawDefinitionHeader(GeneratorConfig generatorConfig, IEnumerable<CallGateChannel> funcChannels)
    {
        var hash = generatorConfig.GetHashCode();
        if (ImGui.CollapsingHeader($"Definition##generatorConfigs{hash}Definition", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var name = generatorConfig.Name;
            ImGui.SetCursorPosX(20);
            if (ImGui.InputText($"Name###generatorConfigs{hash}Name", ref name, ushort.MaxValue))
            {
                generatorConfig.Name = name;
                Config.Save();
            }

            ImGui.SameLine(ImGui.GetWindowWidth() - 115);
            if (ImGui.Button("Export##generatorConfigsExport"))
            {
                ExportGeneratorConfigs([generatorConfig]);
            }
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.DalamudRed);
            if (ImGui.Button($"Delete###generatorConfigs{hash}Delete"))
            {
                DeleteGeneratorConfig(generatorConfig);
            }
            ImGui.PopStyleColor();

            var ipcOrdered = funcChannels.OrderBy(g => g.Name);

            ImGui.SetCursorPosX(20);
            if(ImGui.CollapsingHeader($"IPCs###generatorConfigs{hash}IpcConfigs", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.SetCursorPosX(20);
                if (ImGui.Button($"+###generatorConfigs{hash}IpcConfigsNew"))
                {
                    generatorConfig.IpcConfigs.Add(new());
                    Config.Save();
                }

                ImGui.SameLine(40);
                if (ImGui.BeginTabBar($"generatorConfigs{hash}IpcConfigsTabs", ImGuiTabBarFlags.AutoSelectNewTabs | ImGuiTabBarFlags.FittingPolicyScroll))
                {
                    for (var i = 0; i < generatorConfig.IpcConfigs.Count; i++)
                    {
                        var ipcConfig = generatorConfig.IpcConfigs[i];

                        if (ImGui.BeginTabItem($"#{i}###generatorConfigs{hash}IpcConfigs{i}"))
                        {
                            var ipcNamesForCombo = ipcOrdered.Select(g => g.Name).Prepend(string.Empty);
                            var ipcIndexForCombo = ipcNamesForCombo.IndexOf(ipcConfig.Name);
                            ImGui.SetCursorPosX(20);
                            ImGui.PushItemWidth(500);
                            if (ImGui.Combo($"Name###generatorConfigs{hash}IpcConfigs{i}Name", ref ipcIndexForCombo, ipcNamesForCombo.ToArray(), ipcNamesForCombo.Count()))
                            {
                                ipcConfig.Name = ipcNamesForCombo.ElementAt(ipcIndexForCombo);
                                Config.Save();
                            }
                            ImGui.PopItemWidth();

                            ImGui.SameLine(600);
                            ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.DalamudRed);
                            if (ImGui.Button($"Delete###generatorConfigs{hash}IpcConfigs{i}Delete"))
                            {
                                generatorConfig.IpcConfigs.RemoveAt(i);
                                Config.Save();
                            }
                            ImGui.PopStyleColor();

                            if (!ipcConfig.Name.IsNullOrWhitespace())
                            {
                                ImGui.SetCursorPosX(20);
                                if (ipcIndexForCombo > 0)
                                {
                                    var channel = ipcOrdered.ElementAt(ipcIndexForCombo - 1);
                                    var genericTypes = channel.Func!.GetType().GenericTypeArguments;


                                    ImGui.Text($"Detected Signature: Out={genericTypes.Last().Name}");

                                    if (genericTypes.Length > 1)
                                    {
                                        ImGui.SameLine();
                                        ImGui.Text($"In=[{string.Join(", ", genericTypes.Take(genericTypes.Length - 1).Select(a => a.Name))}]");

                                        var ipcArgs = ipcConfig.Args;
                                        ImGui.SetCursorPosX(20);
                                        ImGui.PushItemWidth(500);
                                        if (ImGui.InputText($"Args###generatorConfigs{hash}IpcConfigs{i}Args", ref ipcArgs, ushort.MaxValue))
                                        {
                                            ipcConfig.Args = ipcArgs;
                                            Config.Save();
                                        }
                                        ImGui.PopItemWidth();
                                    }
                                }
                                else
                                {
                                    ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudRed);
                                    ImGui.Text($"Could not retrieve signature for {ipcConfig.Name}");
                                    ImGui.PopStyleColor();
                                }
                            }

                            ImGui.EndTabItem();
                        }
                    }
                    ImGui.EndTabBar();
                }
            } 

            ImGui.SetCursorPosX(20);
            var scriptInputHeight = GeneratorConfigToState[generatorConfig].GeneratedMacros.Any() ? ImGui.GetTextLineHeight() * 13 : ImGui.GetWindowHeight() - ImGui.GetCursorPosY() - 40;

            var script = generatorConfig.Script;
            if (ImGui.InputTextMultiline($"Script (js)###generatorConfigs{hash}Script", ref script, ushort.MaxValue, new(ImGui.GetWindowWidth() - 100, scriptInputHeight)))
            {
                generatorConfig.Script = script;
                Config.Save();
            }

            ImGui.SetCursorPosX(20);
            if (CurrentJsEngine == null)
            {
                if (ImGui.Button($"Execute###generatorConfigs{hash}GenerateMacros"))
                {
                    GenerateMacros(generatorConfig);
                }
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.DalamudOrange);
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0, 0, 0, 1));
                if (ImGui.Button($"Cancel Execution###generatorConfigs{hash}GenerateMacrosCancel"))
                {
                    CurrentJsEngine.Interrupt();
                }
                ImGui.PopStyleColor();
                ImGui.PopStyleColor();
            }
        }
    }



    private void DrawOutputHeader(GeneratorConfig generatorConfig)
    {
        var hash = generatorConfig.GetHashCode();
        var state = GeneratorConfigToState[generatorConfig];
        var generatedMacros = state.GeneratedMacros;
        if (generatedMacros.Count > 0)
        {
            if (ImGui.CollapsingHeader($"Output###generatorConfigs{hash}GeneratedMacros", ImGuiTreeNodeFlags.DefaultOpen))
            {
                var selectedGeneratedMacros = state.SelectedGeneratedMacros;
                var filteredGeneratedMacros = state.FilteredGeneratedMacros;
                var generatedMacrosFilter = state.GeneratedMacrosFilter;

                var filteredConflictingMacros = CachedMacros.Intersect(selectedGeneratedMacros, MacroComparer.INSTANCE);
                var conflictResolutionPopupId = $"###generatorConfigs{hash}GeneratedMacrosConflictsPopup";
                if (filteredConflictingMacros.Any())
                {
                    if (ImGui.BeginPopup(conflictResolutionPopupId))
                    {
                        ImGui.Text($"Override {filteredConflictingMacros.Count()} macros?");

                        ImGui.SetCursorPosX(15);
                        ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.DalamudRed);
                        if (ImGui.Button("Yes", new(100, 30)))
                        {
                            SaveSelectedGeneratedMacros(state);
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
                }

                ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.HealerGreen);
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0, 0, 0, 1));
                if (ImGui.Button($"Save Selected###generatorConfigs{hash}GeneratedMacrosSaveSelected"))
                {
                    if (filteredConflictingMacros.Any())
                    {
                        ImGui.OpenPopup(conflictResolutionPopupId);
                    }
                    else
                    {
                        SaveSelectedGeneratedMacros(state);
                    }
                }
                ImGui.PopStyleColor();
                ImGui.PopStyleColor();

                ImGui.SameLine();
                if (ImGui.Button($"Invert Selection###generatorConfigs{hash}GeneratedMacrosInvertSelection"))
                {
                    state.SelectedGeneratedMacros = new(generatedMacros.Except(selectedGeneratedMacros, MacroComparer.INSTANCE), MacroComparer.INSTANCE);
                }

                ImGui.SameLine();
                if (ImGui.Button($"Select All Filtered###generatorConfigs{hash}GeneratedMacrosSelectAllFiltered"))
                {
                    state.SelectedGeneratedMacros = filteredGeneratedMacros;
                }

                ImGui.SameLine();
                if (ImGui.Button($"Select All Conflicting###generatorConfigs{hash}GeneratedMacrosSelectAllConflicting"))
                {
                    state.SelectedGeneratedMacros = new(CachedMacros.Intersect(generatedMacros, MacroComparer.INSTANCE), MacroComparer.INSTANCE);
                }

                ImGui.SameLine();
                ImGui.PushItemWidth(250);
                if (ImGui.InputText($"Filter ({filteredGeneratedMacros.Count}/{generatedMacros.Count})###generatorConfigs{hash}GeneratedMacrosFilter", ref generatedMacrosFilter, ushort.MaxValue))
                {
                    state.GeneratedMacrosFilter = generatedMacrosFilter;
                    state.FilteredGeneratedMacros = new(MacroSearch.Lookup(generatedMacros, generatedMacrosFilter), MacroComparer.INSTANCE);
                }

                ImGui.SameLine();
                if (ImGui.Button($"X###generatorConfigs{hash}GeneratedMacrosClearFilter"))
                {
                    state.GeneratedMacrosFilter = string.Empty;
                    state.FilteredGeneratedMacros = generatedMacros;
                }

                ImGui.SameLine(ImGui.GetWindowWidth() - 232);
                var showOnlySelected = state.ShowOnlySelected;
                if (ImGui.Checkbox($"Show Only Selected###generatorConfigs{hash}GeneratedMacrosShowOnlySelected", ref showOnlySelected))
                {
                    state.ShowOnlySelected = showOnlySelected;
                }

                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.DalamudRed);
                if (ImGui.Button($"Delete All###generatorConfigs{hash}GeneratedMacrosDeleteAll"))
                {
                    generatedMacros.Clear();
                    selectedGeneratedMacros.Clear();
                }
                ImGui.PopStyleColor();

                if (ImGui.BeginTable($"generatorConfigs{hash}GeneratedMacros", 7, ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Resizable))
                {
                    ImGui.TableSetupColumn(string.Empty, ImGuiTableColumnFlags.None, 0.05f);
                    ImGui.TableSetupColumn($"Name###generatorConfigs{hash}GeneratedMacrosName", ImGuiTableColumnFlags.None, 0.2f);
                    ImGui.TableSetupColumn($"Path###generatorConfigs{hash}GeneratedMacrosPath", ImGuiTableColumnFlags.None, 0.2f);
                    ImGui.TableSetupColumn($"Tags###generatorConfigs{hash}GeneratedMacrosTags", ImGuiTableColumnFlags.None, 0.1f);
                    ImGui.TableSetupColumn($"Command###generatorConfigs{hash}GeneratedMacrosCommand", ImGuiTableColumnFlags.None, 0.1f);
                    ImGui.TableSetupColumn($"Args###generatorConfigs{hash}GeneratedMacrosArgs", ImGuiTableColumnFlags.None, 0.1f);
                    ImGui.TableSetupColumn($"Content###generatorConfigs{hash}GeneratedMacrosContent", ImGuiTableColumnFlags.None, 0.5f);
                    ImGui.TableHeadersRow();

                    var visibleFilteredGeneratedMacros = showOnlySelected ? filteredGeneratedMacros.Intersect(selectedGeneratedMacros, MacroComparer.INSTANCE) : filteredGeneratedMacros;

                    var clipper = ImGuiHelper.NewListClipper();
                    clipper.Begin(visibleFilteredGeneratedMacros.Count(), 27);

                    while(clipper.Step())
                    {
                        for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                        {
                            var generatedMacro = visibleFilteredGeneratedMacros.ElementAt(i);
                            if (ImGui.TableNextColumn())
                            {
                                var selected = selectedGeneratedMacros.Contains(generatedMacro);
                                if (ImGui.Checkbox($"###generatorConfigs{hash}GeneratedEntries{i}Selected", ref selected))
                                {
                                    if (selected)
                                    {
                                        selectedGeneratedMacros.Add(generatedMacro);
                                    }
                                    else
                                    {
                                        selectedGeneratedMacros.Remove(generatedMacro);
                                    }
                                }
                            }

                            if (ImGui.TableNextColumn())
                            {
                                ImGui.Text(generatedMacro.Name);
                            }

                            if (ImGui.TableNextColumn())
                            {
                                ImGui.Text(generatedMacro.Path);
                            }

                            if (ImGui.TableNextColumn())
                            {
                                ImGui.Text(string.Join(", ", generatedMacro.Tags));
                            }

                            if (ImGui.TableNextColumn())
                            {
                                ImGui.Text(generatedMacro.Command);
                            }

                            if (ImGui.TableNextColumn())
                            {
                                ImGui.Text(generatedMacro.Args);
                            }

                            if (ImGui.TableNextColumn())
                            {
                                var contentLines = generatedMacro.Content.Split("\n");
                                if (contentLines.Length > 1)
                                {
                                    ImGui.Text($"{contentLines[0]}...");
                                    if (ImGui.IsItemHovered())
                                    {
                                        ImGui.SetTooltip(generatedMacro.Content);
                                    }
                                }
                                else
                                {
                                    ImGui.Text(generatedMacro.Content);
                                }         
                            }
                        }
                    }
                    clipper.Destroy();

                    ImGui.EndTable();
                }
            }
        }
    }

    private void NewGeneratorConfig()
    {
        GeneratorConfig generatorConfig = new();
        Config.GeneratorConfigs.Add(generatorConfig);
        GeneratorConfigToState.Add(generatorConfig, new());
        Config.Save();
    }

    private void RecreateDefaultGeneratorConfigs()
    {
        var defaultGeneratorConfigs = GeneratorConfig.GetDefaults();
        Config.GeneratorConfigs.AddRange(defaultGeneratorConfigs);
        Config.Save();
        defaultGeneratorConfigs.ForEach(c => GeneratorConfigToState.Add(c, new()));
    }

    private void DeleteGeneratorConfigs()
    {
        GeneratorConfigToState.Clear();
        Config.GeneratorConfigs.Clear();
        Config.Save();
    }

    private void DeleteGeneratorConfig(GeneratorConfig generatorConfig)
    {
        GeneratorConfigToState.Remove(generatorConfig);
        Config.GeneratorConfigs.Remove(generatorConfig);
        Config.Save();
    }

    private void GenerateMacros(GeneratorConfig generatorConfig)
    {
        Task.Run(() =>
        {
            try
            {
                CurrentJsEngine = JsEngineSwitcher.Current.CreateEngine(Config.GeneratorEngineName);
                var generatedMacros = new Generator(generatorConfig, CurrentJsEngine, PluginLog).Execute();

                var state = GeneratorConfigToState[generatorConfig];
                state.GeneratedMacros = generatedMacros;
                state.SelectedGeneratedMacros = new(generatedMacros, MacroComparer.INSTANCE);
                state.FilteredGeneratedMacros = new(MacroSearch.Lookup(generatedMacros, state.GeneratedMacrosFilter), MacroComparer.INSTANCE);
                GeneratorException = null;
            }
            catch (GeneratorException e)
            {
                if (e.InnerException is JsInterruptedException)
                {
                    PluginLog.Debug("Generator execution has been interrupted");
                } 
                else
                {
                    GeneratorException = e;
                }
            }
            finally
            {
                CurrentJsEngine = null;
            }
        });
        
    }

    private void SaveSelectedGeneratedMacros(GeneratorConfigState state)
    {
        var selectedGeneratedMacros = state.SelectedGeneratedMacros;
        var conflictingMacros = CachedMacros.Intersect(selectedGeneratedMacros);
        CachedMacros.ExceptWith(selectedGeneratedMacros);
        CachedMacros.UnionWith(selectedGeneratedMacros);

        TaskQueue.Enqueue(() =>
        {
            MacroTable.Delete(conflictingMacros);
            MacroTable.Insert(selectedGeneratedMacros);
        });
        

        state.GeneratedMacros.ExceptWith(selectedGeneratedMacros);
        state.FilteredGeneratedMacros.ExceptWith(selectedGeneratedMacros);
        selectedGeneratedMacros.Clear();
    }
}
