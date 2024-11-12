using System;
using System.Collections.Generic;

namespace Quack.Schedulers;

[Serializable]
public class SchedulerConfig
{
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = string.Empty;

    [ObsoleteAttribute("Renamed to SchedulerTriggerConfigs in config version 5")]
    public List<SchedulerCommandConfig> SchedulerCommandConfigs { get; set; } = [];

    [ObsoleteAttribute("Renamed to TriggerConfigs in config version 6")]
    public List<SchedulerTriggerConfig> SchedulerTriggerConfigs { get; set; } = [];
    public List<SchedulerTriggerConfig> TriggerConfigs { get; set; } = [];
}
