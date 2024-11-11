using ImGuiNET;

namespace Quack.UI.Helpers;

public static class ListClipperHelper
{
    public static unsafe ImGuiListClipperPtr Build()
    {
        return new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
    }
}
