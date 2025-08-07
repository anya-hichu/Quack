using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Humanizer;
using Dalamud.Bindings.ImGui;
using Newtonsoft.Json;
using Quack.Chat;
using Quack.Configs;
using Quack.Exports;
using Quack.UI;
using Quack.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

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

        var schedulerConfigsId = "schedulerConfigs";
        if (ImGui.Button($"New###{schedulerConfigsId}New"))
        {
            NewSchedulerConfig();
        }

        ImGui.SameLine(ImGui.GetWindowWidth() - 217);
        ImGui.Button($"Export All###{schedulerConfigsId}ExportAll");
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
        ImGui.Button($"Import All###{schedulerConfigsId}ImportAll");
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(IMPORT_HINT);
        }
        if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
        {
            ImportFromFile(ProcessExportJson, "Import Schedulers");
        }
        else if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            ImportFromClipboard(ProcessExportJson);
        }

        ImGui.SameLine();
        using (ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.DalamudRed))
        {
            if (ImGui.Button($"Delete All###{schedulerConfigsId}DeleteAll") && KeyState[VirtualKey.CONTROL])
            {
                DeleteSchedulerConfigs();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(CONFIRM_DELETE_HINT);
            }
        }

        var schedulerConfigs = Config.SchedulerConfigs;
        using (ImRaii.TabBar($"{schedulerConfigsId}{string.Join("-", schedulerConfigs.Select(c => c.GetHashCode()))}Tabs", ImGuiTabBarFlags.AutoSelectNewTabs | ImGuiTabBarFlags.ListPopupButton | ImGuiTabBarFlags.FittingPolicyScroll))
        {
            
            for (var i = 0; i < schedulerConfigs.Count; i++)
            {
                var schedulerConfig = schedulerConfigs.ElementAt(i);
                var schedulerConfigId = $"schedulerConfigs{schedulerConfig.GetHashCode()}";
                using (var tab = ImRaii.TabItem($"{(schedulerConfig.Name.IsNullOrWhitespace() ? BLANK_NAME : schedulerConfig.Name)}###{schedulerConfigId}Tab"))
                {
                    MoveTabPopup($"{schedulerConfigId}Popup", schedulerConfigs, i, Config.Save);

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

    private List<SchedulerConfig>? ProcessExportJson(string exportJson)
    {
        var export = JsonConvert.DeserializeObject<Export<SchedulerConfig>>(exportJson);
        if (export == null || export.Type != typeof(SchedulerConfig).Name)
        {
            PluginLog.Verbose($"Failed to import scheduler config from json: {exportJson}");
            return null;
        }
        #region deprecated
        ExportMigrator.MaybeMigrate(export);
        #endregion
        var schedulerConfigs = export.Entities;
        schedulerConfigs.ForEach(schedulerConfig => SchedulerConfigToState.Add(schedulerConfig, new()));
        Config.SchedulerConfigs.AddRange(schedulerConfigs);
        Config.Save();
        return schedulerConfigs;
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
                            var triggerConfig = triggerConfigs.ElementAt(i);

                            var triggerConfigId = $"{triggerConfigsId}{i}";

                            var triggerName = triggerConfig.Name;
                            using (var tab = ImRaii.TabItem($"{(triggerName.IsNullOrWhitespace() ? $"#{i}" : triggerName)}###{triggerConfigId}Tab"))
                            {
                                MoveTabPopup($"{triggerConfigId}Popup", triggerConfigs, i, Config.Save);

                                var timeExpression = triggerConfig.TimeExpression;
                                var command = triggerConfig.Command;
                                if (ImGui.IsItemHovered() && !timeExpression.IsNullOrWhitespace() && !command.IsNullOrWhitespace())
                                {
                                    ImGui.SetTooltip($"At [{timeExpression}] execute command [{command}]");
                                }

                                if (!tab)
                                {
                                    continue;
                                }

                                
                                using (ImRaii.PushIndent())
                                {
                                    var triggerNameInputId = $"{triggerConfigId}Name";
                                    if (ImGui.InputText($"Name###{triggerNameInputId}", ref triggerName, ushort.MaxValue))
                                    {
                                        triggerConfig.Name = triggerName;
                                        Debounce(triggerNameInputId, Config.Save);
                                    }

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

                                    var timeExpressionInputId = $"{triggerConfigId}TimeExpression";
                                    if (ImGui.InputText($"Time Expression (Cron)###{timeExpressionInputId}", ref timeExpression, ushort.MaxValue))
                                    {
                                        triggerConfig.TimeExpression = timeExpression;
                                        Debounce(timeExpressionInputId, Config.Save);
                                    }
                                    ImGuiComponents.HelpMarker("* * * * *\n |  |  |  |  |\n |  |  |  |  day of the week (0–6) or (MON to SUN) \n |  |  |  month (1–12)\n |  |  day of the month (1–31)\n |  hour (0–23)\nminute (0–59)\n\nWildcard (*): represents 'all'. For example, using '* * * * *' will run every minute. Using '* * * * 1' will run every minute only on Monday. Using six asterisks means every second when seconds are supported.\nComma (,): used to separate items of a list. For example, using 'MON,WED,FRI' in the 5th field (day of week) means Mondays, Wednesdays and Fridays.\nHyphen (-): defines ranges. For example, '2000-2010' indicates every year between 2000 and 2010, inclusive.");

                                    if (!timeExpression.IsNullOrWhitespace())
                                    {
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

                            var clipper = ImGui.ImGuiListClipper();
                            var entries = schedulerConfig.TriggerConfigs.SelectMany(TriggerConfig =>
                            {
                                return TriggerConfig.GetOccurrences(nowUtc, nowUtc.AddDays(maxDays)).Select(Occurrence =>
                                {
                                    var RemainingTime = Occurrence - nowUtc;
                                    return (TriggerConfig, RemainingTime, Occurrence);
                                });
                            }).OrderBy(p => p.RemainingTime);

                            clipper.Begin(entries.Count(), ImGui.GetTextLineHeightWithSpacing());
                            while (clipper.Step())
                            {
                                for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                                {
                                    var entry = entries.ElementAt(i);
                                    if (ImGui.TableNextColumn())
                                    {
                                        ImGui.Text(entry.RemainingTime.Humanize());
                                        if (ImGui.IsItemHovered())
                                        {
                                            ImGui.SetTooltip(entry.RemainingTime.ToString());
                                        }
                                    }
                                    if (ImGui.TableNextColumn())
                                    {
                                        var timeZone = entry.TriggerConfig.TimeZone;
                                        var timeText = $"{TimeZoneInfo.ConvertTimeFromUtc(entry.Occurrence, timeZone)} {timeZone}";
                                        ImGui.Text(timeText);
                                        if (ImGui.IsItemHovered())
                                        {
                                            ImGui.SetTooltip(timeText);
                                        }
                                    }

                                    if (ImGui.TableNextColumn())
                                    {
                                        var utcTimeText = entry.Occurrence.ToString();
                                        ImGui.Text(utcTimeText);
                                        if (ImGui.IsItemHovered())
                                        {
                                            ImGui.SetTooltip(utcTimeText);
                                        }
                                    }

                                    if (ImGui.TableNextColumn())
                                    {
                                        var localTimeText = entry.Occurrence.ToLocalTime().ToString();
                                        ImGui.Text(localTimeText);
                                        if (ImGui.IsItemHovered())
                                        {
                                            ImGui.SetTooltip(localTimeText);
                                        }
                                    }

                                    if (ImGui.TableNextColumn())
                                    {
                                        var commandText = entry.TriggerConfig.Command;
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
