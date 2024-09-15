using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Quack.Ipcs;

public class PenumbraIpc : IDisposable
{
    private class SortOrderConfig
    {
        [JsonProperty(Required = Required.Always)]
        public Dictionary<string, string> Data { get; set; } = [];
    }

    private class ModDataConfig
    {
        [JsonProperty(Required = Required.Always)]
        public string[] LocalTags { get; set; } = [];
    }

    public class ModData(string dir, string name, string path, string[] localTags, Dictionary<string, object> settings)
    {
        public string dir = dir;
        public string name = name;
        public string path = path;
        public string[] localTags = localTags;
        public Dictionary<string, object> settings = settings;
    }

    public static readonly string MOD_LIST = "Quack.Penumbra.GetModList";
    public static readonly string MOD_LIST_WITH_SETTINGS = "Quack.Penumbra.GetModListWithSettings";

    private IPluginLog PluginLog { get; init; }

    private string PluginConfigsDirectory { get; init; }
    private string SortOrderConfigPath {  get; init; }
    private string ModDataConfigPathTemplate { get; init; }

    private ICallGateSubscriber<Dictionary<string, string>> BaseGetModListSubscriber { get; init; }
    private ICallGateSubscriber<string> BaseGetModDirectory { get; init; }

    private ICallGateProvider<ModData[]> GetModListProvider { get; init; }
    private ICallGateProvider<ModData[]> GetModListWithSettingsProvider { get; init; }

    public PenumbraIpc(IDalamudPluginInterface pluginInterface, IPluginLog pluginLog)
    {
        PluginLog = pluginLog;

        BaseGetModListSubscriber = pluginInterface.GetIpcSubscriber<Dictionary<string, string>>("Penumbra.GetModList");
        BaseGetModDirectory = pluginInterface.GetIpcSubscriber<string>("Penumbra.GetModDirectory");

        GetModListProvider = pluginInterface.GetIpcProvider<ModData[]>(MOD_LIST);
        GetModListWithSettingsProvider = pluginInterface.GetIpcProvider<ModData[]>(MOD_LIST_WITH_SETTINGS);


        PluginConfigsDirectory = Path.GetFullPath(Path.Combine(pluginInterface.GetPluginConfigDirectory(), ".."));

        // %appdata%\xivlauncher\pluginConfigs\Penumbra\sort_order.json
        SortOrderConfigPath = Path.GetFullPath(Path.Combine(PluginConfigsDirectory, "Penumbra\\sort_order.json"));

        // %appdata%\xivlauncher\pluginConfigs\Penumbra\mod_data\{dir}.json
        ModDataConfigPathTemplate = Path.GetFullPath(Path.Combine(PluginConfigsDirectory, "Penumbra\\mod_data\\{0}.json"));

        GetModListProvider.RegisterFunc(GetModList);
        GetModListWithSettingsProvider.RegisterFunc(GetModListWithSettings);
    }

    public void Dispose() {
        GetModListProvider.UnregisterFunc();
    }

    private ModData[] GetModList()
    {
        var modList = BaseGetModListSubscriber.InvokeFunc();

        if (Path.Exists(SortOrderConfigPath))
        {
            using StreamReader sortOrderReader = new(SortOrderConfigPath);
            var sortOrderConfigJson = sortOrderReader.ReadToEnd();
            var sortOrderConfig = JsonConvert.DeserializeObject<SortOrderConfig>(sortOrderConfigJson)!;
            PluginLog.Debug($"Retrieved {sortOrderConfig.Data.Count} penumbra path infos from {Path.GetRelativePath(PluginConfigsDirectory, SortOrderConfigPath)}");

            return modList.Select(d => {
                var modDataConfigPath = string.Format(ModDataConfigPathTemplate, d.Key);

                if (Path.Exists(modDataConfigPath))
                {
                    using StreamReader modDataConfigReader = new(modDataConfigPath);
                    var modDataConfigJson = modDataConfigReader.ReadToEnd();
                    var modDataConfig = JsonConvert.DeserializeObject<ModDataConfig>(modDataConfigJson)!;
                    PluginLog.Debug($"Retrieved {modDataConfig.LocalTags.Length} penumbra local tags from {Path.GetRelativePath(PluginConfigsDirectory, modDataConfigPath)}");

                    return new ModData(d.Key, 
                                       d.Value, 
                                       sortOrderConfig.Data.GetValueOrDefault(d.Key, d.Value),  // Root items don't have a path
                                       modDataConfig.LocalTags,
                                       []);
                }
                else
                {
                    throw new FileNotFoundException($"Failed to find penumbra local tag infos file at #{Path.GetRelativePath(PluginConfigsDirectory, SortOrderConfigPath)}");
                }
            }).ToArray();
        }
        else
        {
            throw new FileNotFoundException($"Failed to find penumbra path infos file at #{SortOrderConfigPath}");
        }
    }

    private ModData[] GetModListWithSettings()
    {
        var modRootDirectoryPath = BaseGetModDirectory.InvokeFunc();

        return GetModList().Select<ModData, ModData>(mod =>
        {
            var modDirectoryPath = Path.Combine(modRootDirectoryPath, mod.dir);
            var defaultSettingsPath = Path.Combine(modDirectoryPath, "default_mod.json");

            PluginLog.Debug(defaultSettingsPath);
            if (Path.Exists(defaultSettingsPath))
            {
                using StreamReader defaultSettingsReader = new(defaultSettingsPath);
                var defaultSettingsJson = defaultSettingsReader.ReadToEnd();
                var defaultSettings = JsonConvert.DeserializeObject<Dictionary<string, object>>(defaultSettingsJson)!.ToDictionary(camelCaseKey, maybeCamelCaseValueKeys);
                PluginLog.Debug($"Retrieved {defaultSettings.Count} penumbra default setting root keys from {defaultSettingsPath}");

                var groupSettingPaths = Directory.GetFiles(modDirectoryPath, "group_*.json");

                var groupSettings = groupSettingPaths.Select(groupSettingsPath =>
                {
                    using StreamReader groupSettingsReader = new(groupSettingsPath);
                    var groupSettingsJson = groupSettingsReader.ReadToEnd();

                    var groupSettings = JsonConvert.DeserializeObject<Dictionary<string, object>>(groupSettingsJson)!.ToDictionary(camelCaseKey, maybeCamelCaseValueKeys);

                    PluginLog.Debug($"Retrieved {groupSettings.Count} penumbra group setting root keys from {groupSettingsPath}");
                    return groupSettings;
                });

                defaultSettings.Add("groupSettings", groupSettings);

                return new(mod.dir, mod.name, mod.path, mod.localTags, defaultSettings);
            }
            else
            {
                throw new FileNotFoundException($"Failed to find penumbra settings file at #{defaultSettingsPath}");
            }
        }).ToArray();
    }


    // Helpers to recursively convert keys to camel case
    private static string camelCaseKey(KeyValuePair<string, object> pair)
    {
        return char.ToLowerInvariant(pair.Key[0]) + pair.Key.Substring(1);
    }

    private static object maybeCamelCaseValueKeys(KeyValuePair<string, object> pair)
    {
        if (pair.Value is JObject jObject)
        {
            var nestedDict = jObject.ToObject<Dictionary<string, object>>();
            if (nestedDict != null)
            {
                return nestedDict.ToDictionary(camelCaseKey, maybeCamelCaseValueKeys);
            } 
            else
            {
                return pair.Value;
            }
        }
        else if (pair.Value is JArray jArray)
        {
            var maybeNestedDicts = jArray.ToObject<List<Dictionary<string, object>>>();
            if (maybeNestedDicts != null)
            {
                return maybeNestedDicts.Select(d => d.ToDictionary(camelCaseKey, maybeCamelCaseValueKeys));
            } 
            else
            {
                return pair.Value;
            }
        }
        else
        {
            return pair.Value;
        }
    }
}

