using Dalamud.Utility;
using Quack.Macros;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Quack.Configs;

public class ConfigInfoTabState : IDisposable
{
    private HashSet<Macro> CachedMacros { get; init; }
    private MacroTable MacroTable { get; init; }

    public string Filter { get; set; } = string.Empty;
    public List<Macro> FilteredMacroWithCommands = [];

    public ConfigInfoTabState(HashSet<Macro> cachedMacros, MacroTable macroTable)
    {
        CachedMacros = cachedMacros;
        MacroTable = macroTable;

        Update();
        MacroTable.OnChange += Update;
    }

    public void Dispose()
    {
        MacroTable.OnChange -= Update;
    }

    public void Update()
    {
        FilteredMacroWithCommands = MacroSearch.Lookup(CachedMacros.Where(m => !m.Command.IsNullOrWhitespace()), Filter);
    }
}
