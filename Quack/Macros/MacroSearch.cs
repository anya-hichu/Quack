using System.Collections.Generic;
using System;
using System.Linq;

namespace Quack.Macros;
public class MacroSearch
{
    private static readonly StringComparison MATCH_FLAGS = StringComparison.InvariantCultureIgnoreCase;
    public static List<Macro> Lookup(IEnumerable<Macro> macros, string filter)
    {
        return new(macros
            .Where(m => CountTokenMatches(m, filter) > 0)
            .OrderBy(m => -CountTokenMatches(m, filter))
            .ThenBy(x => x.Name));
    }

    private static int CountTokenMatches(Macro macro, string filter)
    {
        var tokens = filter.ToLowerInvariant().Split(" ");
        return tokens.Count(t => macro.Name.Contains(t, MATCH_FLAGS) ||
                               macro.Path.Contains(t, MATCH_FLAGS) ||
                               macro.Command.Contains(t, MATCH_FLAGS) ||
                               macro.Tags.Any(tag => tag.Contains(t, MATCH_FLAGS)));
    }
}