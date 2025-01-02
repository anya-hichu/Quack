using Dalamud.Plugin.Services;
using Quack.Collections;
using Quack.Macros;
using System;

namespace Quack.UI;

public class UIEvents(IPluginLog pluginLog)
{
    private IPluginLog PluginLog { get; init; } = pluginLog;

    public event Action<Macro>? OnMacroEditRequest;
    public event Action<Macro>? OnMacroExecutionRequest;
    public event Action<CollectionConfig>? OnCollectionConfigTagsUpdate;

    public void RequestEdit(Macro macro)
    {
        PluginLog.Debug($"Notify OnMacroEditRequest UI event for macro [{macro.Name}]");
        OnMacroEditRequest?.Invoke(macro);
    }

    public void RequestExecution(Macro macro)
    {
        PluginLog.Debug($"Notify OnMacroExecutionRequest UI event for macro [{macro.Name}]");
        OnMacroExecutionRequest?.Invoke(macro);
    }

    public void UpdateTags(CollectionConfig collectionConfig)
    {
        PluginLog.Debug($"Notify OnCollectionConfigTagsUpdate UI event for collection [{collectionConfig.Name}]");
        OnCollectionConfigTagsUpdate?.Invoke(collectionConfig);
    }
}
