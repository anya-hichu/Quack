using Dalamud.Interface.Colors;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Plugin.Ipc.Internal;
using Dalamud.Utility;
using Dalamud;
using ImGuiNET;
using Quack.Generators;
using Quack.Macros;
using Quack.Utils;
using Quack.Windows.Configs.States;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.IO;
using System.Linq;
using System;
using System.Numerics;
using JavaScriptEngineSwitcher.Core;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;

namespace Quack.Windows.Configs.Tabs;

public class GeneratorsTab : ModelTab
{
    private HashSet<Macro> CachedMacros { get; init; }
    private IJsEngine? CurrentJsEngine { get; set; }
    private Config Config { get; init; }
    private FileDialogManager FileDialogManager { get; init; }
    private Dictionary<GeneratorConfig, GeneratorConfigState> GeneratorConfigToState { get; set; }
    private GeneratorException? GeneratorException { get; set; } = null;
    private MacroTableQueue MacroTableQueue { get; init; }
    private IPluginLog PluginLog { get; init; }

    public GeneratorsTab(HashSet<Macro> cachedMacros, Config config, Debouncers debouncers, FileDialogManager fileDialogManager, MacroTableQueue macroTableQueue, IPluginLog pluginLog) : base(debouncers)
    {
        CachedMacros = cachedMacros;
        Config = config;
        FileDialogManager = fileDialogManager;
        MacroTableQueue = macroTableQueue;
        PluginLog = pluginLog;

        GeneratorConfigToState = Config.GeneratorConfigs.ToDictionary(c => c, c => new GeneratorConfigState());
    }

    public void Draw()
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
            ImGui.SetCursorPosX(20);

            var name = generatorConfig.Name;
            var nameInputId = $"generatorConfigs{hash}Name";
            if (ImGui.InputText($"Name###{nameInputId}", ref name, ushort.MaxValue))
            {
                generatorConfig.Name = name;
                Debounce(nameInputId, Config.Save);
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
            if (ImGui.CollapsingHeader($"IPCs###generatorConfigs{hash}IpcConfigs", ImGuiTreeNodeFlags.DefaultOpen))
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

                        if (ImGui.BeginTabItem($"#{i}###generatorConfigs{hash}IpcConfigs{i}Tab"))
                        {
                            ImGui.SetCursorPosX(20);
                            ImGui.PushItemWidth(500);

                            var ipcNamesForCombo = ipcOrdered.Select(g => g.Name).Prepend(string.Empty);
                            var ipcIndexForCombo = ipcNamesForCombo.IndexOf(ipcConfig.Name);
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

                                        ImGui.SetCursorPosX(20);
                                        ImGui.PushItemWidth(500);
                                        var ipcArgs = ipcConfig.Args;
                                        var ipcArgsInputId = $"generatorConfigs{hash}IpcConfigs{i}Args";
                                        if (ImGui.InputText($"Args###{ipcArgsInputId}", ref ipcArgs, ushort.MaxValue))
                                        {
                                            ipcConfig.Args = ipcArgs;
                                            Debounce(ipcArgsInputId, Config.Save);
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
            var scriptInputId = $"generatorConfigs{hash}Script";
            if (ImGui.InputTextMultiline($"Script (js)###{scriptInputId}", ref script, ushort.MaxValue, new(ImGui.GetWindowWidth() - 100, scriptInputHeight)))
            {
                generatorConfig.Script = script;
                Debounce(scriptInputId, Config.Save);
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
                    ImGui.TableSetupColumn($"Name###generatorConfigs{hash}GeneratedMacrosNameColumn", ImGuiTableColumnFlags.None, 0.2f);
                    ImGui.TableSetupColumn($"Path###generatorConfigs{hash}GeneratedMacrosPathColumn", ImGuiTableColumnFlags.None, 0.2f);
                    ImGui.TableSetupColumn($"Tags###generatorConfigs{hash}GeneratedMacrosTagsColumn", ImGuiTableColumnFlags.None, 0.1f);
                    ImGui.TableSetupColumn($"Command###generatorConfigs{hash}GeneratedMacrosCommandColumn", ImGuiTableColumnFlags.None, 0.1f);
                    ImGui.TableSetupColumn($"Args###generatorConfigs{hash}GeneratedMacrosArgsColumn", ImGuiTableColumnFlags.None, 0.1f);
                    ImGui.TableSetupColumn($"Content###generatorConfigs{hash}GeneratedMacrosContentColumn", ImGuiTableColumnFlags.None, 0.5f);
                    ImGui.TableHeadersRow();

                    var visibleFilteredGeneratedMacros = showOnlySelected ? filteredGeneratedMacros.Intersect(selectedGeneratedMacros, MacroComparer.INSTANCE) : filteredGeneratedMacros;

                    var clipper = ImGuiHelper.NewListClipper();
                    clipper.Begin(visibleFilteredGeneratedMacros.Count(), 27);

                    while (clipper.Step())
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
        var generatorConfig = new GeneratorConfig();
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

        MacroTableQueue.Delete(conflictingMacros);
        MacroTableQueue.Insert(selectedGeneratedMacros);

        state.GeneratedMacros.ExceptWith(selectedGeneratedMacros);
        state.FilteredGeneratedMacros.ExceptWith(selectedGeneratedMacros);
        selectedGeneratedMacros.Clear();
    }
}
