using Dalamud;
using Dalamud.Game;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc.Internal;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using ImGuiNET;
using Lumina.Excel.Sheets;
using Quack.Chat;
using Quack.Configs;
using Quack.Generators;
using Quack.Ipcs;
using Quack.Macros;
using Quack.Mains;
using Quack.Schedulers;
using Quack.UI;
using Quack.Utils;
using SQLite;
using System;
using System.Diagnostics;
using System.IO;

namespace Quack;

public sealed class Plugin : IDalamudPlugin
{
    private static readonly Version TAG = typeof(Plugin).Assembly.GetName().Version!;
    private static readonly string RELEASES_URL = "https://github.com/anya-hichu/Quack/releases";
    private static readonly string TUTORIAL_PDF_URL = $"{RELEASES_URL}/download/{TAG}/Tutorial.pdf";

    private const string CommandName = "/quack";
    private const string CommandHelpMessage = $"Available subcommands for {CommandName} are main, config, exec and cancel";
    private const string CommandExecHelpMessage = $"Exec command syntax (supports double quoting): {CommandName} exec [Macro Name or Path or Command]( [Formatting (false/true/format)])?( [Argument Value])*";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog PluginLog { get; private set; } = null!;
    [PluginService] internal static ISigScanner SigScanner { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IKeyState KeyState { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static INotificationManager NotificationManager { get; private set; } = null!;

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
    private LifestreamIpc LifestreamIpc { get; init; }

    private MacroExecutionState MacroExecutionState { get; init; }
    private MacroExecutor MacroExecutor { get; init; }
    private MainWindowKeyBind MainWindowKeyBind { get; init; }
    private MacroCommands MacroCommands { get; init; }
    private SQLiteConnection DbConnection { get; init; }
    private MacroTable MacroTable { get; init; }
    private MacroSharedLock MacroSharedLock { get; init; }
    private ChatSender ChatSender { get; init; }
    private Debouncers Debouncers { get; init; }
    private SchedulerTriggers SchedulerTriggers { get; init; }

    public Plugin()
    {
        Config = PluginInterface.GetPluginConfig() as Config ?? new()
        {
            GeneratorConfigs = GeneratorConfig.GetDefaults()
        };
        #region deprecated
        ConfigMigrator.MigrateDatabasePathToV6(PluginInterface);
        #endregion
        var databasePath = Path.Combine(PluginInterface.GetPluginConfigDirectory(), $"{PluginInterface.InternalName}.db");
        DbConnection = new SQLiteConnection(databasePath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);

        MacroTable = new(DbConnection, PluginLog);
        MacroTable.MaybeCreateTable();

        var configMigrator = new ConfigMigrator(DbConnection, MacroTable);
        configMigrator.MaybeMigrate(Config);

        var cachedMacros = MacroTable.List();

        MacroSharedLock = new(Framework, PluginLog);
        var chatServer = new ChatServer(SigScanner);
        ChatSender = new(chatServer, Framework, MacroSharedLock, PluginLog);
        Debouncers = new(PluginLog);

        MacroExecutor = new(ChatSender, MacroSharedLock, PluginLog);
        var uiEvents = new UIEvents(PluginLog);
        MacroExecutionState = new MacroExecutionState(Config, MacroExecutor, uiEvents);

        MainWindow = new(cachedMacros, Config, MacroExecutionState, MacroExecutor, MacroTable, uiEvents)
        {
            TitleBarButtons = [new() { Icon = FontAwesomeIcon.Cog, ShowTooltip = () => ImGui.SetTooltip("Toggle Config Window"), Click = _ => ToggleConfigUI() }]
        };
        ConfigWindow = new(cachedMacros, Service<CallGate>.Get(), ChatSender, Config, CommandManager, databasePath, Debouncers, KeyState, MacroExecutionState, MacroExecutor, MacroTable, new(MacroTable, new()), PluginLog, NotificationManager, uiEvents)
        {
            TitleBarButtons = [
                new() { Icon = FontAwesomeIcon.InfoCircle, ShowTooltip = () => ImGui.SetTooltip("View Changelogs (Browser)"), Click = _ => OpenUrl(RELEASES_URL) },
                new() { Icon = FontAwesomeIcon.QuestionCircle, ShowTooltip = () => ImGui.SetTooltip("View Tutorial (Browser)"), Click = _ => OpenUrl(TUTORIAL_PDF_URL) },
                new() { Icon = FontAwesomeIcon.ListAlt, ShowTooltip = () => ImGui.SetTooltip("Toggle Search Window"), Click = _ => ToggleMainUI() },
            ]
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
        LifestreamIpc = new(PluginInterface, DataManager.GetExcelSheet<World>()!);

        MainWindowKeyBind = new(ToggleMainUI, Config, Framework, KeyState);
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
        LifestreamIpc.Dispose();

        MainWindowKeyBind.Dispose();
        MacroCommands.Dispose();

        DbConnection.Dispose();
        MacroSharedLock.Dispose();
        ChatSender.Dispose();
        Debouncers.Dispose();

        SchedulerTriggers.Dispose();
        MacroExecutionState.Dispose();
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

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo() { FileName = url, UseShellExecute = true });
    }
}
