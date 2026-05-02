using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using Glamourer.Api.IpcSubscribers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Quack.Ipcs;

public class GlamourerIpc : IDisposable
{
    private class DesignConfig
    {
        [JsonProperty(Required = Required.Always)]
        public string[] Tags { get; set; } = [];
        public string Color { get; set; } = string.Empty;
    }

    public static readonly string DESIGN_LIST = "Quack.Glamourer.GetDesignList";

    private IPluginLog PluginLog { get; init; }

    private string PluginConfigsDirectory { get; init; }
    private string DesignConfigPathTemplate { get; init; }

    private GetDesignListExtended BaseGetDesignListExtended { get; init; }
    private ICallGateProvider<Dictionary<string, object>[]> GetDesignListProvider { get; init; }

    public GlamourerIpc(IDalamudPluginInterface pluginInterface, IPluginLog pluginLog)
    {
        PluginLog = pluginLog;

        BaseGetDesignListExtended = new(pluginInterface);

        GetDesignListProvider = pluginInterface.GetIpcProvider<Dictionary<string, object>[]>(DESIGN_LIST);

        PluginConfigsDirectory = Path.GetFullPath(Path.Combine(pluginInterface.GetPluginConfigDirectory(), ".."));

        // %appdata%\xivlauncher\pluginConfigs\Glamourer\designs\{id}.json
        DesignConfigPathTemplate = Path.Combine(PluginConfigsDirectory, "Glamourer\\designs\\{0}.json");

        GetDesignListProvider.RegisterFunc(GetDesignList);
    }

    public void Dispose() 
    {
        GetDesignListProvider.UnregisterFunc();
    }

    private Dictionary<string, object>[] GetDesignList()
    {
        var designListExtended = BaseGetDesignListExtended.Invoke();

        return [.. designListExtended.Select(d =>
        {
            var designConfigPath = string.Format(DesignConfigPathTemplate, d.Key);
            if (Path.Exists(designConfigPath))
            {
                using StreamReader designConfigFile = new(designConfigPath);
                var designConfigJson = designConfigFile.ReadToEnd();
                var designConfig = JsonConvert.DeserializeObject<DesignConfig>(designConfigJson)!;

                PluginLog.Debug($"Retrieved {designConfig.Tags.Length} glamourer tags from {Path.GetRelativePath(PluginConfigsDirectory, designConfigPath)}");
                var (displayName, fullPath, _, __) = d.Value;

                return new Dictionary<string, object>() {
                    { "id", d.Key},
                    { "name", displayName },
                    { "path", fullPath },
                    { "tags", designConfig.Tags },
                    { "color", designConfig.Color }
                };
            }
            else
            {
                throw new FileNotFoundException($"Failed to find glamourer tag infos file at #{designConfigPath}");
            }
        })];
    }
}
