using Dalamud.Game.Command;
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
using Quack.Utils;

namespace Quack;
public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog PluginLog { get; private set; } = null!;
    [PluginService] internal static ISigScanner SigScanner { get; private set; } = null!;
    
    private const string CommandName = "/quack";
    private const string CommandHelpMessage = $"Available subcommands for {CommandName} are main and config";

    public readonly WindowSystem WindowSystem = new("Quack");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }
    public Config Config { get; init; }

    private EmotesIpc EmotesIpc { get; init; }
    private MacrosIpc MacrosIpc { get; init; }

    public Plugin()
    {
        var engineSwitcher = JsEngineSwitcher.Current;
        engineSwitcher.EngineFactories.Add(new JintJsEngineFactory(new()
        {
            DisableEval = true,
            StrictMode = true
        }));
        engineSwitcher.DefaultEngineName = JintJsEngine.EngineName;

        Config = PluginInterface.GetPluginConfig() as Config ?? new Config(new(GeneratorConfig.DEFAULTS));

        ConfigWindow = new ConfigWindow(PluginInterface, Config, PluginLog);
        MainWindow = new MainWindow(Config, new(SigScanner), PluginLog);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = CommandHelpMessage
        });

        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;

        EmotesIpc = new(PluginInterface, DataManager.GetExcelSheet<Emote>());
        MacrosIpc = new(PluginInterface);
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);

        EmotesIpc.Dispose();
        MacrosIpc.Dispose();
    }

    private void OnCommand(string command, string args)
    {
        var subcommand = args.Split(" ", 2)[0];

        if (subcommand == "main")
        {
            ToggleMainUI();
        }
        else if (subcommand == "config")
        {
            ToggleConfigUI();
        }
        else
        {
            ChatGui.Print(CommandHelpMessage);
        }
    }

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();
}
