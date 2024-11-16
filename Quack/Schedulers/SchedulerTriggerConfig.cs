using Cronos;
using Dalamud.Utility;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quack.Schedulers;

[Serializable]
public class SchedulerTriggerConfig
{
    public TimeZoneInfo TimeZone { get; set; } = TimeZoneInfo.Utc;
    public string TimeExpression { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;

    public bool TryParseCronExpression(out CronExpression cronExpression)
    {
        if (!TimeExpression.IsNullOrWhitespace())
        {
            return CronExpression.TryParse(TimeExpression, out cronExpression);
        }
        else
        {
            cronExpression = null!;
            return false;
        }
    }

    public DateTime? GetNextOccurrence(DateTime fromUtc)
    {
        if (!TryParseCronExpression(out var cronExpression))
        {
            return null;
        }
        var nextOccurrence = cronExpression.GetNextOccurrence(AddOffset(fromUtc));
        return nextOccurrence.HasValue ? RemoveOffset(nextOccurrence.Value) : null;
    }

    public IEnumerable<DateTime> GetOccurrences(DateTime fromUtc, DateTime toUtc)
    {
        if (!TryParseCronExpression(out var cronExpression))
        {
            return [];
        }
        var nextOccurrences = cronExpression.GetOccurrences(AddOffset(fromUtc), AddOffset(toUtc));
        return nextOccurrences.Select(RemoveOffset);
    }

    // Chronos expects UTC
    private DateTime AddOffset(DateTime utc)
    {
        return utc.Add(TimeZone.BaseUtcOffset);
    }

    private DateTime RemoveOffset(DateTime utc)
    {
        return utc.Add(-TimeZone.BaseUtcOffset);
    }

    public SchedulerTriggerConfig Clone()
    {
        return (SchedulerTriggerConfig)MemberwiseClone();
    }
}
