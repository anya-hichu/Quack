using System.Collections.Generic;

namespace Quack.Configs;

public class ConfigEntityExports<T>
{
    public int Version { get; set; } = Config.CURRENT_VERSION;
    public string Type { get; set; } = typeof(T).Name;
    public List<T> Entities { get; set; } = [];
}
