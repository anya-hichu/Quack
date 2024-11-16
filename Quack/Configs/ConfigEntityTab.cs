using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using Humanizer;
using ImGuiNET;
using Newtonsoft.Json;
using Quack.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Quack.Configs;

public abstract class ConfigEntityTab(Debouncers debouncers, FileDialogManager fileDialogManager, INotificationManager notificationManager)
{
    protected static readonly string BLANK_NAME = "(Blank)";
    protected static readonly string CONFIRM_DELETE_HINT = "Press <CTRL> while clicking to confirm";
    protected static readonly string EXPORT_HINT = "Click <RIGHT> for file export\nClick <LEFT> for clipboard export";
    protected static readonly string IMPORT_HINT = "Click <RIGHT> for file import\nClick <LEFT> for clipboard import";

    protected Debouncers Debouncers { get; init; } = debouncers;
    protected FileDialogManager FileDialogManager { get; init; } = fileDialogManager;
    protected INotificationManager NotificationManager { get; init; } = notificationManager;

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

    protected Task ExportToClipboard<T>(IEnumerable<T> entities)
    {
        return Task.Run(() =>
        {
            var exports = new ConfigEntityExports<T>() { Entities = new(entities) };
            var exportsJson = JsonConvert.SerializeObject(exports);
            var exportsJsonBytes = Encoding.UTF8.GetBytes(exportsJson);
            using MemoryStream exportsJsonStream = new(exportsJsonBytes), compressedExportsJsonStream = new();
            using (var compressor = new DeflateStream(compressedExportsJsonStream, CompressionMode.Compress))
            {
                exportsJsonStream.CopyTo(compressor);
            }
            var encodedCompressedExportsJson = Convert.ToBase64String(compressedExportsJsonStream.ToArray());
            ImGui.SetClipboardText(encodedCompressedExportsJson);
            var count = entities.Count();
            var readableType = typeof(T).Name.Titleize().ToLower();
            NotificationManager.AddNotification(new() {
                Type = NotificationType.Success,
                Minimized = false,
                Content = $"Exported {count} {(count > 1 ? readableType.Pluralize() : readableType)} to clipboard"
            });
        });
    }

    protected void ImportFromFile<T>(Func<string, List<T>?> callback, string title)
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
                var maybeEntities = callback(exportsJson);
                var readableType = typeof(T).Name.Titleize().ToLower();
                NotificationManager.AddNotification(maybeEntities != null ? new()
                {
                    Type = NotificationType.Success,
                    Minimized = false,
                    Content = $"Import {maybeEntities.Count} {(maybeEntities.Count > 1 ? readableType.Pluralize() : readableType)} from file"
                } : new()
                {
                    Type = NotificationType.Error,
                    Minimized = false,
                    Content = $"Failed to import {readableType} from file"
                });
            }
        });
    }

    protected Task ImportFromClipboard<T>(Func<string, List<T>?> callback)
    {
        return Task.Run(() =>
        {
            var encodedCompressedExportsJson = ImGui.GetClipboardText();
            var compressedExportsJsonBytes = Convert.FromBase64String(encodedCompressedExportsJson);
            using MemoryStream compressedExportsJsonStream = new(compressedExportsJsonBytes), exportsJsonStream = new();
            using (var decompressor = new DeflateStream(compressedExportsJsonStream, CompressionMode.Decompress))
            {
                decompressor.CopyTo(exportsJsonStream);
            }
            var exportsJson = Encoding.UTF8.GetString(exportsJsonStream.ToArray());
            var maybeEntities = callback(exportsJson);
            var readableType = typeof(T).Name.Titleize().ToLower();
            NotificationManager.AddNotification(maybeEntities != null ? new()
            {
                Type = NotificationType.Success,
                Minimized = false,
                Content = $"Import {maybeEntities.Count} {(maybeEntities.Count > 1 ? readableType.Pluralize() : readableType)} from clipboard"
            } : new()
            {
                Type = NotificationType.Error,
                Minimized = false,
                Content = $"Failed to import {readableType} from clipboard"
            });
        });
    }
}
