using System;
using System.Collections.Generic;
using System.Linq;

namespace Quack.Schedulers;

[Serializable]
public class SchedulerConfig
{
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = string.Empty;

    public List<SchedulerTriggerConfig> TriggerConfigs { get; set; } = [];

    #region deprecated
    [ObsoleteAttribute("Renamed to SchedulerTriggerConfigs in config version 5")]
    public List<SchedulerCommandConfig> SchedulerCommandConfigs { get; set; } = [];

    [ObsoleteAttribute("Renamed to TriggerConfigs in config version 6")]
    public List<SchedulerTriggerConfig> SchedulerTriggerConfigs { get; set; } = [];
    #endregion

    public SchedulerConfig Clone()
    {
        return new()
        {
            Enabled = Enabled,
            Name = Name,
            TriggerConfigs = new(TriggerConfigs.Select(c => c.Clone()))
        };
    }
}
