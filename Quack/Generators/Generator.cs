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

public class Generator
{
    private static readonly string ENTRY_POINT = "main";

    private CallGate CallGate { get; init; }
    private GeneratorConfig GeneratorConfig { get; init; }
    private IPluginLog PluginLog { get; init; }
    private V8JsEngine? MaybeEngine { get; set; }

    public Generator(CallGate callGate, GeneratorConfig generatorConfig, IPluginLog pluginLog)
    {
        CallGate = callGate;
        GeneratorConfig = generatorConfig;
        PluginLog = pluginLog;
    }

    public HashSet<Macro> GenerateMacros()
    {
        try
        {
            MaybeEngine = new V8JsEngine(GetEngineSettings());
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
        return GeneratorConfig.IpcConfigs.Select(ipcConfig =>
        {
            if (IsStopped())
            {
                throw new JsInterruptedException("Cancelled manually while calling IPCs");
            }

            if (!CallGate.Gates.TryGetValue(ipcConfig.Name, out var channel))
            {
                throw new GeneratorException($"Could not find generator {GeneratorConfig.Name} IPC channel: {ipcConfig.Name}");
            }

            var args = JsonConvert.DeserializeObject<object[]>(ipcConfig.Args);
            PluginLog.Verbose($"Calling generator {GeneratorConfig.Name} IPC channel {channel.Name} with arguments: {ipcConfig.Args}");
            var returnValue = channel.InvokeFunc<object>(args);
            PluginLog.Verbose($"IPC channel {channel.Name} returned value: {returnValue}");
            return returnValue.GetType().IsGenericType ? returnValue : returnValue.ToString()!;
        }).ToArray();
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
            throw new JsInterruptedException("Cancelled manually while calling IPCs");
        }
        PluginLog.Verbose($"Calling generator {GeneratorConfig.Name} {ENTRY_POINT} function with: {string.Join(", ", args)}");

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
