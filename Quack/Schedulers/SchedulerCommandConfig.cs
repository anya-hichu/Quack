using System;

namespace Quack.Schedulers;

[Obsolete("Renamed to SchedulerTriggerConfig in config version 5")]
[Serializable]
public class SchedulerCommandConfig
{
    public TimeZoneInfo TimeZone { get; set; } = TimeZoneInfo.Utc;
    public string TimeExpression { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
}
