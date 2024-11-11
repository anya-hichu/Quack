using System;
using System.Collections.Generic;

namespace Quack.Schedulers;

[Serializable]
public class SchedulerConfig
{
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = string.Empty;
    public List<SchedulerCommandConfig> SchedulerCommandConfigs { get; set; } = [];
}
