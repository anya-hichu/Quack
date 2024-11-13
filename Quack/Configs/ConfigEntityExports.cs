using System.Collections.Generic;

namespace Quack.Configs;

public class ConfigEntityExports<T>
{
    public int Version { get; set; } = Config.CURRENT_VERSION;
    public List<T> Entities { get; set; } = [];
}
