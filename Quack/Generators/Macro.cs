

using Newtonsoft.Json;
using System;
using System.Linq;

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
        var tokens = filter.ToLowerInvariant().Split(" ");
        return tokens.Any(t => Name.Contains(t, StringComparison.InvariantCultureIgnoreCase) || 
                               Path.Contains(t, StringComparison.InvariantCultureIgnoreCase));
    }
}
