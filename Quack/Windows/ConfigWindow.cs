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

    private Executor Executor { get; init; }
    private IKeyState KeyState { get; init; }
    private Config Config { get; init; }
    private IPluginLog PluginLog { get; init; }
    private MacrosState MacrosState { get; set; } = null!;

    private Dictionary<GeneratorConfig, GeneratorConfigState> GeneratorConfigToState { get; set; }
    private GeneratorException? GeneratorException { get; set; } = null;

    private FileDialogManager FileDialogManager { get; init; } = new();
    private string? TmpConflictPath { get; set; }

    public ConfigWindow(Executor executor, IKeyState keyState, Config config, IPluginLog pluginLog) : base("Quack Config##configWindow")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(820, 520),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        Executor = executor;
        KeyState = keyState;
        Config = config;
        PluginLog = pluginLog;

        UpdateMacrosState();
        GeneratorConfigToState = Config.GeneratorConfigs.ToDictionary(c => c, c => new GeneratorConfigState());

        Config.OnSave += UpdateMacrosState;
    }

    public void Dispose() 
    {
        Config.OnSave -= UpdateMacrosState;
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
            ImGui.NewLine();
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

            ImGui.NewLine();
            var maxMatches = Config.MaxMatches;
            if (ImGui.InputInt("Max Matches##maxMatches", ref maxMatches))
            {
                Config.MaxMatches = maxMatches;
                Config.Save();
            }
        }

        ImGui.NewLine();
        if (ImGui.CollapsingHeader("Formatter##formatterHeader", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.NewLine();
            var commandFormat = Config.CommandFormat;
            if (ImGui.InputText("Command Format##commandFormat", ref commandFormat, ushort.MaxValue))
            {
                Config.CommandFormat = commandFormat;
                Config.Save();
            }
            ImGui.Text("(PM format supported via {0:P} placeholder)");
        }
    }

    public void UpdateMacrosState()
    {
        var selectedPath = MacrosState?.SelectedPath;
        var filter = MacrosState != null? MacrosState.Filter : string.Empty;
        var filteredMacros = Search.Lookup(Config.Macros, filter);
        MacrosState = new(
            MacrosState.GeneratePathNodes(filteredMacros), 
            selectedPath, 
            filter
        );
    }

    private void DrawMacrosTab()
    {
        if (ImGui.Button("New##newMacro"))
        {
            NewMacro();
        }

        ImGui.SameLine(ImGui.GetWindowWidth() - 305);
        if (ImGui.Button("Export Filtered##filteredMacrosExport"))
        {
            ExportMacros(Search.Lookup(Config.Macros, MacrosState.Filter));
        }

        ImGui.SameLine();
        if (ImGui.Button("Export All##macrosExport"))
        {
            ExportMacros(Config.Macros);
        }

        ImGui.SameLine();
        if (ImGui.Button("Import##macrosImport"))
        {
            ImportMacros();
        }

        var deleteAllMacrosPopup = "deleteAllMacrosPopup";
        if (ImGui.BeginPopup(deleteAllMacrosPopup))
        {
            ImGui.Text($"Confirm deleting {Config.Macros.Count} macros?");

            ImGui.SetCursorPosX(15);
            ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.DalamudRed);
            if (ImGui.Button("Yes", new(100, 30)))
            {
                Config.Macros.Clear();
                Config.Save();
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
            if (Config.Macros.Count > 0)
            {
                ImGui.OpenPopup(deleteAllMacrosPopup);
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
            Config.Macros.FindFirst(m => m.Path == MacrosState.SelectedPath, out var macro);
            if (macro != null)
            {
                var i = Config.Macros.IndexOf(macro);

                var name = macro.Name;
                if (ImGui.InputText($"Name###macros{i}Name", ref name, ushort.MaxValue))
                {
                    macro.Name = name;
                    Config.Save();
                }

                var deleteMacroPopup = $"macros{i}DeletePopup";
                if (ImGui.BeginPopup(deleteMacroPopup))
                {
                    ImGui.Text($"Confirm deleting {(macro.Name.IsNullOrWhitespace()? BLANK_NAME : macro.Name)} macro?");

                    ImGui.SetCursorPosX(15);
                    ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.DalamudRed);
                    if (ImGui.Button($"Yes###{deleteMacroPopup}Yes", new(100, 30)))
                    {
                        DeleteMacro(macro);
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.PopStyleColor();

                    ImGui.SameLine();
                    if (ImGui.Button($"No###{deleteMacroPopup}No", new(100, 30)))
                    {
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.EndPopup();
                }

                ImGui.SameLine(ImGui.GetWindowWidth() - 80);
                ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.DalamudRed);
                if (ImGui.Button($"Delete###macros{i}Delete"))
                {
                    ImGui.OpenPopup(deleteMacroPopup);
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
                        macro.Path = TmpConflictPath!;
                        MacrosState.SelectedPath = TmpConflictPath;
                        Config.Save();

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

                var path = TmpConflictPath != null ? TmpConflictPath : macro.Path;
                if (ImGui.InputText($"Path###macros{i}Path", ref path, ushort.MaxValue))
                {
                    TmpConflictPath = null;
                    if (Config.Macros.FindFirst(m => m.Path == path, out var conflictingMacro) && macro != conflictingMacro)
                    {
                        TmpConflictPath = path;
                        ImGui.OpenPopup(pathConflictPopupId);
                    } 
                    else
                    {
                        macro.Path = path;
                        MacrosState.SelectedPath = path;
                        Config.Save();
                    }
                }

                var tags = string.Join(',', macro.Tags);
                if (ImGui.InputText($"Tags (comma separated)###macros{i}Tags", ref tags, ushort.MaxValue))
                {
                    macro.Tags = tags.Split(',').Select(t => t.Trim()).ToArray();
                    Config.Save();
                }

                var content = macro.Content;
                if (ImGui.InputTextMultiline($"Content###macros{i}Content", ref content, ushort.MaxValue, new(ImGui.GetWindowWidth() - 200, ImGui.GetWindowHeight() - ImGui.GetCursorPosY() - 40)))
                {
                    macro.Content = content;
                    Config.Save();
                }

                if (Executor.HasRunningTasks())
                {
                    ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.DalamudOrange);
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0, 0, 0, 1));
                    if (ImGui.Button($"Cancel###macrosCancelAll"))
                    {
                        Executor.CancelTasks();
                    }
                    ImGui.PopStyleColor();
                    ImGui.PopStyleColor();

                    ImGui.SameLine();
                }

                ImGui.SetCursorPosX(ImGui.GetWindowWidth() - 322);
                if (ImGui.Button($"Execute###macros{i}Execute"))
                {
                    Executor.ExecuteTask(macro);
                }

                ImGui.SameLine();
                if (ImGui.Button($"+ Format###macros{i}ExecuteWithFormat"))
                {
                    Executor.ExecuteTask(macro, Config.CommandFormat);
                }
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

                var popupId = $"Export###macro{node.Item}TreeNodePopup";
                if (ImGui.BeginPopupContextItem(popupId))
                {
                    var macros = Config.Macros.Where(m => m.Path.StartsWith(node.Item));
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
                if (ImGui.BeginPopupContextItem($"Export###{popupId}"))
                {
                    Config.Macros.FindFirst(m => m.Path == node.Item, out var macro);
                    if (macro != null)
                    {
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
        var newMacro = new Macro();
        Config.Macros.Add(newMacro);
        MacrosState.SelectedPath = newMacro.Path;
        Config.Save();
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
                Config.Macros = importedMacros.Union(Config.Macros).ToHashSet(new MacroComparer());
                Config.Save();
            }
        });
    }

    private void DeleteMacro(Macro macro)
    {
        Config.Macros.Remove(macro);
        Config.Save();
    }

    private void DeleteMacros(IEnumerable<Macro> macros)
    {
        foreach(var macro in macros)
        {
            Config.Macros.Remove(macro);
        }
        Config.Save();
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

        ImGui.SameLine(ImGui.GetWindowWidth() - 310);
        if (ImGui.Button("Export##generatorConfigsExport"))
        {
            ExportGeneratorConfigs();
        }

        ImGui.SameLine();
        if (ImGui.Button("Import##generatorConfigsExport"))
        {
            ImportGeneratorConfigs();
        }

        ImGui.SameLine();
        if (ImGui.Button("Recreate Defaults##generatorConfigsAppendDefaults"))
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

    private void ExportGeneratorConfigs()
    {
        FileDialogManager.SaveFileDialog("Export Generators", ".*", "generators.json", ".json", (valid, path) =>
        {
            if (valid)
            {
                using var file = File.CreateText(path);
                new JsonSerializer().Serialize(file, Config.GeneratorConfigs);
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
            if (ImGui.InputText($"name###generatorConfigs{hash}Name", ref name, ushort.MaxValue))
            {
                generatorConfig.Name = name;
                Config.Save();
            }

            ImGui.SameLine(ImGui.GetWindowWidth() - 65);
            ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.DalamudRed);
            if (ImGui.Button($"Delete###generatorConfigs{hash}Delete"))
            {
                DeleteGeneratorConfig(generatorConfig);
            }
            ImGui.PopStyleColor();

            var ipcOrdered = funcChannels.OrderBy(g => g.Name);
            var ipcNamesForCombo = ipcOrdered.Select(g => g.Name).Prepend(string.Empty);
            var ipcIndexForCombo = ipcNamesForCombo.IndexOf(generatorConfig.IpcName);
            if (ImGui.Combo($"IPC Name###generatorConfigs{hash}IpcName", ref ipcIndexForCombo, ipcNamesForCombo.ToArray(), ipcNamesForCombo.Count()))
            {
                generatorConfig.IpcName = ipcNamesForCombo.ElementAt(ipcIndexForCombo);
                Config.Save();
            }

            if (!generatorConfig.IpcName.IsNullOrWhitespace())
            {
                if (ipcIndexForCombo > 0)
                {
                    var channel = ipcOrdered.ElementAt(ipcIndexForCombo - 1);
                    var genericTypes = channel.Func!.GetType().GenericTypeArguments;

                    ImGui.Text($"Detected Signature: Out={genericTypes.Last().Name}");

                    if (genericTypes.Length > 1)
                    {
                        ImGui.SameLine();
                        ImGui.Text($"In=[{string.Join(", ", genericTypes.Take(genericTypes.Length - 1).Select(a => a.Name))}]");
                    }

                    var ipcArgs = generatorConfig.IpcArgs;
                    if (ImGui.InputText($"IPC Args###generatorConfigs{hash}IpcArgs", ref ipcArgs, ushort.MaxValue))
                    {
                        generatorConfig.IpcArgs = ipcArgs;
                        Config.Save();
                    }
                }
                else
                {
                    ImGui.SameLine();
                    ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudRed);
                    ImGui.Text("Could not retrieve signature");
                    ImGui.PopStyleColor();
                }
            }

            var script = generatorConfig.Script;
            if (ImGui.InputTextMultiline($"Script (js)###generatorConfigs{hash}Script", ref script, ushort.MaxValue, new(ImGui.GetWindowWidth() - 100, ImGui.GetTextLineHeight() * 13)))
            {
                generatorConfig.Script = script;
                Config.Save();
            }

            if (ImGui.Button($"Execute###generatorConfigs{hash}GenerateMacros"))
            {
                GenerateMacros(generatorConfig);
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

                var conflictingMacros = Config.Macros.Intersect(generatedMacros, new MacroComparer());
                var conflictResolutionPopupId = $"###generatorConfigs{hash}GeneratedMacrosConflictsPopup";
                if (conflictingMacros.Any())
                {
                    if (ImGui.BeginPopup(conflictResolutionPopupId))
                    {
                        ImGui.Text($"Override {conflictingMacros.Count()} macros?");

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
                
                if (ImGui.Button($"Save Selected###generatorConfigs{hash}GeneratedMacrosSaveSelected"))
                {
                    if (conflictingMacros.Any())
                    {
                        ImGui.OpenPopup(conflictResolutionPopupId);
                    }
                    else
                    {
                        SaveSelectedGeneratedMacros(state);
                    }
                }

                ImGui.SameLine();
                if (filteredGeneratedMacros.All(selectedGeneratedMacros.Contains))
                {
                    if (ImGui.Button($"Deselect All Filtered###generatorConfigs{hash}GeneratedMacrosDeselectAllFiltered"))
                    {
                        selectedGeneratedMacros.ExceptWith(filteredGeneratedMacros);
                    }
                }
                else
                {
                    if (ImGui.Button($"Select All Filtered###generatorConfigs{hash}GeneratedMacrosSelectAllFiltered"))
                    {
                        selectedGeneratedMacros.UnionWith(filteredGeneratedMacros);
                    }
                }

                ImGui.SameLine();
                ImGui.PushItemWidth(250);
                if (ImGui.InputText($"Filter ({filteredGeneratedMacros.Count}/{generatedMacros.Count})###generatorConfigs{hash}GeneratedMacrosFilter", ref generatedMacrosFilter, ushort.MaxValue))
                {
                    state.GeneratedMacrosFilter = generatedMacrosFilter;
                    state.FilteredGeneratedMacros = Search.Lookup(generatedMacros, generatedMacrosFilter).ToHashSet();
                }

                ImGui.SameLine();
                if (ImGui.Button($"X###generatorConfigs{hash}GeneratedMacrosClearFilter"))
                {
                    state.GeneratedMacrosFilter = string.Empty;
                    state.FilteredGeneratedMacros = generatedMacros;
                }

                ImGui.SameLine(ImGui.GetWindowWidth() - 80);
                ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.DalamudRed);
                if (ImGui.Button($"Delete All###generatorConfigs{hash}GeneratedMacrosDeleteAll"))
                {
                    generatedMacros.Clear();
                    selectedGeneratedMacros.Clear();
                }
                ImGui.PopStyleColor();

                if (ImGui.BeginTable($"generatorConfigs{hash}GeneratedMacros", 5, ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Resizable))
                {
                    ImGui.TableSetupColumn(string.Empty, ImGuiTableColumnFlags.None, 0.05f);
                    ImGui.TableSetupColumn($"Name###generatorConfigs{hash}GeneratedMacrosName", ImGuiTableColumnFlags.None, 0.2f);
                    ImGui.TableSetupColumn($"Path###generatorConfigs{hash}GeneratedMacrosPath", ImGuiTableColumnFlags.None, 0.2f);
                    ImGui.TableSetupColumn($"Tags###generatorConfigs{hash}GeneratedMacrosTags", ImGuiTableColumnFlags.None, 0.2f);
                    ImGui.TableSetupColumn($"Content###generatorConfigs{hash}GeneratedMacrosContent", ImGuiTableColumnFlags.None, 0.5f);
                    ImGui.TableHeadersRow();

                    for (var subindex = 0; subindex < generatedMacros.Count(); subindex++)
                    {
                        var generatedMacro = generatedMacros.ElementAt(subindex);
                        if (filteredGeneratedMacros.Contains(generatedMacro))
                        {
                            if (ImGui.TableNextColumn())
                            {
                                var selected = selectedGeneratedMacros.Contains(generatedMacro);
                                if (ImGui.Checkbox($"###generatorConfigs{subindex}GeneratedEntries{subindex}Selected", ref selected))
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
                                ImGui.Text(generatedMacro.Content);
                            }
                        }
                    }
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
                var generatedMacros = new Generator(generatorConfig, PluginLog).Execute();

                var state = GeneratorConfigToState[generatorConfig];
                state.GeneratedMacros = generatedMacros;
                state.SelectedGeneratedMacros = generatedMacros.ToHashSet(new MacroComparer());
                state.FilteredGeneratedMacros = Search.Lookup(generatedMacros, state.GeneratedMacrosFilter).ToHashSet(new MacroComparer());
                GeneratorException = null;
            }
            catch (GeneratorException e)
            {
                GeneratorException = e;
            }
        });

    }

    private void SaveSelectedGeneratedMacros(GeneratorConfigState state)
    {
        var selectedGeneratedMacros = state.SelectedGeneratedMacros;
        Config.Macros = selectedGeneratedMacros.Union(Config.Macros).ToHashSet(new MacroComparer());
        Config.Save();

        state.GeneratedMacros.ExceptWith(selectedGeneratedMacros);
        state.FilteredGeneratedMacros.ExceptWith(selectedGeneratedMacros);
        selectedGeneratedMacros.Clear();
    }
}
