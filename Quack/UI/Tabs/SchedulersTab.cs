using Dalamud.Interface.Colors;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Utility;
using Humanizer;
using ImGuiNET;
using Newtonsoft.Json;
using Quack.Chat;
using Quack.Schedulers;
using Quack.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Quack.UI.Helpers;
using Quack.UI.States;
using Dalamud.Interface.Utility.Raii;

namespace Quack.UI.Tabs;

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
        using (var popup = ImRaii.Popup(deleteAllSchedulerConfigsPopup))
        {
            if (popup.Success)
            {
                ImGui.Text($"Confirm deleting {Config.SchedulerConfigs.Count} schedulers?");

                ImGui.SetCursorPosX(15);
                using (ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.DalamudRed))
                {
                    if (ImGui.Button($"Yes##{deleteAllSchedulerConfigsPopup}Yes", new(100, 30)))
                    {
                        DeleteSchedulerConfigs();
                        ImGui.CloseCurrentPopup();
                    }
                }
                ImGui.SameLine();
                if (ImGui.Button($"No##{deleteAllSchedulerConfigsPopup}No", new(100, 30)))
                {
                    ImGui.CloseCurrentPopup();
                }
            }
        }

        ImGui.SameLine();
        using (ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.DalamudRed))
        {
            if (ImGui.Button("Delete All##schedulerConfigsDeleteAll"))
            {
                if (Config.SchedulerConfigs.Count > 0)
                {
                    ImGui.OpenPopup(deleteAllSchedulerConfigsPopup);
                }
            }
        }

        using (ImRaii.TabBar("schedulerConfigsTabs", ImGuiTabBarFlags.AutoSelectNewTabs | ImGuiTabBarFlags.TabListPopupButton | ImGuiTabBarFlags.FittingPolicyScroll))
        {
            foreach (var schedulerConfig in Config.SchedulerConfigs)
            {
                var hash = schedulerConfig.GetHashCode();
                using (var tab = ImRaii.TabItem($"{(schedulerConfig.Name.IsNullOrWhitespace() ? BLANK_NAME : schedulerConfig.Name)}###schedulerConfigs{hash}"))
                {
                    if (tab.Success)
                    {
                        ImGui.NewLine();
                        DrawDefinitionHeader(schedulerConfig, nowUtc);
                        DrawOutputHeader(schedulerConfig, nowUtc);
                    }
                }
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
                importedSchedulerConfigs.ForEach(Migrator.MigrateSchedulerConfigToV5);
                Config.SchedulerConfigs.AddRange(importedSchedulerConfigs);
                importedSchedulerConfigs.ForEach(c => SchedulerConfigToState.Add(c, new()));
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
        if (ImGui.CollapsingHeader($"Definition##schedulerConfigs{hash}Definition", ImGuiTreeNodeFlags.DefaultOpen))
        {
            using (ImRaii.PushIndent())
            {
                var enabled = schedulerConfig.Enabled;
                var enabledInputId = $"schedulerConfigs{hash}Enabled";
                if (ImGui.Checkbox($"Enabled##{enabledInputId}", ref enabled))
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
                
                using(ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.DalamudRed))
                {
                    if (ImGui.Button($"Delete###schedulerConfigs{hash}Delete"))
                    {
                        DeleteSchedulerConfig(schedulerConfig);
                    }
                }

                var name = schedulerConfig.Name;
                var nameInputId = $"schedulerConfigs{hash}Name";
                if (ImGui.InputText($"Name###{nameInputId}", ref name, ushort.MaxValue))
                {
                    schedulerConfig.Name = name;
                    Debounce(nameInputId, Config.Save);
                }

                if (ImGui.CollapsingHeader($"Triggers###schedulerConfigs{hash}SchedulerTriggerConfigs", ImGuiTreeNodeFlags.DefaultOpen))
                {

                    if (ImGui.Button($"+###schedulerConfigs{hash}SchedulerTriggerConfigsNew"))
                    {
                        schedulerConfig.SchedulerTriggerConfigs.Add(new());
                        Config.Save();
                    }

                    ImGui.SameLine(50);
                    using (ImRaii.TabBar($"schedulerConfigs{hash}SchedulerTriggerConfigsTabs", ImGuiTabBarFlags.AutoSelectNewTabs | ImGuiTabBarFlags.FittingPolicyScroll))
                    {
                        for (var i = 0; i < schedulerConfig.SchedulerTriggerConfigs.Count; i++)
                        {
                            var schedulerTriggerConfig = schedulerConfig.SchedulerTriggerConfigs[i];

                            using (var tab = ImRaii.TabItem($"#{i}###schedulerConfigs{hash}SchedulerTriggerConfigs{i}Tab"))
                            {
                                if (tab.Success)
                                {
                                    using (ImRaii.PushIndent())
                                    {
                                        var timeExpression = schedulerTriggerConfig.TimeExpression;
                                        var timeExpressionInputId = $"schedulerConfigs{hash}SchedulerTriggerConfigs{i}TimeExpression";
                                        if (ImGui.InputText($"Time Expression (Cron)###{timeExpressionInputId}", ref timeExpression, ushort.MaxValue))
                                        {
                                            schedulerTriggerConfig.TimeExpression = timeExpression;
                                            Debounce(timeExpressionInputId, Config.Save);
                                        }

                                        ImGui.SameLine(ImGui.GetWindowWidth() - 60);
                                        using (ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.DalamudRed))
                                        {
                                            if (ImGui.Button($"Delete###schedulerConfigs{hash}SchedulerTriggerConfigs{i}Delete"))
                                            {
                                                schedulerConfig.SchedulerTriggerConfigs.RemoveAt(i);
                                                Config.Save();
                                            }
                                        }

                                        var cronExpression = schedulerTriggerConfig.ParseCronExpression();
                                        if (cronExpression == null)
                                        {
                                            using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudRed))
                                            {
                                                ImGui.Text("Invalid Time Expression");
                                            }
                                        }
                                        else
                                        {
                                            ImGui.Text($"Interpolation: {cronExpression}");
                                        }

                                        var timeZones = TimeZoneInfo.GetSystemTimeZones();
                                        var timeZoneNames = timeZones.Select(z => z.DisplayName).ToArray();
                                        var timeZoneName = schedulerTriggerConfig.TimeZone.DisplayName;
                                        var timeZoneNameIndex = timeZoneNames.IndexOf(timeZoneName);
                                        if (ImGui.Combo($"Time Zone###schedulerConfigs{hash}SchedulerTriggerConfigs{i}TimeZone", ref timeZoneNameIndex, timeZoneNames, timeZoneNames.Length))
                                        {
                                            schedulerTriggerConfig.TimeZone = timeZones.ElementAt(timeZoneNameIndex);
                                            Config.Save();
                                        }

                                        var nextOccurrence = schedulerTriggerConfig.GetNextOccurrence(nowUtc);
                                        if (nextOccurrence.HasValue)
                                        {
                                            ImGui.Text($"Next Occurrence: {nextOccurrence.Value}");
                                        }

                                        var command = schedulerTriggerConfig.Command;
                                        var commandInputId = $"schedulerConfigs{hash}SchedulerTriggerConfigs{i}Command";
                                        if (ImGui.InputText($"Command###{commandInputId}", ref command, ushort.MaxValue))
                                        {
                                            schedulerTriggerConfig.Command = command;
                                            Debounce(commandInputId, Config.Save);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            ImGui.NewLine();
        }
    }

    private void DeleteSchedulerConfig(SchedulerConfig schedulerConfig)
    {
        Config.SchedulerConfigs.Remove(schedulerConfig);
        SchedulerConfigToState.Remove(schedulerConfig);
        Config.Save();
    }

    private void DrawOutputHeader(SchedulerConfig schedulerConfig, DateTime nowUtc)
    {
        if (schedulerConfig.SchedulerTriggerConfigs.Count > 0)
        {
            var hash = schedulerConfig.GetHashCode();
            if (ImGui.CollapsingHeader($"Next Occurrences###schedulerConfigs{hash}SchedulerTriggersConfigNextOccurrencesHeader", ImGuiTreeNodeFlags.DefaultOpen))
            {
                using (ImRaii.PushIndent())
                {
                    var state = SchedulerConfigToState[schedulerConfig];

                    var maxDays = state.MaxDays;
                    if (ImGui.InputInt($"Max Days###schedulerConfigs{hash}StateDaysDisplayLimit", ref maxDays))
                    {
                        state.MaxDays = maxDays;
                    }

                    using (ImRaii.Table($"schedulerConfigs{hash}SchedulerTriggersConfigNextOccurrencesTab", 5, ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Resizable))
                    {
                        ImGui.TableSetupColumn($"Remaining###schedulerTriggerConfigs{hash}RemainingTimeColumn", ImGuiTableColumnFlags.None, 0.1f);
                        ImGui.TableSetupColumn($"Time###schedulerTriggerConfigs{hash}TimeColumn", ImGuiTableColumnFlags.None, 0.2f);
                        ImGui.TableSetupColumn($"UTC Time###schedulerTriggerConfigs{hash}UtcTimeColumn", ImGuiTableColumnFlags.None, 0.1f);
                        ImGui.TableSetupColumn($"Local Time {TimeZoneInfo.Local}###schedulerTriggerConfigs{hash}LocalTimeColumn", ImGuiTableColumnFlags.None, 0.1f);
                        ImGui.TableSetupColumn($"Command###schedulerTriggerConfigs{hash}CommandColumn", ImGuiTableColumnFlags.None, 0.6f);
                        ImGui.TableHeadersRow();

                        var clipper = ListClipperHelper.Build();
                        var entries = schedulerConfig.SchedulerTriggerConfigs.SelectMany(config =>
                        {
                            return config.GetOccurrences(nowUtc, nowUtc.AddDays(maxDays)).Select(occurrence =>
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
                    }
                }
            }
        }
    }
}
