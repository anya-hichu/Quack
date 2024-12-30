using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface.Colors;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Ipc.Internal;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using ImGuiNET;
using JavaScriptEngineSwitcher.Core;
using Newtonsoft.Json;
using Quack.Configs;
using Quack.Exports;
using Quack.Macros;
using Quack.Schedulers;
using Quack.UI;
using Quack.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Quack.Generators;

public class GeneratorConfigTab : ConfigEntityTab
{
    private HashSet<Macro> CachedMacros { get; init; }
    private CallGate CallGate { get; init; }
    private Config Config { get; init; }
    private Dictionary<GeneratorConfig, GeneratorConfigTabState> GeneratorConfigToState { get; set; } = [];
    private IKeyState KeyState { get; init; }
    private MacroTableQueue MacroTableQueue { get; init; }
    private IPluginLog PluginLog { get; init; }

    public GeneratorConfigTab(HashSet<Macro> cachedMacros, CallGate callGate, Config config, Debouncers debouncers, FileDialogManager fileDialogManager, 
        IKeyState keyState, MacroTableQueue macroTableQueue, IPluginLog pluginLog, INotificationManager notificationManager) : base(debouncers, fileDialogManager, notificationManager)
    {
        CachedMacros = cachedMacros;
        CallGate = callGate;
        Config = config;
        KeyState = keyState;
        MacroTableQueue = macroTableQueue;
        PluginLog = pluginLog;
        AddDefaultStates(Config.GeneratorConfigs);
    }

    public void Draw()
    {
        if (ImGui.Button("New###generatorConfigsNew"))
        {
            NewGeneratorConfig();
        }

        ImGui.SameLine(ImGui.GetWindowWidth() - 360);
        ImGui.Button("Export All###generatorConfigsExportAll");
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(EXPORT_HINT);
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
        ImGui.Button("Import All###generatorConfigsImportAll");
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(IMPORT_HINT);
        }
        if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
        {
            ImportFromFile(ProcessExportJson, "Import Generators");
        }
        else if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            ImportFromClipboard(ProcessExportJson);
        }

        ImGui.SameLine();
        if (ImGui.Button($"Recreate Defaults (V{GeneratorConfig.DEFAULTS_VERSION})###generatorConfigsRecreateDefaults"))
        {
            RecreateDefaultGeneratorConfigs();
        }

        ImGui.SameLine();
        using (ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.DalamudRed))
        {
            if (ImGui.Button("Delete All###generatorConfigsDeleteAll") && KeyState[VirtualKey.CONTROL])
            {
                DeleteGeneratorConfigs();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(CONFIRM_DELETE_HINT);
            }
        }

        var generatorConfigs = Config.GeneratorConfigs;
        var funcChannels = CallGate.Gates.Values.Where(g => g.Func != null);

        var generatorConfigsId = "generatorConfigs";
        using (ImRaii.TabBar($"{generatorConfigsId}{string.Join("-", generatorConfigs.Select(c => c.GetHashCode()))}Tabs", ImGuiTabBarFlags.AutoSelectNewTabs | ImGuiTabBarFlags.TabListPopupButton | ImGuiTabBarFlags.FittingPolicyScroll))
        {
            for (var i = 0; i < generatorConfigs.Count; i++)
            {
                var generatorConfig = generatorConfigs.ElementAt(i);
                var generatorConfigId = $"{generatorConfigsId}{generatorConfig.GetHashCode()}";
                using (var tab = ImRaii.TabItem($"{(generatorConfig.Name.IsNullOrWhitespace() ? BLANK_NAME : generatorConfig.Name)}###{generatorConfigId}Tab"))
                {
                    MoveTabPopup($"{generatorConfigId}Popup", generatorConfigs, i, Config.Save);

                    if (tab)
                    {
                        ImGui.NewLine();
                        DrawDefinitionHeader(generatorConfig, funcChannels);
                        DrawOutputHeader(generatorConfig);
                    }
                }
            }
        }
    }

    private List<GeneratorConfig>? ProcessExportJson(string exportJson)
    {
        var export = JsonConvert.DeserializeObject<Export<GeneratorConfig>>(exportJson);
        if (export == null || export.Type != typeof(GeneratorConfig).Name)
        {
            PluginLog.Verbose($"Failed to import generator config from json: {exportJson}");
            return null;
        }
        var generatorConfigs = export.Entities.ToList();
        AddDefaultStates(generatorConfigs);
        Config.GeneratorConfigs.AddRange(generatorConfigs);
        Config.Save();

        return generatorConfigs;
    }

    private void DrawDefinitionHeader(GeneratorConfig generatorConfig, IEnumerable<CallGateChannel> funcChannels)
    {
        var state = GeneratorConfigToState[generatorConfig];
        var generatorConfigId = $"generatorConfigs{generatorConfig.GetHashCode()}";

        var generateMacrosId = $"{generatorConfigId}GenerateMacros";
        var maybeGeneratorException = state.MaybeGeneratorException;
        if (maybeGeneratorException != null)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudRed))
            {
                ImGui.Text(maybeGeneratorException.ToString());
            }
            ImGui.SameLine();
            if (ImGui.Button($"x###{generateMacrosId}ExceptionClear"))
            {
                state.MaybeGeneratorException = null;
            }
        }

        if (ImGui.CollapsingHeader($"Definition###{generatorConfigId}DefinitionHeader", ImGuiTreeNodeFlags.DefaultOpen))
        {
            using (ImRaii.PushIndent())
            {
                var name = generatorConfig.Name;
                var nameInputId = $"{generatorConfigId}Name";
                if (ImGui.InputText($"Name###{nameInputId}", ref name, ushort.MaxValue))
                {
                    generatorConfig.Name = name;
                    Debounce(nameInputId, Config.Save);
                }

                ImGui.SameLine(ImGui.GetWindowWidth() - 180);
                if (ImGui.Button($"Duplicate###{generatorConfigId}Duplicate"))
                {
                    DuplicateGeneratorConfig(generatorConfig);
                }

                ImGui.SameLine();
                ImGui.Button($"Export###{generatorConfigId}Export");
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(EXPORT_HINT);
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
                    if (ImGui.Button($"Delete###{generatorConfigId}Delete") && KeyState[VirtualKey.CONTROL])
                    {
                        DeleteGeneratorConfig(generatorConfig);
                    }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(CONFIRM_DELETE_HINT);
                    }
                }

                var awaitDebugger = generatorConfig.AwaitDebugger;
                if (ImGui.Checkbox($"Await Debugger###{generatorConfigId}AwaitDebugger", ref awaitDebugger))
                {
                    generatorConfig.AwaitDebugger = awaitDebugger;
                    Config.Save();
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip($"Attach chrome debugger by accessing 'chrome://inspect' url");
                }

                var ipcConfigsId = $"{generatorConfigId}IpcConfigsHeader";
                if (ImGui.CollapsingHeader($"IPCs###{ipcConfigsId}", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    using (ImRaii.PushIndent())
                    {
                        
                        if (ImGui.Button($"+###{ipcConfigsId}New"))
                        {
                            generatorConfig.IpcConfigs.Add(new());
                            Config.Save();
                        }

                        ImGui.SameLine(70);
                        using (ImRaii.TabBar($"{ipcConfigsId}Tabs", ImGuiTabBarFlags.AutoSelectNewTabs | ImGuiTabBarFlags.FittingPolicyScroll))
                        {
                            var ipcOrdered = funcChannels.OrderBy(g => g.Name);
                            var ipcNamesForCombo = ipcOrdered.Select(g => g.Name).Prepend(string.Empty);

                            var ipcConfigs = generatorConfig.IpcConfigs;
                            for (var i = 0; i < ipcConfigs.Count; i++)
                            {
                                var ipcConfig = ipcConfigs.ElementAt(i);
                                var IpcName = ipcConfig.Name;

                                var ipcConfigId = $"{ipcConfigsId}{i}";
                                using (var tab = ImRaii.TabItem($"{(IpcName.IsNullOrWhitespace() ? $"#{i}" : IpcName)}###{ipcConfigId}Tab"))
                                {
                                    MoveTabPopup($"{ipcConfigId}Popup", ipcConfigs, i, Config.Save);

                                    if (!tab)
                                    {
                                        continue;
                                    }
                                    
                                    var ipcIndexForCombo = ipcNamesForCombo.IndexOf(IpcName);
                                    using (ImRaii.ItemWidth(500))
                                    {
                                        if (ImGui.Combo($"Name###{ipcConfigId}Name", ref ipcIndexForCombo, ipcNamesForCombo.ToArray(), ipcNamesForCombo.Count()))
                                        {
                                            ipcConfig.Name = ipcNamesForCombo.ElementAt(ipcIndexForCombo);
                                            Config.Save();
                                        }
                                    }

                                    ImGui.SameLine(600);
                                    using (ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.DalamudRed))
                                    {
                                        if (ImGui.Button($"Delete###{ipcConfigId}Delete") && KeyState[VirtualKey.CONTROL])
                                        {
                                            ipcConfigs.RemoveAt(i);
                                            Config.Save();
                                        }
                                        if (ImGui.IsItemHovered())
                                        {
                                            ImGui.SetTooltip(CONFIRM_DELETE_HINT);
                                        }
                                    }

                                    if (ipcConfig.Name.IsNullOrWhitespace())
                                    {
                                        continue;
                                    }
                                    
                                    if (ipcIndexForCombo > 0)
                                    {
                                        var channel = ipcOrdered.ElementAt(ipcIndexForCombo - 1);
                                        var genericTypes = channel.Func!.GetType().GenericTypeArguments;
                                        ImGui.Text($"Detected Signature: Out={genericTypes.Last().Name}");

                                        if (genericTypes.Length == 1)
                                        {
                                            continue;
                                        }

                                        ImGui.SameLine();
                                        ImGui.Text($"In=[{string.Join(", ", genericTypes.Take(genericTypes.Length - 1).Select(a => a.Name))}]");
                                        using (ImRaii.ItemWidth(500))
                                        {
                                            var ipcArgs = ipcConfig.Args;
                                            var ipcArgsInputId = $"{ipcConfigId}Args";
                                            if (ImGui.InputText($"Args (JSON)###{ipcArgsInputId}", ref ipcArgs, ushort.MaxValue))
                                            {
                                                ipcConfig.Args = ipcArgs;
                                                Debounce(ipcArgsInputId, Config.Save);
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

                var script = generatorConfig.Script;
                var scriptInputId = $"{generatorConfigId}Script";
                if (ImGui.InputTextMultiline($"Script (JS)###{scriptInputId}", ref script, ushort.MaxValue, new(ImGui.GetWindowWidth() - 100, state.GeneratedMacros.Count > 0 ? ImGui.GetTextLineHeight() * 13 : ImGui.GetWindowHeight() - ImGui.GetCursorPosY() - 40)))
                {
                    generatorConfig.Script = script;
                    Debounce(scriptInputId, Config.Save);
                }

                var generator = state.Generator;
                if (generator.IsStopped())
                {
                    if (ImGui.Button($"Execute###{generateMacrosId}"))
                    {
                        GenerateMacros(generatorConfig);
                    }
                }
                else
                {
                    using (ImRaii.Color? _ = ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.DalamudOrange), __ = ImRaii.PushColor(ImGuiCol.Text, new Vector4(0, 0, 0, 1)))
                    {
                        if (ImGui.Button($"Cancel###{generateMacrosId}Cancel"))
                        {
                            generator.Cancel();
                        }
                    }
                }
            }
        }
    }

    private void AddDefaultStates(List<GeneratorConfig> generatorConfigs)
    {
        generatorConfigs.ForEach(generatorConfig => GeneratorConfigToState.Add(generatorConfig, new(CallGate, generatorConfig, PluginLog)));
    }

    private void DrawOutputHeader(GeneratorConfig generatorConfig)
    {
        var hash = generatorConfig.GetHashCode();
        var state = GeneratorConfigToState[generatorConfig];
        if (state.GeneratedMacros.Count > 0)
        {
            var generatedMacrosId = $"generatorConfigs{hash}GeneratedMacros";
            if (ImGui.CollapsingHeader($"Output###{generatedMacrosId}Header", ImGuiTreeNodeFlags.DefaultOpen))
            {
                var filteredConflictingMacros = CachedMacros.Intersect(state.SelectedGeneratedMacros, MacroComparer.INSTANCE);
                var generatedMacrosConflictsPopupId = $"###{generatedMacrosId}ConflictsPopup";
                if (filteredConflictingMacros.Any())
                {
                    using (var popup = ImRaii.Popup(generatedMacrosConflictsPopupId))
                    {
                        if (popup)
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
                    if (ImGui.Button($"Save Selected###{generatedMacrosId}SaveSelected"))
                    {
                        if (filteredConflictingMacros.Any())
                        {
                            ImGui.OpenPopup(generatedMacrosConflictsPopupId);
                        }
                        else
                        {
                            SaveSelectedGeneratedMacros(state);
                        }
                    }
                }

                ImGui.SameLine();
                if (ImGui.Button($"Invert Selection###{generatedMacrosId}InvertSelection"))
                {
                    state.SelectedGeneratedMacros = new(state.GeneratedMacros.Except(state.SelectedGeneratedMacros, MacroComparer.INSTANCE), MacroComparer.INSTANCE);
                }

                ImGui.SameLine();
                if (ImGui.Button($"Select All Filtered###{generatedMacrosId}SelectAllFiltered"))
                {
                    state.SelectedGeneratedMacros = state.FilteredGeneratedMacros;
                }

                ImGui.SameLine();
                if (ImGui.Button($"Select All Conflicting###{generatedMacrosId}SelectAllConflicting"))
                {
                    state.SelectedGeneratedMacros = new(CachedMacros.Intersect(state.GeneratedMacros, MacroComparer.INSTANCE), MacroComparer.INSTANCE);
                }

                ImGui.SameLine();
                using (ImRaii.ItemWidth(250))
                {
                    var generatedMacrosFilter = state.GeneratedMacrosFilter;
                    if (ImGui.InputText($"Filter ({state.FilteredGeneratedMacros.Count}/{state.GeneratedMacros.Count})###{generatedMacrosId}Filter", ref generatedMacrosFilter, ushort.MaxValue))
                    {
                        state.GeneratedMacrosFilter = generatedMacrosFilter;
                        state.FilteredGeneratedMacros = new(MacroSearch.Lookup(state.GeneratedMacros, generatedMacrosFilter), MacroComparer.INSTANCE);
                    }
                }

                ImGui.SameLine();
                if (ImGui.Button($"x###{generatedMacrosId}FilterClear"))
                {
                    state.GeneratedMacrosFilter = string.Empty;
                    state.FilteredGeneratedMacros = state.GeneratedMacros;
                }

                ImGui.SameLine(ImGui.GetWindowWidth() - 232);
                var showSelectedOnly = state.ShowSelectedOnly;
                if (ImGui.Checkbox($"Show Selected Only###{generatedMacrosId}ShowSelectedOnly", ref showSelectedOnly))
                {
                    state.ShowSelectedOnly = showSelectedOnly;
                }

                ImGui.SameLine();
                using (ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.DalamudRed))
                {
                    if (ImGui.Button($"Delete All###{generatedMacrosId}DeleteAll"))
                    {
                        state.GeneratedMacros.Clear();
                        state.SelectedGeneratedMacros.Clear();
                        state.FilteredGeneratedMacros.Clear();
                    }
                }

                if (ImGui.GetCursorPosY() < ImGui.GetWindowHeight())
                {
                    var generatedMacrosTableId = $"{generatedMacrosId}Table";
                    using (ImRaii.Table(generatedMacrosTableId, 7, ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Resizable))
                    {
                        ImGui.TableSetupColumn(string.Empty, ImGuiTableColumnFlags.None, 0.05f);
                        ImGui.TableSetupColumn($"Name###{generatedMacrosTableId}Name", ImGuiTableColumnFlags.None, 0.2f);
                        ImGui.TableSetupColumn($"Path###{generatedMacrosTableId}Path", ImGuiTableColumnFlags.None, 0.2f);
                        ImGui.TableSetupColumn($"Tags###{generatedMacrosTableId}Tags", ImGuiTableColumnFlags.None, 0.1f);
                        ImGui.TableSetupColumn($"Command###{generatedMacrosTableId}Command", ImGuiTableColumnFlags.None, 0.1f);
                        ImGui.TableSetupColumn($"Args###{generatedMacrosTableId}Args", ImGuiTableColumnFlags.None, 0.1f);
                        ImGui.TableSetupColumn($"Content###{generatedMacrosTableId}Content", ImGuiTableColumnFlags.None, 0.5f);
                        ImGui.TableHeadersRow();

                        var visibleFilteredGeneratedMacros = showSelectedOnly ? state.FilteredGeneratedMacros.Intersect(state.SelectedGeneratedMacros, MacroComparer.INSTANCE) : state.FilteredGeneratedMacros;

                        var clipper = UIListClipper.Build();
                        clipper.Begin(visibleFilteredGeneratedMacros.Count(), 27);

                        var updatedSelectedGeneratedMacros = state.SelectedGeneratedMacros.ToHashSet(MacroComparer.INSTANCE);
                        while (clipper.Step())
                        {
                            for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                            {
                                var generatedMacro = visibleFilteredGeneratedMacros.ElementAt(i);
                                if (ImGui.TableNextColumn())
                                {
                                    var selected = state.SelectedGeneratedMacros.Contains(generatedMacro);
                                    if (ImGui.Checkbox($"###{generatedMacrosTableId}Entries{i}Selected", ref selected))
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
    }

    private void NewGeneratorConfig()
    {
        var generatorConfig = new GeneratorConfig();
        Config.GeneratorConfigs.Add(generatorConfig);
        AddDefaultStates([generatorConfig]);
        Config.Save();
    }

    private void RecreateDefaultGeneratorConfigs()
    {
        var defaults = GeneratorConfig.GetDefaults();
        Config.GeneratorConfigs.AddRange(defaults);
        AddDefaultStates(defaults);
        Config.Save();
    }

    private void DuplicateGeneratorConfig(GeneratorConfig generatorConfig)
    {
        var duplicate = generatorConfig.Clone();
        duplicate.Name = $"{generatorConfig.Name} (Copy)";
        AddDefaultStates([duplicate]);
        Config.GeneratorConfigs.Add(duplicate);
        Config.Save();
    }

    private void DeleteGeneratorConfigs()
    {
        Config.GeneratorConfigs.Clear();
        GeneratorConfigToState.Clear();
        Config.Save();
    }

    private void DeleteGeneratorConfig(GeneratorConfig generatorConfig)
    {
        Config.GeneratorConfigs.Remove(generatorConfig);
        GeneratorConfigToState.Remove(generatorConfig);
        Config.Save();
    }

    private void GenerateMacros(GeneratorConfig generatorConfig)
    {
        Task.Run(() =>
        {
            var state = GeneratorConfigToState[generatorConfig];
            try
            {
                var generatedMacros = state.Generator.GenerateMacros();
                state.MaybeGeneratorException = null;
                state.GeneratedMacros = generatedMacros;
                state.SelectedGeneratedMacros = new(generatedMacros, MacroComparer.INSTANCE);
                state.FilteredGeneratedMacros = new(MacroSearch.Lookup(generatedMacros, state.GeneratedMacrosFilter), MacroComparer.INSTANCE);
            }
            catch (GeneratorException e)
            {
                if (e.InnerException is JsInterruptedException)
                {
                    PluginLog.Error($"Cancelled macro generation manually");
                } 
                else
                {
                    state.MaybeGeneratorException = e;
                    PluginLog.Error($"Error occured when generating macros: {e}");
                } 
            }
        });

    }

    private void SaveSelectedGeneratedMacros(GeneratorConfigTabState state)
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
