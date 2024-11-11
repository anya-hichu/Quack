using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Quack.Utils;
using Quack.UI.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quack.Macros;
public class MacroCommands: IDisposable
{
    private HashSet<string> RegisteredCommands { get; init; } = [];

    private HashSet<Macro> CachedMacros { get; init; }
    private IChatGui ChatGui { get; init; }
    private Config Config { get; init; }
    private ICommandManager CommandManager { get; init; }
    private MacroExecutor MacroExecutor { get; init; }
    private MacroTable MacroTable { get; init; }

    public MacroCommands(HashSet<Macro> cachedMacros, IChatGui chatGui, Config config, ICommandManager commandManager, MacroExecutor macroExecutor, MacroTable macroTable)
    {
        CachedMacros = cachedMacros;
        ChatGui = chatGui;
        Config = config;
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
        return new(BuildCommand(macro))
        {
            HelpMessage = $"Execute macro '{macro.Name}' ({macro.Path}) using '{macro.Command}'",
            ShowInHelp = false
        };
    }

    private IReadOnlyCommandInfo.HandlerDelegate BuildCommand(Macro macro)
    {
        return (command, args) =>
        {
            var macroExecution = new MacroExecution(macro, Config, MacroExecutor);
            var parsedArgs = Arguments.SplitCommandLine(args);

            if (parsedArgs.Length > 0)
            {
                macroExecution.ParsedArgs = parsedArgs;
            } 
            else
            {
                // Default args
                macroExecution.ParseArgs();
            }

            if (macroExecution.IsExecutable())
            {
                macroExecution.ExecuteTask();
            } 
            else
            {
                ChatGui.PrintError(MacroExecutionHelper.GetNonExecutableMessage(macroExecution));
            }
        };
    }

    private List<Macro> GetMacroWithCommands()
    {
        return new(CachedMacros.Where(m => !m.Command.IsNullOrWhitespace()));
    }
}
