using Cronos;
using Dalamud.Utility;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quack.Schedulers;

[Serializable]
public class SchedulerCommandConfig
{
    public TimeZoneInfo TimeZone { get; set; } = TimeZoneInfo.Utc;
    public string TimeExpression { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;

    public CronExpression? ParseCronExpression()
    {
        if (!TimeExpression.IsNullOrEmpty() && CronExpression.TryParse(TimeExpression, out var cronExpression))
        {
            return cronExpression;
        } 
        else
        {
            return null;
        }
    }

    public DateTime? GetNextOccurrence(DateTime fromUtc)
    {
        var cronExpression = ParseCronExpression();
        if (cronExpression != null)
        {
            var nextOccurrence = cronExpression.GetNextOccurrence(fromUtc.Add(TimeZone.BaseUtcOffset));
            return nextOccurrence.HasValue ? nextOccurrence.Value.Add(-TimeZone.BaseUtcOffset) : null;
        } 
        else
        {
            return null;
        }
    }

    public IEnumerable<DateTime> GetOccurrences(DateTime fromUtc, DateTime toUtc)
    {
        var cronExpression = ParseCronExpression();
        if (cronExpression != null)
        {
            var nextOccurrences = cronExpression.GetOccurrences(fromUtc.Add(TimeZone.BaseUtcOffset), toUtc.Add(TimeZone.BaseUtcOffset));
            return nextOccurrences.Select(o => o.Add(-TimeZone.BaseUtcOffset));
        }
        else
        {
            return [];
        }
    }

    public bool Valid()
    {
        return ParseCronExpression() != null && !Command.IsNullOrWhitespace();
    }
}
