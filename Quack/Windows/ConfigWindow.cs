using System;
using System.Collections.Generic;
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

namespace Quack.Windows;

public class ConfigWindow : Window, IDisposable
{
    private IDalamudPluginInterface PluginInterface { get; init; }
    private Config Config { get; init; }
    private IPluginLog PluginLog { get; init; }

    private GeneratorException? GeneratorException { get; set; } = null;
    private List<HashSet<Macro>> GeneratedMacrosPerGeneratorIndex { get; init; } = [];
    private List<HashSet<Macro>> SelectedGeneratedMacrosPerGeneratorIndex { get; init; } = [];
    private List<string> GeneratedMacrosFiltersPerGeneratorIndex { get; init; } = [];

    public ConfigWindow(IDalamudPluginInterface pluginInterface, Config config, IPluginLog pluginLog) : base("Quack Config##configWindow")
    {
        PluginInterface = pluginInterface;
        Config = config;
        PluginLog = pluginLog;
        Config.GeneratorConfigs.ForEach(_ => {
            PushDefaultGeneratorMacrosValues();
        });
    }

    public void Dispose() { }

    public override void Draw()
    {
        var maxSearchResults = Config.MaxSearchResults;
        if (ImGui.InputInt("Max Search Results##maxSearchResults", ref maxSearchResults))
        {
            Config.MaxSearchResults = maxSearchResults;
            Config.Save();
        }

        var commandFormat = Config.CommandFormat;
        if (ImGui.InputText("Command format - PM format supported via {0:P}##commandFormat", ref commandFormat, ushort.MaxValue))
        {
            Config.CommandFormat = commandFormat;
            Config.Save();
        }

        // Generators
        if (ImGui.CollapsingHeader("Generators##generatorConfigs", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (GeneratorException != null)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudRed);
                ImGui.Text(GeneratorException.ToString());
                ImGui.PopStyleColor();
                ImGui.SameLine();
                if (ImGui.Button("Clear Exception##clearException"))
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
            if (ImGui.Button("Delete All##generatorConfigsDeleteAll"))
            {
                DeleteGeneratorConfigs();
            }

            var generatorConfigs = Config.GeneratorConfigs;
            var funcChannels = Service<CallGate>.Get().Gates.Values.Where(g => g.Func != null);
            for (var i = 0; i < generatorConfigs.Count; i++)
            {
                var generatorConfig = generatorConfigs[i];
                PadLeft(15);
                if (ImGui.CollapsingHeader($"{generatorConfig.Name}###generatorConfigs{i}"))
                {
                    PadLeft(30);
                    var name = generatorConfig.Name;
                    if (ImGui.InputText($"name###generatorConfigs{i}Name", ref name, ushort.MaxValue))
                    {
                        generatorConfig.Name = name;
                        Config.Save();
                    }

                    ImGui.SameLine(ImGui.GetWindowWidth() - 120);
                    if (ImGui.Button($"Delete###generatorConfigs{i}Delete"))
                    {
                        DeleteGeneratorConfig(generatorConfig);
                    }

                    PadLeft(30);
                    var ipcOrdered = funcChannels.OrderBy(g => g.Name);
                    var ipcNamesForCombo = ipcOrdered.Select(g => g.Name).Prepend(string.Empty);
                    var ipcIndexForCombo = ipcNamesForCombo.IndexOf(generatorConfig.IpcName);
                    if (ImGui.Combo($"IPC Name###generatorConfigs{i}IpcName", ref ipcIndexForCombo, ipcNamesForCombo.ToArray(), ipcNamesForCombo.Count()))
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

                            PadLeft(30);
                            var ipcArgs = generatorConfig.IpcArgs;
                            if (ImGui.InputText($"IPC Args###generatorConfigs{i}IpcArgs", ref ipcArgs, ushort.MaxValue))
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


                    PadLeft(30);
                    var script = generatorConfig.Script;
                    if (ImGui.InputTextMultiline($"Script###generatorConfigs{i}Script", ref script, ushort.MaxValue, new(ImGui.GetWindowWidth() - 100, ImGui.GetTextLineHeight() * 13)))
                    {
                        generatorConfig.Script = script;
                        Config.Save();
                    }

                    PadLeft(30);
                    if (ImGui.Button($"Execute###generatorConfigs{i}GenerateMacros"))
                    {
                        GenerateMacros(generatorConfig, i);
                    }

                    var generatedMacros = GeneratedMacrosPerGeneratorIndex[i];
                    if (generatedMacros.Count > 0)
                    {
                        PadLeft(30);
                        if (ImGui.CollapsingHeader($"Generated Macros###generatorConfigs{i}GeneratedMacros", ImGuiTreeNodeFlags.DefaultOpen))
                        {
                            var selectedGeneratedMacros = SelectedGeneratedMacrosPerGeneratorIndex[i];
                            var generatedMacrosFilter = GeneratedMacrosFiltersPerGeneratorIndex[i];

                            PadLeft(40);
                            if (ImGui.Button($"Save Selected###generatorConfigs{i}GeneratedMacrosSaveSelected"))
                            {
                                Config.Macros.AddRange(selectedGeneratedMacros);
                                Config.Save();
                                generatedMacros.ExceptWith(selectedGeneratedMacros);
                                selectedGeneratedMacros.Clear();
                            }

                            ImGui.SameLine();
                            var visibleGeneratedMacros = Macro.FilterAndSort(generatedMacros, generatedMacrosFilter);
                            if (visibleGeneratedMacros.All(selectedGeneratedMacros.Contains))
                            {
                                if (ImGui.Button($"Deselect All Visible###generatorConfigs{i}GeneratedMacrosDeselectAllVisible"))
                                {
                                    selectedGeneratedMacros.ExceptWith(visibleGeneratedMacros);
                                }
                            } 
                            else
                            {
                                if (ImGui.Button($"Select All Visible###generatorConfigs{i}GeneratedMacrosSelectAllVisible"))
                                {
                                    selectedGeneratedMacros.UnionWith(visibleGeneratedMacros);
                                }
                            }

                            ImGui.SameLine();
                            if (ImGui.Button($"Delete All###generatorConfigs{i}GeneratedMacrosDeleteAll"))
                            {
                                GeneratedMacrosPerGeneratorIndex[i].Clear();
                                SelectedGeneratedMacrosPerGeneratorIndex[i].Clear();
                            }

                            ImGui.SameLine();
                            ImGui.PushItemWidth(250);
                            if (ImGui.InputText($"Filter###generatorConfigs{i}GeneratedMacrosFilter", ref generatedMacrosFilter, ushort.MaxValue))
                            {
                                GeneratedMacrosFiltersPerGeneratorIndex[i] = generatedMacrosFilter;
                            }

                            ImGui.SameLine();
                            if (ImGui.Button($"X###generatorConfigs{i}GeneratedMacrosClearFilter"))
                            {
                                GeneratedMacrosFiltersPerGeneratorIndex[i] = string.Empty;
                            }

                            PadLeft(40);
                            if (ImGui.BeginTable($"generatorConfigs{i}GeneratedMacros", 4, ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY, new(ImGui.GetWindowWidth() - 80, ImGui.GetTextLineHeight() * (1.3f + generatedMacros.Count * 1.6f))))
                            {
                                ImGui.TableSetupColumn(string.Empty, ImGuiTableColumnFlags.None, 0.05f);
                                ImGui.TableSetupColumn($"Name###generatorConfigs{i}GeneratedMacrosName", ImGuiTableColumnFlags.None, 0.2f);
                                ImGui.TableSetupColumn($"Path###generatorConfigs{i}GeneratedMacrosPath", ImGuiTableColumnFlags.None, 0.2f);
                                ImGui.TableSetupColumn($"Content###generatorConfigs{i}GeneratedMacrosContent", ImGuiTableColumnFlags.None, 0.5f);
                                ImGui.TableHeadersRow();

                                for (var j = 0; j < generatedMacros.Count; j++)
                                {
                                    var generatedMacro = generatedMacros.ElementAt(j);
                                    if (visibleGeneratedMacros.Contains(generatedMacro))
                                    {
                                        if (ImGui.TableNextColumn())
                                        {
                                            var selected = selectedGeneratedMacros.Contains(generatedMacro);
                                            if (ImGui.Checkbox($"###generatorConfigs{i}GeneratedEntries{j}Selected", ref selected))
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
                                            ImGui.Text(generatedMacro.Content);
                                        }
                                    }
                                }
                                ImGui.EndTable();
                            }
                        }
                    }  
                }
            }
        }
    }

    private void PadLeft(int x)
    {
        ImGui.SetCursorPos(new(x, ImGui.GetCursorPosY()));
    }

    private void NewGeneratorConfig()
    {
        GeneratorConfig generatorConfig = new();
        Config.GeneratorConfigs.Add(generatorConfig);
        Config.Save();
        PushDefaultGeneratorMacrosValues();
    }

    private void RecreateDefaultGeneratorConfigs()
    {
        Config.GeneratorConfigs.AddRange(GeneratorConfig.GetDefaults());
        Config.Save();
    }

    private void DeleteGeneratorConfigs()
    {
        Config.GeneratorConfigs.Clear();
        Config.Save();
    }

    private void DeleteGeneratorConfig(GeneratorConfig generatorConfig)
    {
        var index = Config.GeneratorConfigs.IndexOf(generatorConfig);
        GeneratedMacrosPerGeneratorIndex.RemoveAt(index);
        SelectedGeneratedMacrosPerGeneratorIndex.RemoveAt(index);
        GeneratedMacrosFiltersPerGeneratorIndex.RemoveAt(index);

        Config.GeneratorConfigs.Remove(generatorConfig);
        Config.Save();
    }

    private void PushDefaultGeneratorMacrosValues()
    {
        GeneratedMacrosPerGeneratorIndex.Add([]);
        SelectedGeneratedMacrosPerGeneratorIndex.Add([]);
        GeneratedMacrosFiltersPerGeneratorIndex.Add(string.Empty);
    }

    private void GenerateMacros(GeneratorConfig generatorConfig, int index)
    {
        Task.Run(() =>
        {
            try
            {
                var generatedMacros = new Generator(PluginInterface, generatorConfig, PluginLog).Execute();
                GeneratedMacrosPerGeneratorIndex[index].UnionWith(generatedMacros);
                SelectedGeneratedMacrosPerGeneratorIndex[index].UnionWith(generatedMacros);
                GeneratorException = null;
            }
            catch (GeneratorException e)
            {
                GeneratorException = e;
            }
        });

    }
}
