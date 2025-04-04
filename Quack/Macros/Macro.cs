using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Quack.Macros;

[Serializable]
public class Macro
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("path")]
    public string Path { get; set; } = string.Empty;

    [JsonProperty("command")]
    public string Command { get; set; } = string.Empty;

    [JsonProperty("args")]
    public string Args { get; set; } = string.Empty;

    [JsonProperty("tags")]
    public HashSet<string> Tags { get; set; } = [];

    [JsonProperty("content")]
    public string Content { get; set; } = string.Empty;

    [JsonProperty("loop")]
    public bool Loop { get; set; } = false;

    public Macro Clone()
    {
        return (Macro)MemberwiseClone();
    }
}
