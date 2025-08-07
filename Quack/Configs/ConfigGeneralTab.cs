using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Style;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Humanizer.Configuration;
using Dalamud.Bindings.ImGui;
using Quack.Generators;
using Quack.UI;
using Quack.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quack.Configs;

public class ConfigGeneralTab : ConfigEntityTab
{
    public static readonly VirtualKey[] MODIFIER_KEYS = [VirtualKey.NO_KEY, VirtualKey.CONTROL, VirtualKey.SHIFT, VirtualKey.MENU];

    private Config Config { get; init; }
    private IKeyState KeyState { get; init; }
    private UIEvents UIEvents { get; init; }

    private IEnumerable<VirtualKey> ValidVirtualKeys { get; init; }
    private IEnumerable<VirtualKey> ModifierVirtualKeys { get; init; }

    public ConfigGeneralTab(Config config, Debouncers debouncers, FileDialogManager fileDialogManager, IKeyState keyState, INotificationManager notificationManager, UIEvents uiEvents) : base(debouncers, fileDialogManager, notificationManager)
    {
        Config = config;
        KeyState = keyState;
        UIEvents = uiEvents;

        ValidVirtualKeys = KeyState.GetValidVirtualKeys().Prepend(VirtualKey.NO_KEY);
        ModifierVirtualKeys = ValidVirtualKeys.Intersect(MODIFIER_KEYS);
    }

    public void Draw()
    {
        if (ImGui.CollapsingHeader("Search###searchHeader", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var keyBind = Config.KeyBind;
            var keyBindIndex = ValidVirtualKeys.IndexOf(keyBind);
            if (ImGui.Combo($"Key Bind###keyBind", ref keyBindIndex, ValidVirtualKeys.Select(k => k.GetFancyName()).ToArray(), ValidVirtualKeys.Count()))
            {
                Config.KeyBind = ValidVirtualKeys.ElementAt(keyBindIndex);
                Config.Save();
            }

            var keybindExtraModifier = Config.KeyBindExtraModifier;
            var keybindExtraModifierIndex = ModifierVirtualKeys.IndexOf(keybindExtraModifier);
            if (ImGui.Combo($"Key Bind Extra Modifier###keyBindExtraModifier", ref keybindExtraModifierIndex, ModifierVirtualKeys.Select(k => k.GetFancyName()).ToArray(), ModifierVirtualKeys.Count()))
            {
                Config.KeyBindExtraModifier = ModifierVirtualKeys.ElementAt(keybindExtraModifierIndex);
                Config.Save();
            }
        }

        ImGui.NewLine();
        if (ImGui.CollapsingHeader("Collections###collectionsHeader", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var collectionConfigsId = "collectionConfigs";
            var collectionConfigs = Config.CollectionConfigs;
            if (ImGui.Button($"+###{collectionConfigsId}New"))
            {
                collectionConfigs.Add(new());
                Config.Save();
            }

            ImGui.SameLine();
            using (ImRaii.PushIndent())
            {
                using (ImRaii.TabBar($"{collectionConfigsId}{string.Join("-", collectionConfigs.Select(c => c.GetHashCode()))}Tabs", ImGuiTabBarFlags.AutoSelectNewTabs | ImGuiTabBarFlags.ListPopupButton | ImGuiTabBarFlags.FittingPolicyScroll))
                {
                    for (var i = 0; i < collectionConfigs.Count; i++)
                    {
                        var collectionConfig = collectionConfigs.ElementAt(i);
                        var collectionConfigId = $"{collectionConfigsId}{collectionConfig.GetHashCode()}";
                        using (var tab = ImRaii.TabItem($"{(collectionConfig.Name.IsNullOrWhitespace() ? BLANK_NAME : collectionConfig.Name)}###{collectionConfigId}Tab"))
                        {
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip($"Priority: {i}\nClick <LEFT> to change order/priority");
                            }

                            MoveTabPopup($"{collectionConfigId}Popup", collectionConfigs, i, Config.Save);

                            if (tab)
                            {
                                var nameId = $"{collectionConfigId}Name";
                                var name = collectionConfig.Name;
                                if (ImGui.InputText($"Name###{nameId}", ref name, ushort.MaxValue))
                                {
                                    collectionConfig.Name = name;
                                    Debounce(nameId, Config.Save);
                                }

                                ImGui.SameLine();
                                var selectable = collectionConfig.Selectable;
                                if (ImGui.Checkbox("Selectable", ref selectable))
                                {
                                    collectionConfig.Selectable = selectable;
                                    Config.Save();
                                }
                                if (ImGui.IsItemHovered())
                                {
                                    ImGui.SetTooltip("Display collection in select boxes");
                                }

                                ImGui.SameLine(ImGui.GetWindowWidth() - 150);
                                using (ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.DalamudRed))
                                {
                                    if (ImGui.Button($"Delete###{collectionConfigId}Delete") && KeyState[VirtualKey.CONTROL])
                                    {
                                        collectionConfigs.RemoveAt(i);
                                        Config.Save();
                                    }
                                    if (ImGui.IsItemHovered())
                                    {
                                        ImGui.SetTooltip(CONFIRM_DELETE_HINT);
                                    }
                                }

                                var tags = string.Join(',', collectionConfig.Tags);
                                var tagInputId = $"{collectionConfigId}Tags";
                                if (ImGui.InputText($"Tags (comma separated)###{tagInputId}", ref tags, ushort.MaxValue))
                                {
                                    collectionConfig.Tags = new(tags.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
                                    Debounce(tagInputId, () =>
                                    {
                                        UIEvents.UpdateTags(collectionConfig);
                                        Config.Save();
                                    });
                                }
                                if (ImGui.IsItemHovered())
                                {
                                    ImGui.SetTooltip("All specified tags have to match in macros (AND condition), use multiple collections for logical OR");
                                }

                                ImGui.SameLine();
                                var color = ImGuiComponents.ColorPickerWithPalette(i, "Color", collectionConfig.Color);
                                ImGui.SameLine();
                                ImGui.Text("Color");
                                if (collectionConfig.Color != color)
                                {
                                    collectionConfig.Color = color;
                                    Debounce($"{collectionConfigId}Color", Config.Save);
                                }
                            }
                        }
                    }
                }

            }
        }

        ImGui.NewLine();
        if (ImGui.CollapsingHeader("Macro Executor###macroExecutorHeader", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var extraCommandFormat = Config.ExtraCommandFormat;
            if (ImGui.InputText("Extra Command Format###commandFormat", ref extraCommandFormat, ushort.MaxValue))
            {
                Config.ExtraCommandFormat = extraCommandFormat;
                Config.Save();
            }
            ImGuiComponents.HelpMarker("Usable in advanced execution mode\nPM format supported via {0:P} placeholder for example: \"/cwl2 puppet now ({0:P})\"");
        }
    }
}
