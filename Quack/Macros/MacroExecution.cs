using Quack.Configs;
using Quack.Utils;
using System.Linq;
using System.Text.RegularExpressions;

namespace Quack.Macros;

public partial class MacroExecution(Macro macro, Config config, MacroExecutor macroExecutor)
{
    [GeneratedRegexAttribute(@"(?<!\{)\{(?<index>\d+?)\}(?!\})")]
    private static partial Regex ContentPlaceholderGeneratedRegex();

    public Macro Macro { get; init; } = macro;
    private Config Config { get; init; } = config;
    private MacroExecutor MacroExecutor { get; init; } = macroExecutor;

    public string Format { get; set; } = MacroExecutor.DEFAULT_FORMAT;

    public string Args { get; set; } = macro.Args;

    public string[] ParsedArgs { get; set; } = []; 

    public int RequiredArgsLength()
    {
        var matches = ContentPlaceholderGeneratedRegex().Matches(Macro.Content);
        return matches.Count > 0 ? matches.Max(m => int.Parse(m.Groups["index"].Value)) + 1 : 0;
    }

    public void ParseArgs()
    {
        ParsedArgs = Arguments.SplitCommandLine(Args);
    }

    public bool IsExecutable()
    {
        return RequiredArgsLength() == ParsedArgs.Length;
    }

    public void UseConfigFormat()
    {
        Format = Config.ExtraCommandFormat;
    }

    public void ExecuteTask()
    {
        MacroExecutor.ExecuteTask(Macro, Format, ParsedArgs);
    }

    public string GetNonExecutableMessage()
    {
        return $"Expected {RequiredArgsLength()} argument(s) for macro '{Macro.Name}' (parsed {ParsedArgs.Length})";
    }
}
