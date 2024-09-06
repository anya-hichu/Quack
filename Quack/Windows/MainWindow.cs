using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ImGuiNET;
using Newtonsoft.Json;
using Quack.Macros;

namespace Quack.Windows;

public class MainWindow : Window, IDisposable
{
    private Executor Executor { get; init; }
    private Config Config { get; init; }
    private IPluginLog PluginLog { get; init; }

    private FileDialogManager FileDialogManager { get; init; }

    private string Filter { get; set; } = string.Empty;
    private IEnumerable<Macro> FilteredMacros { get; set; } = [];

    public MainWindow(Executor executor, Config config, IPluginLog pluginLog) : base("Quack##mainWindow")
    {
        Executor = executor;
        Config = config;
        PluginLog = pluginLog;

        FileDialogManager = new();
        UpdateFilteredMacros();
    }

    public void UpdateFilteredMacros()
    {
        FilteredMacros = Search.Lookup(Config.Macros, Filter);
    }

    public void Dispose() { }

    public override void Draw()
    {
        var filter = Filter;
        ImGui.PushItemWidth(200);
        if (ImGui.InputText($"Filter ({FilteredMacros.Count()}/{Config.Macros.Count})###filter", ref filter, ushort.MaxValue))
        {
            Filter = filter;
            UpdateFilteredMacros();
        }

        ImGui.SameLine();
        if (ImGui.Button("X##filterClear"))
        {
            Filter = string.Empty;
        }

        ImGui.SameLine(ImGui.GetWindowWidth() - 190);
        if (ImGui.Button("Export##macrosExport"))
        {
            ExportMacros();
        }

        ImGui.SameLine();
        if (ImGui.Button("Import##macrosImport"))
        {
            ImportMacros();
        }

        ImGui.SameLine();
        if (ImGui.Button("Delete All##macrosDeleteAll"))
        {
            Config.Macros.Clear();
            Config.Save();
        }
        
        if (ImGui.BeginTable("macros", 4, ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
        {
            ImGui.TableSetupColumn($"Name##macroName", ImGuiTableColumnFlags.None, 3);
            ImGui.TableSetupColumn($"Path##macroPath", ImGuiTableColumnFlags.None, 2);
            ImGui.TableSetupColumn($"Tags##macroTags", ImGuiTableColumnFlags.None, 1);
            ImGui.TableSetupColumn($"Actions##macroActions", ImGuiTableColumnFlags.None, 2);
            ImGui.TableHeadersRow();
            
            for (var i = 0; i < FilteredMacros.Take(Config.MaxSearchResults).Count(); i++)
            {
                var macro = FilteredMacros.ElementAt(i);
                if (ImGui.TableNextColumn())
                {
                    ImGui.Text(macro.Name);
                }

                if (ImGui.TableNextColumn())
                {
                    ImGui.Text(macro.Path);
                }

                if (ImGui.TableNextColumn())
                {
                    ImGui.Text(string.Join(", ", macro.Tags));
                }

                if (ImGui.TableNextColumn())
                {
                    if (ImGui.Button($"Execute###macros{i}Execute"))
                    {
                        Executor.RunAsync(macro);
                    }

                    ImGui.SameLine();
                    if (ImGui.Button($"+ Format###macros{i}ExecuteWithFormatting"))
                    {
                        Executor.RunAsync(macro, Config.CommandFormat);
                    }

                    // TODO REMOVE AND HAVE PROPER CONFIG EDITOR FOR MACROS in dedicated tab with a folder like structure on right like penumbra
                    ImGui.SameLine();
                    if (ImGui.Button($"Delete###macros{i}Delete"))
                    {
                        Config.Macros.Remove(macro);
                        Config.Save();
                    }
                }

                ImGui.TableNextRow();
            }
            ImGui.EndTable();
        }

        FileDialogManager.Draw();
    }


    private void ExportMacros()
    {
        FileDialogManager.SaveFileDialog("Export Macros", ".*", "macros.json", ".json", (valid, path) =>
        {
            if (valid)
            {
                using var file = File.CreateText(path);
                new JsonSerializer().Serialize(file, Config.Macros);
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
                Config.Macros.UnionWith(importedMacros);
                Config.Save();
            }
        });
    }
}
