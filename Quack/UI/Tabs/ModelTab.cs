using Dalamud.Interface.ImGuiFileDialog;
using ImGuiNET;
using Newtonsoft.Json;
using Quack.Schedulers;
using Quack.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Quack.UI.Tabs;

public abstract class ModelTab(Debouncers debouncers, FileDialogManager fileDialogManager)
{
    protected static readonly string BLANK_NAME = "(Blank)";
    protected Debouncers Debouncers { get; init; } = debouncers;
    protected FileDialogManager FileDialogManager { get; init; } = fileDialogManager;

    protected void Debounce(string key, Action action)
    {
        Debouncers.Invoke(key, action, TimeSpan.FromSeconds(1));
    }

    protected void ExportToFile<T>(IEnumerable<T> models, string title, string defaultName)
    {
        FileDialogManager.SaveFileDialog(title, ".*", defaultName, ".json", (valid, path) =>
        {
            if (valid)
            {
                using var file = File.CreateText(path);
                new JsonSerializer().Serialize(file, models);
            }
        });
    }

    protected static void ExportToClipboard<T>(IEnumerable<T> models)
    {
        var serialized = JsonConvert.SerializeObject(models);
        var serializedBytes = Encoding.UTF8.GetBytes(serialized);
        var encoded = Convert.ToBase64String(serializedBytes);
        ImGui.SetClipboardText(encoded);
    }

    protected void WithFileContent(Action<string> callback, string title)
    {
        FileDialogManager.OpenFileDialog(title, "{.json}", (valid, path) =>
        {
            if (valid)
            {
                using StreamReader reader = new(path);
                var schedulerConfigsJson = reader.ReadToEnd();
                callback(schedulerConfigsJson);
            }
        });
    }

    protected static void WithDecodedClipboardContent(Action<string> callback)
    {
        var schedulerConfigsJsonBytes = Convert.FromBase64String(ImGui.GetClipboardText());
        var schedulerConfigsJson = Encoding.UTF8.GetString(schedulerConfigsJsonBytes);
        callback(schedulerConfigsJson);
    }
}
