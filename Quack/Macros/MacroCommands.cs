using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quack.Macros;
public class MacroCommands: IDisposable
{
    private HashSet<string> RegisteredCommands { get; init; } = [];

    private HashSet<Macro> CachedMacros { get; init; }
    private ICommandManager CommandManager { get; init; }
    private MacroExecutor MacroExecutor { get; init; }
    private MacroTable MacroTable { get; init; }

    public MacroCommands(HashSet<Macro> cachedMacros, ICommandManager commandManager, MacroExecutor macroExecutor, MacroTable macroTable)
    {
        CachedMacros = cachedMacros;
        CommandManager = commandManager;
        MacroExecutor = macroExecutor;
        MacroTable = macroTable;

        GetMacroWithCommands().ForEach(AddMacroHandler);

        MacroTable.OnChange += UpdateMacroHandlers;
    }

    public void Dispose()
    {
        foreach (var command in RegisteredCommands)
        {
            RemoveHandler(command);
        }

        MacroTable.OnChange -= UpdateMacroHandlers;
    }

    private void UpdateMacroHandlers()
    {
        var macroWithCommands = GetMacroWithCommands();
        var deletedCommands = RegisteredCommands.Except(macroWithCommands.Select(m => m.Command));

        foreach (var deletedCommand in deletedCommands)
        {
            RemoveHandler(deletedCommand);
        }

        foreach (var macro in macroWithCommands)
        {
            if (!RegisteredCommands.Contains(macro.Command))
            {
                AddMacroHandler(macro);
            }
        }
    }

    private void AddMacroHandler(Macro macro)
    {
        CommandManager.AddHandler(macro.Command, BuildCommandInfo(macro));
        RegisteredCommands.Add(macro.Command);
    }

    private void RemoveMacroHandler(Macro macro)
    {
        RemoveHandler(macro.Command);
    }

    private void RemoveHandler(string command)
    {
        CommandManager.RemoveHandler(command);
        RegisteredCommands.Remove(command);
    }

    private CommandInfo BuildCommandInfo(Macro macro)
    {

        return new((command, args) => MacroExecutor.ExecuteTask(macro))
        {
            HelpMessage = $"Execute macro {macro.Name} ({macro.Path}) using {macro.Command}",
            ShowInHelp = false
        };
    }

    private List<Macro> GetMacroWithCommands()
    {
        return new(CachedMacros.Where(m => !m.Command.IsNullOrWhitespace()));
    }
}
