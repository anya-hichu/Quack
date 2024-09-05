using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using ImGuiNET;
using Newtonsoft.Json;
using Quack.Generators;
using Quack.Utils;

namespace Quack.Windows;

public class MainWindow : Window, IDisposable
{
    private Config Config { get; init; }
    private ServerChat ServerChat { get; init; }
    private IPluginLog PluginLog { get; init; }

    private FileDialogManager FileDialogManager { get; init; }

    private string Filter { get; set; } = string.Empty;

    public MainWindow(Config config, ServerChat serverChat, IPluginLog pluginLog) : base("Quack##mainWindow")
    {
        Config = config;
        ServerChat = serverChat;
        PluginLog = pluginLog;

        FileDialogManager = new();
    }

    public void Dispose() { }

    public override void Draw()
    {
        var listedMacros = Macro.FilterAndSort(Config.Macros, Filter).Take(Config.MaxSearchResults);

        var filter = Filter;
        ImGui.PushItemWidth(200);
        if (ImGui.InputText($"Filter ({listedMacros.Count()}/{Config.Macros.Count})###filter", ref filter, ushort.MaxValue))
        {
            Filter = filter;
            PluginLog.Debug($"Search filter changed to {filter}");
        }

        ImGui.SameLine();
        if (ImGui.Button("X##filterClear"))
        {
            Filter = string.Empty;
        }

        ImGui.SameLine(ImGui.GetWindowWidth() - 180);
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
        
        if (ImGui.BeginTable("macros", 3, ImGuiTableFlags.RowBg))
        {
            ImGui.TableSetupColumn($"Name##macroName", ImGuiTableColumnFlags.None, 0.3f);
            ImGui.TableSetupColumn($"Path##macroPath", ImGuiTableColumnFlags.None, 0.3f);
            ImGui.TableSetupColumn($"Actions##macroActions", ImGuiTableColumnFlags.None, 0.4f);
            ImGui.TableHeadersRow();
            
            for (var i = 0; i < listedMacros.Count(); i++)
            {
                var macro = listedMacros.ElementAt(i);
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
                    if (ImGui.Button($"Execute###macros{i}Execute"))
                    {
                        Execute(macro, false);
                    }

                    ImGui.SameLine();
                    if (ImGui.Button($"+ Format###macros{i}ExecuteWithFormatting"))
                    {
                        Execute(macro, true);
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
                Config.Macros.AddRange(importedMacros);
                Config.Save();
            }
        });
    }

    private void Execute(Macro macro, bool formatted)
    {
        PluginLog.Debug($"Executing macro {macro.Name} ({macro.Path}) with content: {macro.Content}");
        foreach(var command in macro.Content.Split("\n"))
        {
            if (!command.IsNullOrWhitespace())
            {
                if (formatted)
                {
                    ServerChat.SendMessage(string.Format(new PMFormatter(), Config.CommandFormat, command));
                }
                else
                {
                    ServerChat.SendMessage(command);
                }
            }
        }
    }
}
