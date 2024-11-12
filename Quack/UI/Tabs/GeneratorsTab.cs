using Dalamud.Interface.Colors;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Plugin.Ipc.Internal;
using Dalamud.Utility;
using Dalamud;
using ImGuiNET;
using Quack.Generators;
using Quack.Macros;
using Quack.Utils;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Linq;
using System;
using System.Numerics;
using JavaScriptEngineSwitcher.Core;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Quack.UI.Helpers;
using Quack.UI.States;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Game.ClientState.Keys;

namespace Quack.UI.Tabs;

public class GeneratorsTab : ModelTab
{
    private HashSet<Macro> CachedMacros { get; init; }
    private IJsEngine? CurrentJsEngine { get; set; }
    private Config Config { get; init; }
    private Dictionary<GeneratorConfig, GeneratorConfigState> GeneratorConfigToState { get; set; }
    private GeneratorException? GeneratorException { get; set; } = null;
    private IKeyState KeyState { get; init; }
    private MacroTableQueue MacroTableQueue { get; init; }
    private IPluginLog PluginLog { get; init; }

    public GeneratorsTab(HashSet<Macro> cachedMacros, Config config, Debouncers debouncers, FileDialogManager fileDialogManager, IKeyState keyState, MacroTableQueue macroTableQueue, IPluginLog pluginLog) : base(debouncers, fileDialogManager)
    {
        CachedMacros = cachedMacros;
        Config = config;
        KeyState = keyState;
        MacroTableQueue = macroTableQueue;
        PluginLog = pluginLog;

        GeneratorConfigToState = Config.GeneratorConfigs.ToDictionary(c => c, c => new GeneratorConfigState());
    }

    public void Draw()
    {
        if (GeneratorException != null)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudRed))
            {
                ImGui.Text(GeneratorException.ToString());
            }
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
        ImGui.Button("Export All##generatorConfigsExportAll");
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Right-click for clipboard base64 export");
        }
        if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
        {
            ExportToFile(Config.GeneratorConfigs, "Export Generators", "generators.json");
        }
        else if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            ExportToClipboard(Config.GeneratorConfigs);
        }

        ImGui.SameLine();
        ImGui.Button("Import All##generatorConfigsImportAll");
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Right-click for clipboard base64 import");
        }
        if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
        {
            WithFileContent(ImportGeneratorConfigsFromJson, "Import Generators");
        }
        else if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            WithDecodedClipboardContent(ImportGeneratorConfigsFromJson);
        }

        ImGui.SameLine();
        if (ImGui.Button($"Recreate Defaults (V{GeneratorConfig.DEFAULTS_VERSION})##generatorConfigsRecreateDefaults"))
        {
            RecreateDefaultGeneratorConfigs();
        }

        ImGui.SameLine();
        using (ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.DalamudRed))
        {
            var deleteAllPressed = ImGui.Button("Delete All##generatorConfigsDeleteAll");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Press <CTRL> while clicking to confirm deleting all generators");
            }
            if (deleteAllPressed && KeyState[VirtualKey.CONTROL])
            {
                DeleteGeneratorConfigs();
            }
        }

        var generatorConfigs = Config.GeneratorConfigs;
        var funcChannels = Service<CallGate>.Get().Gates.Values.Where(g => g.Func != null);
        using (ImRaii.TabBar("generatorConfigsTabs", ImGuiTabBarFlags.AutoSelectNewTabs | ImGuiTabBarFlags.TabListPopupButton | ImGuiTabBarFlags.FittingPolicyScroll))
        {
            foreach (var generatorConfig in generatorConfigs)
            {
                var hash = generatorConfig.GetHashCode();
                using (var tab = ImRaii.TabItem($"{(generatorConfig.Name.IsNullOrWhitespace() ? BLANK_NAME : generatorConfig.Name)}###generatorConfigs{hash}"))
                {
                    if (tab.Success)
                    {
                        ImGui.NewLine();
                        DrawDefinitionHeader(generatorConfig, funcChannels);
                        DrawOutputHeader(generatorConfig);
                    }
                }
            }
        }    
    }

    private void ImportGeneratorConfigsFromJson(string json)
    {
        var generatorConfigs = JsonConvert.DeserializeObject<List<GeneratorConfig>>(json)!;
        generatorConfigs.ForEach(g => GeneratorConfigToState.Add(g, new()));
        Config.GeneratorConfigs.AddRange(generatorConfigs);
        Config.Save();
    }

    private void DrawDefinitionHeader(GeneratorConfig generatorConfig, IEnumerable<CallGateChannel> funcChannels)
    {
        var hash = generatorConfig.GetHashCode();
        if (ImGui.CollapsingHeader($"Definition##generatorConfigs{hash}Definition", ImGuiTreeNodeFlags.DefaultOpen))
        {
            using (ImRaii.PushIndent())
            {
                var name = generatorConfig.Name;
                var nameInputId = $"generatorConfigs{hash}Name";
                if (ImGui.InputText($"Name###{nameInputId}", ref name, ushort.MaxValue))
                {
                    generatorConfig.Name = name;
                    Debounce(nameInputId, Config.Save);
                }

                ImGui.SameLine(ImGui.GetWindowWidth() - 115);
                ImGui.Button("Export##generatorConfigsExport");
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Right-click for clipboard base64 export");
                }
                if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                {
                    ExportToFile([generatorConfig], "Export Generator", $"{(generatorConfig.Name.IsNullOrWhitespace() ? "generator" : generatorConfig.Name)}.json");
                }
                else if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                {
                    ExportToClipboard([generatorConfig]);
                }

                ImGui.SameLine();
                using (ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.DalamudRed))
                {
                    if (ImGui.Button($"Delete###generatorConfigs{hash}Delete"))
                    {
                        DeleteGeneratorConfig(generatorConfig);
                    }
                }

                var ipcOrdered = funcChannels.OrderBy(g => g.Name);
                if (ImGui.CollapsingHeader($"IPCs###generatorConfigs{hash}IpcConfigs"))
                {
                    using (ImRaii.PushIndent())
                    {
                        if (ImGui.Button($"+###generatorConfigs{hash}IpcConfigsNew"))
                        {
                            generatorConfig.IpcConfigs.Add(new());
                            Config.Save();
                        }

                        ImGui.SameLine(70);
                        using (ImRaii.TabBar($"generatorConfigs{hash}IpcConfigsTabs", ImGuiTabBarFlags.AutoSelectNewTabs | ImGuiTabBarFlags.FittingPolicyScroll))
                        {
                            for (var i = 0; i < generatorConfig.IpcConfigs.Count; i++)
                            {
                                var ipcConfig = generatorConfig.IpcConfigs[i];

                                using (var tab = ImRaii.TabItem($"#{i}###generatorConfigs{hash}IpcConfigs{i}Tab"))
                                {
                                    if (tab.Success)
                                    {
                                        var ipcNamesForCombo = ipcOrdered.Select(g => g.Name).Prepend(string.Empty);
                                        var ipcIndexForCombo = ipcNamesForCombo.IndexOf(ipcConfig.Name);
                                        using (ImRaii.ItemWidth(500))
                                        {
                                            if (ImGui.Combo($"Name###generatorConfigs{hash}IpcConfigs{i}Name", ref ipcIndexForCombo, ipcNamesForCombo.ToArray(), ipcNamesForCombo.Count()))
                                            {
                                                ipcConfig.Name = ipcNamesForCombo.ElementAt(ipcIndexForCombo);
                                                Config.Save();
                                            }
                                        }
                                        
                                        ImGui.SameLine(600);
                                        using (ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.DalamudRed))
                                        {
                                            var deletePressed = ImGui.Button($"Delete###generatorConfigs{hash}IpcConfigs{i}Delete");
                                            if (ImGui.IsItemHovered())
                                            {
                                                ImGui.SetTooltip("Press <CTRL> while clicking to confirm ipc deletion");
                                            }
                                            if (deletePressed && KeyState[VirtualKey.CONTROL])
                                            {
                                                generatorConfig.IpcConfigs.RemoveAt(i);
                                                Config.Save();
                                            }
                                        }

                                        if (!ipcConfig.Name.IsNullOrWhitespace())
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
                                                    using (ImRaii.ItemWidth(500))
                                                    {
                                                        var ipcArgs = ipcConfig.Args;
                                                        var ipcArgsInputId = $"generatorConfigs{hash}IpcConfigs{i}Args";
                                                        if (ImGui.InputText($"Args###{ipcArgsInputId}", ref ipcArgs, ushort.MaxValue))
                                                        {
                                                            ipcConfig.Args = ipcArgs;
                                                            Debounce(ipcArgsInputId, Config.Save);
                                                        }
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudRed))
                                                {
                                                    ImGui.Text($"Could not retrieve signature for {ipcConfig.Name}");
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                var scriptInputHeight = GeneratorConfigToState[generatorConfig].GeneratedMacros.Any() ? ImGui.GetTextLineHeight() * 13 : ImGui.GetWindowHeight() - ImGui.GetCursorPosY() - 40;

                var script = generatorConfig.Script;
                var scriptInputId = $"generatorConfigs{hash}Script";
                if (ImGui.InputTextMultiline($"Script (js)###{scriptInputId}", ref script, ushort.MaxValue, new(ImGui.GetWindowWidth() - 100, scriptInputHeight)))
                {
                    generatorConfig.Script = script;
                    Debounce(scriptInputId, Config.Save);
                }

                if (CurrentJsEngine == null)
                {
                    if (ImGui.Button($"Execute###generatorConfigs{hash}GenerateMacros"))
                    {
                        GenerateMacros(generatorConfig);
                    }
                }
                else
                {
                    using (ImRaii.Color? _ = ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.DalamudOrange), __ = ImRaii.PushColor(ImGuiCol.Text, new Vector4(0, 0, 0, 1)))
                    {
                        if (ImGui.Button($"Cancel Execution###generatorConfigs{hash}GenerateMacrosCancel"))
                        {
                            CurrentJsEngine.Interrupt();
                        }
                    }
                }
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
                    using (var popup = ImRaii.Popup(conflictResolutionPopupId))
                    {
                        if (popup.Success)
                        {
                            ImGui.Text($"Override {filteredConflictingMacros.Count()} macros?");

                            ImGui.SetCursorPosX(15);
                            using (ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.DalamudRed))
                            {
                                if (ImGui.Button("Yes", new(100, 30)))
                                {
                                    SaveSelectedGeneratedMacros(state);
                                    ImGui.CloseCurrentPopup();
                                }
                            }

                            ImGui.SameLine();
                            if (ImGui.Button("Cancel", new(100, 30)))
                            {
                                ImGui.CloseCurrentPopup();
                            }
                        }
                    }
                }

                using (ImRaii.Color? _ = ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.HealerGreen), __ = ImRaii.PushColor(ImGuiCol.Text, new Vector4(0, 0, 0, 1)))
                {
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
                }

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
                using (ImRaii.ItemWidth(250))
                {
                    if (ImGui.InputText($"Filter ({filteredGeneratedMacros.Count}/{generatedMacros.Count})###generatorConfigs{hash}GeneratedMacrosFilter", ref generatedMacrosFilter, ushort.MaxValue))
                    {
                        state.GeneratedMacrosFilter = generatedMacrosFilter;
                        state.FilteredGeneratedMacros = new(MacroSearch.Lookup(generatedMacros, generatedMacrosFilter), MacroComparer.INSTANCE);
                    }
                }

                ImGui.SameLine();
                if (ImGui.Button($"X###generatorConfigs{hash}GeneratedMacrosClearFilter"))
                {
                    state.GeneratedMacrosFilter = string.Empty;
                    state.FilteredGeneratedMacros = generatedMacros;
                }

                ImGui.SameLine(ImGui.GetWindowWidth() - 232);
                var showSelectedOnly = state.ShowSelectedOnly;
                if (ImGui.Checkbox($"Show Selected Only###generatorConfigs{hash}GeneratedMacrosShowSelectedOnly", ref showSelectedOnly))
                {
                    state.ShowSelectedOnly = showSelectedOnly;
                }

                ImGui.SameLine();
                using (ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.DalamudRed))
                {
                    if (ImGui.Button($"Delete All###generatorConfigs{hash}GeneratedMacrosDeleteAll"))
                    {
                        generatedMacros.Clear();
                        selectedGeneratedMacros.Clear();
                    }
                }

                using (ImRaii.Table($"generatorConfigs{hash}GeneratedMacrosTable", 7, ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Resizable))
                {
                    ImGui.TableSetupColumn(string.Empty, ImGuiTableColumnFlags.None, 0.05f);
                    ImGui.TableSetupColumn($"Name###generatorConfigs{hash}GeneratedMacrosNameColumn", ImGuiTableColumnFlags.None, 0.2f);
                    ImGui.TableSetupColumn($"Path###generatorConfigs{hash}GeneratedMacrosPathColumn", ImGuiTableColumnFlags.None, 0.2f);
                    ImGui.TableSetupColumn($"Tags###generatorConfigs{hash}GeneratedMacrosTagsColumn", ImGuiTableColumnFlags.None, 0.1f);
                    ImGui.TableSetupColumn($"Command###generatorConfigs{hash}GeneratedMacrosCommandColumn", ImGuiTableColumnFlags.None, 0.1f);
                    ImGui.TableSetupColumn($"Args###generatorConfigs{hash}GeneratedMacrosArgsColumn", ImGuiTableColumnFlags.None, 0.1f);
                    ImGui.TableSetupColumn($"Content###generatorConfigs{hash}GeneratedMacrosContentColumn", ImGuiTableColumnFlags.None, 0.5f);
                    ImGui.TableHeadersRow();

                    var visibleFilteredGeneratedMacros = showSelectedOnly ? filteredGeneratedMacros.Intersect(selectedGeneratedMacros, MacroComparer.INSTANCE) : filteredGeneratedMacros;

                    var clipper = ListClipperHelper.Build();
                    clipper.Begin(visibleFilteredGeneratedMacros.Count(), 27);

                    var updatedSelectedGeneratedMacros = selectedGeneratedMacros.ToHashSet(MacroComparer.INSTANCE);
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
                                        updatedSelectedGeneratedMacros.Add(generatedMacro);
                                    }
                                    else
                                    {
                                        updatedSelectedGeneratedMacros.Remove(generatedMacro);
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
                    state.SelectedGeneratedMacros = updatedSelectedGeneratedMacros;
                    clipper.Destroy();
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
