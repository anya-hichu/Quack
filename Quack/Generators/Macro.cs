using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quack.Generators;

[Serializable]
public class Macro
{
    public static IEnumerable<Macro> FilterAndSort(IEnumerable<Macro> macros, string filter)
    {
        return macros
            .Where(m => m.CountTokenMatches(filter) > 0)
            .OrderBy(m => -m.CountTokenMatches(filter))
            .ThenBy(x => x.Name);
    }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("path")]
    public string Path { get; set; } = string.Empty;

    [JsonProperty("content")]
    public string Content { get; set; } = string.Empty;

    public int CountTokenMatches(string filter)
    {
        var tokens = filter.ToLowerInvariant().Split(" ");
        return tokens.Count(t => Name.Contains(t, StringComparison.InvariantCultureIgnoreCase) || 
                               Path.Contains(t, StringComparison.InvariantCultureIgnoreCase));
    }
}
