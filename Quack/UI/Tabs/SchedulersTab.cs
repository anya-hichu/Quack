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
using System.Linq;
using Quack.UI.Helpers;
using Quack.UI.States;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using Dalamud.Game.ClientState.Keys;

namespace Quack.UI.Tabs;

public class SchedulersTab : ModelTab
{
    private ChatSender ChatSender { get; init; }
    private Config Config { get; init; }
    private IKeyState KeyState { get; init; }
    private Dictionary<SchedulerConfig, SchedulerConfigState> SchedulerConfigToState { get; set; }

    public SchedulersTab(ChatSender chatSender, Config config, Debouncers debouncers, FileDialogManager fileDialogManager, IKeyState keyState) : base(debouncers, fileDialogManager)
    {
        ChatSender = chatSender;
        Config = config;
        KeyState = keyState;

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
        ImGui.Button("Export All##schedulerConfigsExportAll");
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Right-click for clipboard base64 export");
        }
        if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
        {
            ExportToFile(Config.SchedulerConfigs, "Export Schedulers", "schedulers.json");
        }
        else if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            ExportToClipboard(Config.SchedulerConfigs);
        }

        ImGui.SameLine();
        ImGui.Button("Import All##schedulerConfigsImportAll");
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Right-click for clipboard base64 import");
        }
        if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
        {
            WithFileContent(ImportSchedulerConfigsFromJson, "Import Schedulers");
        }
        else if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            WithDecodedClipboardContent(ImportSchedulerConfigsFromJson);
        }

        ImGui.SameLine();
        using (ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.DalamudRed))
        {
            var deleteAllPressed = ImGui.Button("Delete All##schedulerConfigsDeleteAll");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Press <CTRL> while clicking to confirm deleting all schedulers");
            }
            if (deleteAllPressed && KeyState[VirtualKey.CONTROL])
            {
                DeleteSchedulerConfigs();
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

    private void ImportSchedulerConfigsFromJson(string schedulerConfigsJson)
    {
        var schedulerConfigs = JsonConvert.DeserializeObject<List<SchedulerConfig>>(schedulerConfigsJson)!;
        schedulerConfigs.ForEach(schedulerConfig => {
            Migrator.MaybeMigrateSchedulerConfigToV6(schedulerConfig);
            SchedulerConfigToState.Add(schedulerConfig, new());
        });
        Config.SchedulerConfigs.AddRange(schedulerConfigs);
        Config.Save();
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
                ImGui.Button($"Export##schedulerConfigs{hash}Export");
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Right-click for clipboard base64 export");
                }
                if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                {
                    ExportToFile([schedulerConfig], "Export Scheduler", $"{(schedulerConfig.Name.IsNullOrWhitespace() ? "scheduler" : schedulerConfig.Name)}.json");
                }
                else if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                {
                    ExportToClipboard([schedulerConfig]);
                }
                ImGui.SameLine();
                
                using(ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.DalamudRed))
                {
                    var deletePressed = ImGui.Button($"Delete###schedulerConfigs{hash}Delete");
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("Press <CTRL> while clicking to confirm scheduler deletion");
                    }
                    if (deletePressed && KeyState[VirtualKey.CONTROL])
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

                if (ImGui.CollapsingHeader($"Triggers###schedulerConfigs{hash}TriggerConfigs", ImGuiTreeNodeFlags.DefaultOpen))
                {

                    if (ImGui.Button($"+###schedulerConfigs{hash}TriggerConfigsNew"))
                    {
                        schedulerConfig.TriggerConfigs.Add(new());
                        Config.Save();
                    }

                    ImGui.SameLine(50);
                    using (ImRaii.TabBar($"schedulerConfigs{hash}TriggerConfigsTabs", ImGuiTabBarFlags.AutoSelectNewTabs | ImGuiTabBarFlags.FittingPolicyScroll))
                    {
                        for (var i = 0; i < schedulerConfig.TriggerConfigs.Count; i++)
                        {
                            var triggerConfig = schedulerConfig.TriggerConfigs[i];

                            using (var tab = ImRaii.TabItem($"#{i}###schedulerConfigs{hash}TriggerConfigs{i}Tab"))
                            {
                                if (tab.Success)
                                {
                                    using (ImRaii.PushIndent())
                                    {
                                        var timeExpression = triggerConfig.TimeExpression;
                                        var timeExpressionInputId = $"schedulerConfigs{hash}TriggerConfigs{i}TimeExpression";
                                        var timeExpressionInput = ImGui.InputText($"Time Expression (Cron)###{timeExpressionInputId}", ref timeExpression, ushort.MaxValue);
                                        if (ImGui.IsItemHovered())
                                        {
                                            ImGui.SetTooltip("* * * * *\n| | | | |\n| | | | day of the week (0–6) or (MON to SUN; \n| | | month (1–12)\n| | day of the month (1–31)\n| hour (0–23)\nminute (0–59)\n\nWildcard (*): represents 'all'. For example, using '* * * * *' will run every minute. Using '* * * * 1' will run every minute only on Monday. Using six asterisks means every second when seconds are supported.\nComma (,): used to separate items of a list. For example, using 'MON,WED,FRI' in the 5th field (day of week) means Mondays, Wednesdays and Fridays.\nHyphen (-): defines ranges. For example, '2000-2010' indicates every year between 2000 and 2010, inclusive.");
                                        }
                                        if (timeExpressionInput)
                                        {
                                            triggerConfig.TimeExpression = timeExpression;
                                            Debounce(timeExpressionInputId, Config.Save);
                                        }

                                        ImGui.SameLine(ImGui.GetWindowWidth() - 60);
                                        using (ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.DalamudRed))
                                        {
                                            if (ImGui.Button($"Delete###schedulerConfigs{hash}TriggerConfigs{i}Delete"))
                                            {
                                                schedulerConfig.TriggerConfigs.RemoveAt(i);
                                                Config.Save();
                                            }
                                        }

                                        var cronExpression = triggerConfig.ParseCronExpression();
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
                                        var timeZoneName = triggerConfig.TimeZone.DisplayName;
                                        var timeZoneNameIndex = timeZoneNames.IndexOf(timeZoneName);
                                        if (ImGui.Combo($"Time Zone###schedulerConfigs{hash}TriggerConfigs{i}TimeZone", ref timeZoneNameIndex, timeZoneNames, timeZoneNames.Length))
                                        {
                                            triggerConfig.TimeZone = timeZones.ElementAt(timeZoneNameIndex);
                                            Config.Save();
                                        }

                                        ImGui.SameLine();
                                        if (ImGui.Button("Local"))
                                        {
                                            triggerConfig.TimeZone = TimeZoneInfo.Local;
                                            Config.Save();
                                        }

                                        var nextOccurrence = triggerConfig.GetNextOccurrence(nowUtc);
                                        if (nextOccurrence.HasValue)
                                        {
                                            ImGui.Text($"Next Occurrence: {nextOccurrence.Value}");
                                        }

                                        var command = triggerConfig.Command;
                                        var commandInputId = $"schedulerConfigs{hash}TriggerConfigs{i}Command";
                                        if (ImGui.InputText($"Command###{commandInputId}", ref command, ushort.MaxValue))
                                        {
                                            triggerConfig.Command = command;
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
        if (schedulerConfig.TriggerConfigs.Count > 0)
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
                        ImGui.TableSetupColumn($"Remaining###triggerConfigs{hash}RemainingTimeColumn", ImGuiTableColumnFlags.None, 0.1f);
                        ImGui.TableSetupColumn($"Time###triggerConfigs{hash}TimeColumn", ImGuiTableColumnFlags.None, 0.2f);
                        ImGui.TableSetupColumn($"UTC Time###triggerConfigs{hash}UtcTimeColumn", ImGuiTableColumnFlags.None, 0.1f);
                        ImGui.TableSetupColumn($"Local Time {TimeZoneInfo.Local}###triggerConfigs{hash}LocalTimeColumn", ImGuiTableColumnFlags.None, 0.1f);
                        ImGui.TableSetupColumn($"Command###triggerConfigs{hash}CommandColumn", ImGuiTableColumnFlags.None, 0.6f);
                        ImGui.TableHeadersRow();

                        var clipper = ListClipperHelper.Build();
                        var entries = schedulerConfig.TriggerConfigs.SelectMany(config =>
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
