

using Newtonsoft.Json;
using System;
using static FFXIVClientStructs.FFXIV.Client.LayoutEngine.LayoutManager;

namespace Quack.Generators;

[Serializable]
public class Macro
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("path")]
    public string Path { get; set; } = string.Empty;

    [JsonProperty("content")]
    public string Content { get; set; } = string.Empty;

    public bool Matches(string filter)
    {
        return Name.Contains(filter, StringComparison.OrdinalIgnoreCase) || Path.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }
}
