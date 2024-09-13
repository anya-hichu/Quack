using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quack.Macros;
public class MacroCommands: IDisposable
{
    private Dictionary<Macro, string> MacroToRegisteredCommand { get; init; } = new(0, MacroComparer.INSTANCE);

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
        MacroToRegisteredCommand.Keys.ToList().ForEach(RemoveMacroHandler);
    }

    private void UpdateMacroHandlers()
    {
        var macroWithCommands = GetMacroWithCommands();
        foreach (var deletedMacro in MacroToRegisteredCommand.Keys.Except(macroWithCommands, MacroComparer.INSTANCE))
        {
            RemoveMacroHandler(deletedMacro);
        }

        foreach (var macro in macroWithCommands)
        {
            if (MacroToRegisteredCommand.TryGetValue(macro, out var command))
            {
                if(macro.Command != command)
                {
                    CommandManager.RemoveHandler(command);
                    MacroToRegisteredCommand.Remove(macro);
                    AddMacroHandler(macro);
                }
            }
            else
            {
                AddMacroHandler(macro);
            }
        }
    }

    private void AddMacroHandler(Macro macro)
    {
        CommandManager.AddHandler(macro.Command, BuildCommandInfo(macro));
        MacroToRegisteredCommand.Add(macro, macro.Command);
    }

    private void RemoveMacroHandler(Macro macro)
    {
        if(MacroToRegisteredCommand.TryGetValue(macro, out var command))
        {
            CommandManager.RemoveHandler(command);
            MacroToRegisteredCommand.Remove(macro);
        }
    }

    private CommandInfo BuildCommandInfo(Macro macro)
    {
        return new((string command, string args) =>
        {
            MacroExecutor.ExecuteTask(macro);
        });
    }

    private List<Macro> GetMacroWithCommands()
    {
        return new(Config.Macros.Where(m => !m.Command.IsNullOrWhitespace()));
    }
}
