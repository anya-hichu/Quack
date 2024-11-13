using System;

namespace Quack.Schedulers;

#region deprecated
[Obsolete("Renamed to SchedulerTriggerConfig in config version 5")]
[Serializable]
public class SchedulerCommandConfig
{
    
    [ObsoleteAttribute("Moved to SchedulerTriggerConfigs in config version 5")]
    public TimeZoneInfo TimeZone { get; set; } = TimeZoneInfo.Utc;
    [ObsoleteAttribute("Moved to SchedulerTriggerConfigs in config version 5")]
    public string TimeExpression { get; set; } = string.Empty;
    [ObsoleteAttribute("Moved to SchedulerTriggerConfigs in config version 5")]
    public string Command { get; set; } = string.Empty;
}
#endregion
