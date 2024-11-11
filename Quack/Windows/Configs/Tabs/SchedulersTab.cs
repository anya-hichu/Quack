using Dalamud.Interface.Colors;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Utility;
using Humanizer;
using ImGuiNET;
using Newtonsoft.Json;
using Quack.Chat;
using Quack.Schedulers;
using Quack.Utils;
using Quack.Windows.Configs.States;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Quack.Windows.Configs.Tabs;

public class SchedulersTab : ModelTab
{
    private ChatSender ChatSender { get; init; }
    private Config Config { get; init; }
    private FileDialogManager FileDialogManager { get; init; }
    private Dictionary<SchedulerConfig, SchedulerConfigState> SchedulerConfigToState { get; set; }

    public SchedulersTab(ChatSender chatSender, Config config, Debouncers debouncers, FileDialogManager fileDialogManager) : base(debouncers)
    {
        ChatSender = chatSender;
        Config = config;
        FileDialogManager = fileDialogManager;
        SchedulerConfigToState = Config.SchedulerConfigs.ToDictionary(c => c, c => new SchedulerConfigState());
    }

    public void Draw()
    {
        var nowUtc = DateTime.UtcNow;

        if (ImGui.Button("New##schedulerConfigsNew"))
        {
            NewSchedulerConfig();
        }

        ImGui.SameLine(ImGui.GetWindowWidth() - 217);
        if (ImGui.Button("Export All##schedulerConfigsExportAll"))
        {
            ExportSchedulerConfigs(Config.SchedulerConfigs);
        }

        ImGui.SameLine();
        if (ImGui.Button("Import All##schedulerConfigsImportAll"))
        {
            ImportSchedulerConfigs();
        }

        var deleteAllSchedulerConfigsPopup = "deleteAllSchedulerConfigsPopup";
        if (ImGui.BeginPopup(deleteAllSchedulerConfigsPopup))
        {
            ImGui.Text($"Confirm deleting {Config.SchedulerConfigs.Count} schedulers?");

            ImGui.SetCursorPosX(15);
            ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.DalamudRed);
            if (ImGui.Button($"Yes##{deleteAllSchedulerConfigsPopup}Yes", new(100, 30)))
            {
                DeleteSchedulerConfigs();
                ImGui.CloseCurrentPopup();
            }
            ImGui.PopStyleColor();

            ImGui.SameLine();
            if (ImGui.Button($"No##{deleteAllSchedulerConfigsPopup}No", new(100, 30)))
            {
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }

        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.DalamudRed);
        if (ImGui.Button("Delete All##schedulerConfigsDeleteAll"))
        {
            if (Config.SchedulerConfigs.Count > 0)
            {
                ImGui.OpenPopup(deleteAllSchedulerConfigsPopup);
            }
        }
        ImGui.PopStyleColor();

        foreach (var schedulerConfig in Config.SchedulerConfigs)
        {
            var hash = schedulerConfig.GetHashCode();
            if (ImGui.BeginTabBar("schedulerConfigs", ImGuiTabBarFlags.AutoSelectNewTabs | ImGuiTabBarFlags.TabListPopupButton | ImGuiTabBarFlags.FittingPolicyScroll))
            {
                if (ImGui.BeginTabItem($"{(schedulerConfig.Name.IsNullOrWhitespace() ? BLANK_NAME : schedulerConfig.Name)}###schedulerConfigs{hash}"))
                {
                    ImGui.NewLine();
                    DrawDefinitionHeader(schedulerConfig, nowUtc);
                    DrawOutputHeader(schedulerConfig, nowUtc);
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }
        }
    }

    private void NewSchedulerConfig()
    {
        var schedulerConfig = new SchedulerConfig();
        Config.SchedulerConfigs.Add(schedulerConfig);
        SchedulerConfigToState.Add(schedulerConfig, new());
        Config.Save();
    }

    private void ExportSchedulerConfigs(IEnumerable<SchedulerConfig> schedulerConfig)
    {
        FileDialogManager.SaveFileDialog("Export Schedulers", ".*", "schedulers.json", ".json", (valid, path) =>
        {
            if (valid)
            {
                using var file = File.CreateText(path);
                new JsonSerializer().Serialize(file, schedulerConfig);
            }
        });
    }

    private void ImportSchedulerConfigs()
    {
        FileDialogManager.OpenFileDialog("Import Schedulers", "{.json}", (valid, path) =>
        {
            if (valid)
            {
                using StreamReader reader = new(path);
                var json = reader.ReadToEnd();
                var importedSchedulerConfigs = JsonConvert.DeserializeObject<List<SchedulerConfig>>(json)!;
                Config.SchedulerConfigs.AddRange(importedSchedulerConfigs);
                Config.Save();
            }
        });
    }

    private void DeleteSchedulerConfigs()
    {
        Config.SchedulerConfigs.Clear();
        SchedulerConfigToState.Clear();
        Config.Save();
    }

    private void DrawDefinitionHeader(SchedulerConfig schedulerConfig, DateTime nowUtc)
    {
        var hash = schedulerConfig.GetHashCode();

        ImGui.PushStyleVar(ImGuiStyleVar.IndentSpacing, 10);
        if (ImGui.CollapsingHeader($"Definition##schedulerConfigs{hash}Definition", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.Indent();
            var enabled = schedulerConfig.Enabled;
            var enabledInputId = $"schedulerConfigs{hash}Enabled";
            if(ImGui.Checkbox($"Enabled##{enabledInputId}", ref enabled))
            {
                schedulerConfig.Enabled = enabled;
                Debounce(enabledInputId, Config.Save);
            }

            ImGui.SameLine(ImGui.GetWindowWidth() - 115);
            if (ImGui.Button($"Export##schedulerConfigs{hash}Export"))
            {
                ExportSchedulerConfigs([schedulerConfig]);
            }
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.DalamudRed);
            if (ImGui.Button($"Delete###schedulerConfigs{hash}Delete"))
            {
                DeleteSchedulerConfig(schedulerConfig);
            }
            ImGui.PopStyleColor();

            var name = schedulerConfig.Name;
            var nameInputId = $"schedulerConfigs{hash}Name";
            if (ImGui.InputText($"Name###{nameInputId}", ref name, ushort.MaxValue))
            {
                schedulerConfig.Name = name;
                Debounce(nameInputId, Config.Save);
            }

            if (ImGui.CollapsingHeader($"Commands###schedulerConfigs{hash}SchedulerCommandConfigs", ImGuiTreeNodeFlags.DefaultOpen))
            {

                if (ImGui.Button($"+###schedulerConfigs{hash}SchedulerCommandConfigsNew"))
                {
                    schedulerConfig.SchedulerCommandConfigs.Add(new());
                    Config.Save();
                }

                ImGui.SameLine(40);

                if (ImGui.BeginTabBar($"schedulerConfigs{hash}SchedulerCommandConfigsTabs", ImGuiTabBarFlags.AutoSelectNewTabs | ImGuiTabBarFlags.FittingPolicyScroll))
                {
                    for (var i = 0; i < schedulerConfig.SchedulerCommandConfigs.Count; i++)
                    {
                        var schedulerCommandConfig = schedulerConfig.SchedulerCommandConfigs[i];

                        if (ImGui.BeginTabItem($"#{i}###schedulerConfigs{hash}SchedulerCommandConfigs{i}Tab"))
                        {
                            ImGui.Indent();
                            var timeExpression = schedulerCommandConfig.TimeExpression;
                            var timeExpressionInputId = $"schedulerConfigs{hash}SchedulerCommandConfigs{i}TimeExpression";
                            if (ImGui.InputText($"Time Expression (Cron)###{timeExpressionInputId}", ref timeExpression, ushort.MaxValue))
                            {
                                schedulerCommandConfig.TimeExpression = timeExpression;
                                Debounce(timeExpressionInputId, Config.Save);
                            }

                            ImGui.SameLine(ImGui.GetWindowWidth() - 60);
                            ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.DalamudRed);
                            if (ImGui.Button($"Delete###schedulerConfigs{hash}SchedulerCommandConfigs{i}Delete"))
                            {
                                schedulerConfig.SchedulerCommandConfigs.RemoveAt(i);
                                Config.Save();
                            }
                            ImGui.PopStyleColor();

                            var cronExpression = schedulerCommandConfig.ParseCronExpression();
                            if (cronExpression == null)
                            {
                                ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudRed);
                                ImGui.Text("Invalid Time Expression");
                                ImGui.PopStyleColor();
                            } 
                            else
                            {
                                ImGui.Text($"Interpolation: {cronExpression}");
                            }

                            var timeZones = TimeZoneInfo.GetSystemTimeZones();
                            var timeZoneNames = timeZones.Select(z => z.DisplayName).ToArray();
                            var timeZoneName = schedulerCommandConfig.TimeZone.DisplayName;
                            var timeZoneNameIndex = timeZoneNames.IndexOf(timeZoneName);
                            if (ImGui.Combo($"Time Zone###schedulerConfigs{hash}SchedulerCommandConfigs{i}TimeZone", ref timeZoneNameIndex, timeZoneNames, timeZoneNames.Length))
                            {
                                schedulerCommandConfig.TimeZone = timeZones.ElementAt(timeZoneNameIndex);
                                Config.Save();
                            }

                            var nextOccurrence = schedulerCommandConfig.GetNextOccurrence(nowUtc);
                            if (nextOccurrence.HasValue)
                            {
                                ImGui.Text($"Next Occurrence: {nextOccurrence.Value}");
                            }

                            var command = schedulerCommandConfig.Command;
                            var commandInputId = $"schedulerConfigs{hash}SchedulerCommandConfigs{i}Command";
                            if (ImGui.InputText($"Command###{commandInputId}", ref command, ushort.MaxValue))
                            {
                                schedulerCommandConfig.Command = command;
                                Debounce(commandInputId, Config.Save);
                            }
                            ImGui.Unindent();
                            ImGui.EndTabItem();
                        }     
                    }
                    ImGui.EndTabBar();
                }
            }
            ImGui.Unindent();
        }
        ImGui.PopStyleVar();
    }

    private void DeleteSchedulerConfig(SchedulerConfig schedulerConfig)
    {
        Config.SchedulerConfigs.Remove(schedulerConfig);
        SchedulerConfigToState.Remove(schedulerConfig);
        Config.Save();
    }

    private void DrawOutputHeader(SchedulerConfig schedulerConfig, DateTime nowUtc)
    {
        if (schedulerConfig.SchedulerCommandConfigs.Count > 0)
        {
            ImGui.PushStyleVar(ImGuiStyleVar.IndentSpacing, 10);
            ImGui.NewLine();
            var hash = schedulerConfig.GetHashCode();
            if (ImGui.CollapsingHeader($"Next Occurrences###schedulerConfigs{hash}SchedulerCommandsConfigNextOccurrencesHeader", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Indent();
                var state = SchedulerConfigToState[schedulerConfig];

                var daysLimit = state.NextOccurrencesDaysLimit;
                if (ImGui.InputInt($"Days Limit###schedulerConfigs{hash}StateNextOccurrencesDaysLimit", ref daysLimit))
                {
                    state.NextOccurrencesDaysLimit = daysLimit;
                }

                if (ImGui.BeginTable($"schedulerConfigs{hash}SchedulerCommandsConfigNextOccurrencesTab", 5, ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Resizable))
                {
                    ImGui.TableSetupColumn($"Remaining###schedulerCommandConfigs{hash}RemainingTimeColumn", ImGuiTableColumnFlags.None, 0.1f);
                    ImGui.TableSetupColumn($"Time###schedulerCommandConfigs{hash}TimeColumn", ImGuiTableColumnFlags.None, 0.2f);
                    ImGui.TableSetupColumn($"UTC Time###schedulerCommandConfigs{hash}UtcTimeColumn", ImGuiTableColumnFlags.None, 0.1f);
                    ImGui.TableSetupColumn($"Local Time {TimeZoneInfo.Local}###schedulerCommandConfigs{hash}LocalTimeColumn", ImGuiTableColumnFlags.None, 0.1f);
                    ImGui.TableSetupColumn($"Command###schedulerCommandConfigs{hash}CommandColumn", ImGuiTableColumnFlags.None, 0.6f);
                    ImGui.TableHeadersRow();

                    var clipper = ImGuiHelper.NewListClipper();
                    var entries = schedulerConfig.SchedulerCommandConfigs.SelectMany(config => {
                        return config.GetOccurrences(nowUtc, nowUtc.AddDays(state.NextOccurrencesDaysLimit)).Select(occurrence =>
                        {
                            var remainingTime = occurrence - nowUtc;
                            return (config, remainingTime, occurrence);
                        });
                    }).OrderBy(p => p.remainingTime);

                    clipper.Begin(entries.Count(), ImGui.GetTextLineHeightWithSpacing());
                    while (clipper.Step())
                    {
                        for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                        {
                            var entry = entries.ElementAt(i);
                            if (ImGui.TableNextColumn())
                            {
                                ImGui.Text(entry.remainingTime.Humanize());
                                if (ImGui.IsItemHovered())
                                {
                                    ImGui.SetTooltip(entry.remainingTime.ToString());
                                }
                            }
                            if (ImGui.TableNextColumn())
                            {
                                var timeText = $"{TimeZoneInfo.ConvertTimeFromUtc(entry.occurrence, entry.config.TimeZone)} {entry.config.TimeZone}";
                                ImGui.Text(timeText);
                                if (ImGui.IsItemHovered())
                                {
                                    ImGui.SetTooltip(timeText);
                                }
                            }

                            if (ImGui.TableNextColumn())
                            {
                                var utcTimeText = entry.occurrence.ToString();
                                ImGui.Text(utcTimeText);
                                if (ImGui.IsItemHovered())
                                {
                                    ImGui.SetTooltip(utcTimeText);
                                }
                            }

                            if (ImGui.TableNextColumn())
                            {
                                var localTimeText = $"{entry.occurrence.ToLocalTime()}";
                                ImGui.Text(localTimeText);
                                if (ImGui.IsItemHovered())
                                {
                                    ImGui.SetTooltip(localTimeText);
                                }
                            }

                            if (ImGui.TableNextColumn())
                            {
                                var commandText = entry.config.Command;
                                ImGui.Text(commandText);
                                if (ImGui.IsItemHovered())
                                {
                                    ImGui.SetTooltip(commandText);
                                }
                            }
                        }
                    }

                    clipper.Destroy();
                    ImGui.EndTable();
                }

                ImGui.Unindent();
            }
        }

        ImGui.PopStyleVar();
    }
}
