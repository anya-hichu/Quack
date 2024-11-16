using Dalamud.Game.ClientState.Keys;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using ImGuiNET;
using System.Linq;

namespace Quack.Configs;

public class ConfigGeneralTab(Config config, IKeyState keyState)
{
    public static readonly VirtualKey[] MODIFIER_KEYS = [VirtualKey.NO_KEY, VirtualKey.CONTROL, VirtualKey.SHIFT, VirtualKey.MENU];

    private Config Config { get; init; } = config;
    private IKeyState KeyState { get; init; } = keyState;

    public void Draw()
    {
        if (ImGui.CollapsingHeader("Search##searchHeader", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var validVirtualKeys = KeyState.GetValidVirtualKeys().Prepend(VirtualKey.NO_KEY);

            var keyBind = Config.KeyBind;
            var keyBindIndex = validVirtualKeys.IndexOf(keyBind);
            if (ImGui.Combo($"Key Bind##keyBind", ref keyBindIndex, validVirtualKeys.Select(k => k.GetFancyName()).ToArray(), validVirtualKeys.Count()))
            {
                Config.KeyBind = validVirtualKeys.ElementAt(keyBindIndex);
                Config.Save();
            }

            var modifierVirtualKeys = validVirtualKeys.Where(k => MODIFIER_KEYS.Contains(k));
            var keybindExtraModifier = Config.KeyBindExtraModifier;
            var keybindExtraModifierIndex = modifierVirtualKeys.IndexOf(keybindExtraModifier);
            if (ImGui.Combo($"Key Bind Extra Modifier##keyBindExtraModifier", ref keybindExtraModifierIndex, modifierVirtualKeys.Select(k => k.GetFancyName()).ToArray(), modifierVirtualKeys.Count()))
            {
                Config.KeyBindExtraModifier = modifierVirtualKeys.ElementAt(keybindExtraModifierIndex);
                Config.Save();
            }
        }

        ImGui.NewLine();
        if (ImGui.CollapsingHeader("Macro Executor##macroExecutorHeader", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var extraCommandFormat = Config.ExtraCommandFormat;
            if (ImGui.InputText("Extra Command Format##commandFormat", ref extraCommandFormat, ushort.MaxValue))
            {
                Config.ExtraCommandFormat = extraCommandFormat;
                Config.Save();
            }
            ImGui.Text("PM format supported via {0:P} placeholder for example: \"/cwl2 puppet now ({0:P})\"");
        }
    }
}
