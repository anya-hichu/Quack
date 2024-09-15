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

    private ICommandManager CommandManager { get; init; }
    private Config Config { get; init; }
    private MacroExecutor MacroExecutor { get; init; }

    public MacroCommands(ICommandManager commandManager, Config config, MacroExecutor macroExecutor)
    {
        CommandManager = commandManager;
        Config = config;
        MacroExecutor = macroExecutor;

        GetMacroWithCommands().ForEach(AddMacroHandler);

        Config.OnSave += UpdateMacroHandlers;
    }

    public void Dispose()
    {
        foreach (var command in RegisteredCommands)
        {
            RemoveHandler(command);
        }
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
        return new(Config.Macros.Where(m => !m.Command.IsNullOrWhitespace()));
    }
}
