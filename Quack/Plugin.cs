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

namespace Quack;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog PluginLog { get; private set; } = null!;
    [PluginService] internal static ISigScanner SigScanner { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IKeyState KeyState { get; private set; } = null!;


    private const string CommandName = "/quack";
    private const string CommandHelpMessage = $"Available subcommands for {CommandName} are main, config and exec";
    private const string CommandExecHelpMessage = $"Exec command syntax (supports quoting): {CommandName} exec [Macro Name or Path]( [Formatting (false/true/format)])?";


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
    
    public Plugin()
    {
        var engineSwitcher = JsEngineSwitcher.Current;
        engineSwitcher.EngineFactories.Add(new JintJsEngineFactory(new()
        {
            DisableEval = true,
            StrictMode = true
        }));
        engineSwitcher.EngineFactories.Add(new V8JsEngineFactory(new()));

        Config = PluginInterface.GetPluginConfig() as Config ?? new(GeneratorConfig.GetDefaults());
        Config.Macros = new(Config.Macros, MacroComparer.INSTANCE);
        Config.MigrateIfNeeded();

        MacroExecutor = new(Framework, new(SigScanner), PluginLog);

        MainWindow = new(MacroExecutor, Config, PluginLog)
        {
            TitleBarButtons = [BuildTitleBarButton(FontAwesomeIcon.Cog, ToggleConfigUI)]
        };
        ConfigWindow = new(MacroExecutor, KeyState, Config, PluginLog)
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
        MacroCommands = new(CommandManager, Config, MacroExecutor);
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
            else
            {
                ChatGui.PrintError(CommandHelpMessage);
            }
        }
        else if (arguments.Length == 2)
        {
            if (arguments[0] == "exec")
            {
                var nameOrPath = arguments[1];
                var macro = MacroSearch.FindByNameOrPath(Config.Macros, nameOrPath);
                if (macro != null)
                {
                    MacroExecutor.ExecuteTask(macro);
                } 
                else
                {
                    ChatGui.Print($"No macro found with name or path: {nameOrPath}");
                }
            } 
            else
            {
                ChatGui.PrintError(CommandHelpMessage);
            }
        }
        else if (arguments.Length == 3)
        {
            if (arguments[0] == "exec")
            {
                var nameOrPath = arguments[1];
                var macro = MacroSearch.FindByNameOrPath(Config.Macros, nameOrPath);
                if (macro != null)
                {
                    var formatting = arguments[2];
                    if (formatting == "true")
                    {
                        MacroExecutor.ExecuteTask(macro, Config.ExtraCommandFormat);
                    }
                    else if (formatting == "false")
                    {
                        MacroExecutor.ExecuteTask(macro);
                    }
                    else if (!formatting.IsNullOrEmpty())
                    {
                        MacroExecutor.ExecuteTask(macro, formatting);
                    } 
                    else
                    {
                        ChatGui.PrintError(CommandExecHelpMessage);
                    }
                }
                else
                {
                    ChatGui.PrintError($"No macro found with name or path: {nameOrPath}");
                }
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
