using Dalamud.Plugin.Services;
using Quack.Collections;
using Quack.Macros;
using System;

namespace Quack.UI;

public class UIEvents(IPluginLog pluginLog)
{
    private IPluginLog PluginLog { get; init; } = pluginLog;

    public event Action<Macro>? OnMacroEdit;
    public event Action<CollectionConfig>? OnCollectionConfigTagsEdit;

    public void InvokeEdit(Macro macro)
    {
        PluginLog.Debug($"Invoked OnMacroEdit UI event with macro [{macro.Name}]");
        OnMacroEdit?.Invoke(macro);
    }

    public void InvokeTagsEdit(CollectionConfig collectionConfig)
    {
        PluginLog.Debug($"Invoked OnCollectionConfigTagsEdit UI event with collection [{collectionConfig.Name}]");
        OnCollectionConfigTagsEdit?.Invoke(collectionConfig);
    }
}
