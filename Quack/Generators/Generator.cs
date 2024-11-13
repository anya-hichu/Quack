using Dalamud;
using Dalamud.Plugin.Ipc.Internal;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using JavaScriptEngineSwitcher.Core;
using Newtonsoft.Json;
using Quack.Macros;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quack.Generators;

public class Generator(GeneratorConfig generatorConfig, IJsEngine jsEngine, IPluginLog pluginLog)
{
    private static readonly string ENTRY_POINT = "main";

    private GeneratorConfig GeneratorConfig { get; init; } = generatorConfig;
    private IJsEngine JsEngine { get; init; } = jsEngine;
    private IPluginLog PluginLog { get; init; } = pluginLog;

    public HashSet<Macro> Execute()
    {
        var args = CallIpcs();
        var macros = CallFunction<Macro>(ENTRY_POINT, args);
        return new(macros, MacroComparer.INSTANCE);
    }

    private object[] CallIpcs()
    {
        return GeneratorConfig.IpcConfigs.Select(ipcConfig =>
        {
            if (!Service<CallGate>.Get().Gates.TryGetValue(ipcConfig.Name, out var channel))
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
        try
        {
            if (GeneratorConfig.Script.IsNullOrWhitespace())
            {
                throw new GeneratorException($"Empty script for {GeneratorConfig.Name} generator");
            }
            PluginLog.Debug($"Executing generator {GeneratorConfig.Name} script");
            JsEngine.Execute(GeneratorConfig.Script);
            PluginLog.Verbose($"Calling generator {GeneratorConfig.Name} {ENTRY_POINT} function with: {string.Join(", ", args)}");
            var entitiesJson = JsEngine.CallFunction<string>(name, args);
            var entities = JsonConvert.DeserializeObject<T[]>(entitiesJson);
            if (entities == null)
            {
                throw new GeneratorException($"Invalid JSON format returned by generator {GeneratorConfig.Name} {ENTRY_POINT} function: {entitiesJson}");
            }
            PluginLog.Debug($"Successfully generated {entities.Length} macros using {GeneratorConfig.Name} generator");
            return entities;
        }
        catch (Exception e) when (e is not GeneratorException)
        {
            throw new GeneratorException($"Exception occured while executing {GeneratorConfig.Name} generator script", e);
        }
    }
}
