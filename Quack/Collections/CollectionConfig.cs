using Dalamud.Interface.Colors;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Quack.Collections;

[Serializable]
public class CollectionConfig
{
    public string Name { get; set; } = string.Empty;
    public HashSet<string> Tags { get; set; } = [];
    public Vector4 Color { get; set; } = ImGuiColors.DalamudWhite;
    public bool Selectable { get; set; } = true;
}
