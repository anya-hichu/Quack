using Dalamud;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc.Internal;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using JavaScriptEngineSwitcher.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Quack.Generators;

public class Generator(IDalamudPluginInterface pluginInterface, GeneratorConfig generatorConfig, IPluginLog pluginLog)
{
    private IDalamudPluginInterface PluginInterface { get; init; } = pluginInterface;
    private GeneratorConfig GeneratorConfig { get; init; } = generatorConfig;
    private IPluginLog PluginLog { get; init; } = pluginLog; 

    public List<Macro> Execute()
    {
        var args = CallIpc();
        return CallFunction(args);
    }

    private List<object> CallIpc()
    {
        if (GeneratorConfig.IpcName.IsNullOrWhitespace())
        {
            return [];
        }

        if (Service<CallGate>.Get().Gates.TryGetValue(GeneratorConfig.IpcName, out var channel)) 
        {
            //Maybe resolve placeholders like current character name, world, etc
            var args = JsonConvert.DeserializeObject<object[]>(GeneratorConfig.IpcArgs);
            PluginLog.Debug($"Calling generator {GeneratorConfig.Name} IPC channel {channel.Name} with arguments: {args}");
            var returnValue = channel.InvokeFunc<object>(args);

            PluginLog.Debug($"IPC channel {channel.Name} returned value: {returnValue}");
            if (returnValue is JArray || returnValue is JObject)
            {
                return [JsonConvert.SerializeObject(returnValue)];
            }
            else
            {
                return [returnValue];
            }
        }
        else
        {
            throw new GeneratorException($"Could not find generator {GeneratorConfig.Name} IPC channel: {GeneratorConfig.IpcName}");
        }

        //if (ipcData != null)
        //{
        //    var subscriber = PluginInterface.GetIpcSubscriber<string, string>(GeneratorConfig.Ipc);
        //    if (GeneratorConfig.Args.IsNullOrWhitespace())
        //    {
        //        return [subscriber.InvokeFunc()];
        //    }
        //    else
        //    {
        //        var args = ;
        //        if (args != null)
        //        {
        //            return [subscriber.GetType().GetMethod("InvokeFunc")!.Invoke(subscriber, args)!];
        //        }
        //        else
        //        {

        //        }
        //    }
        //} 
        //else
        //{

        //}



    }

    private List<Macro> CallFunction(List<object> args)
    {
        var engine = JsEngineSwitcher.Current.CreateDefaultEngine();
        try
        {
            if (!GeneratorConfig.Script.IsNullOrWhitespace())
            {
                PluginLog.Debug($"Executing generator {GeneratorConfig.Name} script: {GeneratorConfig.Script}");
                engine.Execute(GeneratorConfig.Script);
                PluginLog.Debug($"Calling generator {GeneratorConfig.Name} main function with: {string.Join(", ", args)}");
                var maybeJson = engine.CallFunction<string>("main", args.ToArray());
                var generatedEntries = JsonConvert.DeserializeObject<List<Macro>>(maybeJson);
                if (generatedEntries != null)
                {
                    PluginLog.Debug($"Successfully generated {generatedEntries.Count} macros using {GeneratorConfig.Name} generator");
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
