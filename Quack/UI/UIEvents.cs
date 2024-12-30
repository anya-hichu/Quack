using Dalamud.Plugin.Services;
using Quack.Collections;
using Quack.Macros;
using System;

namespace Quack.UI;

public class UIEvents(IPluginLog pluginLog)
{
    private IPluginLog PluginLog { get; init; } = pluginLog;

    public event Action<Macro>? OnMacroEditRequest;
    public event Action<CollectionConfig>? OnCollectionConfigTagsUpdate;

    public void NotifyEditRequest(Macro macro)
    {
        PluginLog.Debug($"Notify OnMacroEditRequest UI event for macro [{macro.Name}]");
        OnMacroEditRequest?.Invoke(macro);
    }

    public void NotifyTagsUpdate(CollectionConfig collectionConfig)
    {
        PluginLog.Debug($"Notify OnCollectionConfigTagsUpdate UI event for collection [{collectionConfig.Name}]");
        OnCollectionConfigTagsUpdate?.Invoke(collectionConfig);
    }
}
