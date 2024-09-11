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
using System.Linq;
using Dalamud.Utility;
using Dalamud.Interface;
using Quack.Listeners;

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

    public readonly WindowSystem WindowSystem = new("Quack");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    private Config Config { get; init; }
    private Executor Executor { get; init; }
    private EmotesIpc EmotesIpc { get; init; }
    private GlamourerIpc GlamourerIpc { get; init; }
    private MacrosIpc MacrosIpc { get; init; }
    private PenumbraIpc PenumbraIpc { get; init; }

    private KeyBindListener KeyBindListener { get; init; }
    

    public Plugin()
    {
        KeyState.GetValidVirtualKeys();

        var engineSwitcher = JsEngineSwitcher.Current;
        engineSwitcher.EngineFactories.Add(new JintJsEngineFactory(new()
        {
            DisableEval = true,
            StrictMode = true
        }));
        engineSwitcher.DefaultEngineName = JintJsEngine.EngineName;

        Config = PluginInterface.GetPluginConfig() as Config ?? new(GeneratorConfig.GetDefaults());
        Config.Macros = new(Config.Macros, new MacroComparer());

        Executor = new(Framework, new(SigScanner), PluginLog);

        MainWindow = new(Executor, Config, PluginLog)
        {
            TitleBarButtons = [BuildTitleBarButton(FontAwesomeIcon.Cog, ToggleConfigUI)]
        };
        ConfigWindow = new(Executor, KeyState, Config, PluginLog)
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
    }

    private void OnCommand(string command, string args)
    {
        var parts = args.Split(" ", 2).ToList();
        var subcommand = parts[0];

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
            if (parts.Count == 2)
            {
                var subparts = parts[1].Split(";");
                Config.Macros.FindFirst(m => m.Name == subparts[0] || m.Path == subparts[0], out var macro);
                if(macro != null)
                {
                    if (subparts.Length == 2)
                    {
                        var formatting = subparts[1].Trim();
                        if (formatting == "true")
                        {
                            Executor.ExecuteTask(macro, Config.CommandFormat);
                        }
                        else if (formatting == "false")
                        {
                            Executor.ExecuteTask(macro);
                        } 
                        else if (!formatting.IsNullOrEmpty())
                        {
                            Executor.ExecuteTask(macro, formatting);
                        } 
                        else
                        {
                            Executor.ExecuteTask(macro);
                        }
                    } 
                    else
                    {
                        Executor.ExecuteTask(macro);
                    }
                }
                else
                {
                    ChatGui.Print($"No macro found with name or path: {parts[1]}");
                }
            }
            else
            {
                ChatGui.Print($"Supported format: {CommandName} execute <macro name or path>; <formatting>");
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
