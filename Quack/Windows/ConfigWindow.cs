using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dalamud;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc.Internal;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using ImGuiNET;
using Quack.Generators;
using Quack.Macros;
using Quack.Utils;
using Quack.Windows.States;

namespace Quack.Windows;

public class ConfigWindow : Window, IDisposable
{
    private IDalamudPluginInterface PluginInterface { get; init; }
    private MainWindow MainWindow { get; init; }
    private Config Config { get; init; }
    private IPluginLog PluginLog { get; init; }
    private MacrosState MacrosState { get; set; }

    private Dictionary<GeneratorConfig, GeneratorConfigState> GeneratorConfigToState { get; set; }
    private GeneratorException? GeneratorException { get; set; } = null;

    public ConfigWindow(IDalamudPluginInterface pluginInterface, MainWindow mainWindow, Config config, IPluginLog pluginLog) : base("Quack Config##configWindow")
    {
        PluginInterface = pluginInterface;
        MainWindow = mainWindow;
        Config = config;
        PluginLog = pluginLog;

        MacrosState = new(Config.Macros);
        GeneratorConfigToState = Config.GeneratorConfigs.ToDictionary(c => c, c => new GeneratorConfigState());
    }

    public void Dispose() { }

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
    }

    private void DrawGeneralTab()
    {
        ImGui.NewLine();
        var commandFormat = Config.CommandFormat;
        if (ImGui.InputText("Command format##commandFormat", ref commandFormat, ushort.MaxValue))
        {
            Config.CommandFormat = commandFormat;
            Config.Save();
        }
        ImGui.Text("(PM format supported via {0:P} placeholder)");

        ImGui.NewLine();

        var maxSearchResults = Config.MaxSearchResults;
        if (ImGui.InputInt("Max Search Results##maxSearchResults", ref maxSearchResults))
        {
            Config.MaxSearchResults = maxSearchResults;
            Config.Save();
        }
    }


    private void DrawMacrosTab()
    {
        ImGui.Text("Macro editor is not functionnal yet");
        DrawTreeNodes(MacrosState.Nodes);
    }

    private void DrawTreeNodes(HashSet<TreeNode<string>> nodes)
    {
        foreach(var node in nodes.OrderBy(n => Path.GetFileName(n.Item)))
        {
            var name = Path.GetFileName(node.Item);
            if (node.Children.Count > 0)
            {
                if (ImGui.TreeNodeEx($"{name}###Macro{node.Item.GetHashCode()}Path"))
                {
                    DrawTreeNodes(node.Children);
                    ImGui.TreePop();
                }
            }
            else
            {
                if (ImGui.TreeNodeEx($"{name}###Macro{node.Item.GetHashCode()}", ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet))
                {
                    ImGui.TreePop();
                }
            }
        }

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

        ImGui.SameLine(ImGui.GetWindowWidth() - 200);
        if (ImGui.Button("Recreate Defaults##generatorConfigsAppendDefaults"))
        {
            RecreateDefaultGeneratorConfigs();
        }

        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.DalamudRed);
        if (ImGui.Button("Delete All##generatorConfigsDeleteAll"))
        {
            DeleteGeneratorConfigs();
        }
        ImGui.PopStyleColor();

        var generatorConfigs = Config.GeneratorConfigs;
        var funcChannels = Service<CallGate>.Get().Gates.Values.Where(g => g.Func != null);
        foreach (var generatorConfig in generatorConfigs)
        {
            var hash = generatorConfig.GetHashCode();
            if (ImGui.BeginTabBar("generatorConfigs", ImGuiTabBarFlags.AutoSelectNewTabs | ImGuiTabBarFlags.TabListPopupButton | ImGuiTabBarFlags.FittingPolicyScroll))
            {
                if (ImGui.BeginTabItem($"{(generatorConfig.Name.IsNullOrWhitespace() ? "(Blank)" : generatorConfig.Name)}###generatorConfigs{hash}"))
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

                    ImGui.SameLine();
                    ImGui.Spacing();
                    ImGui.SameLine();
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
            if (ImGui.InputTextMultiline($"Script###generatorConfigs{hash}Script", ref script, ushort.MaxValue, new(ImGui.GetWindowWidth() - 100, ImGui.GetTextLineHeight() * 13)))
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
                var conflictResolutionPopupId = $"generatorConfigs{hash}GeneratedMacrosConfictsPopup";
                if (conflictingMacros.Any())
                {
                    if (ImGui.BeginPopup(conflictResolutionPopupId))
                    {
                        ImGui.Text($"Found {conflictingMacros.Count()} path conflicts, confirm override?");

                        ImGui.SetCursorPosX(15);
                        ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.DalamudRed);
                        if (ImGui.Button("Yes", new(100, 30)))
                        {
                            SaveSelectedGeneratedMacros(state);
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
        Config.Save();

        GeneratorConfigToState.Add(generatorConfig, new());
    }

    private void RecreateDefaultGeneratorConfigs()
    {
        var defaultGeneratorConfigs = GeneratorConfig.GetDefaults();
        Config.GeneratorConfigs.AddRange(defaultGeneratorConfigs);
        defaultGeneratorConfigs.ForEach(c => GeneratorConfigToState.Add(c, new()));
        Config.Save();
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
                var generatedMacros = new Generator(PluginInterface, generatorConfig, PluginLog).Execute();

                var state = GeneratorConfigToState[generatorConfig];
                state.GeneratedMacros.UnionWith(generatedMacros);
                state.SelectedGeneratedMacros.UnionWith(generatedMacros);
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

        MacrosState = new(Config.Macros);
        MainWindow.UpdateFilteredMacros();
    }
}
