using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Humanizer;
using ImGuiNET;
using Newtonsoft.Json;
using Quack.Chat;
using Quack.Configs;
using Quack.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quack.Schedulers;

public class SchedulerConfigTab : ConfigEntityTab
{
    private ChatSender ChatSender { get; init; }
    private Config Config { get; init; }
    private IKeyState KeyState { get; init; }
    private IPluginLog PluginLog { get; init; }
    private Dictionary<SchedulerConfig, SchedulerConfigTabState> SchedulerConfigToState { get; set; }

    public SchedulerConfigTab(ChatSender chatSender, Config config, Debouncers debouncers, FileDialogManager fileDialogManager, 
                              IKeyState keyState, IPluginLog pluginLog, INotificationManager notificationManager) : base(debouncers, fileDialogManager, notificationManager)
    {
        ChatSender = chatSender;
        Config = config;
        KeyState = keyState;
        PluginLog = pluginLog;

        SchedulerConfigToState = Config.SchedulerConfigs.ToDictionary(c => c, c => new SchedulerConfigTabState());
    }

    public void Draw()
    {
        var nowUtc = DateTime.UtcNow;

        if (ImGui.Button("New###schedulerConfigsNew"))
        {
            NewSchedulerConfig();
        }

        ImGui.SameLine(ImGui.GetWindowWidth() - 217);
        ImGui.Button("Export All###schedulerConfigsExportAll");
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(EXPORT_HINT);
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
        ImGui.Button("Import All###schedulerConfigsImportAll");
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(IMPORT_HINT);
        }
        if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
        {
            ImportFromFile(ImportSchedulerConfigExportsJson, "Import Schedulers");
        }
        else if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            ImportFromClipboard(ImportSchedulerConfigExportsJson);
        }

        ImGui.SameLine();
        using (ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.DalamudRed))
        {
            if (ImGui.Button("Delete All###schedulerConfigsDeleteAll") && KeyState[VirtualKey.CONTROL])
            {
                DeleteSchedulerConfigs();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(CONFIRM_DELETE_HINT);
            }
        }

        using (ImRaii.TabBar("schedulerConfigsTabs", ImGuiTabBarFlags.AutoSelectNewTabs | ImGuiTabBarFlags.TabListPopupButton | ImGuiTabBarFlags.FittingPolicyScroll))
        {
            foreach (var schedulerConfig in Config.SchedulerConfigs)
            {
                using (var tab = ImRaii.TabItem($"{(schedulerConfig.Name.IsNullOrWhitespace() ? BLANK_NAME : schedulerConfig.Name)}###schedulerConfigs{schedulerConfig.GetHashCode()}Tab"))
                {
                    if (tab)
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

    private int ImportSchedulerConfigExportsJson(string exportsJson)
    {
        var exports = JsonConvert.DeserializeObject<ConfigEntityExports<SchedulerConfig>>(exportsJson);
        if (exports == null)
        {
            PluginLog.Verbose($"Failed to import schedulers from json: {exportsJson}");
            return -1;
        }

        var schedulerConfigs = exports.Entities;
        schedulerConfigs.ForEach(schedulerConfig =>
        {
            #region deprecated
            if (exports.Version < 6)
            {
                ConfigMigrator.MigrateSchedulerConfigToV6(schedulerConfig);
            }
            #endregion
            SchedulerConfigToState.Add(schedulerConfig, new());
        });
        Config.SchedulerConfigs.AddRange(schedulerConfigs);
        Config.Save();
        return schedulerConfigs.Count;
    }

    private void DeleteSchedulerConfigs()
    {
        Config.SchedulerConfigs.Clear();
        SchedulerConfigToState.Clear();
        Config.Save();
    }

    private void DrawDefinitionHeader(SchedulerConfig schedulerConfig, DateTime nowUtc)
    {
        var schedulerConfigsId = $"schedulerConfigs{schedulerConfig.GetHashCode()}";
        if (ImGui.CollapsingHeader($"Definition###{schedulerConfigsId}DefinitionHeader", ImGuiTreeNodeFlags.DefaultOpen))
        {
            using (ImRaii.PushIndent())
            {
                var enabled = schedulerConfig.Enabled;
                var enabledInputId = $"{schedulerConfigsId}Enabled";
                if (ImGui.Checkbox($"Enabled###{enabledInputId}", ref enabled))
                {
                    schedulerConfig.Enabled = enabled;
                    Debounce(enabledInputId, Config.Save);
                }

                ImGui.SameLine(ImGui.GetWindowWidth() - 180);
                if (ImGui.Button($"Duplicate###{schedulerConfigsId}Duplicate"))
                {
                    DuplicateSchedulerConfig(schedulerConfig);
                }
                ImGui.SameLine();
                ImGui.Button($"Export###{schedulerConfigsId}Export");
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(EXPORT_HINT);
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

                using (ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.DalamudRed))
                {
                    if (ImGui.Button($"Delete###{schedulerConfigsId}Delete") && KeyState[VirtualKey.CONTROL])
                    {
                        DeleteSchedulerConfig(schedulerConfig);
                    }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(CONFIRM_DELETE_HINT);
                    }
                }

                var name = schedulerConfig.Name;
                var nameInputId = $"{schedulerConfigsId}Name";
                if (ImGui.InputText($"Name###{nameInputId}", ref name, ushort.MaxValue))
                {
                    schedulerConfig.Name = name;
                    Debounce(nameInputId, Config.Save);
                }

                var triggerConfigsId = $"{schedulerConfigsId}TriggerConfigs";
                if (ImGui.CollapsingHeader($"Triggers###{triggerConfigsId}Header", ImGuiTreeNodeFlags.DefaultOpen))
                {

                    if (ImGui.Button($"+###{triggerConfigsId}New"))
                    {
                        schedulerConfig.TriggerConfigs.Add(new());
                        Config.Save();
                    }

                    ImGui.SameLine(50);
                    using (ImRaii.TabBar($"{triggerConfigsId}Tabs", ImGuiTabBarFlags.AutoSelectNewTabs | ImGuiTabBarFlags.FittingPolicyScroll))
                    {
                        var triggerConfigs = schedulerConfig.TriggerConfigs;
                        for (var i = 0; i < triggerConfigs.Count; i++)
                        {
                            var triggerConfigId = $"{triggerConfigsId}{i}";
                            using (var tab = ImRaii.TabItem($"#{i}###{triggerConfigId}Tab"))
                            {
                                if (!tab)
                                {
                                    continue;
                                }

                                var triggerConfig = triggerConfigs.ElementAt(i);
                                using (ImRaii.PushIndent())
                                {
                                    var timeExpression = triggerConfig.TimeExpression;
                                    var timeExpressionInputId = $"{triggerConfigId}TimeExpression";
                                    if (ImGui.InputText($"Time Expression (Cron)###{timeExpressionInputId}", ref timeExpression, ushort.MaxValue))
                                    {
                                        triggerConfig.TimeExpression = timeExpression;
                                        Debounce(timeExpressionInputId, Config.Save);
                                    }
                                    ImGuiComponents.HelpMarker("* * * * *\n |  |  |  |  |\n |  |  |  |  day of the week (0–6) or (MON to SUN) \n |  |  |  month (1–12)\n |  |  day of the month (1–31)\n |  hour (0–23)\nminute (0–59)\n\nWildcard (*): represents 'all'. For example, using '* * * * *' will run every minute. Using '* * * * 1' will run every minute only on Monday. Using six asterisks means every second when seconds are supported.\nComma (,): used to separate items of a list. For example, using 'MON,WED,FRI' in the 5th field (day of week) means Mondays, Wednesdays and Fridays.\nHyphen (-): defines ranges. For example, '2000-2010' indicates every year between 2000 and 2010, inclusive.");

                                    ImGui.SameLine(ImGui.GetWindowWidth() - 125);
                                    if (ImGui.Button($"Duplicate###{triggerConfigId}Duplicate"))
                                    {
                                        triggerConfigs.Add(triggerConfig.Clone());
                                        Config.Save();
                                    }
                                    ImGui.SameLine();
                                    using (ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.DalamudRed))
                                    {
                                        if (ImGui.Button($"Delete###{triggerConfigId}Delete") && KeyState[VirtualKey.CONTROL])
                                        {
                                            triggerConfigs.RemoveAt(i);
                                            Config.Save();
                                        }
                                        if (ImGui.IsItemHovered())
                                        {
                                            ImGui.SetTooltip(CONFIRM_DELETE_HINT);
                                        }
                                    }

                                    if (triggerConfig.TryParseCronExpression(out var cronExpression))
                                    {
                                        ImGui.Text($"Interpolation: {cronExpression}");
                                    }
                                    else
                                    {
                                        using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudRed))
                                        {
                                            ImGui.Text("Invalid Time Expression");
                                        }
                                    }

                                    var timeZones = TimeZoneInfo.GetSystemTimeZones();
                                    var timeZoneNames = timeZones.Select(z => z.DisplayName).ToArray();
                                    var timeZoneName = triggerConfig.TimeZone.DisplayName;
                                    var timeZoneNameIndex = timeZoneNames.IndexOf(timeZoneName);
                                    if (ImGui.Combo($"Time Zone###{triggerConfigId}TimeZone", ref timeZoneNameIndex, timeZoneNames, timeZoneNames.Length))
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
                                    var commandInputId = $"{triggerConfigId}Command";
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
            ImGui.NewLine();
        }
    }

    private void DuplicateSchedulerConfig(SchedulerConfig schedulerConfig)
    {
        var duplicate = schedulerConfig.Clone();
        duplicate.Name = $"{schedulerConfig.Name} (Copy)";
        SchedulerConfigToState.Add(duplicate, new());
        Config.SchedulerConfigs.Add(duplicate);
        Config.Save();
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
            var nextOccurrencesId = $"schedulerConfigs{schedulerConfig.GetHashCode()}NextOccurrences";
            if (ImGui.CollapsingHeader($"Next Occurrences###{nextOccurrencesId}Header", ImGuiTreeNodeFlags.DefaultOpen))
            {
                using (ImRaii.PushIndent())
                {
                    var state = SchedulerConfigToState[schedulerConfig];
                    var maxDays = state.MaxDays;
                    if (ImGui.InputInt($"Max Days###{nextOccurrencesId}StateMaxDays", ref maxDays))
                    {
                        state.MaxDays = maxDays;
                    }

                    if (ImGui.GetCursorPosY() < ImGui.GetWindowHeight())
                    {
                        var scheduleConfigTableId = $"{nextOccurrencesId}Table";
                        using (ImRaii.Table(scheduleConfigTableId, 5, ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Resizable))
                        {
                            ImGui.TableSetupColumn($"Remaining###{scheduleConfigTableId}RemainingTime", ImGuiTableColumnFlags.None, 0.1f);
                            ImGui.TableSetupColumn($"Time###{scheduleConfigTableId}Time", ImGuiTableColumnFlags.None, 0.2f);
                            ImGui.TableSetupColumn($"UTC Time###{scheduleConfigTableId}UtcTime", ImGuiTableColumnFlags.None, 0.1f);
                            ImGui.TableSetupColumn($"Local Time {TimeZoneInfo.Local}###{scheduleConfigTableId}LocalTime", ImGuiTableColumnFlags.None, 0.1f);
                            ImGui.TableSetupColumn($"Command###{scheduleConfigTableId}Command", ImGuiTableColumnFlags.None, 0.6f);
                            ImGui.TableHeadersRow();

                            var clipper = ListClipper.Build();
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
                                        var localTimeText = entry.occurrence.ToLocalTime().ToString();
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
}
