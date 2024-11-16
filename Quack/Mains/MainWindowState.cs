using Dalamud.Plugin.Services;
using Quack.Macros;
using System;
using System.Collections.Generic;

namespace Quack.Mains;

public class MainWindowState : IDisposable
{
    private MacroTable MacroTable { get; init; }

    public string Query { get; set; } = string.Empty;
    public HashSet<Macro> FilteredMacros { get; set; } = [];

    public MainWindowState(MacroTable macroTable)
    {
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
        FilteredMacros = MacroTable.Search(Query);
    }
}
