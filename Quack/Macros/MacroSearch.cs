using System.Collections.Generic;
using System;
using System.Linq;
using Dalamud.Utility;

namespace Quack.Macros;
public class MacroSearch
{
    private static readonly StringComparison MATCH_FLAGS = StringComparison.InvariantCultureIgnoreCase;
    public static List<Macro> Lookup(IEnumerable<Macro> macros, string filter)
    {
        return new(macros.Where(m => tokenMatches(m, filter)).OrderBy(m => matchLengthRatio(m, filter)));
    }

    private static bool tokenMatches(Macro macro, string filter)
    {
        var tokens = filter.ToLowerInvariant().Split(" ").Where(t => !t.IsNullOrWhitespace());
        return tokens.All(t => macro.Name.Contains(t, MATCH_FLAGS) ||
                               macro.Path.Contains(t, MATCH_FLAGS) ||
                               macro.Command.Contains(t, MATCH_FLAGS) ||
                               macro.Tags.Any(tag => tag.Contains(t, MATCH_FLAGS)));
    }

    private static double matchLengthRatio(Macro macro, string filter)
    {
        var length = macro.Name.Length + macro.Path.Length + macro.Command.Length + string.Join("", macro.Tags).Length;
        var ratio = (double)length / filter.Length;
        return Math.Round(ratio, 0);
    }
}
