using Dalamud;
using Dalamud.Plugin.Ipc.Internal;
using Dalamud.Plugin.Services;
using JavaScriptEngineSwitcher.Core;
using JavaScriptEngineSwitcher.V8;
using Newtonsoft.Json;
using Quack.Macros;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quack.Generators;

public class Generator(GeneratorConfig generatorConfig, IPluginLog pluginLog)
{
    private static readonly string ENTRY_POINT = "main";

    private GeneratorConfig GeneratorConfig { get; init; } = generatorConfig;
    private IPluginLog PluginLog { get; init; } = pluginLog;
    private V8JsEngine? MaybeEngine { get; set; }

    public HashSet<Macro> GenerateMacros()
    {
        try
        {
            MaybeEngine = new V8JsEngine(GetEngineSettings());
            MaybeEngine.EmbedHostType("IPC", typeof(GeneratorIpcCaller));

            var args = CallIpcs();
            var macros = CallFunction<Macro>(ENTRY_POINT, args);
            return new(macros, MacroComparer.INSTANCE);
        }
        catch (Exception e) when(e is not GeneratorException)
        {
            throw new GeneratorException($"Exception occured while executing {GeneratorConfig.Name} generator script", e);
        }
        finally
        {
            MaybeEngine?.Dispose();
            MaybeEngine = null;
        } 
    }

    private object[] CallIpcs()
    {
        return [.. GeneratorConfig.IpcConfigs.Select(ipcConfig =>
        {
            if (IsStopped())
            {
                throw new JsInterruptedException("Cancelled manually while calling IPCs");
            }

            var args = JsonConvert.DeserializeObject<object[]>(ipcConfig.Args);
            return GeneratorIpcCaller.call(ipcConfig.Name, args!);
        })];
    }

    private T[] CallFunction<T>(string name, object[] args)
    {
        if (MaybeEngine == null)
        {
            throw new JsInterruptedException("Cancelled manually before calling script");
        }
        PluginLog.Debug($"Executing generator {GeneratorConfig.Name} script with engine {MaybeEngine.Name} ({MaybeEngine.Version})");
        MaybeEngine.Execute(GeneratorConfig.Script);
        if (IsStopped())
        {
            throw new JsInterruptedException($"Cancelled manually while calling {ENTRY_POINT} function");
        }
        var entitiesJson = MaybeEngine.CallFunction<string>(name, args);
        var entities = JsonConvert.DeserializeObject<T[]>(entitiesJson);
        if (entities == null)
        {
            throw new GeneratorException($"Invalid JSON format returned by generator {GeneratorConfig.Name} {ENTRY_POINT} function: {entitiesJson}");
        }
        PluginLog.Debug($"Successfully generated {entities.Length} macros using {GeneratorConfig.Name} generator");
        return entities;
    }

    private V8Settings GetEngineSettings()
    {
        return new()
        {
            EnableDebugging = GeneratorConfig.AwaitDebugger, 
            AwaitDebuggerAndPauseOnStart = GeneratorConfig.AwaitDebugger
        };
    }

    public bool IsStopped()
    {
       return MaybeEngine == null;
    }

    public void Cancel()
    {
        MaybeEngine?.Interrupt();
        MaybeEngine?.Dispose();
        MaybeEngine = null;
    }
}
