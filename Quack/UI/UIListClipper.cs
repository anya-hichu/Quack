using ImGuiNET;

namespace Quack.UI;

public static class UIListClipper
{
    public static unsafe ImGuiListClipperPtr Build()
    {
        return new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
    }
}
