using System;

namespace Quack.Generators;

[Serializable]
public class GeneratorIpcConfig
{
    public string Name { get; set; } = string.Empty;
    public string Args { get; set; } = string.Empty;

    public GeneratorIpcConfig Clone()
    {
        return (GeneratorIpcConfig)MemberwiseClone();
    }
}
