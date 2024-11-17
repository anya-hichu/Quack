using System.Collections.Generic;
using Quack.Configs;

namespace Quack.Exports;

public class Export<T>
{
    public int Version { get; set; } = Config.CURRENT_VERSION;
    public string Type { get; set; } = typeof(T).Name;
    public List<T> Entities { get; set; } = [];
}
