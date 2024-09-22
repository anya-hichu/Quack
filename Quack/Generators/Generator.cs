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
    private GeneratorConfig GeneratorConfig { get; init; } = generatorConfig;
    private IJsEngine JsEngine { get; init; } = jsEngine;
    private IPluginLog PluginLog { get; init; } = pluginLog;

    public HashSet<Macro> Execute()
    {
        var args = CallIpcs();
        return new(CallFunction(args), MacroComparer.INSTANCE);
    }

    private object[] CallIpcs()
    {
        return GeneratorConfig.IpcConfigs.Select(ipcConfig =>
        {
            if (Service<CallGate>.Get().Gates.TryGetValue(ipcConfig.Name, out var channel))
            {
                var args = JsonConvert.DeserializeObject<object[]>(ipcConfig.Args);
                PluginLog.Verbose($"Calling generator {GeneratorConfig.Name} IPC channel {channel.Name} with arguments: {args}");
                var returnValue = channel.InvokeFunc<object>(args);

                PluginLog.Verbose($"IPC channel {channel.Name} returned value: {returnValue}");
                if (!returnValue.GetType().IsGenericType)
                {
                    return returnValue.ToString()!;
                }
                else
                {
                    return returnValue;
                }
            }
            else
            {
                throw new GeneratorException($"Could not find generator {GeneratorConfig.Name} IPC channel: {ipcConfig.Name}");
            }
        }).ToArray();
    }

    private Macro[] CallFunction(object[] args)
    {
        try
        {
            if (!GeneratorConfig.Script.IsNullOrWhitespace())
            {
                PluginLog.Debug($"Executing generator {GeneratorConfig.Name} script");
                JsEngine.Execute(GeneratorConfig.Script);
                PluginLog.Verbose($"Calling generator {GeneratorConfig.Name} main function with: {string.Join(", ", args)}");
                var maybeJson = JsEngine.CallFunction<string>("main", args);
                var generatedEntries = JsonConvert.DeserializeObject<Macro[]>(maybeJson);
                if (generatedEntries != null)
                {
                    PluginLog.Debug($"Successfully generated {generatedEntries.Length} macros using {GeneratorConfig.Name} generator");
                    return generatedEntries;
                }
                else
                {
                    throw new GeneratorException($"Invalid json format returned by generator {GeneratorConfig.Name} main function: {maybeJson}");
                }
            }
            else
            {
                throw new GeneratorException($"Empty script for {GeneratorConfig.Name} generator");
            }
            
        }
        catch (Exception e)
        {
            throw new GeneratorException($"Exception occured while running {GeneratorConfig.Name} generator main function", e);
        }
    }
}
