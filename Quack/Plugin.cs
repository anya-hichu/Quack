using Dalamud.Game;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using JavaScriptEngineSwitcher.Core;
using JavaScriptEngineSwitcher.Jint;
using JavaScriptEngineSwitcher.V8;
using Lumina.Excel.Sheets;
using Quack.Chat;
using Quack.Configs;
using Quack.Generators;
using Quack.Ipcs;
using Quack.Macros;
using Quack.Mains;
using Quack.Schedulers;
using Quack.Utils;
using SQLite;
using System.IO;

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
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;

    public readonly WindowSystem WindowSystem = new("Quack");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    private Config Config { get; init; }

    private CustomMacrosIpc CustomMacrosIpc { get; init; }
    private EmotesIpc EmotesIpc { get; init; }
    private DalamudIpc DalamudIpc { get; init; }
    private GlamourerIpc GlamourerIpc { get; init; }
    private LocalPlayerIpc LocalPlayerIpc { get; init; }
    private MacrosIpc MacrosIpc { get; init; }
    private PenumbraIpc PenumbraIpc { get; init; }

    private MacroExecutor MacroExecutor { get; init; }
    private MainKeyBind MainKeyBind { get; init; }
    private MacroCommands MacroCommands { get; init; }
    private SQLiteConnection DbConnection { get; init; }
    private MacroTable MacroTable { get; init; }
    private MacroSharedLock MacroSharedLock { get; init; }
    private ChatSender ChatSender { get; init; }
    private Debouncers Debouncers { get; init; }
    private SchedulerTriggers SchedulerTriggers { get; init; }

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
        
        Config = PluginInterface.GetPluginConfig() as Config ?? new()
        {
            GeneratorConfigs = GeneratorConfig.GetDefaults()
        };

        var migrator = new ConfigMigrator(DbConnection, MacroTable);
        migrator.ExecuteMigrations(Config);

        var cachedMacros = MacroTable.List();

        MacroSharedLock = new(Framework, PluginLog);
        var chatServer = new ChatServer(SigScanner);
        ChatSender = new(chatServer, Framework, MacroSharedLock, PluginLog);
        
        MacroExecutor = new(ChatSender, MacroSharedLock, PluginLog);
        var macroExecutionButton = new MacroExecutionButton(Config, MacroExecutor);
        Debouncers = new(PluginLog);

        MainWindow = new(cachedMacros, Config, macroExecutionButton, MacroExecutor, MacroTable, PluginLog)
        {
            TitleBarButtons = [new() { Icon = FontAwesomeIcon.Cog, Click = _ => ToggleConfigUI() }]
        };
        ConfigWindow = new(cachedMacros, ChatSender, Config, Debouncers, KeyState, macroExecutionButton, MacroExecutor, MacroTable, new(MacroTable, new()), PluginLog)
        {
            TitleBarButtons = [new() { Icon = FontAwesomeIcon.ListAlt, Click = _ => ToggleMainUI() }]
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

        CustomMacrosIpc = new(cachedMacros, PluginInterface);
        EmotesIpc = new(PluginInterface, DataManager.GetExcelSheet<Emote>()!);
        DalamudIpc = new(PluginInterface);
        GlamourerIpc = new(PluginInterface, PluginLog);
        LocalPlayerIpc = new(PluginInterface, ClientState);
        MacrosIpc = new(PluginInterface);
        PenumbraIpc = new(PluginInterface, PluginLog);

        MainKeyBind = new(Framework, Config, ToggleMainUI);
        MacroCommands = new(cachedMacros, ChatGui, Config, CommandManager, MacroExecutor, MacroTable);
        SchedulerTriggers = new(Config, Framework, chatServer, PluginLog);
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);

        CustomMacrosIpc.Dispose();
        EmotesIpc.Dispose();
        DalamudIpc.Dispose();
        GlamourerIpc.Dispose();
        LocalPlayerIpc.Dispose();
        MacrosIpc.Dispose();
        PenumbraIpc.Dispose();

        MainKeyBind.Dispose();
        MacroCommands.Dispose();

        DbConnection.Dispose();
        MacroSharedLock.Dispose();
        ChatSender.Dispose();
        Debouncers.Dispose();

        SchedulerTriggers.Dispose();
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

            if (!TryFindMacro(arguments[1], out var macro))
            {
                return;
            }

            var macroExecution = new MacroExecution(macro!, Config, MacroExecutor);
            macroExecution.ParseArgs();

            if (macroExecution.IsExecutable())
            {
                macroExecution.ExecuteTask();
            }
            else
            {
                ChatGui.PrintError(macroExecution.GetNonExecutableMessage());
            }
        }
        else if (arguments.Length >= 3)
        {
            if (arguments[0] != "exec")
            {
                ChatGui.Print(CommandHelpMessage);
                return;
            }

            if (!TryFindMacro(arguments[1], out var macro))
            {
                return;
            }

            var formatting = arguments[2];
            var macroExecution = new MacroExecution(macro!, Config, MacroExecutor);
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
                ChatGui.PrintError(macroExecution.GetNonExecutableMessage());
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

    private bool TryFindMacro(string term, out Macro? macro)
    {
        if (!MacroTable.TryFindByTerm(term, out macro))
        {
            ChatGui.PrintError($"No macro found with name, path or command matching '{term}'");
            return false;
        }
        return true;
    }
}
