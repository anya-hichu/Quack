using Dalamud.Utility;
using Quack.Macros;
using Quack.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quack.Configs;

public class ConfigInfoTabState : IDisposable
{
    private ActionQueue ActionQueue { get; init; } = new();
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
        ActionQueue.Enqueue(() =>
        {
            FilteredMacroWithCommands = MacroSearch.Lookup(GetMacroWithCommands(), Filter);
        }); 
    }

    private IEnumerable<Macro> GetMacroWithCommands()
    {
        return CachedMacros.Where(m => !m.Command.IsNullOrWhitespace());
    }
}
