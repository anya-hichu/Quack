using Dalamud.Interface.ImGuiFileDialog;
using ImGuiNET;
using Newtonsoft.Json;
using Quack.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Quack.Configs;

public abstract class ConfigEntityTab(Debouncers debouncers, FileDialogManager fileDialogManager)
{
    protected static readonly string BLANK_NAME = "(Blank)";
    protected Debouncers Debouncers { get; init; } = debouncers;
    protected FileDialogManager FileDialogManager { get; init; } = fileDialogManager;

    protected void Debounce(string key, Action action)
    {
        Debouncers.Invoke(key, action, TimeSpan.FromSeconds(1));
    }

    protected void ExportToFile<T>(IEnumerable<T> entities, string title, string defaultName)
    {
        FileDialogManager.SaveFileDialog(title, ".*", defaultName, ".json", (valid, path) =>
        {
            if (valid)
            {
                var exports = new ConfigEntityExports<T>(){ Entities = new(entities) };
                var serializer = new JsonSerializer()
                {
                    Formatting = Formatting.Indented
                };
                using var file = File.CreateText(path);
                serializer.Serialize(file, exports);
            }
        });
    }

    protected static void ExportToClipboard<T>(IEnumerable<T> entities)
    {
        var exports = new ConfigEntityExports<T>() { Entities = new(entities) };
        var exportsJson = JsonConvert.SerializeObject(exports);
        var exportsJsonBytes = Encoding.UTF8.GetBytes(exportsJson);
        using MemoryStream exportsJsonStream = new(exportsJsonBytes), compressedExportsJsonStream = new();
        using var compressor = new DeflateStream(exportsJsonStream, CompressionMode.Compress);
        compressor.CopyTo(compressedExportsJsonStream);
        compressor.Close();
        var encodedCompressedExportsJson = Convert.ToBase64String(compressedExportsJsonStream.ToArray());
        ImGui.SetClipboardText(encodedCompressedExportsJson);
    }

    protected void ImportFromFile(Action<string> callback, string title)
    {
        FileDialogManager.OpenFileDialog(title, "{.json}", (valid, path) =>
        {
            if (valid)
            {
                using StreamReader reader = new(path);
                var exportsJson = reader.ReadToEnd();
                #region deprecated
                // Backward compatibility with non-versionned format before config v6
                if (exportsJson.TrimStart().StartsWith('['))
                {
                    var entities = JsonConvert.DeserializeObject<List<object>>(exportsJson);
                    if (entities != null)
                    {
                        var exports = new ConfigEntityExports<object>() { Version = 5, Entities = entities };
                        exportsJson = JsonConvert.SerializeObject(exports);
                    }
                }
                #endregion
                callback(exportsJson);
            }
        });
    }

    protected static void ImportFromClipboard(Action<string> callback)
    {
        var encodedCompressedExportsJson = ImGui.GetClipboardText();
        var compressedExportsJsonBytes = Convert.FromBase64String(encodedCompressedExportsJson);
        using MemoryStream compressedExportsJsonStream = new(compressedExportsJsonBytes), exportsJsonStream = new();
        using var decompressor = new DeflateStream(compressedExportsJsonStream, CompressionMode.Decompress);
        decompressor.CopyTo(exportsJsonStream);
        decompressor.Close();
        var exportsJson = Encoding.UTF8.GetString(exportsJsonStream.ToArray());
        callback(exportsJson);
    }
}
