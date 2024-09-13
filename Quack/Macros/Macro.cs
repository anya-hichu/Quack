using Newtonsoft.Json;
using System;

namespace Quack.Macros;

[Serializable]
public class Macro
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("path")]
    public string Path { get; set; } = string.Empty;

    [JsonProperty("tags")]
    public string[] Tags { get; set; } = [];

    [JsonProperty("content")]
    public string Content { get; set; } = string.Empty;

    [JsonProperty("command")]
    public string Command { get; set; } = string.Empty;
}
