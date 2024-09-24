using Quack.Utils;
using System.Text.RegularExpressions;

namespace Quack.Macros;

public partial class MacroExecution(Macro macro, Config config, MacroExecutor macroExecutor)
{
    [GeneratedRegexAttribute(@"(?:^|[^{])\{\d\}(?:[^}]|$)")]
    private static partial Regex ContentPlaceholderGeneratedRegex();

    public Macro Macro { get; init; } = macro;
    private Config Config { get; init; } = config;
    private MacroExecutor MacroExecutor { get; init; } = macroExecutor;

    public string Format { get; set; } = MacroExecutor.DEFAULT_FORMAT;

    public string Args { get; set; } = macro.Args;

    public string[] ParsedArgs { get; set; } = []; 

    public int CountContentPlaceholders()
    {
        return ContentPlaceholderGeneratedRegex().Count(Macro.Content);
    }

    public void ParseArgs()
    {
        ParsedArgs = Arguments.SplitCommandLine(Args);
    }

    public bool IsExecutable()
    {
        return CountContentPlaceholders() == ParsedArgs.Length;
    }

    public void UseConfigFormat()
    {
        Format = Config.ExtraCommandFormat;
    }

    public void ExecuteTask()
    {
        MacroExecutor.ExecuteTask(Macro, Format, ParsedArgs);
    }
}
