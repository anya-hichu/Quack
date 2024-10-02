using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Quack.Windows;
using JavaScriptEngineSwitcher.Core;
using JavaScriptEngineSwitcher.Jint;
using Quack.Generators;
using Lumina.Excel.GeneratedSheets2;
using Quack.Ipcs;
using Dalamud.Game;
using Quack.Macros;
using Dalamud.Utility;
using Dalamud.Interface;
using Quack.Listeners;
using Quack.Utils;
using JavaScriptEngineSwitcher.V8;
using System.IO;
using System.Collections.Generic;
using SQLite;
namespace Quack;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/quack";
    private const string CommandHelpMessage = $"Available subcommands for {CommandName} are main, config, exec and cancel";
    private const string CommandExecHelpMessage = $"Exec command syntax (supports quoting): {CommandName} exec [Macro Name or Path or Command]( [Formatting (false/true/format)])?( [Argument Value])*";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog PluginLog { get; private set; } = null!;
    [PluginService] internal static ISigScanner SigScanner { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IKeyState KeyState { get; private set; } = null!;

    public readonly WindowSystem WindowSystem = new("Quack");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    private Config Config { get; init; }
    private MacroExecutor MacroExecutor { get; init; }
    private EmotesIpc EmotesIpc { get; init; }
    private GlamourerIpc GlamourerIpc { get; init; }
    private MacrosIpc MacrosIpc { get; init; }
    private PenumbraIpc PenumbraIpc { get; init; }
    private KeyBindListener KeyBindListener { get; init; }
    private MacroCommands MacroCommands { get; init; }
    private SQLiteConnection DbConnection { get; init; }
    private HashSet<Macro> CachedMacros { get; init; }
    private MacroTable MacroTable { get; init; }
    private MacroSharedLock MacroSharedLock { get; init; }

    public Plugin()
    {
        var databasePath = Path.Combine(PluginInterface.GetPluginLocDirectory(), $"{PluginInterface.InternalName}.db");
        DbConnection = new SQLiteConnection(databasePath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);

        MacroTable = new(DbConnection, PluginLog);
        MacroTable.MaybeCreateTable();

        var engineFactories = JsEngineSwitcher.Current.EngineFactories;
        engineFactories.Add(new V8JsEngineFactory());
        engineFactories.Add(new JintJsEngineFactory(new()
        {
            DisableEval = true,
            StrictMode = true
        }));
        
        Config = PluginInterface.GetPluginConfig() as Config ?? new(GeneratorConfig.GetDefaults());

        var migrator = new Migrator(DbConnection, MacroTable);
        migrator.ExecuteMigrations(Config);

        CachedMacros = MacroTable.List();

        MacroSharedLock = new(Framework, PluginLog);
        MacroExecutor = new(new(SigScanner), MacroSharedLock, PluginLog);
        MainWindow = new(CachedMacros, MacroExecutor, MacroTable, Config, PluginLog)
        {
            TitleBarButtons = [BuildTitleBarButton(FontAwesomeIcon.Cog, ToggleConfigUI)]
        };
        ConfigWindow = new(CachedMacros, MacroExecutor, MacroTable, KeyState, Config, PluginLog)
        {
            TitleBarButtons = [BuildTitleBarButton(FontAwesomeIcon.ListAlt, ToggleMainUI)]
        };

        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(ConfigWindow);

        CommandManager.AddHandler(CommandName, new(OnCommand)
        {
            HelpMessage = CommandHelpMessage
        });

        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

        EmotesIpc = new(PluginInterface, DataManager.GetExcelSheet<Emote>());
        GlamourerIpc = new(PluginInterface, PluginLog);
        MacrosIpc = new(PluginInterface);
        PenumbraIpc = new(PluginInterface, PluginLog);

        KeyBindListener = new(Framework, Config, ToggleMainUI);
        MacroCommands = new(CachedMacros, ChatGui, Config, CommandManager, MacroExecutor, MacroTable);
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);

        EmotesIpc.Dispose();
        GlamourerIpc.Dispose();
        MacrosIpc.Dispose();
        PenumbraIpc.Dispose();

        KeyBindListener.Dispose();
        MacroCommands.Dispose();

        DbConnection.Dispose();
        MacroSharedLock.Dispose();
    }

    private void OnCommand(string command, string args)
    {
        var arguments = Arguments.SplitCommandLine(args);
        if (arguments.Length == 1)
        {
            var subcommand = arguments[0];
            if (subcommand == "main")
            {
                ToggleMainUI();
            }
            else if (subcommand == "config")
            {
                ToggleConfigUI();
            }
            else if (subcommand == "exec")
            {
                ChatGui.Print(CommandExecHelpMessage);
            }
            else if (subcommand == "cancel")
            {
                MacroExecutor.CancelTasks();
            }
            else
            {
                ChatGui.PrintError(CommandHelpMessage);
            }
        }
        else if (arguments.Length == 2)
        {
            if (arguments[0] != "exec")
            {
                ChatGui.PrintError(CommandHelpMessage);
                return;
            }

            var macroLookup = arguments[1];
            var macro = MacroTable.FindBy("path", macroLookup);
            macro = macro ?? MacroTable.FindBy("name", macroLookup);
            macro = macro ?? MacroTable.FindBy("command", macroLookup);

            if (macro == null)
            {
                ChatGui.Print($"No macro found with lookup '{macroLookup}'");
                return;
            }

            var macroExecution = new MacroExecution(macro, Config, MacroExecutor);
            macroExecution.ParseArgs();

            if (macroExecution.IsExecutable())
            {
                macroExecution.ExecuteTask();
            }
            else
            {
                ChatGui.PrintError(MacroExecutionGui.GetNonExecutableMessage(macroExecution));
            }
        }
        else if (arguments.Length >= 3)
        {
            if (arguments[0] != "exec")
            {
                ChatGui.Print(CommandHelpMessage);
                return;
            }

            var macroLookup = arguments[1];
            var macro = MacroTable.FindBy("path", macroLookup);
            macro = macro ?? MacroTable.FindBy("name", macroLookup);
            macro = macro ?? MacroTable.FindBy("command", macroLookup);

            if (macro == null)
            {
                ChatGui.PrintError($"No macro found with lookup '{macroLookup}'");
                return;
            }

            var formatting = arguments[2];
            var macroExecution = new MacroExecution(macro, Config, MacroExecutor);
            if (arguments.Length > 3)
            {
                macroExecution.ParsedArgs = arguments[3..];
            } 
            else
            {
                // Default args from macro
                macroExecution.ParseArgs();
            }
                    
            if (formatting == "true")
            {
                macroExecution.UseConfigFormat();
            }
            else if (formatting == "false")
            {
                macroExecution.Format = MacroExecutor.DEFAULT_FORMAT;
            }
            else if (!formatting.IsNullOrEmpty())
            {
                macroExecution.Format = formatting;
            }
            else
            {
                ChatGui.PrintError(CommandExecHelpMessage);
                return;
            }

            if (macroExecution.IsExecutable())
            {
                macroExecution.ExecuteTask();
            } 
            else
            {
                ChatGui.PrintError(MacroExecutionGui.GetNonExecutableMessage(macroExecution));
            }
        } 
        else
        {
            ChatGui.Print(CommandHelpMessage);
        }
    }



    private void DrawUI() => WindowSystem.Draw();
    private void ToggleConfigUI() => ConfigWindow.Toggle();
    private void ToggleMainUI() => MainWindow.Toggle();

    private static Window.TitleBarButton BuildTitleBarButton(FontAwesomeIcon icon, System.Action callback)
    {
        Window.TitleBarButton button = new();
        button.Icon = icon;
        button.Click = (_) => callback();
        return button;
    }
}
