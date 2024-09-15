using System;


namespace Quack.Generators;

[Serializable]
public class GeneratorIpcConfig
{
    public string Name { get; set; } = string.Empty;
    public string Args { get; set; } = string.Empty;

    public GeneratorIpcConfig(){}

    public GeneratorIpcConfig(string name)
    {
        Name = name;
    }

    public GeneratorIpcConfig(string name, string args)
    {
        Name = name;
        Args = args;
    }
}
