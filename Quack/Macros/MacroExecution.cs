using Quack.Utils;
using System.Linq;
using System.Text.RegularExpressions;

namespace Quack.Macros;

public partial class MacroExecution(Macro macro, Config config, MacroExecutor macroExecutor)
{
    [GeneratedRegexAttribute(@"(?:^|[^{])(\{\d\})(?:[^}]|$)")]
    private static partial Regex ContentPlaceholderGeneratedRegex();

    public Macro Macro { get; init; } = macro;
    private Config Config { get; init; } = config;
    private MacroExecutor MacroExecutor { get; init; } = macroExecutor;

    public string Format { get; set; } = MacroExecutor.DEFAULT_FORMAT;

    public string Args { get; set; } = macro.Args;

    public string[] ParsedArgs { get; set; } = []; 

    public int CountDistinctContentPlaceholders()
    {
        var placeholderMatches = ContentPlaceholderGeneratedRegex().Matches(Macro.Content);
        return placeholderMatches.Select(m => m.Value).Distinct().Count();
    }

    public void ParseArgs()
    {
        ParsedArgs = Arguments.SplitCommandLine(Args);
    }

    public bool IsExecutable()
    {
        return CountDistinctContentPlaceholders() == ParsedArgs.Length;
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
